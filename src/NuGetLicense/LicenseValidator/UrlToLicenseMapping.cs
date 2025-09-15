// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NuGetLicense.LicenseValidator
{
    public static class UrlToLicenseMapping
    {

        [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "License mapping requires hardcoded URIs.")]
        public static IImmutableDictionary<Uri, string> Default { get; } = ImmutableDictionary.CreateRange(
            [
                new KeyValuePair<Uri, string>(new Uri("http://www.apache.org/licenses/LICENSE-2.0.html"), LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(new Uri("http://www.apache.org/licenses/LICENSE-2.0"), LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://www.apache.org/licenses/LICENSE-2.0.html"), LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://www.apache.org/licenses/LICENSE-2.0"), LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/owin-contrib/owin-hosting/blob/master/LICENSE.txt"), LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt"), LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(
                    new Uri("https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream/blob/master/LICENSE"),
                    LicenseExpressions.Mit
                ),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/hey-red/markdownsharp/master/LICENSE"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.github.com/JamesNK/Newtonsoft.Json/master/LICENSE.md"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://licenses.nuget.org/MIT"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("http://max.mit-license.org/"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/dotnet/corefx/blob/master/LICENSE.TXT"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://go.microsoft.com/fwlink/?linkid=868514"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("http://go.microsoft.com/fwlink/?linkid=833178"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/AArnott/Validation/8377954d86/LICENSE.txt"), LicenseExpressions.MsPl),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/Microsoft/dotnet/blob/master/LICENSE"), LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/licenses/MIT"),  LicenseExpressions.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/license/apache-2-0"),  LicenseExpressions.Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/license/bsd-3-clause"), LicenseExpressions.Bsd30)
            ]
        );
    }
}
