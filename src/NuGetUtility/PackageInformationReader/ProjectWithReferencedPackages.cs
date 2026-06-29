// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

namespace NuGetUtility.PackageInformationReader
{
    /// <param name="Project">The project file the packages were resolved from.</param>
    /// <param name="ReferencedPackages">The packages referenced by the project.</param>
    /// <param name="PackageFolders">
    /// Local package folders (global packages folder plus any fallback folders, e.g. the SDK's
    /// NuGetFallbackFolder) recorded in the project's assets file. Empty when no assets file is
    /// available (e.g. packages.config projects).
    /// </param>
    public record ProjectWithReferencedPackages(string Project, IEnumerable<PackageIdentity> ReferencedPackages, IReadOnlyList<string> PackageFolders)
    {
        /// <summary>
        /// Maps each referenced package to the content hash (SHA-512) recorded for it in the project's
        /// assets file. Used to key cached metadata so the same id+version resolved to different content
        /// is not shared. Empty when no assets file is available (e.g. packages.config projects).
        /// </summary>
        public IReadOnlyDictionary<PackageIdentity, string> PackageContentHashes { get; init; } = new Dictionary<PackageIdentity, string>();
    }
}
