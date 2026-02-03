// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.CommandLine;
using NuGetUtility;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

#if !NET
using System.Net.Http;
#endif

namespace NuGetLicense
{
    public static class Program
    {
        public static RootCommand CreateRootCommand()
        {
            var inputFileOption = new Option<string?>("-i", "--input")
            {
                Description = "The project (or solution) file for which to analyze dependency licenses"
            };

            var inputJsonFileOption = new Option<string?>("-ji", "--json-input")
            {
                Description = "File in json format that contains an array of all files to be evaluated. The Files can either point to a project or a solution."
            };

            var includeTransitiveOption = new Option<bool>("-t", "--include-transitive")
            {
                Description = "If set, the whole license tree is followed in order to determine all nuget's used by the projects"
            };

            var allowedLicensesOption = new Option<string?>("-a", "--allowed-license-types")
            {
                Description = "Specifies allowed license types. You can provide either a JSON file containing an array of license types, or a semicolon-separated list of license identifiers (e.g., \"MIT;Apache-2.0;BSD-3-Clause\")."
            };

            var ignoredPackagesOption = new Option<string?>("-ignore", "--ignored-packages")
            {
                Description = "Specifies package names to ignore during validation. You can provide either a JSON file containing an array of package names, or a semicolon-separated list (e.g., \"Package1;Package2\"). Wildcards (*) are supported. Note that even though packages are ignored, their transitive dependencies are not."
            };

            var licenseMappingOption = new Option<string?>("-mapping", "--licenseurl-to-license-mappings")
            {
                Description = "Specifies a JSON file containing a dictionary to map license URLs to license types."
            };

            var overridePackageInformationOption = new Option<string?>("-override", "--override-package-information")
            {
                Description = "Specifies a JSON file containing package and license information to use instead of the online version. This option can be used to override the license type of packages that specify the license as a file."
            };

            var downloadLicenseInformationOption = new Option<string?>("-d", "--license-information-download-location")
            {
                Description = "Specifies a folder where the application will download all licenses provided via license URLs."
            };

            var outputTypeOption = new Option<OutputType>("-o", "--output")
            {
                Description = "Specifies the output format. Valid values are Table, Markdown, Json, or JsonPretty (default: Table).",
                DefaultValueFactory = _ => OutputType.Table
            };

            var returnErrorsOnlyOption = new Option<bool>("-err", "--error-only")
            {
                Description = "When set, only validation errors are returned as result. Otherwise, all validation results are always returned."
            };

            var includeIgnoredPackagesOption = new Option<bool>("-include-ignored", "--include-ignored-packages")
            {
                Description = "When set, packages that are ignored from validation are still included in the output."
            };

            var excludedProjectsOption = new Option<string?>("-exclude-projects", "--exclude-projects-matching")
            {
                Description = "Specifies project names to exclude from the analysis. You can provide either a JSON file containing an array of project names, or a semicolon-separated list (e.g., \"*Test*;Legacy*\"). Wildcards (*) are supported. This is useful to exclude test projects when supplying a solution file as input."
            };

            var includeSharedProjectsOption = new Option<bool>("-isp", "--include-shared-projects")
            {
                Description = "If set, shared projects (.shproj) will be included in the analysis. By default, shared projects are excluded."
            };

            var targetFrameworkOption = new Option<string?>("-f", "--target-framework")
            {
                Description = "This option allows to select a Target framework moniker (https://learn.microsoft.com/en-us/dotnet/standard/frameworks) for which to analyze dependencies."
            };

            var destinationFileOption = new Option<string?>("-fo", "--file-output")
            {
                Description = "The destination file to put the validation output to. If omitted, the output is printed to the console."
            };

            var licenseFileMappingsOption = new Option<string?>("-file-mapping", "--licensefile-to-license-mappings")
            {
                Description = "File in json format that contains a dictionary to map license files to licenses."
            };

            var rootCommand = new RootCommand("A .net tool to print and validate the licenses of .net code. This tool supports .NET (Core), .NET Standard and .NET Framework projects.");
            rootCommand.Options.Add(inputFileOption);
            rootCommand.Options.Add(inputJsonFileOption);
            rootCommand.Options.Add(includeTransitiveOption);
            rootCommand.Options.Add(allowedLicensesOption);
            rootCommand.Options.Add(ignoredPackagesOption);
            rootCommand.Options.Add(licenseMappingOption);
            rootCommand.Options.Add(overridePackageInformationOption);
            rootCommand.Options.Add(downloadLicenseInformationOption);
            rootCommand.Options.Add(outputTypeOption);
            rootCommand.Options.Add(returnErrorsOnlyOption);
            rootCommand.Options.Add(includeIgnoredPackagesOption);
            rootCommand.Options.Add(excludedProjectsOption);
            rootCommand.Options.Add(includeSharedProjectsOption);
            rootCommand.Options.Add(targetFrameworkOption);
            rootCommand.Options.Add(destinationFileOption);
            rootCommand.Options.Add(licenseFileMappingsOption);

            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                var options = new CommandLineOptions
                {
                    InputFile = parseResult.GetValue(inputFileOption),
                    InputJsonFile = parseResult.GetValue(inputJsonFileOption),
                    IncludeTransitive = parseResult.GetValue(includeTransitiveOption),
                    AllowedLicenses = parseResult.GetValue(allowedLicensesOption),
                    IgnoredPackages = parseResult.GetValue(ignoredPackagesOption),
                    LicenseMapping = parseResult.GetValue(licenseMappingOption),
                    OverridePackageInformation = parseResult.GetValue(overridePackageInformationOption),
                    DownloadLicenseInformation = parseResult.GetValue(downloadLicenseInformationOption),
                    OutputType = parseResult.GetValue(outputTypeOption),
                    ReturnErrorsOnly = parseResult.GetValue(returnErrorsOnlyOption),
                    IncludeIgnoredPackages = parseResult.GetValue(includeIgnoredPackagesOption),
                    ExcludedProjects = parseResult.GetValue(excludedProjectsOption),
                    IncludeSharedProjects = parseResult.GetValue(includeSharedProjectsOption),
                    TargetFramework = parseResult.GetValue(targetFrameworkOption),
                    DestinationFile = parseResult.GetValue(destinationFileOption),
                    LicenseFileMappings = parseResult.GetValue(licenseFileMappingsOption)
                };

                using var httpClient = new HttpClient();
                var fileSystem = new System.IO.Abstractions.FileSystem();
                var solutionPersistance = new SolutionPersistanceWrapper();
                var msBuild = new MsBuildAbstraction();
                IPackagesConfigReader packagesConfigReader = GetPackagesConfigReader();

                var optionsParser = new CommandLineOptionsParser(fileSystem, httpClient);
                var orchestrator = new LicenseValidationOrchestrator(
                    fileSystem,
                    solutionPersistance,
                    msBuild,
                    packagesConfigReader,
                    optionsParser,
                    Console.OpenStandardOutput(),
                    Console.OpenStandardError());

                return await orchestrator.ValidateAsync(options, cancellationToken);
            });

            return rootCommand;
        }

        private static IPackagesConfigReader GetPackagesConfigReader()
        {
#if NETFRAMEWORK
            return new WindowsPackagesConfigReader();
#else
            return OperatingSystem.IsWindows() ? new WindowsPackagesConfigReader() : new FailingPackagesConfigReader();
#endif
        }
    }
}
