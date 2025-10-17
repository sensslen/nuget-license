$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDir = Join-Path $scriptDir "..\..\..\\integration\\TestPackageWithBackslashLicense"
$buildDir = Join-Path $packageDir "bin\\Release\\netstandard2.0"
$outputDir = Join-Path $scriptDir "..\..\..\\integration\\LocalPackages"
$tempDir = Join-Path $env:TEMP "nuget_package_build"

# Clean up temp directory
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force
}
New-Item -Path $tempDir -ItemType Directory | Out-Null

# Create directory structure
New-Item -Path (Join-Path $tempDir "lib\\netstandard2.0") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $tempDir "licenses") -ItemType Directory -Force | Out-Null

# Copy files
Copy-Item -Path (Join-Path $buildDir "TestPackageWithBackslashLicense.dll") -Destination (Join-Path $tempDir "lib\\netstandard2.0\\")
Copy-Item -Path (Join-Path $packageDir "licenses\\MIT.txt") -Destination (Join-Path $tempDir "licenses\\")

# Create the nuspec file with backslashes
$nuspecContent = @'
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
'@

$nuspecContent | Out-File -FilePath (Join-Path $tempDir "TestPackageWithBackslashLicense.nuspec") -Encoding utf8

# Create output directory
if (-not (Test-Path $outputDir)) {
    New-Item -Path $outputDir -ItemType Directory | Out-Null
}

# Create the package using Compress-Archive (nupkg is just a zip file)
$packagePath = Join-Path $outputDir "TestPackageWithBackslashLicense.1.0.0.nupkg"
if (Test-Path $packagePath) {
    Remove-Item -Path $packagePath -Force
}

# Use .NET's ZipFile class for better compatibility
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $packagePath)

Write-Host "Package created successfully at $packagePath"
