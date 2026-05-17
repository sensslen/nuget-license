// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Wrapper.NuGetWrapper.Protocol
{
    using IWrappedPackageMetadata = NuGetUtility.Wrapper.NuGetWrapper.Packaging.IPackageMetadata;

    internal class WrappedPackageMetadata(ManifestMetadata metadata) : IWrappedPackageMetadata
    {
        public PackageIdentity Identity { get; } = new(metadata.Id ?? throw new ArgumentException("Id must not be null", nameof(metadata)),
                                                       new WrappedNuGetVersion(metadata.Version ?? throw new ArgumentException("Version must not be null", nameof(metadata))));

        public string? Title => metadata.Title;

        public Uri? LicenseUrl => metadata.LicenseUrl;

        public string? ProjectUrl => metadata.ProjectUrl?.ToString();

        public string? Description => metadata.Description;

        public string? Summary => metadata.Summary;

        public string? Copyright => metadata.Copyright;

        public string? Authors => string.Join(",", metadata.Authors); // https://learn.microsoft.com/en-us/nuget/reference/nuspec#authors

        public Packaging.LicenseMetadata? LicenseMetadata { get; } = metadata.LicenseMetadata;
    }
}
