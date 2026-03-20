// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Globalization;
using NuGetLicense.LicenseValidator;

namespace NuGetLicense.Output.Csv
{
    /// <summary>
    /// Outputs license validation results in CSV format.
    /// </summary>
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
            else if (_skipIgnoredPackages)
            {
                results = results.Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored).ToList();
            }

            using var writer = new StreamWriter(stream, leaveOpen: true);
            
            // Write header
            await writer.WriteLineAsync("Package,Version,License Information Origin,License Expression,License Url,Copyright,Authors,Description,Summary,Error,Error Context");
            
            // Write rows
            foreach (var result in results)
            {
                string errors = result.ValidationErrors.Any() 
                    ? string.Join("; ", result.ValidationErrors.Select(e => EscapeCsvField(e.Error))) 
                    : "";
                string errorContexts = result.ValidationErrors.Any() 
                    ? string.Join("; ", result.ValidationErrors.Select(e => EscapeCsvField(e.Context))) 
                    : "";
                
                var line = string.Join(",",
                    EscapeCsvField(result.PackageId),
                    EscapeCsvField(result.PackageVersion.ToString()),
                    EscapeCsvField(result.LicenseInformationOrigin.ToString()),
                    EscapeCsvField(result.License),
                    EscapeCsvField(result.LicenseUrl),
                    EscapeCsvField(result.Copyright),
                    EscapeCsvField(result.Authors),
                    EscapeCsvField(result.Description),
                    EscapeCsvField(result.Summary),
                    EscapeCsvField(errors),
                    EscapeCsvField(errorContexts)
                );
                
                await writer.WriteLineAsync(line);
            }
            
            await writer.FlushAsync();
        }

        /// <summary>
        /// Escapes a field for CSV output.
        /// Handles fields containing commas, quotes, or newlines.
        /// </summary>
        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return "";
            }

            // If the field contains comma, quote, or newline, wrap it in quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                // Replace double quotes with two double quotes
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }
    }
}
