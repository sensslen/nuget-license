// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root


// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetLicense.LicenseValidator;

namespace NuGetLicense.Output
{
    public interface IOutputFormatter
    {
        Task Write(Stream stream, IList<LicenseValidationResult> results);
    }
}
