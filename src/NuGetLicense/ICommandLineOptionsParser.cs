// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using FileLicenseMatcher;
using NuGetLicense.Output;
using NuGetUtility;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.HttpClientWrapper;

namespace NuGetLicense
{
    public interface ICommandLineOptionsParser
    {
        string[] GetInputFiles(string? inputFile, string? inputJsonFile);

        string[] GetAllowedLicenses(string? allowedLicenses);

        string[] GetIgnoredPackages(string? ignoredPackages);

        string[] GetExcludedProjects(string? excludedProjects);

        IImmutableDictionary<Uri, string> GetLicenseMappings(string? licenseMapping);

        CustomPackageInformation[] GetOverridePackageInformation(string? overridePackageInformation);

        IFileLicenseMatcher GetLicenseMatcher(string? licenseFileMappings);

        IFileDownloader GetFileDownloader(string? downloadLicenseInformation);

        IOutputFormatter GetOutputFormatter(OutputType outputType, bool returnErrorsOnly, bool includeIgnoredPackages);
    }
}
