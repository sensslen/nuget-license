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
    public record ProjectWithReferencedPackages(string Project, IEnumerable<PackageIdentity> ReferencedPackages, IReadOnlyList<string> PackageFolders);
}
