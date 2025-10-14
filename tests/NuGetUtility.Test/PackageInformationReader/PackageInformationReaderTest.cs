// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension;
using NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;
using IPackageMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.IPackageMetadata;
using LicenseMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.LicenseMetadata;
using LicenseType = NuGetUtility.Wrapper.NuGetWrapper.Packaging.LicenseType;
using PackageIdentity = NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core.PackageIdentity;

namespace NuGetUtility.Test.PackageInformationReader
{
    [TestFixture]
    public class PackageInformationReaderTest
    {
        [SetUp]
        public void SetUp()
        {
            _sourceRepositoryProvider = Substitute.For<IWrappedSourceRepositoryProvider>();
            _customPackageInformation = Enumerable.Empty<CustomPackageInformation>().ToList();
            _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
            _fixture.Customizations.Add(new NuGetVersionBuilder());
            _fixture.Customizations.Add(new CustomPackageInformationBuilderWithOptionalFileds());
            _repositories = Array.Empty<ISourceRepository>();
            _globalPackagesFolderUtility = Substitute.For<IGlobalPackagesFolderUtility>();

            _globalPackagesFolderUtility.GetPackage(Arg.Any<PackageIdentity>()).Returns(default(IPackageMetadata?));

            _sourceRepositoryProvider.GetRepositories()
                .Returns(_ =>
                {
                    Assert.That(_repositories, Is.Empty);
                    _repositories = _fixture.CreateMany<ISourceRepository>().ToArray();
                    foreach (ISourceRepository repo in _repositories)
                    {
                        repo.GetPackageMetadataResourceAsync(default).Returns(_ => Task.FromResult(default(IPackageMetadataResource?)));
                    }
                    return _repositories;
                });

            SetupUut();
        }

        [TearDown]
        public void TearDown()
        {
            _repositories = Array.Empty<ISourceRepository>();
            _uut = null!;
        }

        private void SetupUut()
        {
            TearDown();
            _uut = new NuGetUtility.PackageInformationReader.PackageInformationReader(_sourceRepositoryProvider, _globalPackagesFolderUtility, _customPackageInformation);
        }

        private NuGetUtility.PackageInformationReader.PackageInformationReader _uut = null!;
        private IWrappedSourceRepositoryProvider _sourceRepositoryProvider = null!;
        private List<CustomPackageInformation> _customPackageInformation = null!;
        private IFixture _fixture = null!;
        private ISourceRepository[] _repositories = null!;
        private IGlobalPackagesFolderUtility _globalPackagesFolderUtility = null!;

        [Test]
        public async Task GetPackageInfo_Should_PreferProvidedCustomInformation()
        {
            _customPackageInformation = _fixture.CreateMany<CustomPackageInformation>().ToList();
            SetupUut();

            IEnumerable<PackageIdentity> searchedPackages = _customPackageInformation.Select(p => new PackageIdentity(p.Id, p.Version));

            (string project, ReferencedPackageWithContext[] result) = await PerformSearch(searchedPackages);
            CheckResult(result, project, _customPackageInformation, LicenseType.Overwrite);
        }

        private async Task<(string Project, ReferencedPackageWithContext[] Result)> PerformSearch(
            IEnumerable<PackageIdentity> searchedPackages)
        {
            string project = _fixture.Create<string>();
            var packageSearchRequest = new ProjectWithReferencedPackages(project, searchedPackages);
            ReferencedPackageWithContext[] result = (await _uut!.GetPackageInfo(packageSearchRequest, CancellationToken.None).Synchronize())
                .ToArray();
            return (project, result);
        }

        private static void CheckResult(ReferencedPackageWithContext[] result,
            string project,
            IEnumerable<CustomPackageInformation> packages,
            LicenseType licenseType)
        {
            Assert.That(packages, Is.EquivalentTo(result.Select(s => new CustomPackageInformation(s.PackageInfo.Identity.Id,
                                                                                                  s.PackageInfo.Identity.Version,
                                                                                                  s.PackageInfo.LicenseMetadata!.License,
                                                                                                  s.PackageInfo.Copyright,
                                                                                                  s.PackageInfo.Authors,
                                                                                                  s.PackageInfo.Title,
                                                                                                  s.PackageInfo.ProjectUrl,
                                                                                                  s.PackageInfo.Summary,
                                                                                                  s.PackageInfo.Description,
                                                                                                  s.PackageInfo.LicenseUrl))));
            foreach (ReferencedPackageWithContext r in result)
            {
                Assert.That(r.Context, Is.EqualTo(project));
                Assert.That(r.PackageInfo.LicenseMetadata!.Type, Is.EqualTo(licenseType));
            }
        }

