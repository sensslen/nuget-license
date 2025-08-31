// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    public interface IPackageDownloader
    {
        Task<string> ReadAsync(string path, CancellationToken cancellationToken);
    }
}
