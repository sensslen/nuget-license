// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using FileLicenseMatcher;
using NuGetLicense.Output;
using NuGetUtility;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.HttpClientWrapper;

namespace NuGetLicense
{
    /// <summary>
    /// Parses and transforms command line options into the format required by the license validation orchestrator.
    /// </summary>
    public interface ICommandLineOptionsParser
    {
        /// <summary>
        /// Gets the input files from the command line options.
        /// </summary>
        string[] GetInputFiles(string? inputFile, string? inputJsonFile);

        /// <summary>
        /// Gets the allowed licenses from the command line options.
        /// </summary>
        string[] GetAllowedLicenses(string? allowedLicenses);

        /// <summary>
        /// Gets the ignored packages from the command line options.
        /// </summary>
        string[] GetIgnoredPackages(string? ignoredPackages);

        /// <summary>
        /// Gets the excluded projects from the command line options.
        /// </summary>
        string[] GetExcludedProjects(string? excludedProjects);

        /// <summary>
        /// Gets the license mappings from the command line options.
        /// </summary>
        IImmutableDictionary<Uri, string> GetLicenseMappings(string? licenseMapping);

        /// <summary>
        /// Gets the override package information from the command line options.
        /// </summary>
        CustomPackageInformation[] GetOverridePackageInformation(string? overridePackageInformation);

        /// <summary>
        /// Gets the license file matcher from the command line options.
        /// </summary>
        IFileLicenseMatcher GetLicenseMatcher(string? licenseFileMappings);

        /// <summary>
        /// Gets the file downloader from the command line options.
        /// </summary>
        IFileDownloader GetFileDownloader(string? downloadLicenseInformation);

        /// <summary>
        /// Gets the output formatter from the command line options.
        /// </summary>
        IOutputFormatter GetOutputFormatter(OutputType outputType, bool returnErrorsOnly, bool includeIgnoredPackages);
    }
}
