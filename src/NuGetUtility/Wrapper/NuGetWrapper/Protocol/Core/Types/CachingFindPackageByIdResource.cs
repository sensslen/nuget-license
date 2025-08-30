// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    internal class CachingFindPackageByIdResource(FindPackageByIdResource findPackageByIdResource, SourceCacheContext cacheContext) : IFindPackageByIdResource
    {
        public async Task<IPackageDownloader?> TryGetPackageArchiveReader(PackageIdentity identity, CancellationToken cancellationToken)
        {
            try
            {
                NuGet.Packaging.IPackageDownloader result = await findPackageByIdResource.GetPackageDownloaderAsync(
                    new NuGet.Packaging.Core.PackageIdentity(identity.Id, new NuGetVersion(identity.Version.ToString()!)),
                    cacheContext,
                    NullLogger.Instance,
                    cancellationToken);
                return new WrappedPackageDownloader(result);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
