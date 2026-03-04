// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGetUtility.Extensions;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ReferencedPackageReader
    {
        private const string ProjectReferenceIdentifier = "project";
        private const string PackageTypeIdentifier = "package";
        private readonly ILockFileFactory _lockFileFactory;
        private readonly IPackagesConfigReader _packagesConfigReader;
        private readonly IMsBuildAbstraction _msBuild;

        public ReferencedPackageReader(IMsBuildAbstraction msBuild,
            ILockFileFactory lockFileFactory,
            IPackagesConfigReader packagesConfigReader)
        {
            _msBuild = msBuild;
            _lockFileFactory = lockFileFactory;
            _packagesConfigReader = packagesConfigReader;
        }

        /// <summary>
        /// Gets installed NuGet packages for the specified project.
        /// </summary>
        /// <param name="projectPath">Path to the project file.</param>
        /// <param name="includeTransitive">True to include transitive dependencies; otherwise, false.</param>
        /// <param name="targetFramework">
        /// Target framework moniker to evaluate. If null, all available target frameworks are evaluated.
        /// </param>
        /// <param name="excludePublishFalse">
        /// True to exclude packages with Publish="false" metadata. When transitive dependencies are included,
        /// packages reachable only through those excluded roots are also excluded.
        /// </param>
        /// <returns>Resolved package identities from project assets or packages.config.</returns>
        public IEnumerable<PackageIdentity> GetInstalledPackages(string projectPath, bool includeTransitive, string? targetFramework = null, bool excludePublishFalse = false)
        {
            IProject project = _msBuild.GetProject(projectPath);

            if (TryGetInstalledPackagesFromAssetsFile(includeTransitive, project, targetFramework, excludePublishFalse, out IEnumerable<PackageIdentity>? dependencies))
            {
                return dependencies;
            }

            if (project.HasPackagesConfigFile())
            {
                return _packagesConfigReader.GetPackages(project);
            }

            return Array.Empty<PackageIdentity>();
        }

        private bool TryGetInstalledPackagesFromAssetsFile(bool includeTransitive,
            IProject project,
            string? targetFramework,
            bool excludePublishFalse,
            [NotNullWhen(true)] out IEnumerable<PackageIdentity>? installedPackages)
        {
            installedPackages = null;
            if (!TryLoadAssetsFile(project, out ILockFile? assetsFile, out string? assetsPath))
            {
                return false;
            }

            var referencedLibraries = new HashSet<ILockFileLibrary>();
            List<ILockFileTarget> selectedTargets;
            NuGetFramework? requestedTargetFramework = ParseTargetFrameworkOrNull(targetFramework);
            Dictionary<string, HashSet<string>> publishFalsePackagesByFramework = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string[]> directDependenciesByFramework = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Dictionary<string, HashSet<string>>> packageDependenciesByFramework = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> recursiveExclusionsByInput = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            LockFile? parsedAssetsLockFile = null;

            if (requestedTargetFramework is not null)
            {
                selectedTargets = assetsFile.Targets!
                    .Where(t => IsMatchingTarget(requestedTargetFramework, t.TargetFramework))
                    .ToList();
                if (!selectedTargets.Any())
                {
                    throw new ReferencedPackageReaderException($"Target framework {targetFramework} not found.");
                }
            }
            else
            {
                selectedTargets = assetsFile.Targets!.ToList();
            }

            foreach (ILockFileTarget target in selectedTargets)
            {
                HashSet<ILockFileLibrary> targetReferencedLibraries =
                    new HashSet<ILockFileLibrary>(GetReferencedLibrariesForTarget(includeTransitive, assetsFile, target));

                if (excludePublishFalse)
                {
                    string? targetFrameworkForPublishMetadata = requestedTargetFramework?.GetShortFolderName() ?? target.TargetFramework.ToString();
                    string targetFrameworkCacheKey = targetFrameworkForPublishMetadata ?? string.Empty;

                    // Remove packages with Publish=false metadata from the evaluated PackageReferences for this target only.
                    if (!publishFalsePackagesByFramework.TryGetValue(targetFrameworkCacheKey, out HashSet<string>? cachedPublishFalsePackages))
                    {
                        cachedPublishFalsePackages = GetPackagesExcludedFromPublish(project, targetFrameworkForPublishMetadata);
                        publishFalsePackagesByFramework[targetFrameworkCacheKey] = cachedPublishFalsePackages;
                    }

                    HashSet<string> excludedPackages = new HashSet<string>(cachedPublishFalsePackages, StringComparer.OrdinalIgnoreCase);
                    if (includeTransitive && excludedPackages.Any())
                    {
                        if (!directDependenciesByFramework.TryGetValue(targetFrameworkCacheKey, out string[]? directDependenciesForFramework))
                        {
                            directDependenciesForFramework = GetDirectDependenciesForTargets(assetsFile, new[] { target }).ToArray();
                            directDependenciesByFramework[targetFrameworkCacheKey] = directDependenciesForFramework;
                        }

                        NuGetFramework? targetFrameworkIdentifier = ParseTargetFrameworkOrNull(target.TargetFramework.ToString());
                        if (targetFrameworkIdentifier is not null)
                        {
                            if (!packageDependenciesByFramework.TryGetValue(targetFrameworkCacheKey, out Dictionary<string, HashSet<string>>? packageDependencies))
                            {
                                packageDependencies = GetPackageDependenciesForTargetFramework(
                                    assetsPath,
                                    targetFrameworkIdentifier,
                                    ref parsedAssetsLockFile);
                                packageDependenciesByFramework[targetFrameworkCacheKey] = packageDependencies;
                            }

                            if (packageDependencies.Count > 0)
                            {
                                string recursiveExclusionCacheKey = BuildExclusionCacheKey(
                                    targetFrameworkCacheKey,
                                    directDependenciesForFramework,
                                    excludedPackages);

                                if (!recursiveExclusionsByInput.TryGetValue(recursiveExclusionCacheKey, out HashSet<string>? recursivelyExcludedPackages))
                                {
                                    recursivelyExcludedPackages = GetPackagesExcludedFromPublishDependencyPaths(
                                        packageDependencies,
                                        directDependenciesForFramework,
                                        excludedPackages);
                                    recursiveExclusionsByInput[recursiveExclusionCacheKey] = recursivelyExcludedPackages;
                                }

                                excludedPackages.UnionWith(recursivelyExcludedPackages);
                            }
                        }
                    }

                    targetReferencedLibraries.RemoveWhere(library => excludedPackages.Contains(library.Name));
                }

                referencedLibraries.AddRange(targetReferencedLibraries);
            }

            installedPackages = referencedLibraries.Select(r => new PackageIdentity(r.Name, r.Version));
            return true;
        }

        private static IEnumerable<ILockFileLibrary> GetReferencedLibrariesForTarget(bool includeTransitive,
            ILockFile assetsFile,
            ILockFileTarget target)
        {
            IEnumerable<ILockFileLibrary> dependencies = target.Libraries.Where(l => l.Type != ProjectReferenceIdentifier);
            if (!includeTransitive)
            {
                ITargetFrameworkInformation targetFrameworkInformation = GetTargetFrameworkInformation(target, assetsFile);
                IEnumerable<ILibraryDependency> directDependencies = targetFrameworkInformation.Dependencies;
                return dependencies.Where(d => directDependencies.Any(direct => direct.Name == d.Name));
            }
            return dependencies;
        }

        private static ITargetFrameworkInformation GetTargetFrameworkInformation(ILockFileTarget target,
            ILockFile assetsFile)
        {
            try
            {
                return assetsFile.PackageSpec.TargetFrameworks.First(
                    t => t.FrameworkName.Equals(target.TargetFramework));
            }
            catch (Exception e)
            {
                throw new ReferencedPackageReaderException(
                    $"Failed to identify the target framework information for {target}",
                    e);
            }
        }

        private static IEnumerable<string> GetDirectDependenciesForTargets(ILockFile assetsFile,
            IEnumerable<ILockFileTarget> selectedTargets)
        {
            HashSet<string> directDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ILockFileTarget target in selectedTargets)
            {
                ITargetFrameworkInformation targetFrameworkInformation = GetTargetFrameworkInformation(target, assetsFile);
                foreach (ILibraryDependency dependency in targetFrameworkInformation.Dependencies)
                {
                    directDependencies.Add(dependency.Name);
                }
            }

            return directDependencies;
        }

        private static HashSet<string> GetPackagesExcludedFromPublishDependencyPaths(
            Dictionary<string, HashSet<string>> packageDependencies,
            IEnumerable<string> directDependencies,
            ISet<string> publishFalseDirectDependencies)
        {
            HashSet<string> excludedPackages = new HashSet<string>(publishFalseDirectDependencies, StringComparer.OrdinalIgnoreCase);
            if (packageDependencies.Count == 0)
            {
                return excludedPackages;
            }

            HashSet<string> publishableRoots = new HashSet<string>(
                directDependencies.Where(package => !publishFalseDirectDependencies.Contains(package)),
                StringComparer.OrdinalIgnoreCase);

            HashSet<string> reachableFromPublishableRoots = GetReachablePackages(packageDependencies, publishableRoots);

            foreach (string packageName in packageDependencies.Keys)
            {
                if (!reachableFromPublishableRoots.Contains(packageName))
                {
                    excludedPackages.Add(packageName);
                }
            }

            return excludedPackages;
        }

        private static Dictionary<string, HashSet<string>> BuildDependencyMapFromAssetsFile(LockFile lockFile,
            IEnumerable<NuGetFramework> targetFrameworks)
        {
            HashSet<NuGetFramework> targetFrameworkSet = new HashSet<NuGetFramework>(NuGetFrameworkFullComparer.Instance);
            targetFrameworkSet.UnionWith(targetFrameworks);
            Dictionary<string, HashSet<string>> packageDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (LockFileTarget target in lockFile.Targets)
            {
                if (!IsMatchingTarget(targetFrameworkSet, target.TargetFramework))
                {
                    continue;
                }

                foreach (LockFileTargetLibrary library in target.Libraries)
                {
                    if (!string.Equals(library.Type, PackageTypeIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? packageName = library.Name;
                    if (packageName is null)
                    {
                        continue;
                    }

                    string packageNameValue = packageName.Trim();
                    if (packageNameValue.Length == 0)
                    {
                        continue;
                    }

                    if (!packageDependencies.TryGetValue(packageNameValue, out HashSet<string>? dependencies))
                    {
                        dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        packageDependencies[packageNameValue] = dependencies;
                    }

                    foreach (var dependency in library.Dependencies)
                    {
                        string dependencyName = dependency.Id;
                        dependencies.Add(dependencyName);
                        if (!packageDependencies.ContainsKey(dependencyName))
                        {
                            packageDependencies[dependencyName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
            }

            return packageDependencies;
        }

        private static Dictionary<string, HashSet<string>> GetPackageDependenciesForTargetFramework(
            string? assetsPath,
            NuGetFramework targetFramework,
            ref LockFile? parsedAssetsLockFile)
        {
            if (assetsPath is not { Length: > 0 } resolvedAssetsPath || !File.Exists(resolvedAssetsPath))
            {
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                parsedAssetsLockFile ??= new LockFileFormat().Read(resolvedAssetsPath);
                return BuildDependencyMapFromAssetsFile(parsedAssetsLockFile, new[] { targetFramework });
            }
            catch (IOException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to I/O error. AssetsPath={0}, TargetFramework={1}, Exception={2}",
                    resolvedAssetsPath,
                    targetFramework.GetShortFolderName(),
                    exception);
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch (FormatException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to invalid assets data. AssetsPath={0}, TargetFramework={1}, Exception={2}",
                    resolvedAssetsPath,
                    targetFramework.GetShortFolderName(),
                    exception);
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string BuildExclusionCacheKey(string targetFramework,
            IEnumerable<string> directDependencies,
            IEnumerable<string> publishFalseDirectDependencies)
        {
            string directDependenciesKey = string.Join(";", directDependencies.OrderBy(dependency => dependency, StringComparer.OrdinalIgnoreCase));
            string publishFalseDependenciesKey = string.Join(";", publishFalseDirectDependencies.OrderBy(dependency => dependency, StringComparer.OrdinalIgnoreCase));
            return $"{targetFramework}|{directDependenciesKey}|{publishFalseDependenciesKey}";
        }

        private static bool IsMatchingTarget(NuGetFramework expectedTargetFramework, Wrapper.NuGetWrapper.Frameworks.INuGetFramework targetFramework)
        {
            NuGetFramework parsedTargetFramework = ParseTargetFramework(targetFramework);
            return NuGetFrameworkFullComparer.Instance.Equals(expectedTargetFramework, parsedTargetFramework);
        }

        private static bool IsMatchingTarget(HashSet<NuGetFramework> targetFrameworkSet, NuGetFramework targetFramework)
        {
            return targetFrameworkSet.Contains(targetFramework);
        }

        private static HashSet<string> GetReachablePackages(Dictionary<string, HashSet<string>> packageDependencies,
            IEnumerable<string> roots)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stack<string> stack = new Stack<string>(roots);

            while (stack.Count > 0)
            {
                string packageName = stack.Pop();
                if (!visited.Add(packageName))
                {
                    continue;
                }

                if (!packageDependencies.TryGetValue(packageName, out HashSet<string>? dependencies))
                {
                    continue;
                }

                foreach (string dependency in dependencies)
                {
                    stack.Push(dependency);
                }
            }

            return visited;
        }

        private static NuGetFramework ParseTargetFramework(Wrapper.NuGetWrapper.Frameworks.INuGetFramework targetFramework)
        {
            string? targetFrameworkString = targetFramework.ToString();
            if (targetFrameworkString is null)
            {
                throw new ReferencedPackageReaderException("Failed to parse target framework from lock file target.");
            }

            string targetFrameworkValue = targetFrameworkString.Trim();
            if (targetFrameworkValue.Length == 0)
            {
                throw new ReferencedPackageReaderException("Failed to parse target framework from lock file target.");
            }

            return NuGetFramework.Parse(targetFrameworkValue);
        }

        private static NuGetFramework? ParseTargetFrameworkOrNull(string? targetFramework)
        {
            if (targetFramework is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                return null;
            }

            return NuGetFramework.Parse(targetFramework.Trim());
        }

        private bool TryLoadAssetsFile(IProject project,
            [NotNullWhen(true)] out ILockFile? assetsFile,
            out string? assetsPath)
        {
            if (!project.TryGetAssetsPath(out string projectAssetsPath))
            {
                assetsFile = null;
                assetsPath = null;
                return false;
            }

            assetsFile = _lockFileFactory.GetFromFile(projectAssetsPath);

            if (assetsFile.TryGetErrors(out string[] errors))
            {
                throw new ReferencedPackageReaderException($"Project assets file for project {project.FullPath} contains errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
            }

            if (!assetsFile.PackageSpec.IsValid() || !(assetsFile.Targets?.Any() ?? false))
            {
                throw new ReferencedPackageReaderException(
                    $"Failed to validate project assets for project {project.FullPath}");
            }

            assetsPath = projectAssetsPath;
            return true;
        }

        private static HashSet<string> GetPackagesExcludedFromPublish(IProject project, string? targetFramework)
        {
            // Publish metadata is not available in project.assets.json, so resolve it via MSBuild items.
            IEnumerable<PackageReferenceMetadata> packageReferences = targetFramework is null
                ? project.GetPackageReferences()
                : project.GetPackageReferencesForTarget(targetFramework);

            HashSet<string> excludedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PackageReferenceMetadata packageReference in packageReferences ?? Array.Empty<PackageReferenceMetadata>())
            {
                if (packageReference.Metadata.TryGetValue("Publish", out string? value) &&
                    string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    excludedPackages.Add(packageReference.PackageName);
                }
            }

            return excludedPackages;
        }
    }
}
