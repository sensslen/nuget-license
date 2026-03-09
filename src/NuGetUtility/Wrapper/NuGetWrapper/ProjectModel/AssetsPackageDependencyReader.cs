// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    /// <summary>
    /// Reads package dependency relationships from a NuGet assets file for a requested target framework.
    /// </summary>
    public class AssetsPackageDependencyReader : IAssetsPackageDependencyReader
    {
        private const string PackageTypeIdentifier = "package";
        private readonly INuGetFrameworkUtility _nuGetFrameworkUtility;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetsPackageDependencyReader"/> class.
        /// </summary>
        /// <param name="nuGetFrameworkUtility">
        /// Utility used to determine semantic equivalence between target framework representations.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="nuGetFrameworkUtility"/> is <see langword="null"/>.
        /// </exception>
        public AssetsPackageDependencyReader(INuGetFrameworkUtility nuGetFrameworkUtility)
        {
            _nuGetFrameworkUtility = nuGetFrameworkUtility ?? throw new ArgumentNullException(nameof(nuGetFrameworkUtility));
        }

        /// <summary>
        /// Gets package-to-dependency mappings for libraries in the target framework section of the specified assets file.
        /// </summary>
        /// <param name="assetsPath">Path to the <c>project.assets.json</c> file to inspect.</param>
        /// <param name="normalizedTargetFramework">Normalized target framework moniker used to select matching targets.</param>
        /// <returns>
        /// A dictionary that maps package IDs to a case-insensitive set of dependency package IDs
        /// (<c>Dictionary&lt;string, HashSet&lt;string&gt;&gt;</c>).
        /// Returns an empty dictionary when the assets file does not exist or cannot be read.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="assetsPath"/> or <paramref name="normalizedTargetFramework"/> is <see langword="null"/>.
        /// </exception>
        public Dictionary<string, HashSet<string>> GetPackageDependenciesForTargetFramework(ILockFile lockFile, string normalizedTargetFramework)
        {
            if (normalizedTargetFramework is null)
            {
                throw new ArgumentNullException(nameof(normalizedTargetFramework));
            }

            try
            {
                return BuildDependencyMapFromAssetsFile(lockFile, normalizedTargetFramework);
            }
            catch (IOException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to I/O error. AssetsPath={0}, TargetFramework={1}, Exception={2}",
                    lockFile.Path,
                    normalizedTargetFramework,
                    exception);
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch (FormatException exception)
            {
                Trace.TraceWarning(
                    "Failed to analyze transitive Publish=false exclusions due to invalid assets data. AssetsPath={0}, TargetFramework={1}, Exception={2}",
                    lockFile.Path,
                    normalizedTargetFramework,
                    exception);
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private Dictionary<string, HashSet<string>> BuildDependencyMapFromAssetsFile(ILockFile lockFile, string requestedTargetFramework)
        {
            Dictionary<string, HashSet<string>> packageDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (ILockFileTarget target in lockFile.Targets)
            {
                if (!_nuGetFrameworkUtility.IsEquivalent(requestedTargetFramework, target.TargetFramework))
                {
                    continue;
                }

                foreach (ILockFileTargetLibrary library in target.Libraries)
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

                    foreach (string dependencyName in library.Dependencies.Select(d => d.Id))
                    {
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
