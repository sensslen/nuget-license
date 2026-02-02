// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Text.Json;
using FileLicenseMatcher;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Output;
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
    public class LicenseValidationHandler
    {
        private readonly IFileSystem _fileSystem;
        private readonly HttpClient _httpClient;
        private readonly ISolutionPersistanceWrapper _solutionPersistance;
        private readonly IMsBuildAbstraction _msBuild;
        private readonly IPackagesConfigReader _packagesConfigReader;
        private readonly Stream _outputStream;
        private readonly Stream _errorStream;

        public LicenseValidationHandler(
            IFileSystem fileSystem,
            HttpClient httpClient,
            ISolutionPersistanceWrapper solutionPersistance,
            IMsBuildAbstraction msBuild,
            IPackagesConfigReader packagesConfigReader,
            Stream outputStream,
            Stream errorStream)
        {
            _fileSystem = fileSystem;
            _httpClient = httpClient;
            _solutionPersistance = solutionPersistance;
            _msBuild = msBuild;
            _packagesConfigReader = packagesConfigReader;
            _outputStream = outputStream;
            _errorStream = errorStream;
        }

        public async Task<int> HandleAsync(CommandLineOptions options, CancellationToken cancellationToken = default)
        {
            string[] inputFiles = GetInputFiles(options.InputFile, options.InputJsonFile);
            string[] ignoredPackagesArray = GetIgnoredPackages(options.IgnoredPackages);
            IImmutableDictionary<Uri, string> licenseMappings = GetLicenseMappings(options.LicenseMapping);
            string[] allowedLicensesArray = GetAllowedLicenses(options.AllowedLicenses);
            CustomPackageInformation[] overridePackageInformationArray = GetOverridePackageInformation(options.OverridePackageInformation);
            IFileDownloader? licenseDownloader = GetFileDownloader(options.DownloadLicenseInformation);
            IOutputFormatter output = LicenseValidationHandler.GetOutputFormatter(options.OutputType, options.ReturnErrorsOnly, options.IncludeIgnoredPackages);

            var projectCollector = new ProjectsCollector(_solutionPersistance);
            var projectReader = new ReferencedPackageReader(_msBuild, new LockFileFactory(), _packagesConfigReader);
            var validator = new LicenseValidator.LicenseValidator(licenseMappings,
                allowedLicensesArray,
                licenseDownloader,
                GetLicenseMatcher(options.LicenseFileMappings),
                ignoredPackagesArray);

            string[] excludedProjectsArray = GetExcludedProjects(options.ExcludedProjects);
            IEnumerable<string> projects = (await inputFiles.SelectManyAsync(projectCollector.GetProjectsAsync)).Where(p => !Array.Exists(excludedProjectsArray, ignored => p.Like(ignored)));
            IEnumerable<ProjectWithReferencedPackages> packagesForProject = LicenseValidationHandler.GetPackagesPerProject(projects, projectReader, options.IncludeTransitive, options.TargetFramework, options.IncludeSharedProjects, out IReadOnlyCollection<Exception>? projectReaderExceptions);
            IAsyncEnumerable<ReferencedPackageWithContext> downloadedLicenseInformation =
                packagesForProject.SelectMany(p => LicenseValidationHandler.GetPackageInformations(p, overridePackageInformationArray, cancellationToken));
            var results = (await validator.Validate(downloadedLicenseInformation, cancellationToken)).ToList();

            if (projectReaderExceptions.Count > 0)
            {
                await WriteValidationExceptions(projectReaderExceptions);

                return -1;
            }

            try
            {
                Stream outputStream = GetOutputStream(options.DestinationFile);
                bool shouldDisposeStream = options.DestinationFile != null;

                try
                {
                    await output.Write(outputStream, results.OrderBy(r => r.PackageId).ThenBy(r => r.PackageVersion).ToList());
                    return results.Count(r => r.ValidationErrors.Any());
                }
                finally
                {
                    if (shouldDisposeStream)
                    {
                        outputStream.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                await WriteToErrorStreamAsync(e.ToString());
                return -1;
            }
        }

        private Stream GetOutputStream(string? destinationFile)
        {
            if (destinationFile is null)
            {
                return _outputStream;
            }
            return _fileSystem.File.Open(_fileSystem.Path.GetFullPath(destinationFile), FileMode.Create, FileAccess.Write, FileShare.None);
        }

        private async Task WriteValidationExceptions(IReadOnlyCollection<Exception> validationExceptions)
        {
            foreach (Exception exception in validationExceptions)
            {
                await WriteToErrorStreamAsync(exception.ToString());
            }
        }

        private async Task WriteToErrorStreamAsync(string message)
        {
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message + Environment.NewLine);
            await _errorStream.WriteAsync(messageBytes, 0, messageBytes.Length);
            await _errorStream.FlushAsync();
        }

        private CustomPackageInformation[] GetOverridePackageInformation(string? overridePackageInformation)
        {
            if (overridePackageInformation == null)
            {
                return Array.Empty<CustomPackageInformation>();
            }

            var serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new NuGetVersionJsonConverter());
            return JsonSerializer.Deserialize<CustomPackageInformation[]>(_fileSystem.File.ReadAllText(overridePackageInformation), serializerOptions)!;
        }

        private string[] GetAllowedLicenses(string? allowedLicenses)
        {
            if (allowedLicenses == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(_fileSystem.File.ReadAllText(allowedLicenses))!;
        }

        private IImmutableDictionary<Uri, string> GetLicenseMappings(string? licenseMapping)
        {
            if (licenseMapping == null)
            {
                return UrlToLicenseMapping.Default;
            }

            Dictionary<Uri, string> userDictionary = JsonSerializer.Deserialize<Dictionary<Uri, string>>(_fileSystem.File.ReadAllText(licenseMapping))!;

            return UrlToLicenseMapping.Default.SetItems(userDictionary);
        }

        private string[] GetIgnoredPackages(string? ignoredPackages)
        {
            if (ignoredPackages == null)
            {
                return Array.Empty<string>();
            }

            return JsonSerializer.Deserialize<string[]>(_fileSystem.File.ReadAllText(ignoredPackages))!;
        }

        private string[] GetExcludedProjects(string? excludedProjects)
        {
            if (excludedProjects == null)
            {
                return Array.Empty<string>();
            }

            if (_fileSystem.File.Exists(excludedProjects))
            {
                return JsonSerializer.Deserialize<string[]>(_fileSystem.File.ReadAllText(excludedProjects))!;
            }

            return [excludedProjects];
        }

        private string[] GetInputFiles(string? inputFile, string? inputJsonFile)
        {
            if (inputFile != null)
            {
                return [inputFile];
            }

            if (inputJsonFile != null)
            {
                return JsonSerializer.Deserialize<string[]>(_fileSystem.File.ReadAllText(inputJsonFile))!;
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

        private IFileLicenseMatcher GetLicenseMatcher(string? licenseFileMappings)
        {
            var spdxLicemseMatcher = new FileLicenseMatcher.SPDX.FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses);
            if (licenseFileMappings is null)
            {
                return spdxLicemseMatcher;
            }

            string containingDirectory = _fileSystem.Path.GetDirectoryName(_fileSystem.Path.GetFullPath(licenseFileMappings))!;
            Dictionary<string, string> rawMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(_fileSystem.File.ReadAllText(licenseFileMappings))!;
            var fullPathMappings = rawMappings.ToDictionary(kvp => _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(containingDirectory, kvp.Key)), kvp => kvp.Value);

            return new FileLicenseMatcher.Combine.LicenseMatcher([
                new FileLicenseMatcher.Compare.LicenseMatcher(_fileSystem, fullPathMappings),
                spdxLicemseMatcher
            ]);
        }

        private static async IAsyncEnumerable<ReferencedPackageWithContext> GetPackageInformations(
            ProjectWithReferencedPackages projectWithReferences,
            IEnumerable<CustomPackageInformation> overridePackageInformation,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
        {
            ISettings settings = Settings.LoadDefaultSettings(projectWithReferences.Project);
            var sourceProvider = new PackageSourceProvider(settings);

            using var sourceRepositoryProvider = new WrappedSourceRepositoryProvider(new SourceRepositoryProvider(sourceProvider, Repository.Provider.GetCoreV3()));
            var globalPackagesFolderUtility = new GlobalPackagesFolderUtility(settings);
            var informationReader = new PackageInformationReader(sourceRepositoryProvider, globalPackagesFolderUtility, overridePackageInformation);

            await foreach (ReferencedPackageWithContext package in informationReader.GetPackageInfo(new ProjectWithReferencedPackages(projectWithReferences.Project, projectWithReferences.ReferencedPackages), cancellation))
            {
                yield return package;
            }
        }

        private static IOutputFormatter GetOutputFormatter(OutputType outputType, bool returnErrorsOnly, bool includeIgnoredPackages)
        {
            return outputType switch
            {
                OutputType.Json => new Output.Json.JsonOutputFormatter(false, returnErrorsOnly, !includeIgnoredPackages),
                OutputType.JsonPretty => new Output.Json.JsonOutputFormatter(true, returnErrorsOnly, !includeIgnoredPackages),
                OutputType.Table => new Output.Table.TableOutputFormatter(returnErrorsOnly, !includeIgnoredPackages),
                OutputType.Markdown => new Output.Table.TableOutputFormatter(returnErrorsOnly, !includeIgnoredPackages, printMarkdown: true),
                _ => throw new ArgumentOutOfRangeException($"{outputType} not supported")
            };
        }

        private IFileDownloader GetFileDownloader(string? downloadLicenseInformation)
        {
            if (downloadLicenseInformation == null)
            {
                return new NopFileDownloader();
            }

            if (!_fileSystem.Directory.Exists(downloadLicenseInformation))
            {
                _fileSystem.Directory.CreateDirectory(downloadLicenseInformation);
            }

            return new FileDownloader(_httpClient, downloadLicenseInformation);
        }
    }
}
