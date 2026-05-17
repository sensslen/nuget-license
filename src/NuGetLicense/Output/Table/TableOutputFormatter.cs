// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetLicense.LicenseValidator;
using NuGetUtility.Output.Table;

namespace NuGetLicense.Output.Table
{
    public class TableOutputFormatter(bool printErrorsOnly, bool skipIgnoredPackages, bool printMarkdown = false)
        : IOutputFormatter
    {
        public async Task Write(Stream stream, IList<LicenseValidationResult> results)
        {
            var errorColumnDefinition = new ColumnDefinition("Error", license => license.ValidationErrors.Select(e => e.Error), license => license.ValidationErrors.Any());
            ColumnDefinition[] columnDefinitions =
            [
                new("Package", license => license.PackageId, license => true, true),
                new("Version", license => license.PackageVersion, license => true, true),
                new("License Information Origin", license => license.LicenseInformationOrigin, license => true, true),
                new("License Expression", license => license.License?.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries), license => license.License != null),
                new("License Url", license => license.LicenseUrl, license => license.LicenseUrl != null),
                new("Copyright", license => license.Copyright, license => license.Copyright != null),
                new("Authors", license => license.Authors, license => license.Authors != null),
                new("Package Project Url",license => license.PackageProjectUrl, license => license.PackageProjectUrl != null),
                errorColumnDefinition,
                new("Error Context", license => license.ValidationErrors.Select(e => e.Context), license => license.ValidationErrors.Any()),
            ];

            foreach (LicenseValidationResult license in results)
            {
                foreach (ColumnDefinition? definition in columnDefinitions)
                {
                    definition.Enabled |= definition.IsRelevant(license);
                }
            }

            if (printErrorsOnly)
            {
                results = results.Where(r => r.ValidationErrors.Any()).ToList();
            }
            else if (skipIgnoredPackages)
            {
                results = results.Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored).ToList();
            }

            ColumnDefinition[] relevantColumns = columnDefinitions.Where(c => c.Enabled).ToArray();
            await TablePrinterExtensions
                .Create(stream, relevantColumns.Select(d => d.Title), printMarkdown)
                .FromValues(
                    results,
                    license => relevantColumns.Select(d => d.PropertyAccessor(license)))
                .Print();
        }

        private sealed record ColumnDefinition(string Title, Func<LicenseValidationResult, object?> PropertyAccessor, Func<LicenseValidationResult, bool> IsRelevant, bool Enabled = false)
        {
            public bool Enabled { get; set; } = Enabled;
        }
    }
}
