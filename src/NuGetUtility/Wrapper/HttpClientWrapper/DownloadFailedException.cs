// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.HttpClientWrapper
{
    public class DownloadFailedException : Exception
    {
        public DownloadFailedException(Uri url) : base($"Download failed for URL: {url}")
        {
        }
    }
}
