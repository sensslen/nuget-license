// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;

namespace NuGetUtility.LicenseValidator
{
    public static class UrlToLicenseMapping
    {
        private const string Apache20 = "Apache-2.0";
        private const string Gpl20 = "GPL-2.0";
        private const string Mit = "MIT";
        private const string MsPl = "MS-PL";
        private const string Bsd30 = "BSD-3-Clause";

        public static IImmutableDictionary<Uri, string> Default { get; } = ImmutableDictionary.CreateRange(
            new[]
            {
                new KeyValuePair<Uri, string>(new Uri("http://www.apache.org/licenses/LICENSE-2.0.html"), Apache20),
                new KeyValuePair<Uri, string>(new Uri("http://www.apache.org/licenses/LICENSE-2.0"), Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://www.apache.org/licenses/LICENSE-2.0.html"), Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://www.apache.org/licenses/LICENSE-2.0"), Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/owin-contrib/owin-hosting/blob/master/LICENSE.txt"), Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt"), Apache20),
                new KeyValuePair<Uri, string>(
                    new Uri("https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream/blob/master/LICENSE"),
                    Mit
                ),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/hey-red/markdownsharp/master/LICENSE"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.github.com/JamesNK/Newtonsoft.Json/master/LICENSE.md"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://licenses.nuget.org/MIT"), Mit),
                new KeyValuePair<Uri, string>(new Uri("http://max.mit-license.org/"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/dotnet/corefx/blob/master/LICENSE.TXT"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://go.microsoft.com/fwlink/?linkid=868514"), Mit),
                new KeyValuePair<Uri, string>(new Uri("http://go.microsoft.com/fwlink/?linkid=833178"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://raw.githubusercontent.com/AArnott/Validation/8377954d86/LICENSE.txt"), MsPl),
                new KeyValuePair<Uri, string>(new Uri("https://github.com/Microsoft/dotnet/blob/master/LICENSE"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/licenses/MIT"), Mit),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/license/apache-2-0"), Apache20),
                new KeyValuePair<Uri, string>(new Uri("https://opensource.org/license/bsd-3-clause"), Bsd30)
            }
        );
    }
}
