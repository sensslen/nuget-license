// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    internal class WrappedPackageDownloader(NuGet.Packaging.IPackageDownloader downloader) : IPackageDownloader
    {
        private const int BUFFER_SIZE = 81920;

        public async Task ReadAsync(string filePath, Stream destination, CancellationToken cancellationToken)
        {
            using Stream stream = await downloader.CoreReader.GetStreamAsync(filePath, cancellationToken);
            await stream.CopyToAsync(destination, BUFFER_SIZE, cancellationToken);
        }
    }
}
