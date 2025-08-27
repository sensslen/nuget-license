// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using System.Text;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.ZipArchiveWrapper;

namespace NuGetUtility.PackageInformationReader;

public sealed class PackageLicenseFileReader(IFileSystem fileSystem, IZipArchiveWrapper zipArchive, string profilePath)
    : IPackageLicenseFileReader
{
    public async Task ReadLicenseFromFileAsync(IPackageMetadata metadata)
    {
        string? licenseFilePath = metadata.LicenseMetadata?.License;

        // Get the package file path - this depends on your package source
        string packageFilePath = GetPackageFilePath(metadata.Identity);

        if (!fileSystem.File.Exists(packageFilePath))
        {
            return;
        }

        try
        {
            using FileSystemStream fileStream = fileSystem.FileStream.New(packageFilePath, FileMode.Open, FileAccess.Read); ;
            using IZipArchive archive = zipArchive.Open(fileStream);

            if (licenseFilePath != null)
            {
                IZipArchiveEntry? licenseEntry = archive.GetEntry(licenseFilePath);
                if (licenseEntry == null)
                {
                    return;
                }

                using Stream entryStream = licenseEntry.Open();
                using var reader = new StreamReader(entryStream, Encoding.UTF8);

                // Read the license file into the metadata
                metadata.LicenseFileContent = await reader.ReadToEndAsync();
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private string GetPackageFilePath(PackageIdentity identity)
    {
        string userProfile = profilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string versionString = identity.Version.ToString() ?? "unknown";

        return fileSystem.Path.Combine(userProfile, ".nuget", "packages",
            identity.Id.ToLowerInvariant(),
            versionString.ToLowerInvariant(),
            $"{identity.Id.ToLowerInvariant()}.{versionString.ToLowerInvariant()}.nupkg");
    }
}
