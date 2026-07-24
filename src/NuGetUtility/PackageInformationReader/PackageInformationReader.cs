// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;

namespace NuGetUtility.PackageInformationReader
{
    public class PackageInformationReader(IWrappedSourceRepositoryProvider sourceRepositoryProvider,
                                          IGlobalPackagesFolderUtility globalPackagesFolderUtility,
                                          IEnumerable<CustomPackageInformation> customPackageInformation,
                                          ConcurrentDictionary<PackageMetadataCacheKey, IPackageMetadata> resolvedMetadataCache)
    {
        private readonly ISourceRepository[] _repositories = sourceRepositoryProvider.GetRepositories();

        public async IAsyncEnumerable<ReferencedPackageWithContext> GetPackageInfo(ProjectWithReferencedPackages projectWithReferencedPackages,
                                                                                   [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (PackageIdentity package in projectWithReferencedPackages.ReferencedPackages)
            {
                CustomPackageInformation? customInformation = TryGetPackageInfoFromCustomInformation(package);
                PackageMetadataCacheKey? cacheKey = TryCreateCacheKey(projectWithReferencedPackages, package);

                IPackageMetadata? metadata = TryGetCachedMetadata(cacheKey);
                if (metadata is null)
                {
                    metadata = TryGetPackageInformationFromGlobalPackageFolder(package)
                               ?? await TryGetPackageInformationFromRepositories(_repositories, package, cancellation);

                    if (metadata is not null && cacheKey is not null)
                    {
                        resolvedMetadataCache.TryAdd(cacheKey, metadata);
                    }
                }

                yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project,
                                                              BuildPackageMetadata(package, metadata, customInformation));
            }
        }

        // A package's metadata (license / nuspec) is intrinsic to the resolved package content, so
        // resolve it at most once per (id+version, content hash) even when many projects reference
        // the same package - avoiding a re-open and re-parse of the same nuspec per project. The
        // content hash (recorded in the assets file) is part of the key so that the same id+version
        // resolved to different content (different feeds/folders) is never served a stale entry.
        // Without a content hash we cannot prove two references resolved to the same content, so
        // such packages are not cached. Override information is applied per result, so it is
        // never baked into the entry.
        private static PackageMetadataCacheKey? TryCreateCacheKey(ProjectWithReferencedPackages projectWithReferencedPackages, PackageIdentity package)
        {
            if (projectWithReferencedPackages.PackageContentHashes.TryGetValue(package, out string? contentHash) && contentHash is { Length: > 0 })
            {
                return new PackageMetadataCacheKey(package, contentHash);
            }
            return null;
        }

        private IPackageMetadata? TryGetCachedMetadata(PackageMetadataCacheKey? cacheKey)
        {
            if (cacheKey is not null && resolvedMetadataCache.TryGetValue(cacheKey, out IPackageMetadata? cachedMetadata))
            {
                return cachedMetadata;
            }
            return null;
        }

        private static IPackageMetadata BuildPackageMetadata(PackageIdentity package, IPackageMetadata? metadata, CustomPackageInformation? customInformation)
        {
            if (metadata is not null)
            {
                return ApplyCustomInformation(metadata, customInformation);
            }
            if (customInformation is not null)
            {
                return new PackageMetadata(package, customInformation);
            }
            // simply return input - validation will fail later, as the required fields are missing
            return new PackageMetadata(package);
        }

        private IPackageMetadata? TryGetPackageInformationFromGlobalPackageFolder(PackageIdentity package)
        {
            return globalPackagesFolderUtility.GetPackage(package);
        }

        private static async Task<IPackageMetadata?> TryGetPackageInformationFromRepositories(ISourceRepository[] repositories,
                                                                                              PackageIdentity package,
                                                                                              CancellationToken cancellation)
        {
            foreach (ISourceRepository repository in repositories)
            {
                IPackageMetadataResource? resource = await TryGetPackageMetadataResource(repository, cancellation);
                if (resource is null)
                {
                    continue;
                }

                IPackageMetadata? updatedPackageMetadata = await resource.TryGetMetadataAsync(package, cancellation);
                if (updatedPackageMetadata is null)
                {
                    continue;
                }

                if (updatedPackageMetadata.LicenseMetadata is not LicenseMetadata.File file)
                {
                    return updatedPackageMetadata;
                }

                IPackageDownloader? downloader = await TryGetPackageDownloaderAsync(repository, package, cancellation);
                if (downloader is null)
                {
                    return null;
                }
                return new LicenseAugmentedPackageMetadata(updatedPackageMetadata,
                                                           await downloader.ReadAsync(file.FileLocation, cancellation));
            }

            return null;
        }

        private CustomPackageInformation? TryGetPackageInfoFromCustomInformation(PackageIdentity package)
        {
            CustomPackageInformation? resolvedCustomInformation = customPackageInformation.FirstOrDefault(info =>
                string.Equals(info.Id, package.Id, StringComparison.OrdinalIgnoreCase) && info.Version.Equals(package.Version));
            return resolvedCustomInformation;
        }

        private static IPackageMetadata ApplyCustomInformation(IPackageMetadata metadata, CustomPackageInformation? customInformation)
        {
            if (customInformation is null)
            {
                return metadata;
            }

            return new OverridePackageMetadata(metadata, customInformation);
        }

        private static async Task<IPackageMetadataResource?> TryGetPackageMetadataResource(ISourceRepository repository, CancellationToken token)
        {
            try
            {
                return await repository.GetPackageMetadataResourceAsync(token);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<IPackageDownloader?> TryGetPackageDownloaderAsync(ISourceRepository repository, PackageIdentity package, CancellationToken token)
        {
            try
            {
                IFindPackageByIdResource? archiveReader = await repository.GetPackageArchiveReaderAsync(token);
                if (archiveReader is null)
                {
                    return null;
                }
                return await archiveReader.TryGetPackageDownloader(package, token);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
