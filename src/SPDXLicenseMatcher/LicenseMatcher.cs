// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text.RegularExpressions;

namespace SPDXLicenseMatcher
{
    public class LicenseMatcher : ILicenseMatcher
    {
        public string? Match(string licenseText)
        {
            foreach ((string SpdxId, System.Text.RegularExpressions.Regex Matcher) in SpdxLicenseMatcher.AllLicenseMatchers)
            {
                if (Matcher.IsMatch(Regex.Replace(licenseText, @"\s+", " ").Trim()))
                {
                    return SpdxId;
                }
            }
            return null;
        }
    }
}
