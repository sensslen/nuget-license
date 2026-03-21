// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text;
using NuGetLicense.LicenseValidator;

namespace NuGetLicense.Output.Csv
{
    public class CsvOutputFormatter : IOutputFormatter
    {
        private readonly bool _printErrorsOnly;
        private readonly bool _skipIgnoredPackages;

        public CsvOutputFormatter(bool printErrorsOnly, bool skipIgnoredPackages)
        {
            _printErrorsOnly = printErrorsOnly;
            _skipIgnoredPackages = skipIgnoredPackages;
        }

        public async Task Write(Stream stream, IList<LicenseValidationResult> results)
        {
            if (_printErrorsOnly)
            {
                results = results.Where(r => r.ValidationErrors.Any()).ToList();
            }

            if (_skipIgnoredPackages)
            {
                results = results
                    .Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored)
                    .ToList();
            }

            using var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);

            await writer.WriteLineAsync(
                "Package,Version,License Information Origin,License,License Url,Copyright,Authors,Package Project Url,Errors with Context");

            foreach (var license in results)
            {
                string[] row = new[]
                {
                    EscapeCsvValue(license.PackageId), EscapeCsvValue(license.PackageVersion.ToString()),
                    EscapeCsvValue(license.LicenseInformationOrigin.ToString()),
                    EscapeCsvValue(license.License ?? string.Empty),
                    EscapeCsvValue(license.LicenseUrl ?? string.Empty),
                    EscapeCsvValue(license.Copyright ?? string.Empty),
                    EscapeCsvValue(license.Authors ?? string.Empty),
                    EscapeCsvValue(license.PackageProjectUrl ?? string.Empty),
                    GetValidationErrorsString(license.ValidationErrors)
                };

                await writer.WriteLineAsync(string.Join(",", row));
            }

            await writer.FlushAsync();
        }

        private static string EscapeCsvValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                string escaped = value!.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }

            return value!;
        }

        private static string GetValidationErrorsString(IEnumerable<ValidationError> errors)
        {
            string result = string.Join("; ", errors.Select(e => $"{e.Error} ({e.Context})"));
            return EscapeCsvValue(result);
        }
    }
}
