// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGetUtility.Extension;
using NuGetUtility.Extensions;
using NuGetUtility.LicenseValidator;
using NuGetUtility.Output;
using NuGetUtility.Output.Json;
using NuGetUtility.Output.Table;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Serialization;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;

namespace NuGetUtility
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

        [Option(LongName = "target-framework",
            ShortName = "f",
            Description = "This option allows to select a Target framework moniker (https://learn.microsoft.com/en-us/dotnet/standard/frameworks) for which to analyze dependencies.")]
        public string? TargetFramework { get; } = null;

        [Option(LongName = "file-output",
            ShortName = "fo",
            Description = "The destination file to put the valiation output to. If omitted, the output is printed to the console.")]
        public string? DestinationFile { get; } = null;

        private static string GetVersion()
            => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

#pragma warning disable S1144 // Unused private types or members should be removed
        private async Task<int> OnExecuteAsync(CancellationToken cancellationToken)
#pragma warning restore S1144 // Unused private types or members should be removed
        {
            using var httpClient = new HttpClient();
            string[] inputFiles = GetInputFiles();
            string[] ignoredPackages = GetIgnoredPackages();
            IImmutableDictionary<Uri, string> licenseMappings = GetLicenseMappings();
            string[] allowedLicenses = GetAllowedLicenses();
            CustomPackageInformation[] overridePackageInformation = GetOverridePackageInformation();
            IFileDownloader urlLicenseFileDownloader = GetFileDownloader(httpClient);
            IOutputFormatter output = GetOutputFormatter();

            MsBuildAbstraction msBuild = new MsBuildAbstraction();
            var projectCollector = new ProjectsCollector(msBuild);
            var projectReader = new ReferencedPackageReader(msBuild, new LockFileFactory(), GetPackagesConfigReader());
            var validator = new LicenseValidator.LicenseValidator(licenseMappings,
                allowedLicenses,
                urlLicenseFileDownloader,
                ignoredPackages);

            string[] excludedProjects = GetExcludedProjects();
            IEnumerable<string> projects = inputFiles.SelectMany(projectCollector.GetProjects).Where(p => !Array.Exists(excludedProjects, ignored => p.Like(ignored)));
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
                using Stream outputStream = GetOutputStream();
                await output.Write(outputStream, results.OrderBy(r => r.PackageId).ThenBy(r => r.PackageVersion).ToList());
                return results.Count(r => r.ValidationErrors.Any());
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync(e.ToString());
                return -1;
            }
        }

        private Stream GetOutputStream()
        {
            if (DestinationFile is null)
            {
                return Console.OpenStandardOutput();
            }
            return File.Open(Path.GetFullPath(DestinationFile), FileMode.Create, FileAccess.Write, FileShare.None);
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
            var informationReader = new PackageInformationReader.PackageInformationReader(sourceRepositoryProvider, globalPackagesFolderUtility, overridePackageInformation);

            return informationReader.GetPackageInfo(new ProjectWithReferencedPackages(projectWithReferences.Project, projectWithReferences.ReferencedPackages), cancellation);
        }

        private IOutputFormatter GetOutputFormatter()
        {
            return OutputType switch
            {
                OutputType.Json => new JsonOutputFormatter(false, ReturnErrorsOnly, !IncludeIgnoredPackages),
                OutputType.JsonPretty => new JsonOutputFormatter(true, ReturnErrorsOnly, !IncludeIgnoredPackages),
                OutputType.Table => new TableOutputFormatter(ReturnErrorsOnly, !IncludeIgnoredPackages),
                _ => throw new ArgumentOutOfRangeException($"{OutputType} not supported")
            };
        }

        private IFileDownloader GetFileDownloader(HttpClient httpClient)
        {
            if (DownloadLicenseInformation == null)
            {
                return new NopFileDownloader();
            }

            if (!Directory.Exists(DownloadLicenseInformation))
            {
                Directory.CreateDirectory(DownloadLicenseInformation);
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

        private CustomPackageInformation[] GetOverridePackageInformation()
        {
            if (OverridePackageInformation == null)
            {
                return Array.Empty<CustomPackageInformation>();
            }

            var serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new NuGetVersionJsonConverter());
            return JsonSerializer.Deserialize<CustomPackageInformation[]>(File.ReadAllText(OverridePackageInformation), serializerOptions)!;
        }

        private string[] GetAllowedLicenses()
        {
            if (AllowedLicenses == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(File.ReadAllText(AllowedLicenses))!;
        }

        private IImmutableDictionary<Uri, string> GetLicenseMappings()
        {
            if (LicenseMapping == null)
            {
                return UrlToLicenseMapping.Default;
            }

            var serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new UriDictionaryJsonConverter<string>());
            Dictionary<Uri, string> userDictionary = JsonSerializer.Deserialize<Dictionary<Uri, string>>(File.ReadAllText(LicenseMapping),
                serializerOptions)!;

            return UrlToLicenseMapping.Default.SetItems(userDictionary);
        }

        private string[] GetIgnoredPackages()
        {
            if (IgnoredPackages == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(File.ReadAllText(IgnoredPackages))!;
        }

        private string[] GetExcludedProjects()
        {
            if (ExcludedProjects == null)
            {
                return Array.Empty<string>();
            }

            if (File.Exists(ExcludedProjects))
            {
                return JsonSerializer.Deserialize<string[]>(File.ReadAllText(ExcludedProjects))!;
            }

            return new[] { ExcludedProjects };
        }

        private string[] GetInputFiles()
        {
            if (InputFile != null)
            {
                return new[] { InputFile };
            }

            if (InputJsonFile != null)
            {
                return JsonSerializer.Deserialize<string[]>(File.ReadAllText(InputJsonFile))!;
            }

            throw new FileNotFoundException("Please provide an input file");
        }

        private IReadOnlyCollection<ProjectWithReferencedPackages> GetPackagesPerProject(IEnumerable<string> projects, ReferencedPackageReader reader, out IReadOnlyCollection<Exception> exceptions)
        {
            var encounteredExceptions = new List<Exception>();
            var result = new List<ProjectWithReferencedPackages>();
            exceptions = encounteredExceptions;
            foreach (string project in projects)
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
    }
}
