// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using System.IO.Abstractions;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGetLicense.Output;
using NuGetUtility.Extensions;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.ProjectFiltering;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol;
using NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

namespace NuGetLicense
{
    /// <summary>
    /// Orchestrates the license validation process.
    /// </summary>
    public class LicenseValidationOrchestrator : ILicenseValidationOrchestrator
    {
        private readonly IFileSystem _fileSystem;
        private readonly ISolutionPersistanceWrapper _solutionPersistance;
        private readonly IMsBuildAbstraction _msBuild;
        private readonly IPackagesConfigReader _packagesConfigReader;
        private readonly ICommandLineOptionsParser _optionsParser;
        private readonly Stream _outputStream;
        private readonly Stream _errorStream;

        public LicenseValidationOrchestrator(
            IFileSystem fileSystem,
            ISolutionPersistanceWrapper solutionPersistance,
            IMsBuildAbstraction msBuild,
            IPackagesConfigReader packagesConfigReader,
            ICommandLineOptionsParser optionsParser,
            Stream outputStream,
            Stream errorStream)
        {
            _fileSystem = fileSystem;
            _solutionPersistance = solutionPersistance;
            _msBuild = msBuild;
            _packagesConfigReader = packagesConfigReader;
            _optionsParser = optionsParser;
            _outputStream = outputStream;
            _errorStream = errorStream;
        }

        public async Task<int> ValidateAsync(ICommandLineOptions options, CancellationToken cancellationToken = default)
        {
            string[] inputFiles = _optionsParser.GetInputFiles(options.InputFile, options.InputJsonFile);
            string[] ignoredPackagesArray = _optionsParser.GetIgnoredPackages(options.IgnoredPackages);
            IImmutableDictionary<Uri, string> licenseMappings = _optionsParser.GetLicenseMappings(options.LicenseMapping);
            string[] allowedLicensesArray = _optionsParser.GetAllowedLicenses(options.AllowedLicenses);
            CustomPackageInformation[] overridePackageInformationArray = _optionsParser.GetOverridePackageInformation(options.OverridePackageInformation);
            IFileDownloader licenseDownloader = _optionsParser.GetFileDownloader(options.DownloadLicenseInformation);
            IOutputFormatter output = _optionsParser.GetOutputFormatter(options.OutputType, options.ReturnErrorsOnly, options.IncludeIgnoredPackages);

            var projectCollector = new ProjectsCollector(_solutionPersistance, _fileSystem);
            var projectReader = new ReferencedPackageReader(_msBuild, new LockFileFactory(), _packagesConfigReader);
            var validator = new LicenseValidator.LicenseValidator(licenseMappings,
                allowedLicensesArray,
                licenseDownloader,
                _optionsParser.GetLicenseMatcher(options.LicenseFileMappings),
                ignoredPackagesArray);

            string[] excludedProjectsArray = _optionsParser.GetExcludedProjects(options.ExcludedProjects);
            IEnumerable<string> projects = (await inputFiles.SelectManyAsync(projectCollector.GetProjectsAsync)).Where(p => !Array.Exists(excludedProjectsArray, ignored => p.Like(ignored)));
            IEnumerable<ProjectWithReferencedPackages> packagesForProject = GetPackagesPerProject(projects, projectReader, options.IncludeTransitive, options.TargetFramework, options.IncludeSharedProjects, out IReadOnlyCollection<Exception> projectReaderExceptions);
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
            return destinationFile is null
                ? _outputStream
                : _fileSystem.File.Open(_fileSystem.Path.GetFullPath(destinationFile), FileMode.Create, FileAccess.Write, FileShare.None);
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
    }
}
