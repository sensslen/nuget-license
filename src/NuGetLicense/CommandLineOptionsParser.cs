// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using FileLicenseMatcher;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Output;
using NuGetUtility;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Serialization;
using NuGetUtility.Wrapper.HttpClientWrapper;

#if !NET
using System.Net.Http;
#endif

namespace NuGetLicense
{
    /// <summary>
    /// Parses and transforms command line options into the format required by the license validation orchestrator.
    /// </summary>
    public class CommandLineOptionsParser(IFileSystem fileSystem, HttpClient httpClient) : ICommandLineOptionsParser
    {
        public string[] GetInputFiles(string? inputFile, string? inputJsonFile)
        {
            if (inputFile is not null)
            {
                return [inputFile];
            }

            if (inputJsonFile is not null)
            {
                return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(inputJsonFile))!;
            }

            // Defensive check: validation should already be done at command line parsing level,
            // but throw exception if called directly or if validation is bypassed
            throw new ArgumentException("Please provide an input file using --input or --json-input");
        }

        public string[] GetAllowedLicenses(string? allowedLicenses)
        {
            return ParseStringArrayOrFile(allowedLicenses);
        }

        public string[] GetIgnoredPackages(string? ignoredPackages)
        {
            return ParseStringArrayOrFile(ignoredPackages);
        }

        public string[] GetExcludedProjects(string? excludedProjects)
        {
            return ParseStringArrayOrFile(excludedProjects);
        }

        public IImmutableDictionary<Uri, string> GetLicenseMappings(string? licenseMapping)
        {
            if (licenseMapping is null)
            {
                return UrlToLicenseMapping.Default;
            }

            Dictionary<Uri, string> userDictionary = JsonSerializer.Deserialize<Dictionary<Uri, string>>(fileSystem.File.ReadAllText(licenseMapping))!;

            return UrlToLicenseMapping.Default.SetItems(userDictionary);
        }

        public CustomPackageInformation[] GetOverridePackageInformation(string? overridePackageInformation)
        {
            if (overridePackageInformation is null)
            {
                return [];
            }

            try
            {
                string fileContent = fileSystem.File.ReadAllText(overridePackageInformation);
                var serializerOptions = new JsonSerializerOptions();
                serializerOptions.Converters.Add(new NuGetVersionJsonConverter());
                CustomPackageInformation[]? result = JsonSerializer.Deserialize<CustomPackageInformation[]>(fileContent, serializerOptions);
                return result ?? throw new ArgumentException($"File '{overridePackageInformation}' contains invalid JSON: expected an array of package information but got null.");
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Failed to parse override package information file '{overridePackageInformation}': {ex.Message}", ex);
            }
        }

        public IFileLicenseMatcher GetLicenseMatcher(string? licenseFileMappings)
        {
            var spdxLicenseMatcher = new FileLicenseMatcher.SPDX.FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses);
            if (licenseFileMappings is null)
            {
                return spdxLicenseMatcher;
            }

            string containingDirectory = fileSystem.Path.GetDirectoryName(fileSystem.Path.GetFullPath(licenseFileMappings))!;
            Dictionary<string, string> rawMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(fileSystem.File.ReadAllText(licenseFileMappings))!;
            var fullPathMappings = rawMappings.ToDictionary(kvp => fileSystem.Path.GetFullPath(fileSystem.Path.Combine(containingDirectory, kvp.Key)), kvp => kvp.Value);

            return new FileLicenseMatcher.Combine.LicenseMatcher([
                new FileLicenseMatcher.Compare.LicenseMatcher(fileSystem, fullPathMappings),
                spdxLicenseMatcher
            ]);
        }

        public IFileDownloader GetFileDownloader(string? downloadLicenseInformation)
        {
            if (downloadLicenseInformation is null)
            {
                return new NopFileDownloader();
            }

            if (!fileSystem.Directory.Exists(downloadLicenseInformation))
            {
                fileSystem.Directory.CreateDirectory(downloadLicenseInformation);
            }

            return new FileDownloader(httpClient, downloadLicenseInformation);
        }

        public IOutputFormatter GetOutputFormatter(OutputType outputType, bool returnErrorsOnly, bool includeIgnoredPackages)
        {
            return outputType switch
            {
                OutputType.Json => new Output.Json.JsonOutputFormatter(false, returnErrorsOnly, !includeIgnoredPackages),
                OutputType.JsonPretty => new Output.Json.JsonOutputFormatter(true, returnErrorsOnly, !includeIgnoredPackages),
                OutputType.Table => new Output.Table.TableOutputFormatter(returnErrorsOnly, !includeIgnoredPackages),
                OutputType.Markdown => new Output.Table.TableOutputFormatter(returnErrorsOnly, !includeIgnoredPackages, printMarkdown: true),
                OutputType.Csv => new Output.Csv.CsvOutputFormatter(returnErrorsOnly, !includeIgnoredPackages),
                _ => throw new ArgumentOutOfRangeException($"{outputType} not supported")
            };
        }

        private string[] ParseStringArrayOrFile(string? value)
        {
            if (value is null)
            {
                return [];
            }

            // Check if the value is a path to an existing file
            if (fileSystem.File.Exists(value))
            {
                try
                {
                    string fileContent = fileSystem.File.ReadAllText(value);
                    string[]? result = JsonSerializer.Deserialize<string[]>(fileContent);
                    return result ?? throw new ArgumentException($"File '{value}' contains invalid JSON: expected an array of strings but got null.");
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"Failed to parse JSON file '{value}': {ex.Message}", ex);
                }
            }

            // Parse as semicolon-separated inline values
            string[] parts = value.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            // Trim each part manually for .NET Framework compatibility
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }
            return Array.FindAll(parts, part => part.Length > 0);
        }
    }
}
