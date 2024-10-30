// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Net.Http;

namespace NuGetUtility.Wrapper.HttpClientWrapper
{
    public class FileDownloader : IFileDownloader
    {
        private readonly SemaphoreSlim _parallelDownloadLimiter = new SemaphoreSlim(10, 10);
        private readonly HttpClient _client;
        private readonly string _downloadDirectory;
        private const int EXPONENTIAL_BACKOFF_WAIT_TIME_MILLISECONDS = 200;
        private const int MAX_RETRIES = 5;

        public FileDownloader(HttpClient client, string downloadDirectory)
        {
            _client = client;
            _downloadDirectory = downloadDirectory;
        }

        public async Task DownloadFile(Uri url, string fileNameStem, CancellationToken token)
        {
            await _parallelDownloadLimiter.WaitAsync(token);
            try
            {
                for (int i = 0; i < MAX_RETRIES; i++)
                {
                    if (await TryDownload(fileNameStem, url, token))
                    {
                        return;
                    }
                    await Task.Delay((int)Math.Pow(EXPONENTIAL_BACKOFF_WAIT_TIME_MILLISECONDS, i + 1), token);
                }
            }
            finally
            {
                _parallelDownloadLimiter.Release();
            }
        }

#pragma warning disable S1172
        private async Task<bool> TryDownload(string fileNameStem, Uri url, CancellationToken token)
#pragma warning restore S1172
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

#if NETFRAMEWORK
            HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            // System.Net.HttpStatusCode.TooManyRequests does not exist in .net472
            if (response.StatusCode == (System.Net.HttpStatusCode)429)
#else
            HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
#endif
            {
                return false;
            }
            response.EnsureSuccessStatusCode();

            string extension = "html";
            if (response.Content.Headers.ContentType?.MediaType == "text/plain")
            {
                extension = "txt";
            }
            string fileName = fileNameStem + "." + extension;
#if NETFRAMEWORK
            using FileStream file = File.OpenWrite(Path.Combine(_downloadDirectory, fileName));
#else
            await using FileStream file = File.OpenWrite(Path.Combine(_downloadDirectory, fileName));
#endif
            using Stream downloadStream = await response.Content.ReadAsStreamAsync();

#if NETFRAMEWORK
            await downloadStream.CopyToAsync(file);
#else
            await downloadStream.CopyToAsync(file, token);
#endif
            return true;
        }
    }
}
