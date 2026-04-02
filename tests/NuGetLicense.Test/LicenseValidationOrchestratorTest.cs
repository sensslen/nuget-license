// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using NuGetLicense.LicenseValidator;
using NuGetUtility;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;
using LicenseOutput = NuGetLicense.Output;

namespace NuGetLicense.Test
{
    internal class LicenseValidationOrchestratorTest
    {
        private MockFileSystem _fileSystem = null!;
        private ISolutionPersistanceWrapper _solutionPersistance = null!;
        private IMsBuildAbstraction _msBuild = null!;
        private IPackagesConfigReader _packagesConfigReader = null!;
        private ICommandLineOptionsParser _optionsParser = null!;
        private MemoryStream _outputStream = null!;
        private MemoryStream _errorStream = null!;
        private ICommandLineOptions _options = null!;
        private LicenseValidationOrchestrator _orchestrator = null!;

        [Before(Test)]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _solutionPersistance = Substitute.For<ISolutionPersistanceWrapper>();
            _msBuild = Substitute.For<IMsBuildAbstraction>();
            _packagesConfigReader = Substitute.For<IPackagesConfigReader>();
            _optionsParser = Substitute.For<ICommandLineOptionsParser>();
            _outputStream = new MemoryStream();
            _errorStream = new MemoryStream();
            _options = Substitute.For<ICommandLineOptions>();

            _options.DestinationFile.Returns(default(string?));

            _orchestrator = new LicenseValidationOrchestrator(
                _fileSystem,
                _solutionPersistance,
                _msBuild,
                _packagesConfigReader,
                _optionsParser,
                _outputStream,
                _errorStream);
        }

        [After(Test)]
        public void TearDown()
        {
            _outputStream?.Dispose();
            _errorStream?.Dispose();
        }

        [Test]
        public async Task ValidateAsync_CallsOptionsParserWithCorrectArguments()
        {
            // Arrange
            _options.InputFile.Returns("/test/project.csproj");
            _options.AllowedLicenses.Returns("MIT");
            _options.IgnoredPackages.Returns("TestPkg");
            _options.ExcludedProjects.Returns("*Test*");

            _optionsParser.GetInputFiles(_options.InputFile, _options.InputJsonFile).Returns(["/test/project.csproj"]);
            _optionsParser.GetAllowedLicenses(_options.AllowedLicenses).Returns(["MIT"]);
            _optionsParser.GetIgnoredPackages(_options.IgnoredPackages).Returns(["TestPkg"]);
            _optionsParser.GetExcludedProjects(_options.ExcludedProjects).Returns(["*Test*"]);
            _optionsParser.GetLicenseMappings(null).Returns(ImmutableDictionary<Uri, string>.Empty);
            _optionsParser.GetOverridePackageInformation(null).Returns(Array.Empty<CustomPackageInformation>());
            _optionsParser.GetFileDownloader(null).Returns(new NopFileDownloader());
            _optionsParser.GetOutputFormatter(OutputType.Table, false, false).Returns(new LicenseOutput.Table.TableOutputFormatter(false, false));
            _optionsParser.GetLicenseMatcher(null).Returns(new FileLicenseMatcher.SPDX.FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses));

            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            await _orchestrator.ValidateAsync(_options);

