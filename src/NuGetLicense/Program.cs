// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.CommandLine;
using System.IO.Abstractions;
using System.Reflection;
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
    public class Program
    {
        public static RootCommand CreateRootCommand()
        {
            var inputFileOption = new Option<string?>(
                aliases: new[] { "-i", "--input" },
                description: "The project (or solution) file for which to analyze dependency licenses");

            var inputJsonFileOption = new Option<string?>(
                aliases: new[] { "-ji", "--json-input" },
                description: "File in json format that contains an array of all files to be evaluated. The Files can either point to a project or a solution.");

            var includeTransitiveOption = new Option<bool>(
                aliases: new[] { "-t", "--include-transitive" },
                description: "If set, the whole license tree is followed in order to determine all nuget's used by the projects");

            var allowedLicensesOption = new Option<string?>(
                aliases: new[] { "-a", "--allowed-license-types" },
                description: "File in json format that contains an array of all allowed license types");

            var ignoredPackagesOption = new Option<string?>(
                aliases: new[] { "-ignore", "--ignored-packages" },
                description: "File in json format that contains an array of nuget package names to ignore (e.g. useful for nuget packages built in-house). Note that even though the packages are ignored, their transitive dependencies are not. Wildcard characters (*) are supported to specify ranges of ignored packages.");

            var licenseMappingOption = new Option<string?>(
                aliases: new[] { "-mapping", "--licenseurl-to-license-mappings" },
                description: "File in json format that contains a dictionary to map license urls to licenses.");

            var overridePackageInformationOption = new Option<string?>(
                aliases: new[] { "-override", "--override-package-information" },
                description: "File in json format that contains a list of package and license information which should be used in favor of the online version. This option can be used to override the license type of packages that e.g. specify the license as file.");

            var downloadLicenseInformationOption = new Option<string?>(
                aliases: new[] { "-d", "--license-information-download-location" },
                description: "When set, the application downloads all licenses given using a license URL to the specified folder.");

            var outputTypeOption = new Option<OutputType>(
                aliases: new[] { "-o", "--output" },
                getDefaultValue: () => OutputType.Table,
                description: "This parameter allows to choose between tabular and json output.");

            var returnErrorsOnlyOption = new Option<bool>(
                aliases: new[] { "-err", "--error-only" },
                description: "If this option is set and there are license validation errors, only the errors are returned as result. Otherwise all validation results are always returned.");

            var includeIgnoredPackagesOption = new Option<bool>(
                aliases: new[] { "-include-ignored", "--include-ignored-packages" },
                description: "If this option is set, the packages that are ignored from validation are still included in the output.");

            var excludedProjectsOption = new Option<string?>(
                aliases: new[] { "-exclude-projects", "--exclude-projects-matching" },
                description: "This option allows to specify project name(s) to exclude from the analysis. This can be useful to exclude test projects from the analysis when supplying a solution file as input. Wildcard characters (*) are supported to specify ranges of ignored projects. The input can either be a file name containing a list of project names in json format or a plain string that is then used as a single entry.");

            var includeSharedProjectsOption = new Option<bool>(
                aliases: new[] { "-isp", "--include-shared-projects" },
                description: "If set, shared projects (.shproj) will be included in the analysis. By default, shared projects are excluded.");

            var targetFrameworkOption = new Option<string?>(
                aliases: new[] { "-f", "--target-framework" },
                description: "This option allows to select a Target framework moniker (https://learn.microsoft.com/en-us/dotnet/standard/frameworks) for which to analyze dependencies.");

            var destinationFileOption = new Option<string?>(
                aliases: new[] { "-fo", "--file-output" },
                description: "The destination file to put the validation output to. If omitted, the output is printed to the console.");

            var licenseFileMappingsOption = new Option<string?>(
                aliases: new[] { "-file-mapping", "--licensefile-to-license-mappings" },
                description: "File in json format that contains a dictionary to map license files to licenses.");

            var rootCommand = new RootCommand("A .net tool to print and validate the licenses of .net code. This tool supports .NET (Core), .NET Standard and .NET Framework projects.");
            rootCommand.AddOption(inputFileOption);
            rootCommand.AddOption(inputJsonFileOption);
            rootCommand.AddOption(includeTransitiveOption);
            rootCommand.AddOption(allowedLicensesOption);
            rootCommand.AddOption(ignoredPackagesOption);
            rootCommand.AddOption(licenseMappingOption);
            rootCommand.AddOption(overridePackageInformationOption);
            rootCommand.AddOption(downloadLicenseInformationOption);
            rootCommand.AddOption(outputTypeOption);
            rootCommand.AddOption(returnErrorsOnlyOption);
            rootCommand.AddOption(includeIgnoredPackagesOption);
            rootCommand.AddOption(excludedProjectsOption);
            rootCommand.AddOption(includeSharedProjectsOption);
            rootCommand.AddOption(targetFrameworkOption);
            rootCommand.AddOption(destinationFileOption);
            rootCommand.AddOption(licenseFileMappingsOption);

            rootCommand.SetHandler(async (context) =>
            {
                string? inputFile = context.ParseResult.GetValueForOption(inputFileOption);
                string? inputJsonFile = context.ParseResult.GetValueForOption(inputJsonFileOption);
                bool includeTransitive = context.ParseResult.GetValueForOption(includeTransitiveOption);
                string? allowedLicenses = context.ParseResult.GetValueForOption(allowedLicensesOption);
                string? ignoredPackages = context.ParseResult.GetValueForOption(ignoredPackagesOption);
                string? licenseMapping = context.ParseResult.GetValueForOption(licenseMappingOption);
                string? overridePackageInformation = context.ParseResult.GetValueForOption(overridePackageInformationOption);
                string? downloadLicenseInformation = context.ParseResult.GetValueForOption(downloadLicenseInformationOption);
                OutputType outputType = context.ParseResult.GetValueForOption(outputTypeOption);
                bool returnErrorsOnly = context.ParseResult.GetValueForOption(returnErrorsOnlyOption);
                bool includeIgnoredPackages = context.ParseResult.GetValueForOption(includeIgnoredPackagesOption);
                string? excludedProjects = context.ParseResult.GetValueForOption(excludedProjectsOption);
                bool includeSharedProjects = context.ParseResult.GetValueForOption(includeSharedProjectsOption);
                string? targetFramework = context.ParseResult.GetValueForOption(targetFrameworkOption);
                string? destinationFile = context.ParseResult.GetValueForOption(destinationFileOption);
                string? licenseFileMappings = context.ParseResult.GetValueForOption(licenseFileMappingsOption);

                int exitCode = await ExecuteAsync(
                    inputFile,
                    inputJsonFile,
                    includeTransitive,
                    allowedLicenses,
                    ignoredPackages,
                    licenseMapping,
                    overridePackageInformation,
                    downloadLicenseInformation,
                    outputType,
                    returnErrorsOnly,
                    includeIgnoredPackages,
                    excludedProjects,
                    includeSharedProjects,
                    targetFramework,
                    destinationFile,
                    licenseFileMappings,
                    context.GetCancellationToken());

                context.ExitCode = exitCode;
            });

            return rootCommand;
        }

        private static string GetVersion() =>
            typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

        private static async Task<int> ExecuteAsync(
            string? inputFile,
            string? inputJsonFile,
            bool includeTransitive,
            string? allowedLicenses,
            string? ignoredPackages,
            string? licenseMapping,
            string? overridePackageInformation,
            string? downloadLicenseInformation,
            OutputType outputType,
            bool returnErrorsOnly,
            bool includeIgnoredPackages,
            string? excludedProjects,
            bool includeSharedProjects,
            string? targetFramework,
            string? destinationFile,
            string? licenseFileMappings,
            CancellationToken cancellationToken)
        {
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
        }

        private static Stream GetOutputStream(IFileSystem fileSystem, string? destinationFile)
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

        private static IFileDownloader GetFileDownloader(HttpClient httpClient, IFileSystem fileSystem, string? downloadLicenseInformation)
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

        private static CustomPackageInformation[] GetOverridePackageInformation(IFileSystem fileSystem, string? overridePackageInformation)
        {
            if (overridePackageInformation == null)
            {
                return Array.Empty<CustomPackageInformation>();
            }

            var serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new NuGetVersionJsonConverter());
            return JsonSerializer.Deserialize<CustomPackageInformation[]>(fileSystem.File.ReadAllText(overridePackageInformation), serializerOptions)!;
        }

        private static string[] GetAllowedLicenses(IFileSystem fileSystem, string? allowedLicenses)
        {
            if (allowedLicenses == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(allowedLicenses))!;
        }

        private static IImmutableDictionary<Uri, string> GetLicenseMappings(IFileSystem fileSystem, string? licenseMapping)
        {
            if (licenseMapping == null)
            {
                return UrlToLicenseMapping.Default;
            }

            Dictionary<Uri, string> userDictionary = JsonSerializer.Deserialize<Dictionary<Uri, string>>(fileSystem.File.ReadAllText(licenseMapping))!;

            return UrlToLicenseMapping.Default.SetItems(userDictionary);
        }

        private static string[] GetIgnoredPackages(IFileSystem fileSystem, string? ignoredPackages)
        {
            if (ignoredPackages == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(ignoredPackages))!;
        }

        private static string[] GetExcludedProjects(IFileSystem fileSystem, string? excludedProjects)
        {
            if (excludedProjects == null)
            {
                return Array.Empty<string>();
            }

            if (fileSystem.File.Exists(excludedProjects))
            {
                return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(excludedProjects))!;
            }

            return [excludedProjects];
        }

        private static string[] GetInputFiles(IFileSystem fileSystem, string? inputFile, string? inputJsonFile)
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

        private static IFileLicenseMatcher GetLicenseMatcher(IFileSystem fileSystem, string? licenseFileMappings)
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
