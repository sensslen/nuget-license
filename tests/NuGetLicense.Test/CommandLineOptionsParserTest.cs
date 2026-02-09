// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.IO.Abstractions.TestingHelpers;
using FileLicenseMatcher;
using NuGetUtility;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.HttpClientWrapper;
using RichardSzalay.MockHttp;
using LicenseOutput = NuGetLicense.Output;

#if !NET
using System.Net.Http;
#endif

namespace NuGetLicense.Test
{
    [TestFixture]
    internal class CommandLineOptionsParserTest
    {
        private MockFileSystem _fileSystem = null!;
        private MockHttpMessageHandler _mockHttp = null!;
        private HttpClient _httpClient = null!;
        private CommandLineOptionsParser _parser = null!;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _mockHttp = new MockHttpMessageHandler();
            _httpClient = _mockHttp.ToHttpClient();
            _parser = new CommandLineOptionsParser(_fileSystem, _httpClient);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient?.Dispose();
            _mockHttp?.Dispose();
        }

        [TestFixture]
        internal class GetInputFilesTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetInputFiles_WithInputFile_ReturnsFileInArray()
            {
                // Arrange
                string inputFile = "/test/project.csproj";

                // Act
                string[] result = _parser.GetInputFiles(inputFile, null);

                // Assert
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0], Is.EqualTo(inputFile));
            }

            [Test]
            public void GetInputFiles_WithInputJsonFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string jsonFile = "/test/input.json";
                string[] expectedFiles = ["/test/project1.csproj", "/test/project2.csproj"];
                _fileSystem.AddFile(jsonFile, new MockFileData($"[\"{expectedFiles[0]}\",\"{expectedFiles[1]}\"]"));

                // Act
                string[] result = _parser.GetInputFiles(null, jsonFile);

                // Assert
                Assert.That(result, Is.EqualTo(expectedFiles));
            }

            [Test]
            public void GetInputFiles_WithNeitherOption_ThrowsArgumentException()
            {
                // Act & Assert
                ArgumentException? ex = Assert.Throws<ArgumentException>(() =>
                    _parser.GetInputFiles(null, null));
                Assert.That(ex!.Message, Does.Contain("Please provide an input file using --input or --json-input"));
            }

            [Test]
            public void GetInputFiles_WithBothOptions_PrefersInputFile()
            {
                // Arrange
                string inputFile = "/test/project.csproj";
                string jsonFile = "/test/input.json";
                _fileSystem.AddFile(jsonFile, new MockFileData("[\"should_not_be_used.csproj\"]"));

                // Act
                string[] result = _parser.GetInputFiles(inputFile, jsonFile);

                // Assert
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0], Is.EqualTo(inputFile));
            }
        }

        [TestFixture]
        internal class GetAllowedLicensesTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetAllowedLicenses_WithNull_ReturnsEmptyArray()
            {
                // Act
                string[] result = _parser.GetAllowedLicenses(null);

                // Assert
                Assert.That(result, Is.Empty);
            }

            [Test]
            public void GetAllowedLicenses_WithInlineList_ReturnsParsedArray()
            {
                // Arrange
                string allowedLicenses = "MIT;Apache-2.0;BSD-3-Clause";

                // Act
                string[] result = _parser.GetAllowedLicenses(allowedLicenses);

                // Assert
                Assert.That(result, Is.EqualTo(["MIT", "Apache-2.0", "BSD-3-Clause"]));
            }

            [Test]
            public void GetAllowedLicenses_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string licenseFile = "/test/allowed.json";
                string[] expectedLicenses = ["MIT", "Apache-2.0"];
                _fileSystem.AddFile(licenseFile, new MockFileData($"[\"{expectedLicenses[0]}\",\"{expectedLicenses[1]}\"]"));

                // Act
                string[] result = _parser.GetAllowedLicenses(licenseFile);

                // Assert
                Assert.That(result, Is.EqualTo(expectedLicenses));
            }

            [Test]
            public void GetAllowedLicenses_WithWhitespace_TrimsValues()
            {
                // Arrange
                string allowedLicenses = " MIT ; Apache-2.0 ; BSD-3-Clause ";

                // Act
                string[] result = _parser.GetAllowedLicenses(allowedLicenses);

                // Assert
                Assert.That(result, Is.EqualTo(["MIT", "Apache-2.0", "BSD-3-Clause"]));
            }

            [Test]
            public void GetAllowedLicenses_WithInvalidJsonFile_ThrowsArgumentException()
            {
                // Arrange
                string licenseFile = "/test/allowed.json";
                _fileSystem.AddFile(licenseFile, new MockFileData("invalid json"));

                // Act & Assert
                ArgumentException? ex = Assert.Throws<ArgumentException>(() =>
                    _parser.GetAllowedLicenses(licenseFile));
                Assert.That(ex!.Message, Does.Contain("Failed to parse JSON file"));
            }
        }

        [TestFixture]
        internal class GetIgnoredPackagesTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetIgnoredPackages_WithNull_ReturnsEmptyArray()
            {
                // Act
                string[] result = _parser.GetIgnoredPackages(null);

                // Assert
                Assert.That(result, Is.Empty);
            }

            [Test]
            public void GetIgnoredPackages_WithInlineList_ReturnsParsedArray()
            {
                // Arrange
                string ignoredPackages = "Package1;Package2;Package3";

                // Act
                string[] result = _parser.GetIgnoredPackages(ignoredPackages);

                // Assert
                Assert.That(result, Is.EqualTo(["Package1", "Package2", "Package3"]));
            }

            [Test]
            public void GetIgnoredPackages_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string packageFile = "/test/ignored.json";
                string[] expectedPackages = ["MyCompany.*", "TestPackage"];
                _fileSystem.AddFile(packageFile, new MockFileData($"[\"{expectedPackages[0]}\",\"{expectedPackages[1]}\"]"));

                // Act
                string[] result = _parser.GetIgnoredPackages(packageFile);

                // Assert
                Assert.That(result, Is.EqualTo(expectedPackages));
            }
        }

        [TestFixture]
        internal class GetExcludedProjectsTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetExcludedProjects_WithNull_ReturnsEmptyArray()
            {
                // Act
                string[] result = _parser.GetExcludedProjects(null);

                // Assert
                Assert.That(result, Is.Empty);
            }

            [Test]
            public void GetExcludedProjects_WithInlineList_ReturnsParsedArray()
            {
                // Arrange
                string excludedProjects = "*Test*;*.Test;Legacy*";

                // Act
                string[] result = _parser.GetExcludedProjects(excludedProjects);

                // Assert
                Assert.That(result, Is.EqualTo(["*Test*", "*.Test", "Legacy*"]));
            }

            [Test]
            public void GetExcludedProjects_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string projectFile = "/test/excluded.json";
                string[] expectedProjects = ["*Test*", "*.Test"];
                _fileSystem.AddFile(projectFile, new MockFileData($"[\"{expectedProjects[0]}\",\"{expectedProjects[1]}\"]"));

                // Act
                string[] result = _parser.GetExcludedProjects(projectFile);

                // Assert
                Assert.That(result, Is.EqualTo(expectedProjects));
            }
        }

        [TestFixture]
        internal class GetLicenseMappingsTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetLicenseMappings_WithNull_ReturnsDefaultMapping()
            {
                // Act
                IImmutableDictionary<Uri, string> result = _parser.GetLicenseMappings(null);

                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Count, Is.GreaterThan(0)); // Should contain default mappings
            }

            [Test]
            public void GetLicenseMappings_WithFile_MergesWithDefaultMappings()
            {
                // Arrange
                string mappingFile = "/test/mappings.json";
                var customUrl = new Uri("https://example.com/license");
                string customLicense = "CustomLicense";
                _fileSystem.AddFile(mappingFile, new MockFileData($"{{\"{customUrl}\":\"{customLicense}\"}}"));

                // Act
                IImmutableDictionary<Uri, string> result = _parser.GetLicenseMappings(mappingFile);

                // Assert
                Assert.That(result.ContainsKey(customUrl), Is.True);
                Assert.That(result[customUrl], Is.EqualTo(customLicense));
            }
        }

        [TestFixture]
        internal class GetOverridePackageInformationTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetOverridePackageInformation_WithNull_ReturnsEmptyArray()
            {
                // Act
                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(null);

                // Assert
                Assert.That(result, Is.Empty);
            }

            [Test]
            public void GetOverridePackageInformation_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"1.0.0\",\"License\":\"MIT\"}]"));

                // Act
                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                // Assert
                Assert.That(result, Has.Length.EqualTo(1));
                Assert.That(result[0].Id, Is.EqualTo("TestPackage"));
                Assert.That(result[0].License, Is.EqualTo("MIT"));
            }
        }

        [TestFixture]
        internal class GetLicenseMatcherTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetLicenseMatcher_WithNull_ReturnsSpdxMatcher()
            {
                // Act
                IFileLicenseMatcher result = _parser.GetLicenseMatcher(null);

                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<FileLicenseMatcher.SPDX.FastLicenseMatcher>());
            }

            [Test]
            public void GetLicenseMatcher_WithFile_ReturnsCombinedMatcher()
            {
                // Arrange
                string mappingFile = "/test/dir/license-mappings.json";
                string licenseFile = "/test/dir/LICENSE.txt";
                _fileSystem.AddFile(licenseFile, new MockFileData("MIT License content"));
                _fileSystem.AddFile(mappingFile, new MockFileData($"{{\"LICENSE.txt\":\"MIT\"}}"));

                // Act
                IFileLicenseMatcher result = _parser.GetLicenseMatcher(mappingFile);

                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.InstanceOf<FileLicenseMatcher.Combine.LicenseMatcher>());
            }
        }

        [TestFixture]
        internal class GetFileDownloaderTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetFileDownloader_WithNull_ReturnsNopDownloader()
            {
                // Act
                IFileDownloader result = _parser.GetFileDownloader(null);

                // Assert
                Assert.That(result, Is.InstanceOf<NopFileDownloader>());
            }

            [Test]
            public void GetFileDownloader_WithDirectory_CreatesDirectoryAndReturnsFileDownloader()
            {
                // Arrange
                string downloadDir = "/test/downloads";

                // Act
                IFileDownloader result = _parser.GetFileDownloader(downloadDir);

                // Assert
                Assert.That(result, Is.InstanceOf<FileDownloader>());
                Assert.That(_fileSystem.Directory.Exists(downloadDir), Is.True);
            }

            [Test]
            public void GetFileDownloader_WithExistingDirectory_ReturnsFileDownloader()
            {
                // Arrange
                string downloadDir = "/test/downloads";
                _fileSystem.AddDirectory(downloadDir);

                // Act
                IFileDownloader result = _parser.GetFileDownloader(downloadDir);

                // Assert
                Assert.That(result, Is.InstanceOf<FileDownloader>());
            }
        }

        [TestFixture]
        internal class GetOutputFormatterTests : CommandLineOptionsParserTest
        {
            [Test]
            public void GetOutputFormatter_WithTable_ReturnsTableFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Table, false, false);

                // Assert
                Assert.That(result, Is.InstanceOf<LicenseOutput.Table.TableOutputFormatter>());
            }

            [Test]
            public void GetOutputFormatter_WithMarkdown_ReturnsTableFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Markdown, false, false);

                // Assert
                Assert.That(result, Is.InstanceOf<LicenseOutput.Table.TableOutputFormatter>());
            }

            [Test]
            public void GetOutputFormatter_WithJson_ReturnsJsonFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Json, false, false);

                // Assert
                Assert.That(result, Is.InstanceOf<LicenseOutput.Json.JsonOutputFormatter>());
            }

            [Test]
            public void GetOutputFormatter_WithJsonPretty_ReturnsJsonFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.JsonPretty, false, false);

                // Assert
                Assert.That(result, Is.InstanceOf<LicenseOutput.Json.JsonOutputFormatter>());
            }

            [Test]
            public void GetOutputFormatter_WithInvalidType_ThrowsArgumentOutOfRangeException()
            {
                // Act & Assert
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    _parser.GetOutputFormatter((OutputType)999, false, false));
            }
        }
    }
}
