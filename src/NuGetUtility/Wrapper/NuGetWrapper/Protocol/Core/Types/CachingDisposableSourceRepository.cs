// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Protocol.Core.Types;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    internal sealed class CachingDisposableSourceRepository : IDisposableSourceRepository
    {
        private readonly SourceCacheContext _cacheContext = new();
        private readonly SourceRepository _sourceRepository;
        private IPackageMetadataResource? _packageMetadataResource;
        private IFindPackageByIdResource? _findPackageByIdResource;

        public CachingDisposableSourceRepository(SourceRepository repo)
        {
            _sourceRepository = repo;
        }

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

            _packageMetadataResource = new CachingPackageMetadataResource(
                await _sourceRepository.GetResourceAsync<PackageMetadataResource>(token),
                _cacheContext);
            return _packageMetadataResource;
        }

        public async Task<IFindPackageByIdResource?> GetPackageArchiveReaderAsync(CancellationToken token)
        {
            if (_findPackageByIdResource is not null)
            {
                return _findPackageByIdResource;
            }

            _findPackageByIdResource = new CachingFindPackageByIdResource(
                await _sourceRepository.GetResourceAsync<FindPackageByIdResource>(token),
                _cacheContext);
            return _findPackageByIdResource;
        }
    }
}
