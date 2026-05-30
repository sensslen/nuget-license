// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Runtime.CompilerServices;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;

namespace NuGetUtility.PackageInformationReader
{
    public class PackageInformationReader(IWrappedSourceRepositoryProvider sourceRepositoryProvider,
                                          IGlobalPackagesFolderUtility globalPackagesFolderUtility,
                                          IEnumerable<CustomPackageInformation> customPackageInformation)
    {
        private readonly ISourceRepository[] _repositories = sourceRepositoryProvider.GetRepositories();

        public async IAsyncEnumerable<ReferencedPackageWithContext> GetPackageInfo(ProjectWithReferencedPackages projectWithReferencedPackages,
                                                                                   [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (PackageIdentity package in projectWithReferencedPackages.ReferencedPackages)
            {
                CustomPackageInformation? customInformation = TryGetPackageInfoFromCustomInformation(package);
                PackageSearchResult result = TryGetPackageInformationFromGlobalPackageFolder(package);
                if (result.Success)
                {
                    yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project,
                                                                  ApplyCustomInformation(result.Metadata!, customInformation));
                    continue;
                }
                result = await TryGetPackageInformationFromRepositories(_repositories, package, cancellation);
                if (result.Success)
                {
                    yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project,
                                                                  ApplyCustomInformation(result.Metadata!, customInformation));
                    continue;
                }
                if (customInformation is not null)
                {
                    yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project,
                                                                  new PackageMetadata(package, LicenseType.Overwrite, customInformation));
                    continue;
                }
                // simply return input - validation will fail later, as the required fields are missing
                yield return new ReferencedPackageWithContext(projectWithReferencedPackages.Project, new PackageMetadata(package));
            }
        }
        private PackageSearchResult TryGetPackageInformationFromGlobalPackageFolder(PackageIdentity package)
        {
            IPackageMetadata? metadata = globalPackagesFolderUtility.GetPackage(package);
            if (metadata is not null)
            {
                return new PackageSearchResult(metadata);
            }
            return new PackageSearchResult();
        }

        private static async Task<PackageSearchResult> TryGetPackageInformationFromRepositories(ISourceRepository[] repositories,
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
                if (updatedPackageMetadata is not null)
                {
                    if (updatedPackageMetadata.LicenseMetadata?.Type == LicenseType.File)
                    {
                        IPackageDownloader? downloader = await TryGetPackageDownloaderAsync(repository, package, cancellation);
                        if (downloader is not null)
                        {
                            return new PackageSearchResult(new LicenseAugmentedPackageMetadata(updatedPackageMetadata,
                                                                                               await downloader.ReadAsync(updatedPackageMetadata.LicenseMetadata.License, cancellation)));
                        }
                        return new PackageSearchResult();
                    }

                    return new PackageSearchResult(updatedPackageMetadata);
                }
            }

            return new PackageSearchResult();
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
