// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NuGetLicense.LicenseValidator
{
    public static class UrlToLicenseMapping
    {
        [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "License mapping requires hardcoded URIs.")]
        [SuppressMessage("Critical Security Hotspot", "S5332:Using http protocol is insecure", Justification = "Legacy license URLs require HTTP for compatibility with existing package metadata.")]
        public static IImmutableDictionary<Uri, string> Default { get; } = ImmutableDictionary.CreateRange(
            new[]
            {
                new KeyValuePair<Uri, string>(new Uri("http://www.apache.org/licenses/LICENSE-2.0.html"), License.Apache2),
                new KeyValuePair<Uri, string>(new Uri("http://www.apache.org/licenses/LICENSE-2.0"), License.Apache2),
                new KeyValuePair<Uri, string>(new Uri("https://www.apache.org/licenses/LICENSE-2.0.html"), License.Apache2),
                new KeyValuePair<Uri, string>(new Uri("https://www.apache.org/licenses/LICENSE-2.0"), License.Apache2),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/owin-contrib/owin-hosting/blob/master/LICENSE.txt"), License.Apache2),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt"), License.Apache2),
                new KeyValuePair<Uri, string>(
                    new Uri("https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream/blob/master/LICENSE"),
                    License.Mit
                ),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/hey-red/markdownsharp/master/LICENSE"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.github.com/JamesNK/Newtonsoft.Json/master/LICENSE.md"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://licenses.nuget.org/MIT"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("http://max.mit-license.org/"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/dotnet/corefx/blob/master/LICENSE.TXT"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://go.microsoft.com/fwlink/?linkid=868514"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("http://go.microsoft.com/fwlink/?linkid=833178"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/AArnott/Validation/8377954d86/LICENSE.txt"), License.Mspl),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/Microsoft/dotnet/blob/master/LICENSE"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/licenses/MIT"), License.Mit),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/license/apache-2-0"), License.Apache2),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/license/bsd-3-clause"), License.Bsd3)
            }
        );
    }
}
