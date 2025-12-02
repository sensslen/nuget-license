// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using FileLicenseMatcher;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Output;
using NuGetLicense.Output.Json;
using NuGetLicense.Output.Table;
using NuGetUtility;
using NuGetUtility.Extension;
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
    [VersionOptionFromMember(MemberName = nameof(GetVersion))]
    public class Program
    {
        [Option(ShortName = "i",
            LongName = "input",
            Description = "The project (or solution) file for which to analyze dependency licenses")]
        public string? InputFile { get; } = null;

        [Option(ShortName = "ji",
            LongName = "json-input",
            Description =
                "File in json format that contains an array of all files to be evaluated. The Files can either point to a project or a solution.")]
        public string? InputJsonFile { get; } = null;

        [Option(LongName = "include-transitive",
            ShortName = "t",
            Description =
                "If set, the whole license tree is followed in order to determine all nuget's used by the projects")]
        public bool IncludeTransitive { get; } = false;

        [Option(LongName = "allowed-license-types",
            ShortName = "a",
            Description = "File in json format that contains an array of all allowed license types")]
        public string? AllowedLicenses { get; } = null;

        [Option(LongName = "ignored-packages",
            ShortName = "ignore",
            Description = "File in json format that contains an array of nuget package names to ignore (e.g. useful for nuget packages built in-house). Note that even though the packages are ignored, their transitive dependencies are not. Wildcard characters (*) are supported to specify ranges of ignored packages.")]
        public string? IgnoredPackages { get; } = null;

        [Option(LongName = "licenseurl-to-license-mappings",
            ShortName = "mapping",
            Description = "File in json format that contains a dictionary to map license urls to licenses.")]
        public string? LicenseMapping { get; } = null;

        [Option(LongName = "override-package-information",
            ShortName = "override",
            Description =
                "File in json format that contains a list of package and license information which should be used in favor of the online version. This option can be used to override the license type of packages that e.g. specify the license as file.")]
        public string? OverridePackageInformation { get; } = null;

        [Option(LongName = "license-information-download-location",
            ShortName = "d",
            Description =
                "When set, the application downloads all licenses given using a license URL to the specified folder.")]
        public string? DownloadLicenseInformation { get; } = null;

        [Option(LongName = "output",
            ShortName = "o",
            Description = "This parameter allows to choose between tabular and json output.")]
        public OutputType OutputType { get; } = OutputType.Table;

        [Option(LongName = "error-only",
            ShortName = "err",
            Description = "If this option is set and there are license validation errors, only the errors are returned as result. Otherwise all validation results are always returned.")]
        public bool ReturnErrorsOnly { get; } = false;

        [Option(LongName = "include-ignored-packages",
            ShortName = "include-ignored",
            Description = "If this option is set, the packages that are ignored from validation are still included in the output.")]
        public bool IncludeIgnoredPackages { get; } = false;

        [Option(LongName = "exclude-projects-matching",
            ShortName = "exclude-projects",
            Description = "This option allows to specify project name(s) to exclude from the analysis. This can be useful to exclude test projects from the analysis when supplying a solution file as input. Wildcard characters (*) are supported to specify ranges of ignored projects. The input can either be a file name containing a list of project names in json format or a plain string that is then used as a single entry.")]
        public string? ExcludedProjects { get; } = null;

        [Option(LongName = "include-shared-projects",
        ShortName = "isp",
        Description = "If set, shared projects (.shproj) will be included in the analysis. By default, shared projects are excluded.")]
        public bool IncludeSharedProjects { get; } = false;

        [Option(LongName = "target-framework",
            ShortName = "f",
            Description = "This option allows to select a Target framework moniker (https://learn.microsoft.com/en-us/dotnet/standard/frameworks) for which to analyze dependencies.")]
        public string? TargetFramework { get; } = null;

        [Option(LongName = "file-output",
            ShortName = "fo",
            Description = "The destination file to put the valiation output to. If omitted, the output is printed to the console.")]
        public string? DestinationFile { get; } = null;

        [Option(LongName = "licensefile-to-license-mappings",
            ShortName = "file-mapping",
            Description = "File in json format that contains a dictionary to map license files to licenses.")]
        public string? LicenseFileMappings { get; } = null;

        private static string GetVersion() =>
            typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

#pragma warning disable S1144 // Unused private types or members should be removed
        private async Task<int> OnExecuteAsync(CancellationToken cancellationToken)
#pragma warning restore S1144 // Unused private types or members should be removed
        {
            using var httpClient = new HttpClient();
            var fileSystem = new System.IO.Abstractions.FileSystem();
            string[] inputFiles = GetInputFiles(fileSystem);
            string[] ignoredPackages = GetIgnoredPackages(fileSystem);
            IImmutableDictionary<Uri, string> licenseMappings = GetLicenseMappings(fileSystem);
            string[] allowedLicenses = GetAllowedLicenses(fileSystem);
            CustomPackageInformation[] overridePackageInformation = GetOverridePackageInformation(fileSystem);
            IFileDownloader? licenseDownloader = GetFileDownloader(httpClient, fileSystem);
            IOutputFormatter output = GetOutputFormatter();

            var solutionPersistance = new SolutionPersistanceWrapper();
            var projectCollector = new ProjectsCollector(solutionPersistance);
            var msBuild = new MsBuildAbstraction();
            var projectReader = new ReferencedPackageReader(msBuild, new LockFileFactory(), GetPackagesConfigReader());
            var validator = new LicenseValidator.LicenseValidator(licenseMappings,
                allowedLicenses,
                licenseDownloader,
                GetLicenseMatcher(fileSystem),
                ignoredPackages);

            string[] excludedProjects = GetExcludedProjects(fileSystem);
            IEnumerable<string> projects = (await inputFiles.SelectManyAsync(projectCollector.GetProjectsAsync)).Where(p => !Array.Exists(excludedProjects, ignored => p.Like(ignored)));
            IEnumerable<ProjectWithReferencedPackages> packagesForProject = GetPackagesPerProject(projects, projectReader, out IReadOnlyCollection<Exception>? projectReaderExceptions);
            IAsyncEnumerable<ReferencedPackageWithContext> downloadedLicenseInformation =
                packagesForProject.SelectMany(p => GetPackageInformations(p, overridePackageInformation, cancellationToken));
            var results = (await validator.Validate(downloadedLicenseInformation, cancellationToken)).ToList();

            if (projectReaderExceptions.Count > 0)
            {
                await WriteValidationExceptions(projectReaderExceptions);

                return -1;
            }

            try
            {
                using Stream outputStream = GetOutputStream(fileSystem);
                await output.Write(outputStream, results.OrderBy(r => r.PackageId).ThenBy(r => r.PackageVersion).ToList());
                return results.Count(r => r.ValidationErrors.Any());
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync(e.ToString());
                return -1;
            }
        }

        private Stream GetOutputStream(IFileSystem fileSystem)
        {
            if (DestinationFile is null)
            {
                return Console.OpenStandardOutput();
            }
            return fileSystem.File.Open(fileSystem.Path.GetFullPath(DestinationFile), FileMode.Create, FileAccess.Write, FileShare.None);
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

        private IOutputFormatter GetOutputFormatter()
        {
            return OutputType switch
            {
                OutputType.Json => new JsonOutputFormatter(false, ReturnErrorsOnly, !IncludeIgnoredPackages),
                OutputType.JsonPretty => new JsonOutputFormatter(true, ReturnErrorsOnly, !IncludeIgnoredPackages),
                OutputType.Table => new TableOutputFormatter(ReturnErrorsOnly, !IncludeIgnoredPackages),
                OutputType.Markdown => new TableOutputFormatter(ReturnErrorsOnly, !IncludeIgnoredPackages, printMarkdown: true),
                _ => throw new ArgumentOutOfRangeException($"{OutputType} not supported")
            };
        }

        private IFileDownloader GetFileDownloader(HttpClient httpClient, IFileSystem fileSystem)
        {
            if (DownloadLicenseInformation == null)
            {
                return new NopFileDownloader();
            }

            if (!fileSystem.Directory.Exists(DownloadLicenseInformation))
            {
                fileSystem.Directory.CreateDirectory(DownloadLicenseInformation);
            }

            return new FileDownloader(httpClient, DownloadLicenseInformation);
        }

        private static async Task WriteValidationExceptions(IReadOnlyCollection<Exception> validationExceptions)
        {
            foreach (Exception exception in validationExceptions)
            {
                await Console.Error.WriteLineAsync(exception.ToString());
            }
        }

        private CustomPackageInformation[] GetOverridePackageInformation(IFileSystem fileSystem)
        {
            if (OverridePackageInformation == null)
            {
                return Array.Empty<CustomPackageInformation>();
            }

            var serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new NuGetVersionJsonConverter());
            return JsonSerializer.Deserialize<CustomPackageInformation[]>(fileSystem.File.ReadAllText(OverridePackageInformation), serializerOptions)!;
        }

        private string[] GetAllowedLicenses(IFileSystem fileSystem)
        {
            if (AllowedLicenses == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(AllowedLicenses))!;
        }

        private IImmutableDictionary<Uri, string> GetLicenseMappings(IFileSystem fileSystem)
        {
            if (LicenseMapping == null)
            {
                return UrlToLicenseMapping.Default;
            }

            Dictionary<Uri, string> userDictionary = JsonSerializer.Deserialize<Dictionary<Uri, string>>(fileSystem.File.ReadAllText(LicenseMapping))!;

            return UrlToLicenseMapping.Default.SetItems(userDictionary);
        }

        private string[] GetIgnoredPackages(IFileSystem fileSystem)
        {
            if (IgnoredPackages == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(IgnoredPackages))!;
        }

        private string[] GetExcludedProjects(IFileSystem fileSystem)
        {
            if (ExcludedProjects == null)
            {
                return Array.Empty<string>();
            }

            if (fileSystem.File.Exists(ExcludedProjects))
            {
                return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(ExcludedProjects))!;
            }

            return [ExcludedProjects];
        }

        private string[] GetInputFiles(IFileSystem fileSystem)
        {
            if (InputFile != null)
            {
                return [InputFile];
            }

            if (InputJsonFile != null)
            {
                return JsonSerializer.Deserialize<string[]>(fileSystem.File.ReadAllText(InputJsonFile))!;
            }

            throw new FileNotFoundException("Please provide an input file");
        }

        private IReadOnlyCollection<ProjectWithReferencedPackages> GetPackagesPerProject(IEnumerable<string> projects, ReferencedPackageReader reader, out IReadOnlyCollection<Exception> exceptions)
        {
            var encounteredExceptions = new List<Exception>();
            var result = new List<ProjectWithReferencedPackages>();
            exceptions = encounteredExceptions;

            ProjectFilter filter = new ProjectFilter();
            foreach (string project in filter.FilterProjects(projects, IncludeSharedProjects))
            {
                try
                {
                    IEnumerable<PackageIdentity> installedPackages = reader.GetInstalledPackages(project, IncludeTransitive, TargetFramework);
                    result.Add(new ProjectWithReferencedPackages(project, installedPackages));
                }
                catch (Exception e)
                {
                    encounteredExceptions.Add(e);
                }
            }

            return result;
        }

        private IFileLicenseMatcher GetLicenseMatcher(IFileSystem fileSystem)
        {
            var spdxLicemseMatcher = new FileLicenseMatcher.SPDX.FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses);
            if (LicenseFileMappings is null)
            {
                return spdxLicemseMatcher;
            }

            Dictionary<string, string> rawMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(fileSystem.File.ReadAllText(LicenseFileMappings))!;
            var fullPathMappings = rawMappings.ToDictionary(kvp => fileSystem.Path.GetFullPath(kvp.Key), kvp => kvp.Value);

            return new FileLicenseMatcher.Combine.LicenseMatcher([
                new FileLicenseMatcher.Compare.LicenseMatcher(fileSystem, fullPathMappings),
                spdxLicemseMatcher
            ]);
        }
    }
}
