// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;

namespace NuGetLicense.LicenseValidator.FileLicense;

public static partial class FileLicenseMap
{
    public static readonly IImmutableDictionary<string, string> Map = ImmutableDictionary.CreateRange(
        StringComparer.OrdinalIgnoreCase,
        [
            new KeyValuePair<string,string>(LicenseExpressions.Apache20, Apache2),
            new KeyValuePair<string,string>(LicenseExpressions.Mit, Mit),
            new KeyValuePair<string,string>(LicenseExpressions.Bsd20, Bsd2),
            new KeyValuePair<string,string>(LicenseExpressions.Bsd30, Bsd3),
            new KeyValuePair<string,string>(LicenseExpressions.Gpl20, Gpl2),
            new KeyValuePair<string,string>(LicenseExpressions.Gpl30, Gpl3),
            new KeyValuePair<string,string>(LicenseExpressions.MsPl, Mspl)
        ]);
}
