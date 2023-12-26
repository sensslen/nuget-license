using System.Text.Json;
using NuGetUtility.LicenseValidator;
using NuGetUtility.Serialization;

namespace NuGetUtility.Output.Json
{
    public class JsonOutputFormatter : IOutputFormatter
    {
        private readonly bool _printErrorsOnly;
        private readonly bool _skipIgnoredPackages;
        private readonly JsonSerializerOptions _options;
        public JsonOutputFormatter(bool prettyPrint, bool printErrorsOnly, bool skipIgnoredPackages, bool alwaysIncludeValidationContext)
        {
            _printErrorsOnly = printErrorsOnly;
            _skipIgnoredPackages = skipIgnoredPackages;
            _options = new JsonSerializerOptions
            {
                Converters = { new NuGetVersionJsonConverter() },
                WriteIndented = prettyPrint
            };

            if (!alwaysIncludeValidationContext)
            {
                _options.Converters.Add(new PackageValidationContextErrorOnlyConverter());
            }
        }

        public async Task Write(Stream stream, IList<LicenseValidationResult> results)
        {
            if (_printErrorsOnly)
            {
                IEnumerable<LicenseValidationResult> resultsWithErrors = results.Where(r => r.ValidationChecks.Exists(c => c.Error is not null));
                if (results.Any())
                {
                    await JsonSerializer.SerializeAsync(stream, resultsWithErrors, _options);
                    return;
                }
            }

            if (_skipIgnoredPackages)
            {
                results = results.Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored).ToList();
            }

            await JsonSerializer.SerializeAsync(stream, results, _options);
        }
    }
}
