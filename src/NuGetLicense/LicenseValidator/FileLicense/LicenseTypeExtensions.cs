// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.LicenseValidator.FileLicense
{
    public static class LicenseTypeExtensions
    {
        public static string ToSpdx(this OssLicenseType type)
        {
            return type switch
            {
                OssLicenseType.Apache2 => "Apache-2.0",
                OssLicenseType.Bsd2 => "BSD-2-Clause",
                OssLicenseType.Bsd3 => "BSD-3-Clause",
                OssLicenseType.Gpl2 => "GPL-2.0-only",
                OssLicenseType.Gpl3 => "GPL-3.0-only",
                OssLicenseType.Mit => "MIT",
                OssLicenseType.Mspl => "MS-PL",
                _ => "Unknown License Type"
            };
        }
    }
}
