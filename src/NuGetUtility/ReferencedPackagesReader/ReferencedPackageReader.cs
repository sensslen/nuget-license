// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using NuGetUtility.Extensions;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ReferencedPackageReader
    {
        private const string ProjectReferenceIdentifier = "project";
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

            var referencedLibraries = new HashSet<ILockFileLibrary>();

            if (targetFramework is not null)
            {
                IEnumerable<ILockFileTarget> matchingTargets = assetsFile.Targets!.Where(t => t.TargetFramework.Equals(targetFramework));
                if (!matchingTargets.Any())
                {
                    throw new ReferencedPackageReaderException($"Target framework {targetFramework} not found.");
                }

                foreach (ILockFileTarget target in matchingTargets)
                {
                    referencedLibraries.AddRange(GetReferencedLibrariesForTarget(includeTransitive, assetsFile, target));
                }
            }
            else
            {
                foreach (ILockFileTarget target in assetsFile.Targets!)
                {
                    referencedLibraries.AddRange(GetReferencedLibrariesForTarget(includeTransitive, assetsFile, target));
                }
            }

            if (excludePublishFalse)
            {
                // Remove packages with Publish=false metadata from the evaluated PackageReferences.
                HashSet<string> excludedPackages = GetPackagesExcludedFromPublish(project, targetFramework);
                referencedLibraries.RemoveWhere(library => excludedPackages.Contains(library.Name));
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

        private bool TryLoadAssetsFile(IProject project, [NotNullWhen(true)] out ILockFile? assetsFile)
        {
            if (!project.TryGetAssetsPath(out string assetsPath))
            {
                assetsFile = null;
                return false;
            }
            assetsFile = _lockFileFactory.GetFromFile(assetsPath);

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
    }
}