            // Assert
            _optionsParser.Received(1).GetInputFiles(_options.InputFile, _options.InputJsonFile);
            _optionsParser.Received(1).GetAllowedLicenses(_options.AllowedLicenses);
            _optionsParser.Received(1).GetIgnoredPackages(_options.IgnoredPackages);
            _optionsParser.Received(1).GetExcludedProjects(_options.ExcludedProjects);
        }

        [Test]
        public async Task ValidateAsync_WithNoProjects_ReturnsZero()
        {
            // Arrange
            _options.InputFile.Returns("/test/project.csproj");

            SetupDefaultMocks();
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _orchestrator.ValidateAsync(_options);

            // Assert
            await Assert.That(result).IsEqualTo(0);
        }

        [Test]
        public async Task ValidateAsync_WithDestinationFile_WritesToFile()
        {
            // Arrange
            string destinationFile = "/test/output.txt";
            _fileSystem.AddDirectory("/test");
            _options.InputFile.Returns("/test/project.csproj");
            _options.DestinationFile.Returns(destinationFile);

            SetupDefaultMocks();
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _orchestrator.ValidateAsync(_options);

            // Assert
            await Assert.That(result).IsEqualTo(0);
            await Assert.That(_fileSystem.File.Exists(destinationFile)).IsTrue();
        }

        [Test]
        public async Task ValidateAsync_WithoutDestinationFile_WritesToOutputStream()
        {
            // Arrange
            _options.InputFile.Returns("/test/project.csproj");

            SetupDefaultMocks();
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            int result = await _orchestrator.ValidateAsync(_options);

            // Assert
            await Assert.That(result).IsEqualTo(0);
            await Assert.That(_outputStream.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ValidateAsync_WithExceptionInOutputFormatter_ReturnsMinusOne()
        {
            // Arrange
            _options.InputFile.Returns("/test/project.csproj");

            SetupDefaultMocks();
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            LicenseOutput.IOutputFormatter throwingFormatter = Substitute.For<LicenseOutput.IOutputFormatter>();
            throwingFormatter.Write(Arg.Any<Stream>(), Arg.Any<IList<LicenseValidationResult>>())
                .Returns(_ => throw new InvalidOperationException("Test exception"));
            _optionsParser.GetOutputFormatter(OutputType.Table, false, false).Returns(throwingFormatter);

            // Act
            int result = await _orchestrator.ValidateAsync(_options);

            // Assert
            await Assert.That(result).IsEqualTo(-1);
            await Assert.That(_errorStream.Length).IsGreaterThan(0);
        }

        [Test]
        public async Task ValidateAsync_WithCancellationToken_CanBeCancelled()
        {
            // Arrange
            _options.InputFile.Returns("/test/project.csproj");

            SetupDefaultMocks();
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await Assert.That(_orchestrator.ValidateAsync(_options, cancellationTokenSource.Token).Wait(2000)).IsTrue();
        }

        [Test]
        public async Task ValidateAsync_UsesAllConfiguredOptions()
        {
            // Arrange
            _options.InputFile.Returns("/test/project.csproj");
            _options.IncludeTransitive.Returns(true);
            _options.TargetFramework.Returns("net8.0");
            _options.IncludeSharedProjects.Returns(true);
            _options.AllowedLicenses.Returns("MIT");
            _options.IgnoredPackages.Returns("TestPkg");
            _options.ExcludedProjects.Returns("*Test*");
            _options.ReturnErrorsOnly.Returns(true);
            _options.IncludeIgnoredPackages.Returns(true);
            _options.OutputType.Returns(OutputType.Json);
            _options.InputJsonFile.Returns(default(string?));

            SetupDefaultMocks();
            _solutionPersistance.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            // Act
            await _orchestrator.ValidateAsync(_options);

            // Assert
            _optionsParser.Received(1).GetInputFiles(_options.InputFile, _options.InputJsonFile);
            _optionsParser.Received(1).GetAllowedLicenses(_options.AllowedLicenses);
            _optionsParser.Received(1).GetIgnoredPackages(_options.IgnoredPackages);
            _optionsParser.Received(1).GetExcludedProjects(_options.ExcludedProjects);
            _optionsParser.Received(1).GetOutputFormatter(_options.OutputType, _options.ReturnErrorsOnly, _options.IncludeIgnoredPackages);
        }

        private void SetupDefaultMocks()
        {
            _optionsParser.GetInputFiles(Arg.Any<string>(), Arg.Any<string>()).Returns(["/test/project.csproj"]);
            _optionsParser.GetAllowedLicenses(Arg.Any<string>()).Returns(Array.Empty<string>());
            _optionsParser.GetIgnoredPackages(Arg.Any<string>()).Returns(Array.Empty<string>());
            _optionsParser.GetExcludedProjects(Arg.Any<string>()).Returns(Array.Empty<string>());
            _optionsParser.GetLicenseMappings(Arg.Any<string>()).Returns(ImmutableDictionary<Uri, string>.Empty);
            _optionsParser.GetOverridePackageInformation(Arg.Any<string>()).Returns(Array.Empty<CustomPackageInformation>());
            _optionsParser.GetFileDownloader(Arg.Any<string>()).Returns(new NopFileDownloader());
            _optionsParser.GetOutputFormatter(Arg.Any<OutputType>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(new LicenseOutput.Table.TableOutputFormatter(false, false));
            _optionsParser.GetLicenseMatcher(Arg.Any<string>()).Returns(new FileLicenseMatcher.SPDX.FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses));
        }
    }
}
