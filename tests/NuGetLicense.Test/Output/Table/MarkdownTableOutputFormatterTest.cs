// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetLicense.Output;
using NuGetLicense.Output.Table;

namespace NuGetLicense.Test.Output.Table
{
    [InheritsTests]
    [Arguments(true, true, true, true, true)]
    [Arguments(true, true, true, true, false)]
    [Arguments(true, true, true, false, true)]
    [Arguments(true, true, true, false, false)]
    [Arguments(true, true, false, true, true)]
    [Arguments(true, true, false, true, false)]
    [Arguments(true, true, false, false, true)]
    [Arguments(true, true, false, false, false)]
    [Arguments(true, false, true, true, true)]
    [Arguments(true, false, true, true, false)]
    [Arguments(true, false, true, false, true)]
    [Arguments(true, false, true, false, false)]
    [Arguments(true, false, false, true, true)]
    [Arguments(true, false, false, true, false)]
    [Arguments(true, false, false, false, true)]
    [Arguments(true, false, false, false, false)]
    [Arguments(false, true, true, true, true)]
    [Arguments(false, true, true, true, false)]
    [Arguments(false, true, true, false, true)]
    [Arguments(false, true, true, false, false)]
    [Arguments(false, true, false, true, true)]
    [Arguments(false, true, false, true, false)]
    [Arguments(false, true, false, false, true)]
    [Arguments(false, true, false, false, false)]
    [Arguments(false, false, true, true, true)]
    [Arguments(false, false, true, true, false)]
    [Arguments(false, false, true, false, true)]
    [Arguments(false, false, true, false, false)]
    [Arguments(false, false, false, true, true)]
    [Arguments(false, false, false, true, false)]
    [Arguments(false, false, false, false, true)]
    [Arguments(false, false, false, false, false)]
    public class MarkdownTableOutputFormatterTest : TestBase
    {
        private readonly bool _omitValidLicensesOnError;
        private readonly bool _skipIgnoredPackages;

        public MarkdownTableOutputFormatterTest(bool omitValidLicensesOnError, bool skipIgnoredPackages, bool includeCopyright, bool includeAuthors, bool includeLicenseUrl) : base(includeCopyright, includeAuthors, includeLicenseUrl)
        {
            _omitValidLicensesOnError = omitValidLicensesOnError;
            _skipIgnoredPackages = skipIgnoredPackages;
        }
        protected override IOutputFormatter CreateUut()
        {
            return new TableOutputFormatter(_omitValidLicensesOnError, _skipIgnoredPackages, true);
        }
    }
}
