// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace SPDXLicenseMatcher
{
    public class LicenseMatcher : ILicenseMatcher
    {
        public string? Match(string licenseText)
        {
            foreach ((string SpdxId, System.Text.RegularExpressions.Regex Matcher) in SpdxLicenseMatcher.AllLicenseMatchers)
            {
                if (Matcher.IsMatch(licenseText))
                {
                    return SpdxId;
                }
            }
            return null;
        }
    }
}
