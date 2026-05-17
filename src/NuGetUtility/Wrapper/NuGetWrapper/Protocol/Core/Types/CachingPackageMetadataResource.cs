// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    internal class CachingPackageMetadataResource(PackageMetadataResource metadataResource, SourceCacheContext cacheContext)
        : IPackageMetadataResource
    {
        public async Task<IPackageMetadata?> TryGetMetadataAsync(PackageIdentity identity,
            CancellationToken cancellationToken)
        {
            try
            {
                IPackageSearchMetadata result = await metadataResource.GetMetadataAsync(new NuGet.Packaging.Core.PackageIdentity(identity.Id, new NuGetVersion(identity.Version.ToString()!)),
                                                                                        cacheContext,
                                                                                        NullLogger.Instance,
                                                                                        cancellationToken);
                return new WrappedPackageSearchMetadata(result);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private sealed class WrappedPackageSearchMetadata(IPackageSearchMetadata searchMetadata) : IPackageMetadata
        {
            public PackageIdentity Identity { get; } = new(searchMetadata.Identity.Id, new WrappedNuGetVersion(searchMetadata.Identity.Version));

            public string? Title => searchMetadata.Title;

            public Uri? LicenseUrl => searchMetadata.LicenseUrl;

            public string? ProjectUrl => searchMetadata.ProjectUrl?.ToString();

            public string? Description => searchMetadata.Description;

            public string? Summary => searchMetadata.Summary;

            public string? Copyright => null;

            public string? Authors => searchMetadata.Authors;

            public LicenseMetadata? LicenseMetadata { get; } = searchMetadata.LicenseMetadata;
        }
    }
}
