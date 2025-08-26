// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Text;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.ZipArchiveWrapper;

namespace NuGetUtility.Test.PackageInformationReader
{
    [TestFixture]
    internal class PackageLicenseFileReaderTests
    {
        private IFixture _fixture = null!;
        private IFileSystem _fileSystem = null!;
        private IZipArchiveWrapper _zipArchiveWrapper = null!;
        private string _profilePath = null!;
        private PackageLicenseFileReader _uut = null!;
        private IPackageMetadata _packageMetadata = null!;
        private PackageIdentity _packageIdentity = null!;

        [SetUp]
        public void SetUp()
        {
            _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
            _fixture.Customizations.Add(new NuGetVersionBuilder());

            _zipArchiveWrapper = Substitute.For<IZipArchiveWrapper>();
            _profilePath = "/test/profile";

            // Create a proper INuGetVersion using AutoFixture
            _packageIdentity = _fixture.Create<PackageIdentity>();

            _packageMetadata = Substitute.For<IPackageMetadata>();
            _packageMetadata.Identity.Returns(_packageIdentity);

            // Set up MockFileSystem with the expected package file path
            string expectedPackagePath = $"/test/profile/.nuget/packages/{_packageIdentity.Id.ToLowerInvariant()}/{_packageIdentity.Version.ToString()!.ToLowerInvariant()}/{_packageIdentity.Id.ToLowerInvariant()}.{_packageIdentity.Version.ToString()!.ToLowerInvariant()}.nupkg";

            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { expectedPackagePath, new MockFileData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }) } // ZIP file signature
            });

            _uut = new PackageLicenseFileReader(_fileSystem, _zipArchiveWrapper, _profilePath);
        }

        [Test]
        public void Constructor_WhenCalled_SetsDependencies()
        {
            // Act
            _uut = new PackageLicenseFileReader(_fileSystem, _zipArchiveWrapper, _profilePath);

            // Assert
            Assert.That(_uut, Is.Not.Null);
        }


        [Test]
        public async Task ReadLicenseFromFileAsync_WhenPackageFileDoesNotExist_DoesNotReadZip()
        {
            // Arrange
            var licenseMetadata = new LicenseMetadata(LicenseType.File, "LICENSE.txt");
            _packageMetadata.LicenseMetadata.Returns(licenseMetadata);

            // Create a package identity that won't match any file in the mock file system
            PackageIdentity nonExistentPackage = _fixture.Create<PackageIdentity>();
            _packageMetadata.Identity.Returns(nonExistentPackage);

            // Act
            await _uut.ReadLicenseFromFileAsync(_packageMetadata);

            // Assert
            _zipArchiveWrapper.DidNotReceive().Open(Arg.Any<Stream>());
        }

        [Test]
        public async Task ReadLicenseFromFileAsync_WhenLicenseFileExists_ReadsLicenseContent()
        {
            // Arrange
            const string expectedLicenseContent = "MIT License\n\nCopyright (c) 2023 Test";
            var licenseMetadata = new LicenseMetadata(LicenseType.File, "LICENSE.txt");
            _packageMetadata.LicenseMetadata.Returns(licenseMetadata);

            // Create mock ZIP archive and entry
            IZipArchive mockZipArchive = Substitute.For<IZipArchive>();
            IZipArchiveEntry mockZipEntry = Substitute.For<IZipArchiveEntry>();
            var licenseStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedLicenseContent));

            // Set up the ZIP archive to return the license entry
            mockZipArchive.GetEntry("LICENSE.txt").Returns(mockZipEntry);
            mockZipEntry.Open().Returns(licenseStream);

            _zipArchiveWrapper.Open(Arg.Any<Stream>()).Returns(mockZipArchive);

            // Act
            await _uut.ReadLicenseFromFileAsync(_packageMetadata);

            // Assert
            Assert.That(_packageMetadata.LicenseFileContent, Is.EqualTo(expectedLicenseContent));
            _zipArchiveWrapper.Received(1).Open(Arg.Any<Stream>());
            mockZipArchive.Received(1).GetEntry("LICENSE.txt");
        }

        [Test]
        public async Task ReadLicenseFromFileAsync_WhenLicenseFileNotInZip_DoesNotSetContent()
        {
            // Arrange
            var licenseMetadata = new LicenseMetadata(LicenseType.File, "LICENSE.txt");
            _packageMetadata.LicenseMetadata.Returns(licenseMetadata);

            IZipArchive mockZipArchive = Substitute.For<IZipArchive>();
            mockZipArchive.GetEntry("LICENSE.txt").Returns((IZipArchiveEntry?)null);

            _zipArchiveWrapper.Open(Arg.Any<Stream>()).Returns(mockZipArchive);

            // Act
            await _uut.ReadLicenseFromFileAsync(_packageMetadata);

            // Assert
            Assert.That(_packageMetadata.LicenseFileContent, Is.Empty);
        }

        [Test]
        public void ReadLicenseFromFileAsync_WhenLicenseMetadataIsNull_DoesNotThrow()
        {
            // Arrange
            _packageMetadata.LicenseMetadata.Returns((LicenseMetadata?)null);

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _uut.ReadLicenseFromFileAsync(_packageMetadata));
        }
    }
}
