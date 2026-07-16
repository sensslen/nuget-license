// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.ComponentModel;
using OriginalLicenseType = NuGet.Packaging.LicenseType;

namespace NuGetUtility.Wrapper.NuGetWrapper.Packaging
{
    public abstract record LicenseMetadata
    {
        private LicenseMetadata() { }

        public sealed record Expression(string License) : LicenseMetadata;

        public sealed record Overwrite(string License) : LicenseMetadata;

        public sealed record File(string FileLocation, string? LicenseText = null) : LicenseMetadata;

        public static implicit operator LicenseMetadata?(NuGet.Packaging.LicenseMetadata? metadata) => metadata switch
        {
            null => null,
            { Type: OriginalLicenseType.Expression } => new Expression(metadata.License),
            { Type: OriginalLicenseType.File } => new File(metadata.License),
            _ => throw new InvalidEnumArgumentException(nameof(metadata), (int)metadata.Type, typeof(OriginalLicenseType)),
        };
    }
}
