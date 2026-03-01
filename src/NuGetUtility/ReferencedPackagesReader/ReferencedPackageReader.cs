// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Text.Json;
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
            string? normalizedTargetFramework = NormalizeTargetFramework(targetFramework);

            if (normalizedTargetFramework is not null)
            {
                selectedTargets = assetsFile.Targets!.Where(t => t.TargetFramework.Equals(normalizedTargetFramework)).ToList();
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
                    string? targetFrameworkForPublishMetadata = normalizedTargetFramework;
                    if (targetFrameworkForPublishMetadata is null)
                    {
                        targetFrameworkForPublishMetadata = NormalizeTargetFramework(target.TargetFramework.ToString());
                    }

                    // Remove packages with Publish=false metadata from the evaluated PackageReferences for this target only.
                    HashSet<string> excludedPackages = GetPackagesExcludedFromPublish(project, targetFrameworkForPublishMetadata);
                    if (includeTransitive && excludedPackages.Any())
                    {
                        IEnumerable<string> directDependencies = GetDirectDependenciesForTargets(assetsFile, new[] { target });

                        string? targetFrameworkIdentifier = NormalizeTargetFramework(target.TargetFramework.ToString());
                        IEnumerable<string> targetFrameworks = targetFrameworkIdentifier is null
                            ? Array.Empty<string>()
                            : new[] { targetFrameworkIdentifier };

                        HashSet<string> recursivelyExcludedPackages = GetPackagesExcludedFromPublishDependencyPaths(
                            assetsPath,
                            directDependencies,
                            excludedPackages,
                            targetFrameworks);
                        excludedPackages.UnionWith(recursivelyExcludedPackages);
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

        private static HashSet<string> GetPackagesExcludedFromPublishDependencyPaths(string? assetsPath,
            IEnumerable<string> directDependencies,
            ISet<string> publishFalseDirectDependencies,
            IEnumerable<string> targetFrameworks)
        {
            HashSet<string> excludedPackages = new HashSet<string>(publishFalseDirectDependencies, StringComparer.OrdinalIgnoreCase);
            if (assetsPath is not { Length: > 0 } resolvedAssetsPath || !File.Exists(resolvedAssetsPath))
            {
                return excludedPackages;
            }

            string[] targetFrameworkArray = targetFrameworks.ToArray();

            Dictionary<string, HashSet<string>> dependencyGraph;
            try
            {
                dependencyGraph = BuildDependencyGraphFromAssetsFile(resolvedAssetsPath, targetFrameworkArray);
            }
            catch (IOException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to I/O error. AssetsPath={0}, TargetFrameworks={1}, Exception={2}",
                    resolvedAssetsPath,
                    string.Join(",", targetFrameworkArray),
                    exception);
                return excludedPackages;
            }
            catch (JsonException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to invalid assets JSON. AssetsPath={0}, TargetFrameworks={1}, Exception={2}",
                    resolvedAssetsPath,
                    string.Join(",", targetFrameworkArray),
                    exception);
                return excludedPackages;
            }

            if (dependencyGraph.Count == 0)
            {
                return excludedPackages;
            }

            HashSet<string> publishableRoots = new HashSet<string>(
                directDependencies.Where(package => !publishFalseDirectDependencies.Contains(package)),
                StringComparer.OrdinalIgnoreCase);

            HashSet<string> reachableFromPublishableRoots = GetReachablePackages(dependencyGraph, publishableRoots);

            foreach (string packageName in dependencyGraph.Keys)
            {
                if (!reachableFromPublishableRoots.Contains(packageName))
                {
                    excludedPackages.Add(packageName);
                }
            }

            return excludedPackages;
        }

        private static Dictionary<string, HashSet<string>> BuildDependencyGraphFromAssetsFile(string assetsPath,
            IEnumerable<string> targetFrameworks)
        {
            // This method intentionally parses project.assets.json directly. ILockFile is used for package
            // enumeration, but it does not provide a simple package-to-package dependency graph for the
            // selected targets required by Publish=false transitive path filtering.
            HashSet<string> targetFrameworkSet = new HashSet<string>(targetFrameworks, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> dependencyGraph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            using FileStream stream = File.OpenRead(assetsPath);
            using JsonDocument json = JsonDocument.Parse(stream);

            if (!json.RootElement.TryGetProperty("targets", out JsonElement targetsElement) ||
                targetsElement.ValueKind != JsonValueKind.Object)
            {
                return dependencyGraph;
            }

            foreach (JsonProperty target in targetsElement.EnumerateObject())
            {
                if (!IsMatchingTarget(targetFrameworkSet, target.Name) || target.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty package in target.Value.EnumerateObject())
                {
                    if (!package.Value.TryGetProperty("type", out JsonElement typeElement) ||
                        !string.Equals(typeElement.GetString(), PackageTypeIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string packageName = ParsePackageNameFromTargetKey(package.Name);
                    if (!dependencyGraph.TryGetValue(packageName, out HashSet<string>? dependencies))
                    {
                        dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        dependencyGraph[packageName] = dependencies;
                    }

                    if (!package.Value.TryGetProperty("dependencies", out JsonElement dependencyElement) ||
                        dependencyElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    foreach (JsonProperty dependency in dependencyElement.EnumerateObject())
                    {
                        dependencies.Add(dependency.Name);
                        if (!dependencyGraph.ContainsKey(dependency.Name))
                        {
                            dependencyGraph[dependency.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
            }

            return dependencyGraph;
        }

        private static bool IsMatchingTarget(HashSet<string> targetFrameworkSet, string targetName)
        {
            if (targetFrameworkSet.Contains(targetName))
            {
                return true;
            }

            int separatorIndex = targetName.IndexOf('/');
            if (separatorIndex <= 0)
            {
                return false;
            }

            string baseTargetFramework = targetName.Substring(0, separatorIndex);
            return targetFrameworkSet.Contains(baseTargetFramework);
        }

        private static HashSet<string> GetReachablePackages(Dictionary<string, HashSet<string>> dependencyGraph,
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

                if (!dependencyGraph.TryGetValue(packageName, out HashSet<string>? dependencies))
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

        private static string ParsePackageNameFromTargetKey(string packageKey)
        {
            int separatorIndex = packageKey.IndexOf('/');
            return separatorIndex > 0 ? packageKey.Substring(0, separatorIndex) : packageKey;
        }

        private static string? NormalizeTargetFramework(string? targetFramework)
        {
            if (targetFramework is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                return targetFramework;
            }

            int separatorIndex = targetFramework.IndexOf('/');
            return separatorIndex > 0 ? targetFramework.Substring(0, separatorIndex) : targetFramework;
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
