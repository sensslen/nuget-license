// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Concurrent;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension;
using NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Test.PackageInformationReader
{
    internal class PackageInformationReaderTest
    {
        public PackageInformationReaderTest()
        {
            _sourceRepositoryProvider = Substitute.For<IWrappedSourceRepositoryProvider>();
            _customPackageInformation = Enumerable.Empty<CustomPackageInformation>().ToList();
            _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
            _fixture.Customizations.Add(new NuGetVersionBuilder());
            _fixture.Customizations.Add(new CustomPackageInformationBuilderWithOptionalFileds());
            _repositories = [];
            _globalPackagesFolderUtility = Substitute.For<IGlobalPackagesFolderUtility>();

            _globalPackagesFolderUtility.GetPackage(Arg.Any<PackageIdentity>()).Returns(default(IPackageMetadata?));

            _sourceRepositoryProvider.GetRepositories()
                .Returns(_ =>
                {
                    _repositories = _fixture.CreateMany<ISourceRepository>().ToArray();
                    foreach (ISourceRepository repo in _repositories)
                    {
                        repo.GetPackageMetadataResourceAsync(default).Returns(_ => Task.FromResult(default(IPackageMetadataResource?)));
                    }
                    return _repositories;
                });

            _uut = SetupUut();
        }

        private NuGetUtility.PackageInformationReader.PackageInformationReader SetupUut()
        {
            return new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, _customPackageInformation, _metadataCache);
        }

        private readonly NuGetUtility.PackageInformationReader.PackageInformationReader _uut;
        private readonly IWrappedSourceRepositoryProvider _sourceRepositoryProvider;
        private readonly List<CustomPackageInformation> _customPackageInformation;
        private readonly IFixture _fixture;
        private ISourceRepository[] _repositories;
        private readonly IGlobalPackagesFolderUtility _globalPackagesFolderUtility;
        private readonly ConcurrentDictionary<PackageIdentity, IPackageMetadata> _metadataCache = new();

        [Test]
        public async Task GetPackageInfo_Should_ReturnCustomInformation_IfPackageMetadataIsNotFound()
        {
            List<CustomPackageInformation> customPackageInformation = _fixture.CreateMany<CustomPackageInformation>().ToList();
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, customPackageInformation, _metadataCache);

            IEnumerable<PackageIdentity> searchedPackages = customPackageInformation.Select(p => new PackageIdentity(p.Id, p.Version));
            string project = _fixture.Create<string>();
            var packageSearchRequest = new ProjectWithReferencedPackages(project, searchedPackages, []);
            ReferencedPackageWithContext[] result = (await localUut.GetPackageInfo(packageSearchRequest, CancellationToken.None).Synchronize())
                .ToArray();

            await CheckResult(result, project, customPackageInformation, LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_AugmentLocalPackageCacheWithCustomLicenseInformation()
        {
            CustomPackageInformation packageMetadata = _fixture.Create<CustomPackageInformation>();
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id, packageMetadata.Version, _fixture.Create<string>());
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await AssertMergedMetadata(result.PackageInfo, packageMetadata, customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_ResolvePackageMetadataOncePerIdentity_WhenCacheIsShared()
        {
            CustomPackageInformation packageMetadata = _fixture.Create<CustomPackageInformation>();
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            var sharedCache = new ConcurrentDictionary<PackageIdentity, IPackageMetadata>();
            var firstProjectReader = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, _customPackageInformation, sharedCache);
            var secondProjectReader = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, _customPackageInformation, sharedCache);

            _ = await firstProjectReader.GetPackageInfo(new ProjectWithReferencedPackages("projectA", [identity], []), CancellationToken.None).Synchronize();
            ReferencedPackageWithContext[] secondResult = (await secondProjectReader.GetPackageInfo(new ProjectWithReferencedPackages("projectB", [identity], []), CancellationToken.None).Synchronize()).ToArray();

            // The package is read from the global packages folder only once, even though two projects reference it.
            _globalPackagesFolderUtility.Received(1).GetPackage(identity);
            // The second project still gets the package - served from the shared cache.
            await Assert.That(secondResult.Length).IsEqualTo(1);
            await Assert.That(secondResult[0].PackageInfo.Identity).IsEqualTo(identity);
        }

        [Test]
        public async Task GetPackageInfo_Should_NotCacheUnresolvedPackages()
        {
            CustomPackageInformation packageMetadata = _fixture.Create<CustomPackageInformation>();
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            // GetPackage returns null by default and the repositories yield nothing, so the package is
            // never resolved - and therefore must never be cached.

            var sharedCache = new ConcurrentDictionary<PackageIdentity, IPackageMetadata>();
            var firstProjectReader = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, _customPackageInformation, sharedCache);
            var secondProjectReader = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, _customPackageInformation, sharedCache);

            _ = await firstProjectReader.GetPackageInfo(new ProjectWithReferencedPackages("projectA", [identity], []), CancellationToken.None).Synchronize();
            _ = await secondProjectReader.GetPackageInfo(new ProjectWithReferencedPackages("projectB", [identity], []), CancellationToken.None).Synchronize();

            // An unresolved package must not be cached, so the second project still attempts to resolve it.
            _globalPackagesFolderUtility.Received(2).GetPackage(identity);
        }

        [Test]
        public async Task GetPackageInfo_Should_MatchCustomInformationPackageIdIgnoringCase()
        {
            CustomPackageInformation packageMetadata = CreatePackageInformationWithOptionalFields();
            var identity = new PackageIdentity(packageMetadata.Id.ToUpperInvariant(), packageMetadata.Version);
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id.ToLowerInvariant(), packageMetadata.Version, _fixture.Create<string>());
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata with { Id = identity.Id }, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await Assert.That(result.PackageInfo.Identity.Id).IsEqualTo(identity.Id);
            await Assert.That(result.PackageInfo.LicenseMetadata!.License).IsEqualTo(customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_FallBackToAllFetchedOptionalFields_WhenCustomInformationOnlyProvidesLicense()
        {
            CustomPackageInformation packageMetadata = CreatePackageInformationWithOptionalFields();
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id, packageMetadata.Version, _fixture.Create<string>());
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await AssertMergedMetadata(result.PackageInfo, packageMetadata, customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_AugmentRepositoryPackageMetadataWithCustomLicenseInformation()
        {
            CustomPackageInformation packageMetadata = _fixture.Create<CustomPackageInformation>();
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id, packageMetadata.Version, _fixture.Create<string>());
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadataResource metadataResource = Substitute.For<IPackageMetadataResource>();
            _repositories[0].GetPackageMetadataResourceAsync(default).Returns(_ => Task.FromResult<IPackageMetadataResource?>(metadataResource));
            metadataResource.TryGetMetadataAsync(identity, Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IPackageMetadata?>(CreatePackageMetadata(packageMetadata, LicenseType.Expression)));

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await AssertMergedMetadata(result.PackageInfo, packageMetadata, customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_PreferCustomOptionalFieldsOverFetchedPackageMetadata()
        {
            CustomPackageInformation packageMetadata = CreatePackageInformationWithOptionalFields();
            CustomPackageInformation customPackageInformation = CreatePackageInformationWithOptionalFields(packageMetadata.Id, packageMetadata.Version);
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await AssertPackageInformation(result.PackageInfo, customPackageInformation);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_MergeCustomAndFetchedOptionalFieldsIndividually()
        {
            CustomPackageInformation packageMetadata = CreatePackageInformationWithOptionalFields();
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id,
                                                                     packageMetadata.Version,
                                                                     _fixture.Create<string>(),
                                                                     Authors: _fixture.Create<string>(),
                                                                     Title: null,
                                                                     ProjectUrl: _fixture.Create<string>(),
                                                                     Summary: null,
                                                                     Description: _fixture.Create<string>(),
                                                                     LicenseUrl: null);
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await Assert.That(result.PackageInfo.LicenseMetadata!.License).IsEqualTo(customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
            await Assert.That(result.PackageInfo.Copyright).IsEqualTo(packageMetadata.Copyright);
            await Assert.That(result.PackageInfo.Authors).IsEqualTo(customPackageInformation.Authors);
            await Assert.That(result.PackageInfo.Title).IsEqualTo(packageMetadata.Title);
            await Assert.That(result.PackageInfo.ProjectUrl).IsEqualTo(customPackageInformation.ProjectUrl);
            await Assert.That(result.PackageInfo.Summary).IsEqualTo(packageMetadata.Summary);
            await Assert.That(result.PackageInfo.Description).IsEqualTo(customPackageInformation.Description);
            await Assert.That(result.PackageInfo.LicenseUrl).IsEqualTo(packageMetadata.LicenseUrl);
        }

        [Test]
        public async Task GetPackageInfo_Should_TreatEmptyCustomOptionalStringsAsOverrides()
        {
            CustomPackageInformation packageMetadata = CreatePackageInformationWithOptionalFields();
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id,
                                                                     packageMetadata.Version,
                                                                     _fixture.Create<string>(),
                                                                     Copyright: string.Empty,
                                                                     Authors: string.Empty,
                                                                     Title: string.Empty,
                                                                     ProjectUrl: string.Empty,
                                                                     Summary: string.Empty,
                                                                     Description: string.Empty);
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadata localMetadata = CreatePackageMetadata(packageMetadata, LicenseType.Expression);
            _globalPackagesFolderUtility.GetPackage(identity).Returns(localMetadata);

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await Assert.That(result.PackageInfo.Copyright).IsEqualTo(string.Empty);
            await Assert.That(result.PackageInfo.Authors).IsEqualTo(string.Empty);
            await Assert.That(result.PackageInfo.Title).IsEqualTo(string.Empty);
            await Assert.That(result.PackageInfo.ProjectUrl).IsEqualTo(string.Empty);
            await Assert.That(result.PackageInfo.Summary).IsEqualTo(string.Empty);
            await Assert.That(result.PackageInfo.Description).IsEqualTo(string.Empty);
            await Assert.That(result.PackageInfo.LicenseUrl).IsEqualTo(packageMetadata.LicenseUrl);
            await Assert.That(result.PackageInfo.LicenseMetadata!.License).IsEqualTo(customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        [Test]
        public async Task GetPackageInfo_Should_ApplyCustomLicenseInformationAfterRepositoryFileLicenseIsDownloaded()
        {
            CustomPackageInformation packageMetadata = _fixture.Create<CustomPackageInformation>();
            var identity = new PackageIdentity(packageMetadata.Id, packageMetadata.Version);
            CustomPackageInformation customPackageInformation = new(packageMetadata.Id, packageMetadata.Version, _fixture.Create<string>());
            var localUut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider,
                                                                                              _globalPackagesFolderUtility,
                                                                                              [customPackageInformation], _metadataCache);
            IPackageMetadataResource metadataResource = Substitute.For<IPackageMetadataResource>();
            IFindPackageByIdResource archiveReader = Substitute.For<IFindPackageByIdResource>();
            IPackageDownloader packageDownloader = Substitute.For<IPackageDownloader>();
            _repositories[0].GetPackageMetadataResourceAsync(default).Returns(_ => Task.FromResult<IPackageMetadataResource?>(metadataResource));
            _repositories[0].GetPackageArchiveReaderAsync(default).Returns(_ => Task.FromResult<IFindPackageByIdResource?>(archiveReader));
            metadataResource.TryGetMetadataAsync(identity, Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IPackageMetadata?>(CreatePackageMetadata(packageMetadata, LicenseType.File, "LICENSE.txt")));
            archiveReader.TryGetPackageDownloader(identity, Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IPackageDownloader?>(packageDownloader));
            packageDownloader.ReadAsync("LICENSE.txt", Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_fixture.Create<string>()));

            ReferencedPackageWithContext result = await GetSinglePackageInfo(localUut, identity);

            await AssertMergedMetadata(result.PackageInfo, packageMetadata, customPackageInformation.License);
            await Assert.That(result.PackageInfo.LicenseMetadata!.Type).IsEqualTo(LicenseType.Overwrite);
        }

        private async Task<ReferencedPackageWithContext> GetSinglePackageInfo(
            NuGetUtility.PackageInformationReader.PackageInformationReader packageInformationReader,
            PackageIdentity packageIdentity)
        {
            string project = _fixture.Create<string>();
            var packageSearchRequest = new ProjectWithReferencedPackages(project, [packageIdentity], []);
            ReferencedPackageWithContext[] result = (await packageInformationReader.GetPackageInfo(packageSearchRequest, CancellationToken.None).Synchronize())
                .ToArray();

            await Assert.That(result.Length).IsEqualTo(1);
            await Assert.That(result[0].Context).IsEqualTo(project);
            return result[0];
        }

        private static IPackageMetadata CreatePackageMetadata(CustomPackageInformation packageInformation, LicenseType licenseType, string? license = null)
        {
            var identity = new PackageIdentity(packageInformation.Id, packageInformation.Version);
            IPackageMetadata metadata = Substitute.For<IPackageMetadata>();
            metadata.Identity.Returns(identity);
            metadata.Copyright.Returns(packageInformation.Copyright);
            metadata.Authors.Returns(packageInformation.Authors);
            metadata.Title.Returns(packageInformation.Title);
            metadata.ProjectUrl.Returns(packageInformation.ProjectUrl);
            metadata.Summary.Returns(packageInformation.Summary);
            metadata.Description.Returns(packageInformation.Description);
            metadata.LicenseUrl.Returns(packageInformation.LicenseUrl);
            metadata.LicenseMetadata.Returns(new LicenseMetadata(licenseType, license ?? packageInformation.License));
            return metadata;
        }

        private CustomPackageInformation CreatePackageInformationWithOptionalFields(string? id = null, INuGetVersion? version = null)
        {
            return new CustomPackageInformation(id ?? _fixture.Create<string>(),
                                                version ?? _fixture.Create<INuGetVersion>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<string>(),
                                                _fixture.Create<Uri>());
        }

        private static async Task AssertMergedMetadata(IPackageMetadata result, CustomPackageInformation packageMetadata, string expectedLicense)
        {
            await Assert.That(result.Identity.Id).IsEqualTo(packageMetadata.Id);
            await Assert.That(result.Identity.Version).IsEqualTo(packageMetadata.Version);
            await Assert.That(result.LicenseMetadata!.License).IsEqualTo(expectedLicense);
            await Assert.That(result.Copyright).IsEqualTo(packageMetadata.Copyright);
            await Assert.That(result.Authors).IsEqualTo(packageMetadata.Authors);
            await Assert.That(result.Title).IsEqualTo(packageMetadata.Title);
            await Assert.That(result.ProjectUrl).IsEqualTo(packageMetadata.ProjectUrl);
            await Assert.That(result.Summary).IsEqualTo(packageMetadata.Summary);
            await Assert.That(result.Description).IsEqualTo(packageMetadata.Description);
            await Assert.That(result.LicenseUrl).IsEqualTo(packageMetadata.LicenseUrl);
        }

        private static async Task AssertPackageInformation(IPackageMetadata result, CustomPackageInformation expected)
        {
            await Assert.That(result.Identity.Id).IsEqualTo(expected.Id);
            await Assert.That(result.Identity.Version).IsEqualTo(expected.Version);
            await Assert.That(result.LicenseMetadata!.License).IsEqualTo(expected.License);
            await Assert.That(result.Copyright).IsEqualTo(expected.Copyright);
            await Assert.That(result.Authors).IsEqualTo(expected.Authors);
            await Assert.That(result.Title).IsEqualTo(expected.Title);
            await Assert.That(result.ProjectUrl).IsEqualTo(expected.ProjectUrl);
            await Assert.That(result.Summary).IsEqualTo(expected.Summary);
            await Assert.That(result.Description).IsEqualTo(expected.Description);
            await Assert.That(result.LicenseUrl).IsEqualTo(expected.LicenseUrl);
        }

        private async Task<(string Project, ReferencedPackageWithContext[] Result)> PerformSearch(
            IEnumerable<PackageIdentity> searchedPackages)
        {
            string project = _fixture.Create<string>();
            var packageSearchRequest = new ProjectWithReferencedPackages(project, searchedPackages, []);
            ReferencedPackageWithContext[] result = (await _uut!.GetPackageInfo(packageSearchRequest, CancellationToken.None).Synchronize())
                .ToArray();
            return (project, result);
        }

        private static async Task CheckResult(ReferencedPackageWithContext[] result,
            string project,
            IEnumerable<CustomPackageInformation> packages,
            LicenseType licenseType)
        {
            await Assert.That(packages).IsEquivalentTo(result.Select(s => new CustomPackageInformation(s.PackageInfo.Identity.Id,
                                                                                                         s.PackageInfo.Identity.Version,
                                                                                                         s.PackageInfo.LicenseMetadata!.License,
                                                                                                         s.PackageInfo.Copyright,
                                                                                                         s.PackageInfo.Authors,
                                                                                                         s.PackageInfo.Title,
                                                                                                         s.PackageInfo.ProjectUrl,
                                                                                                         s.PackageInfo.Summary,
                                                                                                         s.PackageInfo.Description,
                                                                                                         s.PackageInfo.LicenseUrl)));
            foreach (ReferencedPackageWithContext r in result)
            {
                await Assert.That(r.Context).IsEqualTo(project);
                await Assert.That(r.PackageInfo.LicenseMetadata!.Type).IsEqualTo(licenseType);
            }
        }

        [Test]
        public async Task GetPackageInfo_Should_PreferLocalPackageCacheOverRepositories()
        {
            CustomPackageInformation[] searchedPackagesAsPackageInformation = _fixture.CreateMany<CustomPackageInformation>(20).ToArray();

            IEnumerable<PackageIdentity> searchedPackages = searchedPackagesAsPackageInformation.Select(info =>
            {
                var identity = new PackageIdentity(info.Id, info.Version);
                IPackageMetadata mockedInfo = CreatePackageMetadata(info, LicenseType.Expression);
                _globalPackagesFolderUtility.GetPackage(identity).Returns(mockedInfo);

                return identity;
            });

            (string project, ReferencedPackageWithContext[] result) = await PerformSearch(searchedPackages);
            await CheckResult(result, project, searchedPackagesAsPackageInformation, LicenseType.Expression);

            foreach (ISourceRepository repo in _repositories)
            {
                await repo.Received(0).GetPackageMetadataResourceAsync(default);
            }
        }

        private static void SetupPackagesForRepositories(IEnumerable<CustomPackageInformation> packages, IEnumerable<IPackageMetadataResource> packageMetadataResources)
        {
            foreach (CustomPackageInformation package in packages)
            {
                IPackageMetadataResource metadataReturningProperInformation = packageMetadataResources.Shuffle(6435).First();
                IPackageMetadata resultingInfo = CreatePackageMetadata(package, LicenseType.Expression);

                metadataReturningProperInformation.TryGetMetadataAsync(new PackageIdentity(package.Id, package.Version), Arg.Any<CancellationToken>()).
                    Returns(_ => Task.FromResult<IPackageMetadata?>(resultingInfo));
            }
        }

        [Test]
        public async Task GetPackageInfo_Should_IterateThroughRepositoriesToGetAdditionalInformation()
        {
            IEnumerable<ISourceRepository> shuffledRepositories = _repositories!.Shuffle(14563);
            IGrouping<int, (int Index, ISourceRepository Repo)>[] splitRepositories = shuffledRepositories.Select((repo, index) => (Index: index, Repo: repo))
                .GroupBy(e => e.Index % 2)
                .ToArray();

            ISourceRepository[] sourceRepositoriesWithPackageMetadataResource = splitRepositories[0].Select(e => e.Repo).ToArray();
            ISourceRepository[] sourceRepositoriesWithFailingPackageMetadataResource =
                splitRepositories[1].Select(e => e.Repo).ToArray();
            IPackageMetadataResource[] packageMetadataResources = sourceRepositoriesWithPackageMetadataResource.Select(r =>
                {
                    IPackageMetadataResource metadataResource = Substitute.For<IPackageMetadataResource>();
                    r.GetPackageMetadataResourceAsync(default).Returns(_ => Task.FromResult<IPackageMetadataResource?>(metadataResource));
                    return metadataResource;
                })
                .ToArray();
            foreach (ISourceRepository? repo in sourceRepositoriesWithFailingPackageMetadataResource)
            {
                repo.When(m => m.GetPackageMetadataResourceAsync(default)).Do(_ => throw new Exception());
            }

            CustomPackageInformation[] searchedPackagesAsPackageInformation = _fixture.CreateMany<CustomPackageInformation>(20).ToArray();

            SetupPackagesForRepositories(searchedPackagesAsPackageInformation, packageMetadataResources);

            IEnumerable<PackageIdentity> searchedPackages = searchedPackagesAsPackageInformation.Select(i => new PackageIdentity(i.Id, i.Version));

            (string project, ReferencedPackageWithContext[] result) = await PerformSearch(searchedPackages);
            await CheckResult(result, project, searchedPackagesAsPackageInformation, LicenseType.Expression);
        }

        [Test]
        public async Task GetPackageInfo_Should_ReturnDummyPackageMetadataForPackagesNotFound()
        {
            CustomPackageInformation[] searchedPackagesAsPackageInformation = _fixture.CreateMany<CustomPackageInformation>().ToArray();
            PackageIdentity[] searchedPackages = searchedPackagesAsPackageInformation.Select(p => new PackageIdentity(p.Id, p.Version)).ToArray();

            (string project, ReferencedPackageWithContext[] results) = await PerformSearch(searchedPackages);

            await Assert.That(results.Length).IsEqualTo(searchedPackages.Length);
            for (int i = 0; i < results.Length; i++)
            {
                PackageIdentity expectation = searchedPackages[i];
                ReferencedPackageWithContext result = results[i];
                await Assert.That(result.Context).IsEqualTo(project);
                await Assert.That(result.PackageInfo.Identity.Id).IsEqualTo(expectation.Id);
                await Assert.That(result.PackageInfo.Identity.Version).IsEqualTo(expectation.Version);
                await Assert.That(result.PackageInfo.LicenseMetadata).IsNull();
                await Assert.That(result.PackageInfo.LicenseUrl).IsNull();
                await Assert.That(result.PackageInfo.Summary).IsNull();
                await Assert.That(result.PackageInfo.Title).IsNull();
            }
        }
    }
}