        [Test]
        public async Task GetPackageInfo_Should_PreferLocalPackageCacheOverRepositories()
        {
            CustomPackageInformation[] searchedPackagesAsPackageInformation = _fixture.CreateMany<CustomPackageInformation>(20).ToArray();

            IEnumerable<PackageIdentity> searchedPackages = searchedPackagesAsPackageInformation.Select(info =>
            {
                var identity = new PackageIdentity(info.Id, info.Version);
                IPackageMetadata mockedInfo = Substitute.For<IPackageMetadata>();
                mockedInfo.Identity.Returns(identity);
                mockedInfo.Copyright.Returns(info.Copyright);
                mockedInfo.Authors.Returns(info.Authors);
                mockedInfo.Title.Returns(info.Title);
                mockedInfo.ProjectUrl.Returns(info.ProjectUrl);
                mockedInfo.Summary.Returns(info.Summary);
                mockedInfo.Description.Returns(info.Description);
                mockedInfo.LicenseUrl.Returns(info.LicenseUrl);
                mockedInfo.LicenseMetadata.Returns(new LicenseMetadata(LicenseType.Expression, info.License));
                mockedInfo.LicenseUrl.Returns(info.LicenseUrl);
                _globalPackagesFolderUtility.GetPackage(identity).Returns(mockedInfo);

                return identity;
            });

            (string project, ReferencedPackageWithContext[] result) = await PerformSearch(searchedPackages);
            CheckResult(result, project, searchedPackagesAsPackageInformation, LicenseType.Expression);

            foreach (ISourceRepository repo in _repositories)
            {
                await repo.Received(0).GetPackageMetadataResourceAsync(default);
            }
        }

        [Test]
        [TestCase("license/LICENSE.txt", "license\\LICENSE.txt")]
        [TestCase("license/LICENSE.txt", "license/LICENSE.txt")]
        [TestCase("LICENSE.txt", "LICENSE.txt")]
        public void GetPackageInfo_Should_ReadLicenseFromNormalizedPath(string licenseSystemPath, string licenseManifestPath)
        {
            string licenseText = _fixture.Create<string>();
            var manifestMetadata = new ManifestMetadata
            {
                Id = _fixture.Create<string>(),
                Version = new NuGetVersion(_fixture.Create<int>(),_fixture.Create<int>(),_fixture.Create<int>()),
                Authors = _fixture.Create<string[]>(),
                Description = _fixture.Create<string>(),
                LicenseMetadata = new NuGet.Packaging.LicenseMetadata(
                    NuGet.Packaging.LicenseType.File,
                    licenseManifestPath,
                    null,
                    new List<string>(),
                    _fixture.Create<Version>())
            };
            var packageReader = new TestPackageReader(licenseText, manifestMetadata);
            packageReader.SetStreamPath(licenseSystemPath);

            var utility = new TestGlobalPackagesFolderUtility(packageReader);
            var result = utility.GetPackage(_fixture.Create<PackageIdentity>());

            //Then
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.LicenseMetadata!.Type, Is.EqualTo(LicenseType.File));
            Assert.That(result.LicenseMetadata.License, Is.EqualTo(licenseText));
        }

