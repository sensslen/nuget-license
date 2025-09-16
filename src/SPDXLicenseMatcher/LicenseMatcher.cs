// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using SPDXLicenseMatcher.JavaLibrary;

namespace SPDXLicenseMatcher
{
    /// <summary>
    /// Matches license text to SPDX license identifiers. This is pretty much a direct port of the java
    /// code. The license matching is very slow unfortunately.
    /// </summary>
    public class LicenseMatcher : ILicenseMatcher
    {
        public string Match(string licenseText) => string.Join(" OR ", LicenseCompareHelper.GetMatchingLicenses(licenseText));
    }
}
