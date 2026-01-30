// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.CommandLine;
using System.Text.Json;
using FileLicenseMatcher;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Output;
using NuGetLicense.Output.Json;
using NuGetLicense.Output.Table;
using NuGetUtility;
using NuGetUtility.Extensions;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.ProjectFiltering;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Serialization;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

#if !NET
using System.Net.Http;
#endif

namespace NuGetLicense
{
    public static class Program
    {
        public static RootCommand CreateRootCommand()
        {
            var inputFileOption = new Option<string?>("-i", "--input")
            {
                Description = "The project (or solution) file for which to analyze dependency licenses"
            };

            var inputJsonFileOption = new Option<string?>("-ji", "--json-input")
            {
                Description = "File in json format that contains an array of all files to be evaluated. The Files can either point to a project or a solution."
            };

            var includeTransitiveOption = new Option<bool>("-t", "--include-transitive")
            {
                Description = "If set, the whole license tree is followed in order to determine all nuget's used by the projects"
            };

            var allowedLicensesOption = new Option<string?>("-a", "--allowed-license-types")
            {
                Description = "Allowed license types. Can be either a file in json format containing an array of license types, or a semicolon-separated list of license identifiers (e.g., \"MIT;Apache-2.0;BSD-3-Clause\")"
            };

            var ignoredPackagesOption = new Option<string?>("-ignore", "--ignored-packages")
            {
                Description = "Packages to ignore. Can be either a file in json format containing an array of package names, or a semicolon-separated list of package names (e.g., \"Package1;Package2\"). Wildcards (*) are supported. Note that even though packages are ignored, their transitive dependencies are not."
            };

            var licenseMappingOption = new Option<string?>("-mapping", "--licenseurl-to-license-mappings")
            {
                Description = "File in json format that contains a dictionary to map license urls to licenses."
            };

            var overridePackageInformationOption = new Option<string?>("-override", "--override-package-information")
            {
                Description = "File in json format that contains a list of package and license information which should be used in favor of the online version. This option can be used to override the license type of packages that e.g. specify the license as file."
            };

            var downloadLicenseInformationOption = new Option<string?>("-d", "--license-information-download-location")
            {
                Description = "When set, the application downloads all licenses given using a license URL to the specified folder.",
            };

            var outputTypeOption = new Option<OutputType>("-o", "--output")
            {
                Description = "This parameter allows to choose between tabular and json output.",
                DefaultValueFactory = _ => OutputType.Table
            };

            var returnErrorsOnlyOption = new Option<bool>("-err", "--error-only")
            {
                Description = "If this option is set and there are license validation errors, only the errors are returned as result. Otherwise all validation results are always returned."
            };

            var includeIgnoredPackagesOption = new Option<bool>("-include-ignored", "--include-ignored-packages")
            {
                Description = "If this option is set, the packages that are ignored from validation are still included in the output."
            };

            var excludedProjectsOption = new Option<string?>("-exclude-projects", "--exclude-projects-matching")
            {
                Description = "Projects to exclude from analysis. Can be either a file in json format containing an array of project names, or a semicolon-separated list of project names (e.g., \"TestProject1;TestProject2\"). Wildcards (*) are supported. Useful to exclude test projects when supplying a solution file as input."
            };

            var includeSharedProjectsOption = new Option<bool>("-isp", "--include-shared-projects")
            {
                Description = "If set, shared projects (.shproj) will be included in the analysis. By default, shared projects are excluded."
            };

            var targetFrameworkOption = new Option<string?>("-f", "--target-framework")
            {
                Description = "This option allows to select a Target framework moniker (https://learn.microsoft.com/en-us/dotnet/standard/frameworks) for which to analyze dependencies."
            };

            var destinationFileOption = new Option<string?>("-fo", "--file-output")
            {
                Description = "The destination file to put the validation output to. If omitted, the output is printed to the console."
            };

            var licenseFileMappingsOption = new Option<string?>("-file-mapping", "--licensefile-to-license-mappings")
            {
                Description = "File in json format that contains a dictionary to map license files to licenses."
            };

            var rootCommand = new RootCommand("A .net tool to print and validate the licenses of .net code. This tool supports .NET (Core), .NET Standard and .NET Framework projects.");
            rootCommand.Options.Add(inputFileOption);
            rootCommand.Options.Add(inputJsonFileOption);
            rootCommand.Options.Add(includeTransitiveOption);
            rootCommand.Options.Add(allowedLicensesOption);
            rootCommand.Options.Add(ignoredPackagesOption);
            rootCommand.Options.Add(licenseMappingOption);
            rootCommand.Options.Add(overridePackageInformationOption);
            rootCommand.Options.Add(downloadLicenseInformationOption);
            rootCommand.Options.Add(outputTypeOption);
            rootCommand.Options.Add(returnErrorsOnlyOption);
            rootCommand.Options.Add(includeIgnoredPackagesOption);
            rootCommand.Options.Add(excludedProjectsOption);
            rootCommand.Options.Add(includeSharedProjectsOption);
            rootCommand.Options.Add(targetFrameworkOption);
            rootCommand.Options.Add(destinationFileOption);
            rootCommand.Options.Add(licenseFileMappingsOption);

            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                string? inputFile = parseResult.GetValue(inputFileOption);
                string? inputJsonFile = parseResult.GetValue(inputJsonFileOption);
                bool includeTransitive = parseResult.GetValue(includeTransitiveOption);
                string? allowedLicenses = parseResult.GetValue(allowedLicensesOption);
                string? ignoredPackages = parseResult.GetValue(ignoredPackagesOption);
                string? licenseMapping = parseResult.GetValue(licenseMappingOption);
                string? overridePackageInformation = parseResult.GetValue(overridePackageInformationOption);
                string? downloadLicenseInformation = parseResult.GetValue(downloadLicenseInformationOption);
                OutputType outputType = parseResult.GetValue(outputTypeOption);
                bool returnErrorsOnly = parseResult.GetValue(returnErrorsOnlyOption);
                bool includeIgnoredPackages = parseResult.GetValue(includeIgnoredPackagesOption);
                string? excludedProjects = parseResult.GetValue(excludedProjectsOption);
                bool includeSharedProjects = parseResult.GetValue(includeSharedProjectsOption);
                string? targetFramework = parseResult.GetValue(targetFrameworkOption);
                string? destinationFile = parseResult.GetValue(destinationFileOption);
                string? licenseFileMappings = parseResult.GetValue(licenseFileMappingsOption);

                using var httpClient = new HttpClient();
                var fileSystem = new System.IO.Abstractions.FileSystem();
                string[] inputFiles = GetInputFiles(fileSystem, inputFile, inputJsonFile);
                string[] ignoredPackagesArray = GetIgnoredPackages(fileSystem, ignoredPackages);
                IImmutableDictionary<Uri, string> licenseMappings = GetLicenseMappings(fileSystem, licenseMapping);
                string[] allowedLicensesArray = GetAllowedLicenses(fileSystem, allowedLicenses);
                CustomPackageInformation[] overridePackageInformationArray = GetOverridePackageInformation(fileSystem, overridePackageInformation);
                IFileDownloader? licenseDownloader = GetFileDownloader(httpClient, fileSystem, downloadLicenseInformation);
                IOutputFormatter output = GetOutputFormatter(outputType, returnErrorsOnly, includeIgnoredPackages);

                var solutionPersistance = new SolutionPersistanceWrapper();
                var projectCollector = new ProjectsCollector(solutionPersistance);
                var msBuild = new MsBuildAbstraction();
                var projectReader = new ReferencedPackageReader(msBuild, new LockFileFactory(), GetPackagesConfigReader());
                var validator = new LicenseValidator.LicenseValidator(licenseMappings,
                    allowedLicensesArray,
                    licenseDownloader,
                    GetLicenseMatcher(fileSystem, licenseFileMappings),
                    ignoredPackagesArray);

                string[] excludedProjectsArray = GetExcludedProjects(fileSystem, excludedProjects);
                IEnumerable<string> projects = (await inputFiles.SelectManyAsync(projectCollector.GetProjectsAsync)).Where(p => !Array.Exists(excludedProjectsArray, ignored => p.Like(ignored)));
                IEnumerable<ProjectWithReferencedPackages> packagesForProject = GetPackagesPerProject(projects, projectReader, includeTransitive, targetFramework, includeSharedProjects, out IReadOnlyCollection<Exception>? projectReaderExceptions);
                IAsyncEnumerable<ReferencedPackageWithContext> downloadedLicenseInformation =
                    packagesForProject.SelectMany(p => GetPackageInformations(p, overridePackageInformationArray, cancellationToken));
                var results = (await validator.Validate(downloadedLicenseInformation, cancellationToken)).ToList();

                if (projectReaderExceptions.Count > 0)
                {
                    await WriteValidationExceptions(projectReaderExceptions);

                    return -1;
                }

                try
                {
                    using Stream outputStream = GetOutputStream(fileSystem, destinationFile);
                    await output.Write(outputStream, results.OrderBy(r => r.PackageId).ThenBy(r => r.PackageVersion).ToList());
                    return results.Count(r => r.ValidationErrors.Any());
                }
                catch (Exception e)
                {
                    await Console.Error.WriteLineAsync(e.ToString());
                    return -1;
                }
            });

