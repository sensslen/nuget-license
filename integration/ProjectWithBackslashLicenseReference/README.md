# Project with Backslash License Reference

This test project is designed to verify that nuget-license can properly handle NuGet packages that contain backslashes in their license file paths across all platforms (Windows, Linux, macOS).

## Purpose

This project references the `TestPackageWithBackslashLicense` package, which intentionally contains a backslash in its license file path (`licenses\MIT.txt`). This setup tests the tool's ability to:

1. Correctly parse license metadata from packages with non-normalized paths
2. Handle platform-specific path separators appropriately
3. Successfully extract license content on all operating systems

## CI Integration

This project is included in the CI pipeline through the following workflow jobs:

- `check_licenses` (Ubuntu, macOS) - Tests with net8.0 and net9.0
- `check_licenses_net472` (Windows) - Tests with .NET Framework 4.7.2

## Local Testing

To test this project locally:

1. Build the test package:
   ```bash
   cd ../TestPackageWithBackslashLicense
   dotnet build -c Release
   nuget pack TestPackageWithBackslashLicense.nuspec -OutputDirectory ../LocalPackages
   ```

2. Restore this project:
   ```bash
   cd ../ProjectWithBackslashLicenseReference
   dotnet restore
   ```

3. Run nuget-license:
   ```bash
   cd ../..
   dotnet run --project src/NuGetLicenseCore -- -i integration/ProjectWithBackslashLicenseReference/ProjectWithBackslashLicenseReference.csproj -t
   ```

## Expected Behavior

When the backslash path handling is implemented correctly, nuget-license should:
- Successfully locate the license file in the package
- Extract the license content
- Display MIT as the license type
- Complete without errors on all platforms

## Current Behavior (Before Fix)

On Linux and macOS, the tool currently fails with:
```
System.IO.FileNotFoundException: Could not find file '/home/runner/.nuget/packages/testpackagewithbackslashlicense/1.0.0/licenses\MIT.txt'
```

This occurs because the backslash in the path is not converted to a forward slash for Unix-based systems.

## Configuration Files

The project includes minimal configuration for testing:
- `nuget.config` - Points to the local package feed
- No special licenses or overrides required
