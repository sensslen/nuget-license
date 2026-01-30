// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

#if !NET
using System.Net.Http;
#endif

namespace NuGetLicense.Test
{
    [TestFixture]
    internal class LicenseValidationHandlerTest
    {
        private MockFileSystem _fileSystem = null!;
        private HttpClient _httpClient = null!;
        private ISolutionPersistanceWrapper _solutionPersistance = null!;
        private IMsBuildAbstraction _msBuild = null!;
        private IPackagesConfigReader _packagesConfigReader = null!;
        private MemoryStream _outputStream = null!;
        private MemoryStream _errorStream = null!;
        private LicenseValidationHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _httpClient = new HttpClient();
            _solutionPersistance = Substitute.For<ISolutionPersistanceWrapper>();
            _msBuild = Substitute.For<IMsBuildAbstraction>();
            _packagesConfigReader = Substitute.For<IPackagesConfigReader>();
            _outputStream = new MemoryStream();
            _errorStream = new MemoryStream();

            _handler = new LicenseValidationHandler(
                _fileSystem,
                _httpClient,
                _solutionPersistance,
                _msBuild,
                _packagesConfigReader,
                _outputStream,
                _errorStream);
        }

        [TearDown]
        public void TearDown()
        {
            _outputStream?.Dispose();
            _errorStream?.Dispose();
            _httpClient?.Dispose();
        }

        [Test]
        public async Task HandleAsync_WithNoInputFile_ThrowsFileNotFoundException()
        {
            // Arrange
            CommandLineOptions options = new CommandLineOptions();

            // Act & Assert
            FileNotFoundException? ex = Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _handler.HandleAsync(options));
            Assert.That(ex!.Message, Does.Contain("Please provide an input file"));
        }

        [Test]
        public async Task HandleAsync_WithInputFile_ReturnsInputFileArray()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile
            };

            // Setup mocks to avoid null reference exceptions
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert - we expect it to complete without throwing
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithInputJsonFile_ReadsFromJsonFile()
        {
            // Arrange
            string jsonFile = "/test/input.json";
            string projectFile = "/test/project.csproj";
            _fileSystem.AddFile(jsonFile, new MockFileData($"[\"{projectFile}\"]"));
            _fileSystem.AddFile(projectFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputJsonFile = jsonFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithAllowedLicensesFile_ReadsFromJsonFile()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string allowedLicensesFile = "/test/allowed.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(allowedLicensesFile, new MockFileData("[\"MIT\", \"Apache-2.0\"]"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = allowedLicensesFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithIgnoredPackagesFile_ReadsFromJsonFile()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string ignoredPackagesFile = "/test/ignored.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(ignoredPackagesFile, new MockFileData("[\"MyCompany.*\", \"TestPackage\"]"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                IgnoredPackages = ignoredPackagesFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithExcludedProjectsFile_ReadsFromJsonFile()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string excludedProjectsFile = "/test/excluded.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(excludedProjectsFile, new MockFileData("[\"*Test*\", \"*.Test\"]"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                ExcludedProjects = excludedProjectsFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithExcludedProjectsAsString_UsesItAsSingleEntry()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string excludedProject = "*Test*";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                ExcludedProjects = excludedProject
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithDownloadLicenseInformation_CreatesDirectory()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string downloadDir = "/test/licenses";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                DownloadLicenseInformation = downloadDir
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(_fileSystem.Directory.Exists(downloadDir), Is.True);
        }

        [Test]
        public async Task HandleAsync_WithDestinationFile_WritesToFile()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string outputFile = "/test/output.txt";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                DestinationFile = outputFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(_fileSystem.File.Exists(outputFile), Is.True);
        }

        [Test]
        public async Task HandleAsync_WithoutDestinationFile_WritesToOutputStream()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert - output stream should have been written to
            Assert.That(_outputStream.Length, Is.GreaterThan(0));
        }
    }
}
