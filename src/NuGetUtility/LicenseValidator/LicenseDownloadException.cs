// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

namespace NuGetUtility.LicenseValidator
{
    public class LicenseDownloadException : Exception
    {
        public LicenseDownloadException(Exception inner, string context, Uri url, PackageIdentity packageInfo)
            :
            base(
                $"Failed to download license for package {packageInfo.Id} ({packageInfo.Version}) from url: {url}.\nContext: {context}",
                inner)
        { }
    }
}
