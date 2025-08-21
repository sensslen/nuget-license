// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using IWrappedPackageMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.IPackageMetadata;
using OriginalGlobalPackagesFolderUtility = NuGet.Protocol.GlobalPackagesFolderUtility;
using OriginalPackageIdentity = NuGet.Packaging.Core.PackageIdentity;
using PackageIdentity = NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core.PackageIdentity;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol
{
    public class GlobalPackagesFolderUtility : IGlobalPackagesFolderUtility
    {
        private readonly string _globalPackagesFolder;

        public GlobalPackagesFolderUtility(ISettings settings)
        {
            _globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
        }

        public IWrappedPackageMetadata? GetPackage(PackageIdentity identity)
        {
            DownloadResourceResult cachedPackage = OriginalGlobalPackagesFolderUtility.GetPackage(new OriginalPackageIdentity(identity.Id, new NuGetVersion(identity.Version.ToString()!)), _globalPackagesFolder);
            if (cachedPackage == null)
            {
                return null;
            }

            using PackageReaderBase pkgStream = cachedPackage.PackageReader;
            var manifest = Manifest.ReadFrom(pkgStream.GetNuspec(), true);

            if (manifest.Metadata.Version.Equals(identity.Version))
            {
                return null;
            }

            return new WrappedPackageMetadata(manifest.Metadata);
        }
    }
}
