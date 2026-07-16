// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using IWrappedPackageMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.IPackageMetadata;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol
{
    internal sealed class LicenseAugmentedPackageMetadata : IWrappedPackageMetadata
    {
        private readonly IWrappedPackageMetadata _metadata;
        private readonly string _licenseText;
        private readonly string _licenseFileLocation;

        public LicenseAugmentedPackageMetadata(IWrappedPackageMetadata metadata, string licenseText)
        {
            if (metadata.LicenseMetadata is not Packaging.LicenseMetadata.File file)
            {
                throw new ArgumentException("License augmentation is only applicable to file licenses");
            }

            _metadata = metadata;
            _licenseText = licenseText;
            _licenseFileLocation = file.FileLocation;
        }

        public PackageIdentity Identity => _metadata.Identity;
        public string? Title => _metadata.Title;
        public Uri? LicenseUrl => _metadata.LicenseUrl;
        public string? ProjectUrl => _metadata.ProjectUrl;
        public string? Description => _metadata.Description;
        public string? Summary => _metadata.Summary;
        public string? Copyright => _metadata.Copyright;
        public string? Authors => _metadata.Authors;
        public Packaging.LicenseMetadata? LicenseMetadata => new Packaging.LicenseMetadata.File(_licenseFileLocation, _licenseText);
    }
}
