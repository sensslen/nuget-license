// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using McMaster.Extensions.CommandLineUtils;
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
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication<CommandLineOptions>();
            
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(GetServiceProvider());

            app.OnExecuteAsync(async (cancellationToken) =>
            {
                var options = app.Model;

                // Check if mandatory parameters are provided
                if (options.InputFile == null && options.InputJsonFile == null)
                {
                    Console.Error.WriteLine("Error: Please provide an input file using --input or --json-input");
                    Console.Error.WriteLine();
                    app.ShowHelp();
                    return 1;
                }

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

            try
            {
                return await app.ExecuteAsync(args);
            }
            catch (CommandParsingException ex)
            {
                Console.Error.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }

        private static IServiceProvider GetServiceProvider()
        {
            // For now, we don't need dependency injection
            // Return an empty service provider
            return new EmptyServiceProvider();
        }

        private static IPackagesConfigReader GetPackagesConfigReader()
        {
#if NETFRAMEWORK
            return new WindowsPackagesConfigReader();
#else
            return OperatingSystem.IsWindows() ? new WindowsPackagesConfigReader() : new FailingPackagesConfigReader();
#endif
        }

        private class EmptyServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType)
            {
                return null;
            }
        }
    }
}
