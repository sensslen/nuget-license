// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics;
using NuGet.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    public class AssetsPackageDependencyReader : IAssetsPackageDependencyReader
    {
        private const string PackageTypeIdentifier = "package";
        private readonly INuGetFrameworkUtility _nuGetFrameworkUtility;

        public AssetsPackageDependencyReader(INuGetFrameworkUtility nuGetFrameworkUtility)
        {
            _nuGetFrameworkUtility = nuGetFrameworkUtility;
        }

        public Dictionary<string, HashSet<string>> GetPackageDependenciesForTargetFramework(string assetsPath, string normalizedTargetFramework)
        {
            if (!File.Exists(assetsPath))
            {
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                LockFile lockFile = new LockFileFormat().Read(assetsPath);
                return BuildDependencyMapFromAssetsFile(lockFile, normalizedTargetFramework);
            }
            catch (IOException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to I/O error. AssetsPath={0}, TargetFramework={1}, Exception={2}",
                    assetsPath,
                    normalizedTargetFramework,
                    exception);
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch (FormatException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to invalid assets data. AssetsPath={0}, TargetFramework={1}, Exception={2}",
                    assetsPath,
                    normalizedTargetFramework,
                    exception);
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private Dictionary<string, HashSet<string>> BuildDependencyMapFromAssetsFile(LockFile lockFile, string requestedTargetFramework)
        {
            Dictionary<string, HashSet<string>> packageDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (LockFileTarget target in lockFile.Targets)
            {
                if (!_nuGetFrameworkUtility.IsEquivalent(requestedTargetFramework, new WrappedNuGetFramework(target.TargetFramework)))
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

                    foreach (NuGet.Packaging.Core.PackageDependency dependency in library.Dependencies)
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
    }
}
