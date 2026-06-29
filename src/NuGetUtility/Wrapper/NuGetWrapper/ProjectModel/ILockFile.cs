// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    public interface ILockFile
    {
        bool TryGetErrors(out string[] errors);
        IPackageSpec PackageSpec { get; }
        IEnumerable<ILockFileTarget> Targets { get; }
        string Path { get; }

        /// <summary>
        /// All package folders recorded in the assets file (the global packages folder followed by
        /// any fallback folders, e.g. the SDK's NuGetFallbackFolder). Packages may be extracted into
        /// any of these, so all must be consulted when resolving package metadata locally.
        /// </summary>
        IEnumerable<string> PackageFolders { get; }

        /// <summary>
        /// The content hash (SHA-512 of the .nupkg) recorded in the assets file for the given package,
        /// or null if the package has no recorded hash. Because a published id+version is only unique
        /// per feed, this hash is what distinguishes the same id+version resolved to different content.
        /// </summary>
        string? GetPackageContentHash(string packageName, INuGetVersion version);
    }
}
