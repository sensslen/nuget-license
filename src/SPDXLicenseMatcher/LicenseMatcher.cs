// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using SPDXLicenseMatcher.JavaPort;

namespace SPDXLicenseMatcher
{
    public class LicenseMatcher : ILicenseMatcher
    {
        public string? Match(string licenseText) => string.Join(" OR ", MatchingStandardLicenseIds(licenseText));

        private static IEnumerable<string> MatchingStandardLicenseIds(string licenseText)
        {
            foreach (KeyValuePair<string, Spdx.Licenses.ILicense> license in Spdx.Licenses.SpdxLicenseStore.Licenses)
            {
                string licenseTemplate = license.Value.StandardLicenseTemplate;
                if (string.IsNullOrWhiteSpace(licenseTemplate))
                {
                    licenseTemplate = license.Value.LicenseText;
                }

                if (!LicenseCompareHelper.IsTextMatchingTemplate(licenseTemplate, licenseText).DifferenceFound)
                {
                    yield return license.Key;
                }
            }
        }


        public string? Match(string licenseText, string expected)
        {
            Spdx.Licenses.ILicense license = Spdx.Licenses.SpdxLicenseStore.Licenses[expected];
            string licenseTemplate = license.StandardLicenseTemplate;
            if (string.IsNullOrWhiteSpace(licenseTemplate))
            {
                licenseTemplate = license.LicenseText;
            }

            CompareTemplateOutputHandler.DifferenceDescription diff = LicenseCompareHelper.IsTextMatchingTemplate(licenseTemplate, licenseText);
            return diff.DifferenceFound ? diff.DifferenceMessage : null;
        }
    }
}
