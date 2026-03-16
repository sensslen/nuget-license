// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Output.Csv;

using HelperNuGetVersion = NuGetLicense.Test.Output.Helper.NuGetVersion;

namespace NuGetLicense.Test.Output.Csv
{
    [TestFixture]
    public class CsvOutputFormatterSpecialCases
    {
        [Test]
        public async Task Should_EscapeCsv_WithSpecialCharacters_Correctly()
        {
            var licenses = new List<LicenseValidationResult>
            {
                new(
                    PackageId: "PackageId,With,Commas",
                    PackageVersion: new HelperNuGetVersion("1.0.0"),
                    PackageProjectUrl: null,
                    License: "MIT License",
                    LicenseUrl: null,
                    "Copyright \"2024\"",
                    "Author1, Author2",
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Expression,
                    ValidationErrors: new List<ValidationError>()
                ),
                new(
                    PackageId: "PackageIdWith\nNewline",
                    PackageVersion: new HelperNuGetVersion("2.0.0"),
                    PackageProjectUrl: null,
                    License: "Apache-2.0",
                    LicenseUrl: null,
                    Copyright: null,
                    Authors: null,
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Expression,
                    ValidationErrors: new List<ValidationError>()
                )
            };

            string expected =
                "Package,Version,License Information Origin,License,License Url,Copyright,Authors,Package Project Url,Errors with Context\r\n" +
                "\"PackageId,With,Commas\",1.0.0,Expression,MIT License,,\"Copyright \"\"2024\"\"\",\"Author1, Author2\",,\r\n" +
                "\"PackageIdWith\nNewline\",2.0.0,Expression,Apache-2.0,,,,,\r\n";

            var csvFormatter = new CsvOutputFormatter(false, false);
            using var memoryStream = new MemoryStream();

            await csvFormatter.Write(memoryStream, licenses);

            string result = Encoding.UTF8.GetString(memoryStream.ToArray());

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public async Task Should_EscapeCsv_WithErrors_Correctly()
        {
            var licenses = new List<LicenseValidationResult>
            {
                new(
                    PackageId: "TestPackage",
                    PackageVersion: new HelperNuGetVersion("1.0.0"),
                    PackageProjectUrl: null,
                    License: "MIT",
                    LicenseUrl: null,
                    Copyright: null,
                    Authors: null,
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Expression,
                    ValidationErrors: new List<ValidationError>
                    {
                        new("License not allowed", "MIT is not in the allowed list"),
                        new("Missing copyright", "No copyright information")
                    }
                )
            };

            string expected =
                "Package,Version,License Information Origin,License,License Url,Copyright,Authors,Package Project Url,Errors with Context\r\n" +
                "TestPackage,1.0.0,Expression,MIT,,,,,License not allowed (MIT is not in the allowed list); Missing copyright (No copyright information)\r\n";

            var csvFormatter = new CsvOutputFormatter(false, false);
            using var memoryStream = new MemoryStream();

            await csvFormatter.Write(memoryStream, licenses);

            string result = Encoding.UTF8.GetString(memoryStream.ToArray());

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public async Task Should_EscapeCsv_WithSkipIgnoredFilter_Correctly()
        {
            var licenses = new List<LicenseValidationResult>
            {
                new(
                    PackageId: "NormalPackage",
                    PackageVersion: new HelperNuGetVersion("1.0.0"),
                    PackageProjectUrl: null,
                    License: "MIT",
                    LicenseUrl: null,
                    Copyright: null,
                    Authors: null,
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Expression,
                    ValidationErrors: new List<ValidationError>()
                ),
                new(
                    PackageId: "IgnoredPackage",
                    PackageVersion: new HelperNuGetVersion("2.0.0"),
                    PackageProjectUrl: null,
                    License: "MIT",
                    LicenseUrl: null,
                    Copyright: null,
                    Authors: null,
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Ignored,
                    ValidationErrors: new List<ValidationError>()
                )
            };

            string expected =
                "Package,Version,License Information Origin,License,License Url,Copyright,Authors,Package Project Url,Errors with Context\r\n" +
                "NormalPackage,1.0.0,Expression,MIT,,,,,\r\n";

            var csvFormatter = new CsvOutputFormatter(false, skipIgnoredPackages: true);
            using var memoryStream = new MemoryStream();

            await csvFormatter.Write(memoryStream, licenses);

            string result = Encoding.UTF8.GetString(memoryStream.ToArray());

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public async Task Should_EscapeCsvCorrectly_IfPrintErrorsOnly()
        {
            var licenses = new List<LicenseValidationResult>
            {
                new(
                    PackageId: "Package1",
                    PackageVersion: new HelperNuGetVersion("1.0.0"),
                    PackageProjectUrl: null,
                    License: "MIT",
                    LicenseUrl: null,
                    Copyright: null,
                    Authors: null,
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Expression
                ),
                new(
                    PackageId: "Package2",
                    PackageVersion: new HelperNuGetVersion("2.0.0"),
                    PackageProjectUrl: null,
                    License: "MIT",
                    LicenseUrl: null,
                    Copyright: null,
                    Authors: null,
                    Description: null,
                    Summary: null,
                    LicenseInformationOrigin.Expression
                )
            };

            var csvFormatter = new CsvOutputFormatter(printErrorsOnly: true, false);
            using var memoryStream = new MemoryStream();

            await csvFormatter.Write(memoryStream, licenses);

            string[] result = Encoding.UTF8.GetString(memoryStream.ToArray()).Split(['\n'], StringSplitOptions.RemoveEmptyEntries);

            Assert.That(result.Length, Is.EqualTo(1));
        }
    }
}