            return rootCommand;
        }

        private static Stream GetOutputStream(System.IO.Abstractions.IFileSystem fileSystem, string? destinationFile)
        {
            if (destinationFile is null)
            {
                return Console.OpenStandardOutput();
            }
            return fileSystem.File.Open(fileSystem.Path.GetFullPath(destinationFile), FileMode.Create, FileAccess.Write, FileShare.None);
        }

        private static IPackagesConfigReader GetPackagesConfigReader()
        {
#if NETFRAMEWORK
            return new WindowsPackagesConfigReader();
#else
            return OperatingSystem.IsWindows() ? new WindowsPackagesConfigReader() : new FailingPackagesConfigReader();
#endif
        }

        private static IAsyncEnumerable<ReferencedPackageWithContext> GetPackageInformations(
            ProjectWithReferencedPackages projectWithReferences,
            IEnumerable<CustomPackageInformation> overridePackageInformation,
            CancellationToken cancellation)
        {
            ISettings settings = Settings.LoadDefaultSettings(projectWithReferences.Project);
            var sourceProvider = new PackageSourceProvider(settings);

            using var sourceRepositoryProvider = new WrappedSourceRepositoryProvider(new SourceRepositoryProvider(sourceProvider, Repository.Provider.GetCoreV3()));
            var globalPackagesFolderUtility = new GlobalPackagesFolderUtility(settings);
            var informationReader = new PackageInformationReader(sourceRepositoryProvider, globalPackagesFolderUtility, overridePackageInformation);

            return informationReader.GetPackageInfo(new ProjectWithReferencedPackages(projectWithReferences.Project, projectWithReferences.ReferencedPackages), cancellation);
        }

        private static IOutputFormatter GetOutputFormatter(OutputType outputType, bool returnErrorsOnly, bool includeIgnoredPackages)
        {
            return outputType switch
            {
                OutputType.Json => new JsonOutputFormatter(false, returnErrorsOnly, !includeIgnoredPackages),
                OutputType.JsonPretty => new JsonOutputFormatter(true, returnErrorsOnly, !includeIgnoredPackages),
                OutputType.Table => new TableOutputFormatter(returnErrorsOnly, !includeIgnoredPackages),
                OutputType.Markdown => new TableOutputFormatter(returnErrorsOnly, !includeIgnoredPackages, printMarkdown: true),
                _ => throw new ArgumentOutOfRangeException($"{outputType} not supported")
            };
        }

        private static IFileDownloader GetFileDownloader(HttpClient httpClient, System.IO.Abstractions.IFileSystem fileSystem, string? downloadLicenseInformation)
        {
            if (downloadLicenseInformation == null)
            {
                return new NopFileDownloader();
            }

            if (!fileSystem.Directory.Exists(downloadLicenseInformation))
            {
                fileSystem.Directory.CreateDirectory(downloadLicenseInformation);
            }

            return new FileDownloader(httpClient, downloadLicenseInformation);
        }

        private static async Task WriteValidationExceptions(IReadOnlyCollection<Exception> validationExceptions)
        {
            foreach (Exception exception in validationExceptions)
            {
                await Console.Error.WriteLineAsync(exception.ToString());
            }
        }

        private static CustomPackageInformation[] GetOverridePackageInformation(System.IO.Abstractions.IFileSystem fileSystem, string? overridePackageInformation)
        {
            if (overridePackageInformation == null)
            {
                return Array.Empty<CustomPackageInformation>();
            }

            var serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new NuGetVersionJsonConverter());
            return JsonSerializer.Deserialize<CustomPackageInformation[]>(fileSystem.File.ReadAllText(overridePackageInformation), serializerOptions)!;
        }

        private static string[] ParseStringArrayOrFile(System.IO.Abstractions.IFileSystem fileSystem, string? value)
        {
            if (value == null)
            {
                return Array.Empty<string>();
            }

            // Check if the value is a path to an existing file
            if (fileSystem.File.Exists(value))
            {
                try
                {
                    string fileContent = fileSystem.File.ReadAllText(value);
                    string[]? result = JsonSerializer.Deserialize<string[]>(fileContent);
                    if (result == null)
                    {
                        throw new InvalidOperationException($"File '{value}' contains invalid JSON: expected an array of strings but got null.");
                    }
                    return result;
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to parse JSON file '{value}': {ex.Message}", ex);
                }
            }

            // Parse as semicolon-separated inline values
            return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string[] GetAllowedLicenses(System.IO.Abstractions.IFileSystem fileSystem, string? allowedLicenses)
        {
            return ParseStringArrayOrFile(fileSystem, allowedLicenses);
        }

        private static IImmutableDictionary<Uri, string> GetLicenseMappings(System.IO.Abstractions.IFileSystem fileSystem, string? licenseMapping)
        {
            if (licenseMapping == null)
            {
                return UrlToLicenseMapping.Default;
            }

            Dictionary<Uri, string> userDictionary = JsonSerializer.Deserialize<Dictionary<Uri, string>>(fileSystem.File.ReadAllText(licenseMapping))!;

            return UrlToLicenseMapping.Default.SetItems(userDictionary);
        }

        private static string[] GetIgnoredPackages(System.IO.Abstractions.IFileSystem fileSystem, string? ignoredPackages)
        {
            return ParseStringArrayOrFile(fileSystem, ignoredPackages);
        }

        private static string[] GetExcludedProjects(System.IO.Abstractions.IFileSystem fileSystem, string? excludedProjects)
        {
            return ParseStringArrayOrFile(fileSystem, excludedProjects);
        }

        private static string[] GetInputFiles(System.IO.Abstractions.IFileSystem fileSystem, string? inputFile, string? inputJsonFile)
        {
            if (inputFile != null)
            {
                return [inputFile];
            }

            if (inputJsonFile != null)
            {
                return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(inputJsonFile))!;
            }

            throw new FileNotFoundException("Please provide an input file");
        }

        private static IReadOnlyCollection<ProjectWithReferencedPackages> GetPackagesPerProject(
            IEnumerable<string> projects,
            ReferencedPackageReader reader,
            bool includeTransitive,
            string? targetFramework,
            bool includeSharedProjects,
            out IReadOnlyCollection<Exception> exceptions)
        {
            var encounteredExceptions = new List<Exception>();
            var result = new List<ProjectWithReferencedPackages>();
            exceptions = encounteredExceptions;

            ProjectFilter filter = new ProjectFilter();
            foreach (string project in filter.FilterProjects(projects, includeSharedProjects))
            {
                try
                {
                    IEnumerable<PackageIdentity> installedPackages = reader.GetInstalledPackages(project, includeTransitive, targetFramework);
                    result.Add(new ProjectWithReferencedPackages(project, installedPackages));
                }
                catch (Exception e)
                {
                    encounteredExceptions.Add(e);
                }
            }

            return result;
        }

        private static IFileLicenseMatcher GetLicenseMatcher(System.IO.Abstractions.IFileSystem fileSystem, string? licenseFileMappings)
        {
            var spdxLicemseMatcher = new FileLicenseMatcher.SPDX.FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses);
            if (licenseFileMappings is null)
            {
                return spdxLicemseMatcher;
            }

            string containingDirectory = fileSystem.Path.GetDirectoryName(fileSystem.Path.GetFullPath(licenseFileMappings))!;
            Dictionary<string, string> rawMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(fileSystem.File.ReadAllText(licenseFileMappings))!;
            var fullPathMappings = rawMappings.ToDictionary(kvp => fileSystem.Path.GetFullPath(fileSystem.Path.Combine(containingDirectory, kvp.Key)), kvp => kvp.Value);

            return new FileLicenseMatcher.Combine.LicenseMatcher([
                new FileLicenseMatcher.Compare.LicenseMatcher(fileSystem, fullPathMappings),
                spdxLicemseMatcher
            ]);
        }
    }
}
