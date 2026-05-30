// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;

namespace NuGetUtility.PackageInformationReader
{
    internal sealed class OverridePackageMetadata : IPackageMetadata
    {
        private readonly IPackageMetadata _metadata;
        private readonly CustomPackageInformation _customPackageInformation;

        public OverridePackageMetadata(IPackageMetadata metadata, CustomPackageInformation customPackageInformation)
        {
            _metadata = metadata;
            _customPackageInformation = customPackageInformation;
        }

        public PackageIdentity Identity => _metadata.Identity;

        public string? Title => _customPackageInformation.Title ?? _metadata.Title;

        public Uri? LicenseUrl => _customPackageInformation.LicenseUrl ?? _metadata.LicenseUrl;

        public string? ProjectUrl => _customPackageInformation.ProjectUrl ?? _metadata.ProjectUrl;

        public string? Description => _customPackageInformation.Description ?? _metadata.Description;

        public string? Summary => _customPackageInformation.Summary ?? _metadata.Summary;

        public string? Copyright => _customPackageInformation.Copyright ?? _metadata.Copyright;

        public string? Authors => _customPackageInformation.Authors ?? _metadata.Authors;

        public LicenseMetadata? LicenseMetadata => new(LicenseType.Overwrite, _customPackageInformation.License);
    }
}
