// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using FuzzySharp;
using NuGetLicense.LicenseValidator;
using NuGetLicense.LicenseValidator.FileLicenses;
using NUnit.Framework.Constraints;

namespace NuGetLicense.Test.AcceptanceTests
{
    [TestFixture]
    public partial class FuzzySharp
    {
        public static IEnumerable<(string, string, IResolveConstraint)> GetTestData()
        {
            System.Collections.Immutable.IImmutableDictionary<string, string> values = NuGetLicense.LicenseValidator.FileLicense.FileLicenseMap.Map;
            foreach (KeyValuePair<string, string> compareFrom in values)
            {
                foreach (KeyValuePair<string, string> compareTo in values)
                {
                    yield return (compareFrom.Value, compareTo.Value, compareFrom.Key == compareTo.Key ? Is.GreaterThan(FileLicenseMatcher.MATCH_THRESHOLD) : Is.LessThan(FileLicenseMatcher.MATCH_THRESHOLD));
                }
                yield return (compareFrom.Value, string.Empty, Is.LessThan(FileLicenseMatcher.MATCH_THRESHOLD));
                yield return (compareFrom.Value, MyCSharp_HttpUserAgentParser, compareFrom.Value == values[LicenseExpressions.Mit] ? Is.GreaterThan(FileLicenseMatcher.MATCH_THRESHOLD) : Is.LessThan(FileLicenseMatcher.MATCH_THRESHOLD));
            }
        }


        [TestCaseSource(nameof(GetTestData))]
        public void Then_Fuzzy_Should_Score_Matches_Correctly((string compareFrom, string compareTo, IResolveConstraint check) data)
        {
            int score = Fuzz.Ratio(data.compareFrom, data.compareTo);
            Assert.That(score, data.check);
        }
    }
}
