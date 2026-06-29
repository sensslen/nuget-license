// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

namespace NuGetUtility.PackageInformationReader
{
    /// <summary>
    /// Key for the shared resolved-metadata cache. The content hash (the SHA-512 of the .nupkg recorded
    /// in the project's assets file) is part of the key alongside the identity: a published id+version is
    /// only unique per feed, so the same id+version resolved to different content (different feeds or
    /// package folders) produces a different hash and is never served from a shared cache entry.
    /// </summary>
    public sealed record PackageMetadataCacheKey(PackageIdentity Identity, string? ContentHash);
}
