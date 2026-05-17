// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.HttpClientWrapper
{
    public class DownloadFailedException(Uri url) : Exception($"Download failed for URL: {url}");
}
