#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_DIR="$SCRIPT_DIR/../../../integration/TestPackageWithBackslashLicense"
BUILD_DIR="$PACKAGE_DIR/bin/Release/netstandard2.0"
OUTPUT_DIR="$SCRIPT_DIR/../../../integration/LocalPackages"
TEMP_DIR="/tmp/nuget_package_build"

# Clean up temp directory
rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

# Create directory structure
mkdir -p "$TEMP_DIR/lib/netstandard2.0"
mkdir -p "$TEMP_DIR/licenses"

# Copy files
cp "$BUILD_DIR/TestPackageWithBackslashLicense.dll" "$TEMP_DIR/lib/netstandard2.0/"
cp "$PACKAGE_DIR/licenses/MIT.txt" "$TEMP_DIR/licenses/"

# Create the nuspec file with backslashes
cat > "$TEMP_DIR/TestPackageWithBackslashLicense.nuspec" << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>TestPackageWithBackslashLicense</id>
    <version>1.0.0</version>
    <authors>Test</authors>
    <owners>Test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <license type="file">licenses\MIT.txt</license>
    <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>
    <description>Test package with license file path containing backslashes to verify cross-platform handling</description>
    <dependencies>
      <group targetFramework=".NETStandard2.0" />
    </dependencies>
  </metadata>
</package>
EOF

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Create the package using zip (nupkg is just a zip file)
cd "$TEMP_DIR"
zip -r "$OUTPUT_DIR/TestPackageWithBackslashLicense.1.0.0.nupkg" * > /dev/null

echo "Package created successfully at $OUTPUT_DIR/TestPackageWithBackslashLicense.1.0.0.nupkg"
