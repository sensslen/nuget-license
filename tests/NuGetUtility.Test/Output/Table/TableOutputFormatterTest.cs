using NuGetUtility.Output;
using NuGetUtility.Output.Table;

namespace NuGetUtility.Test.Output.Table
{
    [TestFixture(false, false, false)]
    [TestFixture(false, true, false)]
    [TestFixture(true, false, false)]
    [TestFixture(true, true, false)]
    [TestFixture(false, false, true)]
    [TestFixture(false, true, true)]
    [TestFixture(true, false, true)]
    [TestFixture(true, true, true)]
    public class TableOutputFormatterTest : TestBase
    {
        private readonly bool _omitValidLicensesOnError;
        private readonly bool _skipIgnoredPackages;
        private readonly bool _alwaysIncludeValidationContext;

        public TableOutputFormatterTest(bool omitValidLicensesOnError, bool skipIgnoredPackages, bool alwaysIncludeValidationContext)
        {
            _omitValidLicensesOnError = omitValidLicensesOnError;
            _skipIgnoredPackages = skipIgnoredPackages;
            _alwaysIncludeValidationContext = alwaysIncludeValidationContext;
        }
        protected override IOutputFormatter CreateUut()
        {
            return new TableOutputFormatter(_omitValidLicensesOnError, _skipIgnoredPackages, _alwaysIncludeValidationContext);
        }
    }
}
