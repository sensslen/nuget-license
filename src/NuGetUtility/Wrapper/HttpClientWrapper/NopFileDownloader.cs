// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.HttpClientWrapper
{
    public class NopFileDownloader : IFileDownloader
    {
        public Task DownloadFile(Uri url, string fileNameStem, CancellationToken token) => Task.CompletedTask;

        public Task StoreFileAsync(string licenseText, string fileNameStem, CancellationToken token) => Task.CompletedTask;
    }
}
