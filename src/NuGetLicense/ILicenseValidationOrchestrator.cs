// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetLicense
{
    public interface ILicenseValidationOrchestrator
    {
        /// <returns>Exit code: 0 for success, the number of validation errors, or -1 on exception.</returns>
        Task<int> ValidateAsync(ICommandLineOptions options, CancellationToken cancellationToken = default);
    }
}
