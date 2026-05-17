// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Concurrent;
using System.Net.Http;

namespace NuGetUtility.Wrapper.HttpClientWrapper
{
    public class FileDownloader(HttpClient client, string downloadDirectory) : IFileDownloader
    {
        private readonly SemaphoreSlim _parallelDownloadLimiter = new(10, 10);
        private readonly ConcurrentDictionary<Uri, Task<string>> _downloadedLicenses = new();
        private const int EXPONENTIAL_BACKOFF_WAIT_TIME_MILLISECONDS = 200;
        private const int MAX_RETRIES = 5;

        public async Task DownloadFile(Uri url, string fileNameStem, CancellationToken token)
        {
            string initialDownloadName = await _downloadedLicenses.GetOrAdd(url, u => DownloadFileActuallyAsync(u, fileNameStem, token));

            if (!initialDownloadName.StartsWith(fileNameStem))
            {
                string destinationFile = $"{fileNameStem}{Path.GetExtension(initialDownloadName)}";
                File.Copy(Path.Combine(downloadDirectory, initialDownloadName), Path.Combine(downloadDirectory, destinationFile), true);
            }
        }

        private async Task<string> DownloadFileActuallyAsync(Uri url, string fileNameStem, CancellationToken token)
        {
            await _parallelDownloadLimiter.WaitAsync(token);
            try
            {
                for (int i = 0; i < MAX_RETRIES; i++)
                {
                    string? fileLocation = await TryDownload(fileNameStem, url, token);
                    if (fileLocation is not null)
                    {
                        return fileLocation;
                    }
                    await Task.Delay(EXPONENTIAL_BACKOFF_WAIT_TIME_MILLISECONDS * ((int)Math.Pow(2, i)), token);
                }
            }
            finally
            {
                _parallelDownloadLimiter.Release();
            }
            throw new DownloadFailedException(url);
        }

#pragma warning disable S1172 // Unused parameter
        private async Task<string?> TryDownload(string fileNameStem, Uri url, CancellationToken token)
#pragma warning restore S1172 // Unused parameter
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
#if NETFRAMEWORK
            // System.Net.HttpStatusCode.TooManyRequests does not exist in .net472
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
#else
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
#endif
            {
                return null;
            }
            response.EnsureSuccessStatusCode();

            string extension = "html";
            if (response.Content.Headers.ContentType?.MediaType == "text/plain")
            {
                extension = "txt";
            }
            string fileName = $"{fileNameStem}.{extension}";
#if NETFRAMEWORK
            using FileStream file = File.OpenWrite(Path.Combine(downloadDirectory, fileName));
#else
            await using FileStream file = File.OpenWrite(Path.Combine(downloadDirectory, fileName));
#endif
            using Stream downloadStream = await response.Content.ReadAsStreamAsync();

#if NETFRAMEWORK
            await downloadStream.CopyToAsync(file);
#else
            await downloadStream.CopyToAsync(file, token);
#endif
            return fileName;
        }

        public Task StoreFileAsync(string licenseText, string fileNameStem, CancellationToken token)
        {
            string fileName = $"{fileNameStem}.txt";
#if NETFRAMEWORK
            File.WriteAllText(Path.Combine(downloadDirectory, fileName), licenseText);
            return Task.CompletedTask;
#else
            return File.WriteAllTextAsync(Path.Combine(downloadDirectory, fileName), licenseText, token);
#endif
        }
    }
}
