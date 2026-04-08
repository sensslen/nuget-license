// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using NuGetUtility.Extensions;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ReferencedPackageReader
    {
        private const string ProjectReferenceIdentifier = "project";
        private readonly ILockFileFactory _lockFileFactory;
        private readonly INuGetFrameworkUtility _nuGetFrameworkUtility;
        private readonly IAssetsPackageDependencyReader _assetsPackageDependencyReader;
        private readonly IPackagesConfigReader _packagesConfigReader;
        private readonly IMsBuildAbstraction _msBuild;

        public ReferencedPackageReader(IMsBuildAbstraction msBuild,
            ILockFileFactory lockFileFactory,
            INuGetFrameworkUtility nuGetFrameworkUtility,
            IAssetsPackageDependencyReader assetsPackageDependencyReader,
            IPackagesConfigReader packagesConfigReader)
        {
            _msBuild = msBuild;
            _lockFileFactory = lockFileFactory;
            _nuGetFrameworkUtility = nuGetFrameworkUtility;
            _assetsPackageDependencyReader = assetsPackageDependencyReader;
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
            if (!TryLoadAssetsFile(project, out ILockFile? assetsFile))
            {
                return false;
            }

            string? normalizedRequestedTargetFramework = NormalizeTargetFrameworkOrNull(targetFramework);
            List<ILockFileTarget> selectedTargets = GetSelectedTargets(assetsFile, normalizedRequestedTargetFramework, targetFramework);

            HashSet<ILockFileLibrary> referencedLibraries = new HashSet<ILockFileLibrary>();
            PublishExclusionContext publishExclusionContext = new PublishExclusionContext(normalizedRequestedTargetFramework);

            foreach (ILockFileTarget target in selectedTargets)
            {
                HashSet<ILockFileLibrary> targetReferencedLibraries = [.. GetReferencedLibrariesForTarget(includeTransitive, assetsFile, target)];

                if (excludePublishFalse)
                {
                    HashSet<string> excludedPackages = GetExcludedPackagesForTarget(
                        project,
                        assetsFile,
                        target,
                        includeTransitive,
                        publishExclusionContext);

                    targetReferencedLibraries.RemoveWhere(library => excludedPackages.Contains(library.Name));
                }

                referencedLibraries.AddRange(targetReferencedLibraries);
            }

            installedPackages = referencedLibraries.Select(r => new PackageIdentity(r.Name, r.Version));
            return true;
        }

        private List<ILockFileTarget> GetSelectedTargets(ILockFile assetsFile,
            string? normalizedRequestedTargetFramework,
            string? targetFramework)
        {
            if (normalizedRequestedTargetFramework is null)
            {
                return assetsFile.Targets.ToList();
            }

            List<ILockFileTarget> selectedTargets = assetsFile.Targets
                .Where(t => _nuGetFrameworkUtility.IsEquivalent(normalizedRequestedTargetFramework, t.TargetFramework))
                .ToList();
            if (!selectedTargets.Any())
            {
                throw new ReferencedPackageReaderException($"Target framework {targetFramework} not found.");
            }

            return selectedTargets;
        }

        private HashSet<string> GetExcludedPackagesForTarget(IProject project,
            ILockFile assetsFile,
            ILockFileTarget target,
            bool includeTransitive,
            PublishExclusionContext context)
        {
            string targetFrameworkForPublishMetadata = context.NormalizedRequestedTargetFramework ?? _nuGetFrameworkUtility.Normalize(target.TargetFramework);
            string targetFrameworkCacheKey = targetFrameworkForPublishMetadata ?? string.Empty;

            // Remove packages with Publish=false metadata from the evaluated PackageReferences for this target only.
            if (!context.PublishFalsePackagesByFramework.TryGetValue(targetFrameworkCacheKey, out HashSet<string>? cachedPublishFalsePackages))
            {
                cachedPublishFalsePackages = GetPackagesExcludedFromPublish(project, targetFrameworkForPublishMetadata);
                context.PublishFalsePackagesByFramework[targetFrameworkCacheKey] = cachedPublishFalsePackages;
            }

            HashSet<string> excludedPackages = new HashSet<string>(cachedPublishFalsePackages, StringComparer.OrdinalIgnoreCase);
            if (!includeTransitive || !excludedPackages.Any())
            {
                return excludedPackages;
            }

            if (!context.DirectDependenciesByFramework.TryGetValue(targetFrameworkCacheKey, out HashSet<string>? directDependenciesForFramework))
            {
                directDependenciesForFramework = GetDirectDependenciesForTargets(assetsFile, [target]);
                context.DirectDependenciesByFramework[targetFrameworkCacheKey] = directDependenciesForFramework;
            }

            if (!context.PackageDependenciesByFramework.TryGetValue(targetFrameworkCacheKey, out Dictionary<string, HashSet<string>>? packageDependencies))
            {
                packageDependencies = _assetsPackageDependencyReader.GetPackageDependenciesForTargetFramework(
                    assetsFile,
                    targetFrameworkCacheKey);
                context.PackageDependenciesByFramework[targetFrameworkCacheKey] = packageDependencies;
            }

            if (packageDependencies.Count == 0)
            {
                return excludedPackages;
            }

            string recursiveExclusionCacheKey = BuildExclusionCacheKey(
                targetFrameworkCacheKey,
                directDependenciesForFramework,
                excludedPackages);

            if (!context.RecursiveExclusionsByInput.TryGetValue(recursiveExclusionCacheKey, out HashSet<string>? recursivelyExcludedPackages))
            {
                recursivelyExcludedPackages = GetPackagesExcludedFromPublishDependencyPaths(
                    packageDependencies,
                    directDependenciesForFramework,
                    excludedPackages);
                context.RecursiveExclusionsByInput[recursiveExclusionCacheKey] = recursivelyExcludedPackages;
            }

            excludedPackages.UnionWith(recursivelyExcludedPackages);
            return excludedPackages;
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

        private static HashSet<string> GetDirectDependenciesForTargets(ILockFile assetsFile,
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

        private static string BuildExclusionCacheKey(string targetFramework,
            IEnumerable<string> directDependencies,
            IEnumerable<string> publishFalseDirectDependencies)
        {
            string directDependenciesKey = string.Join(";", directDependencies.OrderBy(dependency => dependency, StringComparer.OrdinalIgnoreCase));
            string publishFalseDependenciesKey = string.Join(";", publishFalseDirectDependencies.OrderBy(dependency => dependency, StringComparer.OrdinalIgnoreCase));
            return $"{targetFramework}|{directDependenciesKey}|{publishFalseDependenciesKey}";
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

        private string? NormalizeTargetFrameworkOrNull(string? targetFramework)
        {
            if (targetFramework is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                return null;
            }

            return _nuGetFrameworkUtility.Normalize(targetFramework);
        }

        private bool TryLoadAssetsFile(IProject project,
            [NotNullWhen(true)] out ILockFile? assetsFile)
        {
            if (!project.TryGetAssetsPath(out string projectAssetsPath))
            {
                assetsFile = null;
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

        private sealed record PublishExclusionContext(string? NormalizedRequestedTargetFramework)
        {
            public Dictionary<string, HashSet<string>> PublishFalsePackagesByFramework { get; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, HashSet<string>> DirectDependenciesByFramework { get; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, Dictionary<string, HashSet<string>>> PackageDependenciesByFramework { get; } = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, HashSet<string>> RecursiveExclusionsByInput { get; } = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        }
    }
}
