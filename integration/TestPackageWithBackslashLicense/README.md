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

The package is intentionally built using a custom script (`create_package_with_backslash.sh` or `create_package_with_backslash.ps1`) that preserves backslashes in the license file path within the `.nuspec` file. This ensures the test accurately reflects edge cases that might be encountered in real-world scenarios.

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
../../.github/workflows/scripts/create_package_with_backslash.sh

# Test with nuget-license
cd ../..
dotnet run --project src/NuGetLicenseCore -- -i integration/ProjectWithBackslashLicenseReference/ProjectWithBackslashLicenseReference.csproj -t
```

## Not for Publishing

This package is **not meant for publishing** to NuGet.org or any public repository. It exists solely for internal testing purposes within the nuget-license repository.
