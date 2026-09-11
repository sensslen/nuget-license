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
                string inputFile = "/test/project.csproj";

                string[] result = _parser.GetInputFiles(inputFile, null);

                await Assert.That(result).HasAtLeast(1);
                await Assert.That(result[0]).IsEqualTo(inputFile);
            }

            [Test]
            public async Task GetInputFiles_WithInputJsonFile_ReadsAndDeserializesFile()
            {
                string jsonFile = "/test/input.json";
                string[] expectedFiles = ["/test/project1.csproj", "/test/project2.csproj"];
                _fileSystem.AddFile(jsonFile, new MockFileData($"[\"{expectedFiles[0]}\",\"{expectedFiles[1]}\"]"));

                string[] result = _parser.GetInputFiles(null, jsonFile);

                await Assert.That(result).IsEquivalentTo(expectedFiles);
            }

            [Test]
            public async Task GetInputFiles_WithNeitherOption_ThrowsArgumentException()
            {
                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetInputFiles(null, null)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Please provide an input file using --input or --json-input");
            }

            [Test]
            public async Task GetInputFiles_WithBothOptions_PrefersInputFile()
            {
                string inputFile = "/test/project.csproj";
                string jsonFile = "/test/input.json";
                _fileSystem.AddFile(jsonFile, new MockFileData("[\"should_not_be_used.csproj\"]"));

                string[] result = _parser.GetInputFiles(inputFile, jsonFile);

                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0]).IsEqualTo(inputFile);
            }
        }

        public class GetAllowedLicensesTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetAllowedLicenses_WithNull_ReturnsEmptyArray()
            {
                string[] result = _parser.GetAllowedLicenses(null);

                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetAllowedLicenses_WithInlineList_ReturnsParsedArray()
            {
                string allowedLicenses = "MIT;Apache-2.0;BSD-3-Clause";

                string[] result = _parser.GetAllowedLicenses(allowedLicenses);

                await Assert.That(result).IsEquivalentTo(["MIT", "Apache-2.0", "BSD-3-Clause"]);
            }

            [Test]
            public async Task GetAllowedLicenses_WithFile_ReadsAndDeserializesFile()
            {
                string licenseFile = "/test/allowed.json";
                string[] expectedLicenses = ["MIT", "Apache-2.0"];
                _fileSystem.AddFile(licenseFile, new MockFileData($"[\"{expectedLicenses[0]}\",\"{expectedLicenses[1]}\"]"));

                string[] result = _parser.GetAllowedLicenses(licenseFile);

                await Assert.That(result).IsEquivalentTo(expectedLicenses);
            }

            [Test]
            public async Task GetAllowedLicenses_WithWhitespace_TrimsValues()
            {
                string allowedLicenses = " MIT ; Apache-2.0 ; BSD-3-Clause ";

                string[] result = _parser.GetAllowedLicenses(allowedLicenses);

                await Assert.That(result).IsEquivalentTo(["MIT", "Apache-2.0", "BSD-3-Clause"]);
            }

            [Test]
            public async Task GetAllowedLicenses_WithInvalidJsonFile_ThrowsArgumentException()
            {
                string licenseFile = "/test/allowed.json";
                _fileSystem.AddFile(licenseFile, new MockFileData("invalid json"));

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
                string[] result = _parser.GetIgnoredPackages(null);

                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetIgnoredPackages_WithInlineList_ReturnsParsedArray()
            {
                string ignoredPackages = "Package1;Package2;Package3";

                string[] result = _parser.GetIgnoredPackages(ignoredPackages);

                await Assert.That(result).IsEquivalentTo(["Package1", "Package2", "Package3"]);
            }

            [Test]
            public async Task GetIgnoredPackages_WithFile_ReadsAndDeserializesFile()
            {
                string packageFile = "/test/ignored.json";
                string[] expectedPackages = ["MyCompany.*", "TestPackage"];
                _fileSystem.AddFile(packageFile, new MockFileData($"[\"{expectedPackages[0]}\",\"{expectedPackages[1]}\"]"));

                string[] result = _parser.GetIgnoredPackages(packageFile);

                await Assert.That(result).IsEquivalentTo(expectedPackages);
            }
        }

        public class GetExcludedProjectsTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetExcludedProjects_WithNull_ReturnsEmptyArray()
            {
                string[] result = _parser.GetExcludedProjects(null);

                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetExcludedProjects_WithInlineList_ReturnsParsedArray()
            {
                string excludedProjects = "*Test*;*.Test;Legacy*";

                string[] result = _parser.GetExcludedProjects(excludedProjects);

                await Assert.That(result).IsEquivalentTo(["*Test*", "*.Test", "Legacy*"]);
            }

            [Test]
            public async Task GetExcludedProjects_WithFile_ReadsAndDeserializesFile()
            {
                string projectFile = "/test/excluded.json";
                string[] expectedProjects = ["*Test*", "*.Test"];
                _fileSystem.AddFile(projectFile, new MockFileData($"[\"{expectedProjects[0]}\",\"{expectedProjects[1]}\"]"));

                string[] result = _parser.GetExcludedProjects(projectFile);

                await Assert.That(result).IsEquivalentTo(expectedProjects);
            }
        }

        public class GetLicenseMappingsTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetLicenseMappings_WithNull_ReturnsDefaultMapping()
            {
                IImmutableDictionary<Uri, string> result = _parser.GetLicenseMappings(null);

                await Assert.That(result).IsNotNull();
                await Assert.That(result.Count).IsGreaterThan(0); // Should contain default mappings
            }

            [Test]
            public async Task GetLicenseMappings_WithFile_MergesWithDefaultMappings()
            {
                string mappingFile = "/test/mappings.json";
                var customUrl = new Uri("https://example.com/license");
                string customLicense = "CustomLicense";
                _fileSystem.AddFile(mappingFile, new MockFileData($"{{\"{customUrl}\":\"{customLicense}\"}}"));

                IImmutableDictionary<Uri, string> result = _parser.GetLicenseMappings(mappingFile);

                await Assert.That(result.ContainsKey(customUrl)).IsTrue();
                await Assert.That(result[customUrl]).IsEqualTo(customLicense);
            }
        }

        public class GetOverridePackageInformationTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetOverridePackageInformation_WithNull_ReturnsEmptyArray()
            {
                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(null);

                await Assert.That(result).IsEmpty();
            }

            [Test]
            public async Task GetOverridePackageInformation_WithFile_ReadsAndDeserializesFile()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"1.0.0\",\"License\":\"MIT\"}]"));

                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0].Id).IsEqualTo("TestPackage");
                await Assert.That(result[0].License).IsEqualTo("MIT");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithMissingId_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Version\":\"1.0.0\",\"License\":\"MIT\"}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithMissingVersion_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"License\":\"MIT\"}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithMissingLicense_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"1.0.0\"}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithNullId_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":null,\"Version\":\"1.0.0\",\"License\":\"MIT\"}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithNullVersion_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":null,\"License\":\"MIT\"}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithNullLicense_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"1.0.0\",\"License\":null}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithInvalidVersion_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"not-a-version\",\"License\":\"MIT\"}]"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithValidOptionalFields_DeserializesSuccessfully()
            {
                string overrideFile = "/test/override.json";
                string jsonContent = "[{" +
                    "\"Id\":\"TestPackage\"," +
                    "\"Version\":\"1.0.0\"," +
                    "\"License\":\"MIT\"," +
                    "\"Copyright\":\"Copyright (c) 2023 Test\"," +
                    "\"Authors\":\"Author1;Author2\"," +
                    "\"Title\":\"Test Title\"," +
                    "\"ProjectUrl\":\"https://example.com\"," +
                    "\"Summary\":\"Test Summary\"," +
                    "\"Description\":\"Test Description\"," +
                    "\"LicenseUrl\":\"https://opensource.org/licenses/MIT\"" +
                    "}]";
                _fileSystem.AddFile(overrideFile, new MockFileData(jsonContent));

                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0].Id).IsEqualTo("TestPackage");
                await Assert.That(result[0].Version.ToString()).IsEqualTo("1.0.0");
                await Assert.That(result[0].License).IsEqualTo("MIT");
                await Assert.That(result[0].Copyright).IsEqualTo("Copyright (c) 2023 Test");
                await Assert.That(result[0].Authors).IsEqualTo("Author1;Author2");
                await Assert.That(result[0].Title).IsEqualTo("Test Title");
                await Assert.That(result[0].ProjectUrl).IsEqualTo("https://example.com");
                await Assert.That(result[0].Summary).IsEqualTo("Test Summary");
                await Assert.That(result[0].Description).IsEqualTo("Test Description");
                await Assert.That(result[0].LicenseUrl).IsEqualTo(new Uri("https://opensource.org/licenses/MIT"));
            }

            [Test]
            public async Task GetOverridePackageInformation_WithOnlyRequiredFields_DeserializesSuccessfully()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[{\"Id\":\"TestPackage\",\"Version\":\"2.1.0\",\"License\":\"Apache-2.0\"}]"));

                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                await Assert.That(result).Count().IsEqualTo(1);
                await Assert.That(result[0].Id).IsEqualTo("TestPackage");
                await Assert.That(result[0].Version.ToString()).IsEqualTo("2.1.0");
                await Assert.That(result[0].License).IsEqualTo("Apache-2.0");
                await Assert.That(result[0].Copyright).IsNull();
                await Assert.That(result[0].Authors).IsNull();
                await Assert.That(result[0].Title).IsNull();
                await Assert.That(result[0].ProjectUrl).IsNull();
                await Assert.That(result[0].Summary).IsNull();
                await Assert.That(result[0].Description).IsNull();
                await Assert.That(result[0].LicenseUrl).IsNull();
            }

            [Test]
            public async Task GetOverridePackageInformation_WithMultiplePackages_DeserializesAll()
            {
                string overrideFile = "/test/override.json";
                string jsonContent = "[" +
                    "{\"Id\":\"Package1\",\"Version\":\"1.0.0\",\"License\":\"MIT\"}," +
                    "{\"Id\":\"Package2\",\"Version\":\"2.0.0\",\"License\":\"Apache-2.0\"}," +
                    "{\"Id\":\"Package3\",\"Version\":\"3.0.0\",\"License\":\"BSD-3-Clause\"}" +
                    "]";
                _fileSystem.AddFile(overrideFile, new MockFileData(jsonContent));

                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                await Assert.That(result).Count().IsEqualTo(3);
                await Assert.That(result[0].Id).IsEqualTo("Package1");
                await Assert.That(result[0].License).IsEqualTo("MIT");
                await Assert.That(result[1].Id).IsEqualTo("Package2");
                await Assert.That(result[1].License).IsEqualTo("Apache-2.0");
                await Assert.That(result[2].Id).IsEqualTo("Package3");
                await Assert.That(result[2].License).IsEqualTo("BSD-3-Clause");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithInvalidJson_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("not valid json"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("Failed to parse override package information file");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithNullContent_ThrowsArgumentException()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("null"));

                ArgumentException? ex = await Assert.That(() =>
                    _parser.GetOverridePackageInformation(overrideFile)).Throws<ArgumentException>();
                await Assert.That(ex!.Message).Contains("expected an array of package information but got null");
            }

            [Test]
            public async Task GetOverridePackageInformation_WithEmptyArray_ReturnsEmptyArray()
            {
                string overrideFile = "/test/override.json";
                _fileSystem.AddFile(overrideFile, new MockFileData("[]"));

                CustomPackageInformation[] result = _parser.GetOverridePackageInformation(overrideFile);

                await Assert.That(result).IsEmpty();
            }
        }

        public class GetLicenseMatcherTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetLicenseMatcher_WithNull_ReturnsSpdxMatcher()
            {
                IFileLicenseMatcher result = _parser.GetLicenseMatcher(null);

                await Assert.That(result).IsNotNull();
                await Assert.That(result).IsTypeOf<FileLicenseMatcher.SPDX.FastLicenseMatcher>();
            }

            [Test]
            public async Task GetLicenseMatcher_WithFile_ReturnsCombinedMatcher()
            {
                string mappingFile = "/test/dir/license-mappings.json";
                string licenseFile = "/test/dir/LICENSE.txt";
                _fileSystem.AddFile(licenseFile, new MockFileData("MIT License content"));
                _fileSystem.AddFile(mappingFile, new MockFileData("{\"LICENSE.txt\":\"MIT\"}"));

                IFileLicenseMatcher result = _parser.GetLicenseMatcher(mappingFile);

                await Assert.That(result).IsNotNull();
                await Assert.That(result).IsTypeOf<FileLicenseMatcher.Combine.LicenseMatcher>();
            }
        }

        public class GetFileDownloaderTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetFileDownloader_WithNull_ReturnsNopDownloader()
            {
                IFileDownloader result = _parser.GetFileDownloader(null);

                await Assert.That(result).IsTypeOf<NopFileDownloader>();
            }

            [Test]
            public async Task GetFileDownloader_WithDirectory_CreatesDirectoryAndReturnsFileDownloader()
            {
                string downloadDir = "/test/downloads";

                IFileDownloader result = _parser.GetFileDownloader(downloadDir);

                await Assert.That(result).IsTypeOf<FileDownloader>();
                await Assert.That(_fileSystem.Directory.Exists(downloadDir)).IsTrue();
            }

            [Test]
            public async Task GetFileDownloader_WithExistingDirectory_ReturnsFileDownloader()
            {
                string downloadDir = "/test/downloads";
                _fileSystem.AddDirectory(downloadDir);

                IFileDownloader result = _parser.GetFileDownloader(downloadDir);

                await Assert.That(result).IsTypeOf<FileDownloader>();
            }
        }

        public class GetOutputFormatterTests : CommandLineOptionsParserTest
        {
            [Test]
            public async Task GetOutputFormatter_WithTable_ReturnsTableFormatter()
            {
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Table, false, false);

                await Assert.That(result).IsTypeOf<LicenseOutput.Table.TableOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithMarkdown_ReturnsTableFormatter()
            {
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Markdown, false, false);

                await Assert.That(result).IsTypeOf<LicenseOutput.Table.TableOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithJson_ReturnsJsonFormatter()
            {
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.Json, false, false);

                await Assert.That(result).IsTypeOf<LicenseOutput.Json.JsonOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithJsonPretty_ReturnsJsonFormatter()
            {
                LicenseOutput.IOutputFormatter result = _parser.GetOutputFormatter(OutputType.JsonPretty, false, false);

                await Assert.That(result).IsTypeOf<LicenseOutput.Json.JsonOutputFormatter>();
            }

            [Test]
            public async Task GetOutputFormatter_WithInvalidType_ThrowsArgumentOutOfRangeException()
            {
                await Assert.That(() =>
                    _parser.GetOutputFormatter((OutputType)999, false, false)).Throws<ArgumentOutOfRangeException>();
            }
        }
    }
}
