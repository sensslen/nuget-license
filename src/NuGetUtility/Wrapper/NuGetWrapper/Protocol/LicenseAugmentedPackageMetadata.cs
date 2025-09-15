// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using IWrappedPackageMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.IPackageMetadata;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol
{
    internal sealed class LicenseAugmentedPackageMetadata : IWrappedPackageMetadata
    {
        private readonly IWrappedPackageMetadata _metadata;
        private readonly string _licenseText;

        public LicenseAugmentedPackageMetadata(IWrappedPackageMetadata metadata, string licenseText)
        {
            if (metadata.LicenseMetadata?.Type != Packaging.LicenseType.File)
            {
                throw new ArgumentException("For LicenseType.File use the constructor with LicenseText parameter");
            }

            _metadata = metadata;
            _licenseText = licenseText;
        }

        public PackageIdentity Identity => _metadata.Identity;
        public string? Title => _metadata.Title;
        public Uri? LicenseUrl => _metadata.LicenseUrl;
        public string? ProjectUrl => _metadata.ProjectUrl;
        public string? Description => _metadata.Description;
        public string? Summary => _metadata.Summary;
        public string? Copyright => _metadata.Copyright;
        public string? Authors => _metadata.Authors;
        public Packaging.LicenseMetadata? LicenseMetadata => new Packaging.LicenseMetadata(Packaging.LicenseType.File, _licenseText);
    }
}
