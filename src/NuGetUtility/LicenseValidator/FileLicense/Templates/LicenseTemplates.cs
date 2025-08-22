// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.LicenseValidator.FileLicense.Templates
{
    public static partial class LicenseTemplates
    {
        public static readonly Dictionary<OssLicenseType, string> Map = new()
        {
            { OssLicenseType.Apache2, Apache2 },
            { OssLicenseType.Mit, Mit },
            { OssLicenseType.Bsd2, Bsd2 },
            { OssLicenseType.Bsd3, Bsd3 },
            { OssLicenseType.Gpl2, Gpl2 },
            { OssLicenseType.Gpl3, Gpl3 }
        };
    }
}
