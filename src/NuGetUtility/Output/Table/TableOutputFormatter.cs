// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Extensions;
using NuGetUtility.LicenseValidator;

namespace NuGetUtility.Output.Table
{
    public class TableOutputFormatter : IOutputFormatter
    {
        private readonly bool _printErrorsOnly;
        private readonly bool _skipIgnoredPackages;
        private readonly HashSet<OutputColumnType>? _ignoredColumns;

        public TableOutputFormatter(bool printErrorsOnly, bool skipIgnoredPackages, IEnumerable<OutputColumnType>? ignoredColumns = null)
        {
            _printErrorsOnly = printErrorsOnly;
            _skipIgnoredPackages = skipIgnoredPackages;
            _ignoredColumns = ignoredColumns?.ToHashSet();
        }

        public async Task Write(Stream stream, IList<LicenseValidationResult> results)
        {
            var errorColumnDefinition = new ColumnDefinition(OutputColumnType.ValidationErrors, license => license.ValidationErrors.Select(e => e.Error), license => license.ValidationErrors.Any());
            ColumnDefinition[] columnDefinitions = new[]
            {
                new ColumnDefinition(OutputColumnType.PackageId, license => license.PackageId, license => true, true),
                new ColumnDefinition(OutputColumnType.PackageVersion, license => license.PackageVersion, license => true, true),
                new ColumnDefinition(OutputColumnType.LicenseInformationOrigin, license => license.LicenseInformationOrigin, license => true, true),
                new ColumnDefinition(OutputColumnType.License, license => license.License, license => license.License != null),
                new ColumnDefinition(OutputColumnType.LicenseUrl, license => license.LicenseUrl, license => license.LicenseUrl != null),
                new ColumnDefinition(OutputColumnType.Copyright, license => license.Copyright, license => license.Copyright != null),
                new ColumnDefinition(OutputColumnType.Authors, license => license.Authors, license => license.Authors != null),
                new ColumnDefinition(OutputColumnType.PackageProjectUrl,license => license.PackageProjectUrl, license => license.PackageProjectUrl != null),
                errorColumnDefinition,
                new ColumnDefinition(OutputColumnType.ErrorContext, license => license.ValidationErrors.Select(e => e.Context), license => license.ValidationErrors.Any()),
            };

            foreach (LicenseValidationResult license in results)
            {
                foreach (ColumnDefinition? definition in columnDefinitions)
                {
                    definition.Enabled |= definition.IsRelevant(license);
                }
            }

            if (_ignoredColumns is not null)
            {
                foreach (ColumnDefinition? definition in columnDefinitions)
                {
                    definition.Enabled &= !_ignoredColumns.Contains(definition.Type);
                }
            }

            if (_printErrorsOnly)
            {
                results = results.Where(r => r.ValidationErrors.Any()).ToList();
            }
            else if (_skipIgnoredPackages)
            {
                results = results.Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored).ToList();
            }

            ColumnDefinition[] relevantColumns = columnDefinitions.Where(c => c.Enabled).ToArray();
            await TablePrinterExtensions
                .Create(stream, relevantColumns.Select(d => d.Title))
                .FromValues(
                    results,
                    license => relevantColumns.Select(d => d.PropertyAccessor(license)))
                .Print();
        }

        private sealed record ColumnDefinition(OutputColumnType Type, Func<LicenseValidationResult, object?> PropertyAccessor, Func<LicenseValidationResult, bool> IsRelevant, bool Enabled = false)
        {
            public bool Enabled { get; set; } = Enabled;

            public string Title { get; } = Type.GetDescription() ?? throw new InvalidOperationException($"Enum value {Type} is missing the Description attribute");
        }
    }
}
