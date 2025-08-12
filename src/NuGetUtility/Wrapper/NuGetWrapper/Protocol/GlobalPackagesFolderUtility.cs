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
    internal class GlobalPackagesFolderUtility : IGlobalPackagesFolderUtility
    {
        private static readonly Uri s_deprecatedLicenseUrl = new Uri("https://aka.ms/deprecateLicenseUrl");
        private const string NugetLicenseUrl = "https://www.nuget.org/packages/{0}/{1}/License";

        private readonly string _globalPackagesFolder;

        public GlobalPackagesFolderUtility(ISettings settings)
        {
            _globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
        }

        public IWrappedPackageMetadata? GetPackage(PackageIdentity identity, DeprecatedLicenseAction licenseFileAction)
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

            Uri? licenseUrl = null;

            if (licenseFileAction == DeprecatedLicenseAction.Link && manifest.Metadata.LicenseUrl != null
                                                                  && manifest.Metadata.LicenseUrl.Equals(s_deprecatedLicenseUrl))
            {
                licenseUrl = new Uri(string.Format(NugetLicenseUrl, manifest.Metadata.Id, manifest.Metadata.Version));
            }

            return new WrappedPackageMetadata(manifest.Metadata, licenseUrl);
        }
    }
}
