// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    internal class WrappedPackageDownloader(NuGet.Packaging.IPackageDownloader downloader) : IPackageDownloader
    {
        public async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
        {
            string normalizedPath = NuGet.Common.PathUtility.GetPathWithDirectorySeparator(path);
            using Stream stream = await downloader.CoreReader.GetStreamAsync(normalizedPath, cancellationToken);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
