# Test Package with Backslash in License Path

This is a test NuGet package designed to verify that nuget-license can properly handle license file paths containing backslashes on all platforms (Windows, Linux, macOS).

## Purpose

NuGet packages can specify license files using paths in the `.nuspec` file. While the NuGet SDK typically normalizes paths to forward slashes, some packages may contain backslashes in their license file paths. This test package ensures nuget-license handles such cases correctly across all platforms.

## Package Structure

- **Package ID**: `TestPackageWithBackslashLicense`
- **Version**: `1.0.0`
- **License File Path**: `licenses\MIT.txt` (with backslash)
- **License Type**: MIT

## How It's Built

The package is built using `nuget pack` with a custom `.nuspec` file that specifies backslashes in the license file path. The `nuget pack` command preserves the backslashes in the metadata exactly as written in the nuspec file, ensuring the test accurately reflects edge cases that might be encountered in real-world scenarios.

## CI Testing

This package is used in the GitHub Actions CI pipeline (`check_licenses` and `check_licenses_net472` jobs) to verify cross-platform compatibility. The test ensures that:

1. The package can be built successfully
2. Projects referencing this package can be restored
3. nuget-license can correctly parse and extract license information from the package on Linux, macOS, and Windows

## Local Testing

To test locally:

```bash
# Build the test package
cd integration/TestPackageWithBackslashLicense
dotnet build -c Release
nuget pack TestPackageWithBackslashLicense.nuspec -OutputDirectory ../LocalPackages

# Test with nuget-license
cd ../..
dotnet run --project src/NuGetLicenseCore -- -i integration/ProjectWithBackslashLicenseReference/ProjectWithBackslashLicenseReference.csproj -t
```

## Not for Publishing

This package is **not meant for publishing** to NuGet.org or any public repository. It exists solely for internal testing purposes within the nuget-license repository.