        private static void SetupPackagesForRepositories(IEnumerable<CustomPackageInformation> packages, IEnumerable<IPackageMetadataResource> packageMetadataResources)
        {
            foreach (CustomPackageInformation package in packages)
            {
                IPackageMetadataResource metadataReturningProperInformation = packageMetadataResources.Shuffle(6435).First();
                IPackageMetadata resultingInfo = Substitute.For<IPackageMetadata>();
                resultingInfo.Identity.Returns(new PackageIdentity(package.Id, package.Version));
                resultingInfo.LicenseMetadata.Returns(new LicenseMetadata(LicenseType.Expression, package.License));
                resultingInfo.LicenseUrl.Returns(package.LicenseUrl);
                resultingInfo.Copyright.Returns(package.Copyright);
                resultingInfo.Authors.Returns(package.Authors);
                resultingInfo.Title.Returns(package.Title);
                resultingInfo.Summary.Returns(package.Summary);
                resultingInfo.Description.Returns(package.Description);
                resultingInfo.ProjectUrl.Returns(package.ProjectUrl);
                resultingInfo.LicenseUrl.Returns(package.LicenseUrl);

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
            CheckResult(result, project, searchedPackagesAsPackageInformation, LicenseType.Expression);
        }

        [Test]
        public async Task GetPackageInfo_Should_ReturnDummyPackageMetadataForPackagesNotFound()
        {
            CustomPackageInformation[] searchedPackagesAsPackageInformation = _fixture.CreateMany<CustomPackageInformation>().ToArray();
            PackageIdentity[] searchedPackages = searchedPackagesAsPackageInformation.Select(p => new PackageIdentity(p.Id, p.Version)).ToArray();

            (string project, ReferencedPackageWithContext[] results) = await PerformSearch(searchedPackages);

            Assert.That(results, Has.Length.EqualTo(searchedPackages.Length));
            for (int i = 0; i < results.Length; i++)
            {
                PackageIdentity expectation = searchedPackages[i];
                ReferencedPackageWithContext result = results[i];
                Assert.That(result.Context, Is.EqualTo(project));
                Assert.That(result.PackageInfo.Identity.Id, Is.EqualTo(expectation.Id));
                Assert.That(result.PackageInfo.Identity.Version, Is.EqualTo(expectation.Version));
                Assert.That(result.PackageInfo.LicenseMetadata, Is.Null);
                Assert.That(result.PackageInfo.LicenseUrl, Is.Null);
                Assert.That(result.PackageInfo.Summary, Is.Null);
                Assert.That(result.PackageInfo.Title, Is.Null);
            }
        }

        private class TestGlobalPackagesFolderUtility(PackageReaderBase packageReader)
            : GlobalPackagesFolderUtility(new NullSettings())
        {
            protected override DownloadResourceResult GetPackageFromOriginalUtility(PackageIdentity identity)
            {
                return new DownloadResourceResult(packageReader, "test repo");
            }
        }

        private class TestPackageReader(string licenseText, ManifestMetadata metadata)
            : PackageReaderBase(new FrameworkNameProvider(null, null))
        {
            private string? _path;

            public void SetStreamPath(string path)
            {
                _path = path;
            }
            public override Stream GetNuspec()
            {
                var manifest = new Manifest(metadata);
                var memoryStream = new MemoryStream();
                manifest.Save(memoryStream);
                memoryStream.Position = 0;
                return memoryStream;
            }

            public override Stream? GetStream(string path)
            {
                if (_path != null && _path != path)
                {
                    return null;
                }
                var memoryStream = new MemoryStream();
                memoryStream.Write(Encoding.UTF8.GetBytes(licenseText), 0, licenseText.Length);
                memoryStream.Position = 0;
                return memoryStream;
            }

            public override IEnumerable<string> GetFiles() => [];

            public override IEnumerable<string> GetFiles(string folder) => [];

            public override IEnumerable<string> CopyFiles(string destination, IEnumerable<string> packageFiles, ExtractPackageFileDelegate extractFile,
                ILogger logger, CancellationToken token) => [];

            protected override void Dispose(bool disposing){}

            public override Task<PrimarySignature> GetPrimarySignatureAsync(CancellationToken token) => throw new NotImplementedException();

            public override Task<bool> IsSignedAsync(CancellationToken token) => Task.FromResult(false);

            public override Task<byte[]> GetArchiveHashAsync(HashAlgorithmName hashAlgorithm, CancellationToken token) => Task.FromResult<byte[]>([]);

            public override bool CanVerifySignedPackages(SignedPackageVerifierSettings verifierSettings) => false;

            public override string
                GetContentHash(CancellationToken token, Func<string>? GetUnsignedPackageHash = null) => "";

            public override Task ValidateIntegrityAsync(SignatureContent signatureContent, CancellationToken token) => Task.CompletedTask;
        }
    }
}
