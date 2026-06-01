// Licensed to the project contributors.
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
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;
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
    public class LicenseValidationOrchestrator(IFileSystem fileSystem,
                                               ISolutionPersistenceWrapper solutionPersistence,
                                               IMsBuildAbstraction msBuild,
                                               IPackagesConfigReader packagesConfigReader,
                                               ICommandLineOptionsParser optionsParser,
                                               Stream outputStream,
                                               Stream errorStream)
        : ILicenseValidationOrchestrator
    {
        public async Task<int> ValidateAsync(ICommandLineOptions options, CancellationToken cancellationToken = default)
        {
            string[] inputFiles = optionsParser.GetInputFiles(options.InputFile, options.InputJsonFile);
            string[] ignoredPackagesArray = optionsParser.GetIgnoredPackages(options.IgnoredPackages);
            IImmutableDictionary<Uri, string> licenseMappings = optionsParser.GetLicenseMappings(options.LicenseMapping);
            string[] allowedLicensesArray = optionsParser.GetAllowedLicenses(options.AllowedLicenses);
            CustomPackageInformation[] overridePackageInformationArray = optionsParser.GetOverridePackageInformation(options.OverridePackageInformation);
            IFileDownloader licenseDownloader = optionsParser.GetFileDownloader(options.DownloadLicenseInformation);
            IOutputFormatter output = optionsParser.GetOutputFormatter(options.OutputType, options.ReturnErrorsOnly, options.IncludeIgnoredPackages);

            var projectCollector = new ProjectsCollector(solutionPersistence, fileSystem);
            var projectReader = new ReferencedPackageReader(msBuild,
                                                            new LockFileFactory(),
                                                            new NuGetFrameworkUtility(),
                                                            new AssetsPackageDependencyReader(new NuGetFrameworkUtility()),
                                                            packagesConfigReader);
            var validator = new LicenseValidator.LicenseValidator(licenseMappings,
                                                                  allowedLicensesArray,
                                                                  licenseDownloader,
                                                                  optionsParser.GetLicenseMatcher(options.LicenseFileMappings),
                                                                  ignoredPackagesArray);

            string[] excludedProjectsArray = optionsParser.GetExcludedProjects(options.ExcludedProjects);
            IEnumerable<string> projects = (await inputFiles.SelectManyAsync(projectCollector.GetProjectsAsync)).Where(p => !Array.Exists(excludedProjectsArray, p.PathLike));
            var packageReaderOptions = new PackageReaderOptions
            {
                IncludeTransitive = options.IncludeTransitive,
                TargetFramework = options.TargetFramework,
                ExcludePublishFalse = options.ExcludePublishFalse,
                ExcludePrivateAssets = options.ExcludePrivateAssets,
                IncludeSharedProjects = options.IncludeSharedProjects
            };
            IEnumerable<ProjectWithReferencedPackages> packagesForProject = GetPackagesPerProject(projects,
                                                                                                  projectReader,
                                                                                                  packageReaderOptions,
                                                                                                  out IReadOnlyCollection<Exception> projectReaderExceptions);
            IAsyncEnumerable<ReferencedPackageWithContext> downloadedLicenseInformation =
                packagesForProject.SelectMany(p => GetPackageInformation(p, overridePackageInformationArray, cancellationToken));
            var results = (await validator.Validate(downloadedLicenseInformation, cancellationToken)).ToList();

            if (projectReaderExceptions.Count > 0)
            {
                await WriteValidationExceptions(projectReaderExceptions);

                return -1;
            }

            try
            {
                Stream os = GetOutputStream(options.DestinationFile);
                bool shouldDisposeStream = options.DestinationFile != null;

                try
                {
                    await output.Write(os, results.OrderBy(r => r.PackageId).ThenBy(r => r.PackageVersion).ToList());
                    return results.Count(r => r.ValidationErrors.Any());
                }
                finally
                {
                    if (shouldDisposeStream)
                    {
#if NETFRAMEWORK
                        os.Dispose();
#else
                        await os.DisposeAsync();
#endif
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
                return outputStream;
            }

            string fullPath = fileSystem.Path.GetFullPath(destinationFile);
            string? directoryName = fileSystem.Path.GetDirectoryName(fullPath);
            if (directoryName != null && !fileSystem.Directory.Exists(directoryName))
            {
                fileSystem.Directory.CreateDirectory(directoryName);
            }

            return fileSystem.File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
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
            await errorStream.WriteAsync(messageBytes, 0, messageBytes.Length);
            await errorStream.FlushAsync();
        }

        private static IReadOnlyCollection<ProjectWithReferencedPackages> GetPackagesPerProject(IEnumerable<string> projects,
                                                                                                ReferencedPackageReader reader,
                                                                                                PackageReaderOptions options,
                                                                                                out IReadOnlyCollection<Exception> exceptions)
        {
            var encounteredExceptions = new List<Exception>();
            var result = new List<ProjectWithReferencedPackages>();
            exceptions = encounteredExceptions;

            ProjectFilter filter = new();
            foreach (string project in filter.FilterProjects(projects, options.IncludeSharedProjects))
            {
                try
                {
                    IEnumerable<PackageIdentity> installedPackages = reader.GetInstalledPackages(project, options.IncludeTransitive, options.TargetFramework, options.ExcludePublishFalse, options.ExcludePrivateAssets);
                    result.Add(new ProjectWithReferencedPackages(project, installedPackages));
                }
                catch (Exception e)
                {
                    encounteredExceptions.Add(e);
                }
            }

            return result;
        }

        private static async IAsyncEnumerable<ReferencedPackageWithContext> GetPackageInformation(ProjectWithReferencedPackages projectWithReferences,
                                                                                                  IEnumerable<CustomPackageInformation> overridePackageInformation,
                                                                                                  [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
        {
            ISettings settings = Settings.LoadDefaultSettings(projectWithReferences.Project);
            var sourceProvider = new PackageSourceProvider(settings);

            using var sourceRepositoryProvider = new WrappedSourceRepositoryProvider(new SourceRepositoryProvider(sourceProvider, Repository.Provider.GetCoreV3()));
            var globalPackagesFolderUtility = new GlobalPackagesFolderUtility(settings);
            var informationReader = new PackageInformationReader(sourceRepositoryProvider, globalPackagesFolderUtility, overridePackageInformation);

            await foreach (ReferencedPackageWithContext package in informationReader.GetPackageInfo(new ProjectWithReferencedPackages(projectWithReferences.Project,
                                                                                                                                      projectWithReferences.ReferencedPackages),
                                                                                                    cancellation))
            {
                yield return package;
            }
        }
    }
}
