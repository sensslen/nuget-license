// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;
using IWrappedPackageMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.IPackageMetadata;
using OriginalGlobalPackagesFolderUtility = NuGet.Protocol.GlobalPackagesFolderUtility;
using OriginalPackageIdentity = NuGet.Packaging.Core.PackageIdentity;
using PackageIdentity = NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core.PackageIdentity;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol
{
    public class GlobalPackagesFolderUtility : IGlobalPackagesFolderUtility
    {
        private readonly string[] _packageFolders;

        public GlobalPackagesFolderUtility(ISettings settings, IEnumerable<string> additionalPackageFolders)
        {
            _packageFolders = additionalPackageFolders.Prepend(SettingsUtility.GetGlobalPackagesFolder(settings))
                                                      .Where(f => !string.IsNullOrEmpty(f))
                                                      .Distinct()
                                                      .ToArray();
        }

        public IWrappedPackageMetadata? GetPackage(PackageIdentity identity)
        {
            foreach (string packageFolder in _packageFolders)
            {
                IWrappedPackageMetadata? metadata = TryGetPackageFromFolder(identity, packageFolder);
                if (metadata is not null)
                {
                    return metadata;
                }
            }

            return null;
        }

        private static IWrappedPackageMetadata? TryGetPackageFromFolder(PackageIdentity identity, string packageFolder)
        {
            DownloadResourceResult cachedPackage = OriginalGlobalPackagesFolderUtility.GetPackage(new OriginalPackageIdentity(identity.Id, new NuGetVersion(identity.Version.ToString()!)), packageFolder);
            if (cachedPackage == null)
            {
                return null;
            }

            using PackageReaderBase pkgStream = cachedPackage.PackageReader;
            var manifest = Manifest.ReadFrom(pkgStream.GetNuspec(), true);

            if (manifest.Metadata.Version is not { } manifestVersion ||
                !new WrappedNuGetVersion(manifestVersion).Equals(identity.Version))
            {
                return null;
            }

            var result = new WrappedPackageMetadata(manifest.Metadata);
            if (result.LicenseMetadata?.Type == Packaging.LicenseType.File)
            {
                string normalizedPath = NuGet.Common.PathUtility.GetPathWithDirectorySeparator(result.LicenseMetadata.License);
                using Stream licenseStream = pkgStream.GetStream(normalizedPath);
                using var reader = new StreamReader(licenseStream);
                return new LicenseAugmentedPackageMetadata(result, reader.ReadToEnd());
            }

            return result;
        }
    }
}
