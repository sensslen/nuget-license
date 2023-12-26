using NuGetUtility.Output;
using NuGetUtility.Output.Json;

namespace NuGetUtility.Test.Output.Json
{
    [TestFixture(false, false, false, false)]
    [TestFixture(true, false, false, false)]
    [TestFixture(false, true, false, false)]
    [TestFixture(true, true, false, false)]
    [TestFixture(false, false, true, false)]
    [TestFixture(true, false, true, false)]
    [TestFixture(false, true, true, false)]
    [TestFixture(true, true, true, false)]
    [TestFixture(false, false, false, true)]
    [TestFixture(true, false, false, true)]
    [TestFixture(false, true, false, true)]
    [TestFixture(true, true, false, true)]
    [TestFixture(false, false, true, true)]
    [TestFixture(true, false, true, true)]
    [TestFixture(false, true, true, true)]
    [TestFixture(true, true, true, true)]
    public class JsonOutputFormatterTest : TestBase
    {
        private readonly bool _prettyPrint;
        private readonly bool _omitValidLicensesOnError;
        private readonly bool _skipIgnoredPackages;
        private readonly bool _alwaysIncludeValidationContext;

        public JsonOutputFormatterTest(bool prettyPrint, bool omitValidLicensesOnError, bool skipIgnoredPackages, bool alwaysIncludeValidationContext)
        {
            _prettyPrint = prettyPrint;
            _omitValidLicensesOnError = omitValidLicensesOnError;
            _skipIgnoredPackages = skipIgnoredPackages;
            _alwaysIncludeValidationContext = alwaysIncludeValidationContext;
        }
        protected override IOutputFormatter CreateUut()
        {
            return new JsonOutputFormatter(_prettyPrint, _omitValidLicensesOnError, _skipIgnoredPackages, _alwaysIncludeValidationContext);
        }
    }
}
