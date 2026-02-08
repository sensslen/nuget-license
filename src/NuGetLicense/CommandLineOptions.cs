// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using McMaster.Extensions.CommandLineUtils;
using NuGetUtility;

namespace NuGetLicense
{
    /// <summary>
    /// Represents the parsed command line options for the nuget-license tool.
    /// </summary>
    [Command(Name = "nuget-license", Description = "A .net tool to print and validate the licenses of .net code. This tool supports .NET (Core), .NET Standard and .NET Framework projects.")]
    public class CommandLineOptions
    {
        [Option("-i|--input", Description = "The project (or solution) file for which to analyze dependency licenses")]
        public string? InputFile { get; set; }

        [Option("-ji|--json-input", Description = "File in json format that contains an array of all files to be evaluated. The Files can either point to a project or a solution.")]
        public string? InputJsonFile { get; set; }

        [Option("-t|--include-transitive", Description = "If set, the whole license tree is followed in order to determine all nuget's used by the projects")]
        public bool IncludeTransitive { get; set; }

        [Option("-a|--allowed-license-types", Description = "Specifies allowed license types. You can provide either a JSON file containing an array of license types, or a semicolon-separated list of license identifiers (e.g., \"MIT;Apache-2.0;BSD-3-Clause\").")]
        public string? AllowedLicenses { get; set; }

        [Option("-ignore|--ignored-packages", Description = "Specifies package names to ignore during validation. You can provide either a JSON file containing an array of package names, or a semicolon-separated list (e.g., \"Package1;Package2\"). Wildcards (*) are supported. Note that even though packages are ignored, their transitive dependencies are not.")]
        public string? IgnoredPackages { get; set; }

        [Option("-mapping|--licenseurl-to-license-mappings", Description = "Specifies a JSON file containing a dictionary to map license URLs to license types.")]
        public string? LicenseMapping { get; set; }

        [Option("-override|--override-package-information", Description = "Specifies a JSON file containing package and license information to use instead of the online version. This option can be used to override the license type of packages that specify the license as a file.")]
        public string? OverridePackageInformation { get; set; }

        [Option("-d|--license-information-download-location", Description = "Specifies a folder where the application will download all licenses provided via license URLs.")]
        public string? DownloadLicenseInformation { get; set; }

        [Option("-o|--output", Description = "Specifies the output format. Valid values are Table, Markdown, Json, or JsonPretty (default: Table).")]
        public OutputType OutputType { get; set; } = OutputType.Table;

        [Option("-err|--error-only", Description = "When set, only validation errors are returned as result. Otherwise, all validation results are always returned.")]
        public bool ReturnErrorsOnly { get; set; }

        [Option("-include-ignored|--include-ignored-packages", Description = "When set, packages that are ignored from validation are still included in the output.")]
        public bool IncludeIgnoredPackages { get; set; }

        [Option("-exclude-projects|--exclude-projects-matching", Description = "Specifies project names to exclude from the analysis. You can provide either a JSON file containing an array of project names, or a semicolon-separated list (e.g., \"*Test*;Legacy*\"). Wildcards (*) are supported. This is useful to exclude test projects when supplying a solution file as input.")]
        public string? ExcludedProjects { get; set; }

        [Option("-isp|--include-shared-projects", Description = "If set, shared projects (.shproj) will be included in the analysis. By default, shared projects are excluded.")]
        public bool IncludeSharedProjects { get; set; }

        [Option("-f|--target-framework", Description = "This option allows to select a Target framework moniker (https://learn.microsoft.com/en-us/dotnet/standard/frameworks) for which to analyze dependencies.")]
        public string? TargetFramework { get; set; }

        [Option("-fo|--file-output", Description = "The destination file to put the validation output to. If omitted, the output is printed to the console.")]
        public string? DestinationFile { get; set; }

        [Option("-file-mapping|--licensefile-to-license-mappings", Description = "File in json format that contains a dictionary to map license files to licenses.")]
        public string? LicenseFileMappings { get; set; }
    }
}
