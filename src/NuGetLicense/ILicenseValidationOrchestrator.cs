// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetLicense
{
    /// <summary>
    /// Orchestrates the license validation process.
    /// </summary>
    public interface ILicenseValidationOrchestrator
    {
        /// <summary>
        /// Orchestrates the license validation process and returns the exit code.
        /// </summary>
        /// <param name="options">The parsed command line options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Exit code: 0 for success, number of validation errors, or -1 for exceptions.</returns>
        Task<int> ValidateAsync(ICommandLineOptions options, CancellationToken cancellationToken = default);
    }
}
