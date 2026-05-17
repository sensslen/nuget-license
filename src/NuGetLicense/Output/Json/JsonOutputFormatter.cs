// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text.Json;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Serialization;
using NuGetUtility.Serialization;

namespace NuGetLicense.Output.Json
{
    public class JsonOutputFormatter(bool prettyPrint, bool printErrorsOnly, bool skipIgnoredPackages)
        : IOutputFormatter
    {
        private readonly JsonSerializerOptions _options = new()
        {
            Converters = { new NuGetVersionJsonConverter(), new ValidatedLicenseJsonConverterWithOmittingEmptyErrorList() },
            WriteIndented = prettyPrint
        };

        public async Task Write(Stream stream, IList<LicenseValidationResult> results)
        {
            if (printErrorsOnly)
            {
                results = results.Where(r => r.ValidationErrors.Any()).ToList();
            }
            else if (skipIgnoredPackages)
            {
                results = results.Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored).ToList();
            }

            await JsonSerializer.SerializeAsync(stream, results, _options);
        }
    }
}
