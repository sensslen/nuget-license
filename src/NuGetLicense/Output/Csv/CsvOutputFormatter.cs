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
        private readonly string[] _includedColumns;

        public CsvOutputFormatter(bool printErrorsOnly, bool skipIgnoredPackages, string[]? includedColumns = null)
        {
            _printErrorsOnly = printErrorsOnly;
            _skipIgnoredPackages = skipIgnoredPackages;
            _includedColumns = includedColumns ?? Array.Empty<string>();
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
            
            // Define all available columns
            var allColumns = new Dictionary<string, Func<LicenseValidationResult, string?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Package"] = r => r.PackageId,
                ["Version"] = r => r.PackageVersion.ToString(),
                ["LicenseInformationOrigin"] = r => r.LicenseInformationOrigin.ToString(),
                ["LicenseExpression"] = r => r.License,
                ["LicenseUrl"] = r => r.LicenseUrl,
                ["Copyright"] = r => r.Copyright,
                ["Authors"] = r => r.Authors,
                ["Description"] = r => r.Description,
                ["Summary"] = r => r.Summary,
                ["Error"] = r => r.ValidationErrors.Any() ? string.Join("; ", r.ValidationErrors.Select(e => e.Error)) : null,
                ["ErrorContext"] = r => r.ValidationErrors.Any() ? string.Join("; ", r.ValidationErrors.Select(e => e.Context)) : null
            };
            
            // Determine which columns to include
            var columnsToInclude = _includedColumns.Length > 0
                ? allColumns.Where(c => _includedColumns.Contains(c.Key, StringComparer.OrdinalIgnoreCase)).ToList()
                : allColumns.ToList();
            
            // If no valid columns specified, use all columns
            if (columnsToInclude.Count == 0)
            {
                columnsToInclude = allColumns.ToList();
            }
            
            // Write header
            var header = string.Join(",", columnsToInclude.Select(c => EscapeCsvField(c.Key)));
            await writer.WriteLineAsync(header);
            
            // Write rows
            foreach (var result in results)
            {
                var values = columnsToInclude.Select(c => EscapeCsvField(c.Value(result)));
                var line = string.Join(",", values);
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
