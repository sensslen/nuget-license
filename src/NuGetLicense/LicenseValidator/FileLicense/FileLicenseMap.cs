using System.Diagnostics.CodeAnalysis;
using NuGetLicense.LicenseValidator.FileLicense.Templates;

namespace NuGetLicense.LicenseValidator.FileLicense;

public static class FileLicenseMap
{
    public static readonly Dictionary<string, string> Map = new()
    {
        { License.Apache2, LicenseTemplate.Apache2 },
        { License.Mit, LicenseTemplate.Mit },
        { License.Bsd2, LicenseTemplate.Bsd2 },
        { License.Bsd3, LicenseTemplate.Bsd3 },
        { License.Gpl2, LicenseTemplate.Gpl2 },
        { License.Gpl3, LicenseTemplate.Gpl3 },
        { License.Mspl, LicenseTemplate.Mspl }
    };
}
