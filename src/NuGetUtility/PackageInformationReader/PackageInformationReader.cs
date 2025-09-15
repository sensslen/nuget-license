// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Runtime.CompilerServices;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;

namespace NuGetUtility.PackageInformationReader
{
    public class PackageInformationReader
    {
        private readonly IGlobalPackagesFolderUtility _globalPackagesFolderUtility;
        private readonly IEnumerable<CustomPackageInformation> _customPackageInformation;
        private readonly ISourceRepository[] _repositories;

        public PackageInformationReader(IWrappedSourceRepositoryProvider sourceRepositoryProvider,
            IGlobalPackagesFolderUtility globalPackagesFolderUtility,
            IEnumerable<CustomPackageInformation> customPackageInformation)
        {
            _globalPackagesFolderUtility = globalPackagesFolderUtility;
            _customPackageInformation = customPackageInformation;
            _repositories = sourceRepositoryProvider.GetRepositories();
        }

        public async IAsyncEnumerable<ReferencedPackageWithContext> GetPackageInfo(
            ProjectWithReferencedPackages projectWithReferencedPackages,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (PackageIdentity package in projectWithReferencedPackages.ReferencedPackages)
            {
                PackageSearchResult result = TryGetPackageInfoFromCustomInformation(package);
                if (result.Success)
                {
                    yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project, result.Metadata!);
                    continue;
                }
                result = TryGetPackageInformationFromGlobalPackageFolder(package);
                if (result.Success)
                {
                    yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project, result.Metadata!);
                    continue;
                }
                result = await TryGetPackageInformationFromRepositories(_repositories, package, cancellation);
                if (result.Success)
                {
                    yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project, result.Metadata!);
                    continue;
                }
                // simply return input - validation will fail later, as the required fields are missing
                yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project, new PackageMetadata(package));
            }
        }
        private PackageSearchResult TryGetPackageInformationFromGlobalPackageFolder(PackageIdentity package)
        {
            IPackageMetadata? metadata = _globalPackagesFolderUtility.GetPackage(package);
            if (metadata != null)
            {
                return new PackageSearchResult(metadata);
            }
            return new PackageSearchResult();
        }

        private static async Task<PackageSearchResult> TryGetPackageInformationFromRepositories(
            ISourceRepository[] repositories,
            PackageIdentity package,
            CancellationToken cancellation)
        {
            foreach (ISourceRepository repository in repositories)
            {
                IPackageMetadataResource? resource = await TryGetPackageMetadataResource(repository, cancellation);
                if (resource == null)
                {
                    continue;
                }

                IPackageMetadata? updatedPackageMetadata = await resource.TryGetMetadataAsync(package, cancellation);
                if (updatedPackageMetadata != null)
                {
                    if (updatedPackageMetadata.LicenseMetadata?.Type == LicenseType.File)
                    {
                        IPackageDownloader? downloader = await TryGetPackageDownloaderAsync(repository, package, cancellation);
                        if (downloader != null)
                        {
                            return new PackageSearchResult(new LicenseAugmentedPackageMetadata(updatedPackageMetadata, await downloader.ReadAsync(updatedPackageMetadata.LicenseMetadata.License, cancellation)));
                        }
                        return new PackageSearchResult();
                    }

                    return new PackageSearchResult(updatedPackageMetadata);
                }
            }

            return new PackageSearchResult();
        }

        private PackageSearchResult TryGetPackageInfoFromCustomInformation(PackageIdentity package)
        {
            CustomPackageInformation resolvedCustomInformation = _customPackageInformation.FirstOrDefault(info =>
                info.Id.Equals(package.Id) && info.Version.Equals(package.Version));
            if (resolvedCustomInformation == default)
            {
                return new PackageSearchResult();
            }

            return new PackageSearchResult(new PackageMetadata(package, LicenseType.Overwrite, resolvedCustomInformation));
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

        private sealed record PackageSearchResult
        {
            public bool Success { get; }
            public IPackageMetadata? Metadata { get; }

            public PackageSearchResult(IPackageMetadata metadata)
            {
                Success = true;
                Metadata = metadata;
            }

            public PackageSearchResult()
            {
                Success = false;
            }
        }
    }
}
