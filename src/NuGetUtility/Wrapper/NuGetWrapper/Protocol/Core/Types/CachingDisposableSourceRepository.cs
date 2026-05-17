// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Protocol.Core.Types;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    internal sealed class CachingDisposableSourceRepository(SourceRepository sourceRepository) : IDisposableSourceRepository
    {
        private readonly SourceCacheContext _cacheContext = new();
        private IPackageMetadataResource? _packageMetadataResource;
        private IFindPackageByIdResource? _findPackageByIdResource;

        public void Dispose()
        {
            _packageMetadataResource = null;
            _cacheContext.Dispose();
        }

        public async Task<IPackageMetadataResource?> GetPackageMetadataResourceAsync(CancellationToken token)
        {
            if (_packageMetadataResource is not null)
            {
                return _packageMetadataResource;
            }

            _packageMetadataResource = new CachingPackageMetadataResource(await sourceRepository.GetResourceAsync<PackageMetadataResource>(token),
                                                                          _cacheContext);
            return _packageMetadataResource;
        }

        public async Task<IFindPackageByIdResource?> GetPackageArchiveReaderAsync(CancellationToken token)
        {
            if (_findPackageByIdResource is not null)
            {
                return _findPackageByIdResource;
            }

            _findPackageByIdResource = new CachingFindPackageByIdResource(await sourceRepository.GetResourceAsync<FindPackageByIdResource>(token),
                                                                          _cacheContext);
            return _findPackageByIdResource;
        }
    }
}
