// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using NuGetUtility.Extensions;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ReferencedPackageReader(IMsBuildAbstraction msBuild,
                                         ILockFileFactory lockFileFactory,
                                         INuGetFrameworkUtility nuGetFrameworkUtility,
                                         IAssetsPackageDependencyReader assetsPackageDependencyReader,
                                         IPackagesConfigReader packagesConfigReader)
    {
        private const string ProjectReferenceIdentifier = "project";

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
        /// <returns>
        /// The project together with its resolved package identities (from project assets or
        /// packages.config) and the package folders recorded in the assets file (global packages folder
        /// plus any fallback folders, e.g. the SDK's NuGetFallbackFolder; empty when no assets file is
        /// available, such as packages.config projects).
        /// </returns>
        public ProjectWithReferencedPackages GetInstalledPackages(string projectPath, bool includeTransitive, string? targetFramework = null, bool excludePublishFalse = false)
        {
            IProject project = msBuild.GetProject(projectPath);

            if (TryGetInstalledPackagesFromAssetsFile(includeTransitive, project, targetFramework, excludePublishFalse, out IEnumerable<PackageIdentity>? dependencies, out IReadOnlyList<string> packageFolders, out IReadOnlyDictionary<PackageIdentity, string> packageContentHashes))
            {
                return new ProjectWithReferencedPackages(projectPath, dependencies, packageFolders) { PackageContentHashes = packageContentHashes };
            }

            if (project.HasPackagesConfigFile())
            {
                return new ProjectWithReferencedPackages(projectPath, packagesConfigReader.GetPackages(project), []);
            }

            return new ProjectWithReferencedPackages(projectPath, [], []);
        }

        private bool TryGetInstalledPackagesFromAssetsFile(bool includeTransitive,
                                                           IProject project,
                                                           string? targetFramework,
                                                           bool excludePublishFalse,
                                                           [NotNullWhen(true)] out IEnumerable<PackageIdentity>? installedPackages,
                                                           out IReadOnlyList<string> packageFolders,
                                                           out IReadOnlyDictionary<PackageIdentity, string> packageContentHashes)
        {
            installedPackages = null;
            packageFolders = [];
            packageContentHashes = new Dictionary<PackageIdentity, string>();
            if (!TryLoadAssetsFile(project, out ILockFile? assetsFile))
            {
                return false;
            }

            packageFolders = assetsFile.PackageFolders.ToList();

            string? normalizedRequestedTargetFramework = NormalizeTargetFrameworkOrNull(targetFramework);
            List<ILockFileTarget> selectedTargets = GetSelectedTargets(assetsFile, normalizedRequestedTargetFramework, targetFramework);

            HashSet<ILockFileLibrary> referencedLibraries = [];
            PublishExclusionContext publishExclusionContext = new(normalizedRequestedTargetFramework);

            foreach (ILockFileTarget target in selectedTargets)
            {
                HashSet<ILockFileLibrary> targetReferencedLibraries = [.. GetReferencedLibrariesForTarget(includeTransitive, assetsFile, target)];

                if (excludePublishFalse)
                {
                    HashSet<string> excludedPackages = GetExcludedPackagesForTarget(project,
                                                                                    assetsFile,
                                                                                    target,
                                                                                    includeTransitive,
                                                                                    publishExclusionContext);

                    targetReferencedLibraries.RemoveWhere(library => excludedPackages.Contains(library.Name));
                }

                referencedLibraries.AddRange(targetReferencedLibraries);
            }

            var identities = new List<PackageIdentity>(referencedLibraries.Count);
            var contentHashes = new Dictionary<PackageIdentity, string>();
            foreach (ILockFileLibrary library in referencedLibraries)
            {
                var identity = new PackageIdentity(library.Name, library.Version);
                identities.Add(identity);

                string? sha512 = assetsFile.GetPackageContentHash(library.Name, library.Version);
                if (sha512 is { Length: > 0 })
                {
                    contentHashes[identity] = sha512;
                }
            }

            installedPackages = identities;
            packageContentHashes = contentHashes;
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
                .Where(t => nuGetFrameworkUtility.IsEquivalent(normalizedRequestedTargetFramework, t.TargetFramework))
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
            string targetFrameworkForPublishMetadata = context.NormalizedRequestedTargetFramework ?? nuGetFrameworkUtility.Normalize(target.TargetFramework);
            string targetFrameworkCacheKey = targetFrameworkForPublishMetadata ?? string.Empty;

            // Remove packages with Publish=false metadata from the evaluated PackageReferences for this target only.
            if (!context.PublishFalsePackagesByFramework.TryGetValue(targetFrameworkCacheKey, out HashSet<string>? cachedPublishFalsePackages))
            {
                cachedPublishFalsePackages = GetPackagesExcludedFromPublish(project, targetFrameworkForPublishMetadata);
                context.PublishFalsePackagesByFramework[targetFrameworkCacheKey] = cachedPublishFalsePackages;
            }

            HashSet<string> excludedPackages = new(cachedPublishFalsePackages, StringComparer.OrdinalIgnoreCase);
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
                packageDependencies = assetsPackageDependencyReader.GetPackageDependenciesForTargetFramework(
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
            if (includeTransitive)
            {
                return dependencies;
            }

            ITargetFrameworkInformation targetFrameworkInformation = GetTargetFrameworkInformation(target, assetsFile);
            IEnumerable<ILibraryDependency> directDependencies = targetFrameworkInformation.Dependencies;
            return dependencies.Where(d => directDependencies.Any(direct => direct.Name == d.Name));
        }

        private static ITargetFrameworkInformation GetTargetFrameworkInformation(ILockFileTarget target, ILockFile assetsFile)
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
            HashSet<string> directDependencies = new(StringComparer.OrdinalIgnoreCase);
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

        private static HashSet<string> GetPackagesExcludedFromPublishDependencyPaths(Dictionary<string, HashSet<string>> packageDependencies,
                                                                                     IEnumerable<string> directDependencies,
                                                                                     ISet<string> publishFalseDirectDependencies)
        {
            HashSet<string> excludedPackages = new(publishFalseDirectDependencies, StringComparer.OrdinalIgnoreCase);
            if (packageDependencies.Count == 0)
            {
                return excludedPackages;
            }

            HashSet<string> publishableRoots = new(
                directDependencies.Where(package => !publishFalseDirectDependencies.Contains(package)),
                StringComparer.OrdinalIgnoreCase);

            HashSet<string> reachableFromPublishableRoots = GetReachablePackages(packageDependencies, publishableRoots);

            excludedPackages.UnionWith(packageDependencies.Keys.Where(packageName => !reachableFromPublishableRoots.Contains(packageName)));

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

        private static HashSet<string> GetReachablePackages(Dictionary<string, HashSet<string>> packageDependencies, IEnumerable<string> roots)
        {
            HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
            Stack<string> stack = new(roots);

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

            return nuGetFrameworkUtility.Normalize(targetFramework);
        }

        private bool TryLoadAssetsFile(IProject project,
            [NotNullWhen(true)] out ILockFile? assetsFile)
        {
            if (!project.TryGetAssetsPath(out string projectAssetsPath))
            {
                assetsFile = null;
                return false;
            }

            assetsFile = lockFileFactory.GetFromFile(projectAssetsPath);

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

            HashSet<string> excludedPackages = new(StringComparer.OrdinalIgnoreCase);
            foreach (PackageReferenceMetadata packageReference in packageReferences ?? [])
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
            public Dictionary<string, HashSet<string>> PublishFalsePackagesByFramework { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, HashSet<string>> DirectDependenciesByFramework { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, Dictionary<string, HashSet<string>>> PackageDependenciesByFramework { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, HashSet<string>> RecursiveExclusionsByInput { get; } = new(StringComparer.Ordinal);
        }
    }
}
