// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

#if !NET
using System.Net.Http;
#endif

namespace NuGetLicense
{
    /// <summary>
    /// Handles license validation by delegating to the orchestrator.
    /// </summary>
    public class LicenseValidationHandler
    {
        private readonly ILicenseValidationOrchestrator _orchestrator;

        public LicenseValidationHandler(
            IFileSystem fileSystem,
            HttpClient httpClient,
            ISolutionPersistanceWrapper solutionPersistance,
            IMsBuildAbstraction msBuild,
            IPackagesConfigReader packagesConfigReader,
            Stream outputStream,
            Stream errorStream)
        {
            var optionsParser = new CommandLineOptionsParser(fileSystem, httpClient);
            _orchestrator = new LicenseValidationOrchestrator(
                fileSystem,
                solutionPersistance,
                msBuild,
                packagesConfigReader,
                optionsParser,
                outputStream,
                errorStream);
        }

        public async Task<int> HandleAsync(CommandLineOptions options, CancellationToken cancellationToken = default)
        {
            return await _orchestrator.ValidateAsync(options, cancellationToken);
        }
    }
}
