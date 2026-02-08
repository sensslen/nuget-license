// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility;

namespace NuGetLicense
{
    /// <summary>
    /// Represents the parsed command line options for the nuget-license tool.
    /// </summary>
    public class CommandLineOptions
    {
        public string? InputFile { get; set; }
        public string? InputJsonFile { get; set; }
        public bool IncludeTransitive { get; set; }
        public bool SkipInvalidProjects { get; set; }
        public string? AllowedLicenses { get; set; }
        public string? IgnoredPackages { get; set; }
        public string? LicenseMapping { get; set; }
        public string? OverridePackageInformation { get; set; }
        public string? DownloadLicenseInformation { get; set; }
        public OutputType OutputType { get; set; }
        public bool ReturnErrorsOnly { get; set; }
        public bool IncludeIgnoredPackages { get; set; }
        public string? ExcludedProjects { get; set; }
        public bool IncludeSharedProjects { get; set; }
        public string? TargetFramework { get; set; }
        public string? DestinationFile { get; set; }
        public string? LicenseFileMappings { get; set; }
    }
}
