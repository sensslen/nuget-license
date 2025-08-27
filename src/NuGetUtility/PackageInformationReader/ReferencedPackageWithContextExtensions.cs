// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging;

namespace NuGetUtility.PackageInformationReader
{
    public static class ReferencedPackageWithContextExtensions
    {
        public static async Task<ReferencedPackageWithContext> WithFileLicenseExtractedAsync(
            this ReferencedPackageWithContext referencedPackageWithContext, IPackageLicenseFileReader packageLicenseFileReader)
        {
            if (referencedPackageWithContext.PackageInfo.LicenseMetadata?.Type == LicenseType.File)
            {
                await packageLicenseFileReader.ReadLicenseFromFileAsync(referencedPackageWithContext.PackageInfo);
            }

            return referencedPackageWithContext;
        }
    }
}
