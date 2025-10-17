# Integration Test Projects

This directory contains integration test projects used to verify nuget-license functionality in real-world scenarios.

## Test Projects

### ProjectWithReferenceContainingLicenseExpression
Tests handling of packages that use SPDX license expressions in their metadata.

### ProjectWithReferenceContainingFileLicense  
Tests handling of packages that specify licenses as files (with forward slashes in paths).

### ProjectWithBackslashLicenseReference
Tests handling of packages that specify license files with **backslashes** in their paths. This is an edge case that verifies cross-platform path handling.

**Test Package**: `TestPackageWithBackslashLicense`

This test ensures nuget-license can properly handle license file paths containing backslashes on all platforms (Windows, Linux, macOS). While the NuGet SDK typically normalizes paths to forward slashes, some packages in the wild may contain backslashes, and the tool must handle these correctly.

## Local Package Feed

The `LocalPackages/` directory serves as a local NuGet feed for test packages that are built during CI runs. These packages are not meant for publishing and are excluded from version control.

## CI Testing

All integration test projects are tested in the CI pipeline:
- **check_licenses** job (Ubuntu, macOS): Tests with .NET 8.0 and 9.0
- **check_licenses_net472** job (Windows): Tests with .NET Framework 4.7.2

## Building Test Packages

Test packages with special characteristics (like backslashes in license paths) use custom `.nuspec` files and are built with `nuget pack` to preserve those characteristics as specified in the nuspec metadata.
