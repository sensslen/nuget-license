// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using SPDXLicenseMatcher.JavaLibrary;

namespace SPDXLicenseMatcher
{
    public class LicenseMatcher : ILicenseMatcher
    {
        public string? Match(string licenseText) => string.Join(" OR ", LicenseCompareHelper.GetMatchingLicenses(licenseText));
    }
}
