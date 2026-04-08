// Licensed to the project contributors.
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
    public class CommandLineOptionsParserTest
    {
        private readonly MockFileSystem _fileSystem;
        private readonly MockHttpMessageHandler _mockHttp;
        private readonly HttpClient _httpClient;
        private readonly CommandLineOptionsParser _parser;

        protected CommandLineOptionsParserTest()
        {
            _fileSystem = new MockFileSystem();
            _mockHttp = new MockHttpMessageHandler();
            _httpClient = _mockHttp.ToHttpClient();
            _parser = new CommandLineOptionsParser(_fileSystem, _httpClient);
        }

        [After(HookType.Test)]
        public void TearDown()
        {
            _httpClient.Dispose();
            _mockHttp.Dispose();
        }

        public class GetInputFilesTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetInputFiles_WithInputFile_ReturnsFileInArray()
            {
                // Arrange
                string inputFile = "/test/project.csproj";

                // Act
                string[] result = _parser.GetInputFiles(inputFile, null);

                // Assert
                await Assert.That(result).HasAtLeast(1);
                await Assert.That(result[0]).IsEqualTo(inputFile);
            }

            [Test]
            public async Task GetInputFiles_WithInputJsonFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string jsonFile = "/test/input.json";
                string[] expectedFiles = ["/test/project1.csproj", "/test/project2.csproj"];
                _fileSystem.AddFile(jsonFile, new MockFileData($"[\"{expectedFiles[0]}\",\"{expectedFiles[1]}\"]"));

                // Act
                string[] result = _parser.GetInputFiles(null, jsonFile);

                // Assert
                await Assert.That(result).IsEquivalentTo(expectedFiles);
            }

            [Test]
            public async Task GetInputFiles_WithNeitherOption_ThrowsArgumentException()
            {
                // Act & Assert
                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetInputFiles(null, null)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Please provide an input file using --input or --json-input");
            }

            [Test]
            public async Task GetInputFiles_WithBothOptions_PrefersInputFile()
            {
                // Arrange
                string inputFile = "/test/project.csproj";
                string jsonFile = "/test/input.json";
                _fileSystem.AddFile(jsonFile, new MockFileData("[\"should_not_be_used.csproj\"]"));

                // Act
                string[] result = _parser.GetInputFiles(inputFile, jsonFile);

                // Assert
                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0]).IsEqualTo(inputFile);
            }
        }

        public class GetAllowedLicensesTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetAllowedLicenses_WithNull_ReturnsEmptyArray()
            {
                // Act
                string[] result = _parser.GetAllowedLicenses(null);

                // Assert
                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetAllowedLicenses_WithInlineList_ReturnsParsedArray()
            {
                // Arrange
                string allowedLicenses = "MIT;Apache-2.0;BSD-3-Clause";

                // Act
                string[] result = _parser.GetAllowedLicenses(allowedLicenses);

                // Assert
                await Assert.That(result).IsEquivalentTo(["MIT", "Apache-2.0", "BSD-3-Clause"]);
            }

            [Test]
            public async Task GetAllowedLicenses_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string licenseFile = "/test/allowed.json";
                string[] expectedLicenses = ["MIT", "Apache-2.0"];
                _fileSystem.AddFile(licenseFile, new MockFileData($"[\"{expectedLicenses[0]}\",\"{expectedLicenses[1]}\"]"));

                // Act
                string[] result = _parser.GetAllowedLicenses(licenseFile);

                // Assert
                await Assert.That(result).IsEquivalentTo(expectedLicenses);
            }

            [Test]
            public async Task GetAllowedLicenses_WithWhitespace_TrimsValues()
            {
                // Arrange
                string allowedLicenses = " MIT ; Apache-2.0 ; BSD-3-Clause ";

                // Act
                string[] result = _parser.GetAllowedLicenses(allowedLicenses);

                // Assert
                await Assert.That(result).IsEquivalentTo(["MIT", "Apache-2.0", "BSD-3-Clause"]);
            }

            [Test]
            public async Task GetAllowedLicenses_WithInvalidJsonFile_ThrowsArgumentException()
            {
                // Arrange
                string licenseFile = "/test/allowed.json";
                _fileSystem.AddFile(licenseFile, new MockFileData("invalid json"));

                // Act & Assert
                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetAllowedLicenses(licenseFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse JSON file");
            }
        }

        public class GetIgnoredPackagesTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetIgnoredPackages_WithNull_ReturnsEmptyArray()
            {
                // Act
                string[] result = _parser.GetIgnoredPackages(null);

                // Assert
                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetIgnoredPackages_WithInlineList_ReturnsParsedArray()
            {
                // Arrange
                string ignoredPackages = "Package1;Package2;Package3";

                // Act
                string[] result = _parser.GetIgnoredPackages(ignoredPackages);

                // Assert
                await Assert.That(result).IsEquivalentTo(["Package1", "Package2", "Package3"]);
            }

            [Test]
            public async Task GetIgnoredPackages_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string packageFile = "/test/ignored.json";
                string[] expectedPackages = ["MyCompany.*", "TestPackage"];
                _fileSystem.AddFile(packageFile, new MockFileData($"[\"{expectedPackages[0]}\",\"{expectedPackages[1]}\"]"));

                // Act
                string[] result = _parser.GetIgnoredPackages(packageFile);

                // Assert
                await Assert.That(result).IsEquivalentTo(expectedPackages);
            }
        }

        public class GetExcludedProjectsTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetExcludedProjects_WithNull_ReturnsEmptyArray()
            {
                // Act
                string[] result = _parser.GetExcludedProjects(null);

                // Assert
                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetExcludedProjects_WithInlineList_ReturnsParsedArray()
            {
                // Arrange
                string excludedProjects = "*Test*;*.Test;Legacy*";

                // Act
                string[] result = _parser.GetExcludedProjects(excludedProjects);

                // Assert
                await Assert.That(result).IsEquivalentTo(["*Test*", "*.Test", "Legacy*"]);
            }

            [Test]
            public async Task GetExcludedProjects_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string projectFile = "/test/excluded.json";
                string[] expectedProjects = ["*Test*", "*.Test"];
                _fileSystem.AddFile(projectFile, new MockFileData($"[\"{expectedProjects[0]}\",\"{expectedProjects[1]}\"]"));

                // Act
                string[] result = _parser.GetExcludedProjects(projectFile);

                // Assert
                await Assert.That(result).IsEquivalentTo(expectedProjects);
            }
        }

        public class GetLicenseMappingsTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetLicenseMappings_WithNull_ReturnsDefaultMapping()
            {
                // Act
                IImmutableDictionary<Uri, string> result = _parser.GetLicenseMappings(null);

                // Assert
                await Assert.That(result).IsNotNull();
                await Assert.That(result.Count).IsGreaterThan(0); // Should contain default mappings
            }

            [Test]
            public async Task GetLicenseMappings_WithFile_MergesWithDefaultMappings()
            {
                // Arrange
                string mappingFile = "/test/mappings.json";
                var customUrl = new Uri("https://example.com/license");
                string customLicense = "CustomLicense";
                _fileSystem.AddFile(mappingFile, new MockFileData($"{{\"{customUrl}\":\"{customLicense}\"}}"));

                // Act
                IImmutableDictionary<Uri, string> result = _parser.GetLicenseMappings(mappingFile);

                // Assert
                await Assert.That(result.ContainsKey(customUrl)).IsTrue();
                await Assert.That(result[customUrl]).IsEqualTo(customLicense);
            }
        }

        public class GetOverridePackageInformationTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetOverridePackageInformation_WithNull_ReturnsEmptyArray()
            {
                // Act
                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(null);

                // Assert
                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetOverridePackageInformation_WithFile_ReadsAndDeserializesFile()
            {
                // Arrange
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"1.0.0\",\"License\":\"MIT\"}]"));

                // Act
                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                // Assert
                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0].Id).IsEqualTo("TestPackage");
                await Assert.That(result[0].License).IsEqualTo("MIT");
            }
        }

        public class GetLicenseMatcherTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetLicenseMatcher_WithNull_ReturnsSpdxMatcher()
            {
                // Act
                IFileLicenseMatcher result = _parser.GetLicenseMatcher(null);

                // Assert
                await Assert.That(result).IsNotNull();
                await Assert.That(result).IsTypeOf<FileLicenseMatcher.SPDX.FastLicenseMatcher>();
            }

            [Test]
            public async Task GetLicenseMatcher_WithFile_ReturnsCombinedMatcher()
            {
                // Arrange
                string mappingFile = "/test/dir/license-mappings.json";
                string licenseFile = "/test/dir/LICENSE.txt";
                _fileSystem.AddFile(licenseFile, new MockFileData("MIT License content"));
                _fileSystem.AddFile(mappingFile, new MockFileData($"{{\"LICENSE.txt\":\"MIT\"}}"));

                // Act
                IFileLicenseMatcher result = _parser.GetLicenseMatcher(mappingFile);

                // Assert
                await Assert.That(result).IsNotNull();
                await Assert.That(result).IsTypeOf<FileLicenseMatcher.Combine.LicenseMatcher>();
            }
        }

        public class GetFileDownloaderTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetFileDownloader_WithNull_ReturnsNopDownloader()
            {
                // Act
                IFileDownloader result = _parser.GetFileDownloader(null);

                // Assert
                await Assert.That(result).IsTypeOf<NopFileDownloader>();
            }

            [Test]
            public async Task GetFileDownloader_WithDirectory_CreatesDirectoryAndReturnsFileDownloader()
            {
                // Arrange
                string downloadDir = "/test/downloads";

                // Act
                IFileDownloader result = _parser.GetFileDownloader(downloadDir);

                // Assert
                await Assert.That(result).IsTypeOf<FileDownloader>();
                await Assert.That(_fileSystem.Directory.Exists(downloadDir)).IsTrue();
            }

            [Test]
            public async Task GetFileDownloader_WithExistingDirectory_ReturnsFileDownloader()
            {
                // Arrange
                string downloadDir = "/test/downloads";
                _fileSystem.AddDirectory(downloadDir);

                // Act
                IFileDownloader result = _parser.GetFileDownloader(downloadDir);

                // Assert
                await Assert.That(result).IsTypeOf<FileDownloader>();
            }
        }

        public class GetOutputFormatterTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetOutputFormatter_WithTable_ReturnsTableFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Table, false, false);

                // Assert
                await Assert.That(result).IsTypeOf<LicenseOutput.Table.TableOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithMarkdown_ReturnsTableFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Markdown, false, false);

                // Assert
                await Assert.That(result).IsTypeOf<LicenseOutput.Table.TableOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithJson_ReturnsJsonFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Json, false, false);

                // Assert
                await Assert.That(result).IsTypeOf<LicenseOutput.Json.JsonOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithJsonPretty_ReturnsJsonFormatter()
            {
                // Act
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.JsonPretty, false, false);

                // Assert
                await Assert.That(result).IsTypeOf<LicenseOutput.Json.JsonOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithInvalidType_ThrowsArgumentOutOfRangeException()
            {
                // Act & Assert
                await Assert.That(() =>
                    _parser.GetOutputFormatter((OutputType)999, false, false)).Throws<ArgumentOutOfRangeException>();
            }
        }
    }
}
