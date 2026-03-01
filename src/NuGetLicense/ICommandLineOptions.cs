// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility;

namespace NuGetLicense
{
    public interface ICommandLineOptions
    {
        string? InputFile { get; }
        string? InputJsonFile { get; }
        bool IncludeTransitive { get; }
        string? AllowedLicenses { get; }
        string? IgnoredPackages { get; }
        string? LicenseMapping { get; }
        string? OverridePackageInformation { get; }
        string? DownloadLicenseInformation { get; }
        OutputType OutputType { get; }
        public bool ReturnErrorsOnly { get; }
        public bool IncludeIgnoredPackages { get; }
        public string? ExcludedProjects { get; }
        public bool IncludeSharedProjects { get; }
        public string? TargetFramework { get; }
        public string? DestinationFile { get; }
        public string? LicenseFileMappings { get; }
        public bool ExcludePublishFalse { get; }
    }
}
