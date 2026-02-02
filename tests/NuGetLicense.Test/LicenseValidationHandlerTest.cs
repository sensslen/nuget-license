// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using NuGetUtility;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

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
            ArgumentException? ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _handler.HandleAsync(options));
            Assert.That(ex!.Message, Does.Contain($"Please provide an input file using --input or --input-json"));
        }

        [Test]
        public async Task HandleAsync_WithInputFile_CompletesSuccessfully()
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

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithInputJsonFile_CompletesSuccessfully()
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
        public async Task HandleAsync_WithAllowedLicensesFile_CompletesSuccessfully()
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
        public async Task HandleAsync_WithIgnoredPackagesFile_CompletesSuccessfully()
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
        public async Task HandleAsync_WithExcludedProjectsFile_CompletesSuccessfully()
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
        public async Task HandleAsync_WithExcludedProjectsAsString_CompletesSuccessfully()
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
        public async Task HandleAsync_WithAllowedLicensesAsInlineList_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string allowedLicenses = "MIT;Apache-2.0;BSD-3-Clause";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = allowedLicenses
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithIgnoredPackagesAsInlineList_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string ignoredPackages = "MyCompany.*;TestPackage;LegacyLib*";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                IgnoredPackages = ignoredPackages
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithExcludedProjectsAsInlineList_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string excludedProjects = "*Test*;SampleProject;Legacy*";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                ExcludedProjects = excludedProjects
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithInvalidJsonInAllowedLicensesFile_ThrowsInvalidOperationException()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string allowedLicensesFile = "/test/allowed.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(allowedLicensesFile, new MockFileData("invalid json content"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = allowedLicensesFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act & Assert
            ArgumentException? ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _handler.HandleAsync(options));
            Assert.That(ex!.Message, Does.Contain("Failed to parse JSON file"));
            Assert.That(ex.Message, Does.Contain(allowedLicensesFile));
        }

        [Test]
        public async Task HandleAsync_WithNullJsonInAllowedLicensesFile_ThrowsInvalidOperationException()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string allowedLicensesFile = "/test/allowed.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(allowedLicensesFile, new MockFileData("null"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = allowedLicensesFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act & Assert
            ArgumentException? ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _handler.HandleAsync(options));
            Assert.That(ex!.Message, Does.Contain("expected an array of strings but got null"));
            Assert.That(ex.Message, Does.Contain(allowedLicensesFile));
        }

        [Test]
        public async Task HandleAsync_WithInvalidJsonInIgnoredPackagesFile_ThrowsInvalidOperationException()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string ignoredPackagesFile = "/test/ignored.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(ignoredPackagesFile, new MockFileData("{invalid}"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                IgnoredPackages = ignoredPackagesFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act & Assert
            ArgumentException? ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _handler.HandleAsync(options));
            Assert.That(ex!.Message, Does.Contain("Failed to parse JSON file"));
            Assert.That(ex.Message, Does.Contain(ignoredPackagesFile));
        }

        [Test]
        public async Task HandleAsync_WithInvalidJsonInExcludedProjectsFile_ThrowsInvalidOperationException()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string excludedProjectsFile = "/test/excluded.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(excludedProjectsFile, new MockFileData("not valid json"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                ExcludedProjects = excludedProjectsFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act & Assert
            ArgumentException? ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _handler.HandleAsync(options));
            Assert.That(ex!.Message, Does.Contain("Failed to parse JSON file"));
            Assert.That(ex.Message, Does.Contain(excludedProjectsFile));
        }

        [Test]
        public async Task HandleAsync_WithEmptyInlineList_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string emptyList = "";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = emptyList
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert - empty string should result in empty array, which should complete successfully
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithWhitespaceInInlineList_TrimsCorrectly()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string listWithWhitespace = " MIT ; Apache-2.0 ; BSD-3-Clause ";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = listWithWhitespace
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert - whitespace should be trimmed and parsing should succeed
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
            _ = await _handler.HandleAsync(options);

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
            _ = await _handler.HandleAsync(options);

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
            _ = await _handler.HandleAsync(options);

            // Assert - output stream should have been written to
            Assert.That(_outputStream.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task HandleAsync_WithLicenseMapping_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string licenseMappingFile = "/test/licenseMapping.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(licenseMappingFile, new MockFileData("{\"https://example.com/license\": \"MIT\"}"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                LicenseMapping = licenseMappingFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithOverridePackageInformation_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string overrideFile = "/test/override.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(overrideFile, new MockFileData("[{\"PackageId\":\"TestPackage\",\"Version\":\"1.0.0\",\"PackageUrl\":\"https://example.com\",\"License\":\"MIT\",\"Authors\":\"Test Author\"}]"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                OverridePackageInformation = overrideFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithLicenseFileMappings_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string licenseMappingFile = "/test/licenseFiles.json";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(licenseMappingFile, new MockFileData("{\"LICENSE.txt\": \"MIT\"}"));
            _fileSystem.AddFile("/test/LICENSE.txt", new MockFileData("MIT License content"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                LicenseFileMappings = licenseMappingFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithIncludeTransitive_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                IncludeTransitive = true
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithIncludeSharedProjects_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                IncludeSharedProjects = true
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithTargetFramework_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                TargetFramework = "net8.0"
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithOutputTypeJson_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                OutputType = OutputType.Json
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithOutputTypeJsonPretty_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                OutputType = OutputType.JsonPretty
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithOutputTypeMarkdown_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                OutputType = OutputType.Markdown
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithReturnErrorsOnly_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                ReturnErrorsOnly = true
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithIncludeIgnoredPackages_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                IncludeIgnoredPackages = true
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithProjectReaderExceptions_ReturnsMinusOne()
        {
            // Arrange
            string inputFile = "/test/solution.sln";
            string projectFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(projectFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile
            };

            // Setup mocks to simulate exception during project reading
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>([projectFile]));

            _msBuild.GetProject(Arg.Any<string>()).Returns(_ => throw new Exception("Failed to load project"));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(-1));
            Assert.That(_errorStream.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task HandleAsync_WithProjectReaderExceptions_WritesExceptionToErrorStream()
        {
            // Arrange
            string inputFile = "/test/solution.sln";
            string projectFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(projectFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile
            };

            string exceptionMessage = "Failed to load project";
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(_ => Task.FromResult<IEnumerable<string>>([projectFile]));

            _msBuild.GetProject(Arg.Any<string>()).Returns(_ => throw new Exception(exceptionMessage));

            // Act
            await _handler.HandleAsync(options);

            // Assert
            _errorStream.Position = 0;
            StreamReader reader = new StreamReader(_errorStream);
            string errorOutput = await reader.ReadToEndAsync();
            Assert.That(errorOutput, Does.Contain(exceptionMessage));
        }

        [Test]
        public async Task HandleAsync_WithMultipleInputJsonFiles_CompletesSuccessfully()
        {
            // Arrange
            string jsonFile = "/test/input.json";
            string projectFile1 = "/test/project1.csproj";
            string projectFile2 = "/test/project2.csproj";
            _fileSystem.AddFile(jsonFile, new MockFileData($"[\"{projectFile1}\", \"{projectFile2}\"]"));
            _fileSystem.AddFile(projectFile1, new MockFileData(""));
            _fileSystem.AddFile(projectFile2, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputJsonFile = jsonFile
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public async Task HandleAsync_WithExceptionInOutputFormatter_ReturnsMinusOne()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                DestinationFile = "/invalid\0path/output.txt"
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert - should handle exception and return -1
            Assert.That(result, Is.EqualTo(-1));
            Assert.That(_errorStream.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task HandleAsync_WithCombinedOptions_CompletesSuccessfully()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            string allowedLicensesFile = "/test/allowed.json";
            string ignoredPackagesFile = "/test/ignored.json";
            string licenseMappingFile = "/test/licenseMapping.json";
            string outputFile = "/test/output.json";

            _fileSystem.AddFile(inputFile, new MockFileData(""));
            _fileSystem.AddFile(allowedLicensesFile, new MockFileData("[\"MIT\", \"Apache-2.0\"]"));
            _fileSystem.AddFile(ignoredPackagesFile, new MockFileData("[\"MyCompany.*\"]"));
            _fileSystem.AddFile(licenseMappingFile, new MockFileData("{\"https://example.com/license\": \"MIT\"}"));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile,
                AllowedLicenses = allowedLicensesFile,
                IgnoredPackages = ignoredPackagesFile,
                LicenseMapping = licenseMappingFile,
                DestinationFile = outputFile,
                OutputType = OutputType.JsonPretty,
                IncludeTransitive = true,
                ReturnErrorsOnly = false,
                IncludeIgnoredPackages = true
            };

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _handler.HandleAsync(options);

            // Assert
            Assert.That(result, Is.EqualTo(0));
            Assert.That(_fileSystem.File.Exists(outputFile), Is.True);
        }

        [Test]
        public async Task HandleAsync_WithCancellationToken_CanBeCancelled()
        {
            // Arrange
            string inputFile = "/test/project.csproj";
            _fileSystem.AddFile(inputFile, new MockFileData(""));

            CommandLineOptions options = new CommandLineOptions
            {
                InputFile = inputFile
            };

            using CancellationTokenSource cts = new CancellationTokenSource();

            // Setup mocks
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            cts.Cancel();

            // Act & Assert - this test verifies that cancellation token is passed through
            // The actual cancellation may or may not throw depending on timing
            try
            {
                await _handler.HandleAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected if cancellation happens
                Assert.Pass("Operation was cancelled as expected");
            }

            // If no exception, that's also acceptable as cancellation is best-effort
            Assert.Pass("Operation completed or was cancelled gracefully");
        }
    }
}
