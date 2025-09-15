// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.HttpClientWrapper
{
    public interface IFileDownloader
    {
        public Task DownloadFile(Uri url, string fileNameStem, CancellationToken token);
        public Task StoreFileAsync(string licenseText, string fileNameStem, CancellationToken token);
    }
}
