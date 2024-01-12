using NuGetUtility.Output;
using NuGetUtility.Output.Table;

namespace NuGetUtility.Test.Output.Table
{
    [TestFixture(true, true, true, true)]
    [TestFixture(true, true, true, false)]
    [TestFixture(true, true, false, true)]
    [TestFixture(true, true, false, false)]
    [TestFixture(true, false, true, true)]
    [TestFixture(true, false, true, false)]
    [TestFixture(true, false, false, true)]
    [TestFixture(true, false, false, false)]
    [TestFixture(false, true, true, true)]
    [TestFixture(false, true, true, false)]
    [TestFixture(false, true, false, true)]
    [TestFixture(false, true, false, false)]
    [TestFixture(false, false, true, true)]
    [TestFixture(false, false, true, false)]
    [TestFixture(false, false, false, true)]
    [TestFixture(false, false, false, false)]
    public class TableOutputFormatterTest : TestBase
    {
        private readonly bool _omitValidLicensesOnError;
        private readonly bool _skipIgnoredPackages;

        public TableOutputFormatterTest(bool omitValidLicensesOnError, bool skipIgnoredPackages, bool includeCopyright, bool includeAuthors) : base(includeCopyright, includeAuthors)
        {
            _omitValidLicensesOnError = omitValidLicensesOnError;
            _skipIgnoredPackages = skipIgnoredPackages;
        }
        protected override IOutputFormatter CreateUut()
        {
            return new TableOutputFormatter(_omitValidLicensesOnError, _skipIgnoredPackages);
        }
    }
}
