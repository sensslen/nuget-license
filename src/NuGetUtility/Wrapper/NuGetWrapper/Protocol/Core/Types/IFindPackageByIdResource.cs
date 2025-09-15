// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol.Core.Types
{
    public interface IFindPackageByIdResource
    {
        Task<IPackageDownloader?> TryGetPackageDownloader(PackageIdentity identity, CancellationToken cancellationToken);
    }
}
