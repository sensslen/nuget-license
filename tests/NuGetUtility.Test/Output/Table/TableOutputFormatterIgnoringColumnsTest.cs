// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Output;
using NuGetUtility.Output.Table;
using NuGetUtility.Test.Extensions;

namespace NuGetUtility.Test.Output.Table
{
    [TestFixture(false, true, true, true, true, new[] { nameof(OutputColumnType.LicenseInformationOrigin), nameof(OutputColumnType.License) })]
    [TestFixture(false, true, true, true, true, new[] {
        nameof(OutputColumnType.Authors),
        nameof(OutputColumnType.Copyright),
        nameof(OutputColumnType.LicenseUrl),
        nameof(OutputColumnType.PackageVersion),
        nameof(OutputColumnType.ValidationErrors),
        nameof(OutputColumnType.ErrorContext),
        nameof(OutputColumnType.PackageProjectUrl),
        nameof(OutputColumnType.License),
        nameof(OutputColumnType.LicenseInformationOrigin)
    })]
    public class TableOutputFormatterIgnoringColumnsTest : TestBase
    {
        private readonly bool _omitValidLicensesOnError;
        private readonly bool _skipIgnoredPackages;
        private readonly List<OutputColumnType> _ignoredColumns;

        public TableOutputFormatterIgnoringColumnsTest(bool omitValidLicensesOnError,
            bool skipIgnoredPackages, bool includeCopyright, bool includeAuthors, bool includeLicenseUrl, string[] ignoredColumns) :
            base(includeCopyright, includeAuthors, includeLicenseUrl)
        {
            _omitValidLicensesOnError = omitValidLicensesOnError;
            _skipIgnoredPackages = skipIgnoredPackages;
            _ignoredColumns = ignoredColumns.Select(columnName => (OutputColumnType)Enum.Parse(typeof(OutputColumnType), columnName, true)).ToList();
        }
        protected override IOutputFormatter CreateUut()
        {
            return new TableOutputFormatter(_omitValidLicensesOnError, _skipIgnoredPackages, _ignoredColumns);
        }


        [Test]
        public Task ValidatedLicenses_ShouldThrowIfAllColumnsAreIgnored(
            [Values(0, 1, 5, 20, 100)] int validatedLicenseCount)
        {
            var formatter = new TableOutputFormatter(_omitValidLicensesOnError, _skipIgnoredPackages, new[] {
                OutputColumnType.Authors,
                OutputColumnType.Copyright,
                OutputColumnType.LicenseUrl,
                OutputColumnType.PackageId,
                OutputColumnType.PackageVersion,
                OutputColumnType.ValidationErrors,
                OutputColumnType.ErrorContext,
                OutputColumnType.PackageProjectUrl,
                OutputColumnType.License,
                OutputColumnType.LicenseInformationOrigin
            });

            using var stream = new MemoryStream();
            var validated = ValidatedLicenseFaker.GenerateForever().Take(validatedLicenseCount).ToList();

            Assert.ThrowsAsync(typeof(InvalidOperationException), async Task () => await formatter.Write(stream, validated));

            return Task.CompletedTask;
        }
    }
}
