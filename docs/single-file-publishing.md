# Single-File Publishing

This document describes the single-file publishing configuration for the nuget-license application.

## Overview

The nuget-license application can be published as a single-file executable for standalone distribution. This provides several benefits:

- **Simplified deployment**: A single executable file instead of multiple DLLs
- **No runtime dependency**: Users can download and run without installing the .NET runtime
- **Easier distribution**: Convenient for users who want a standalone tool without package managers

## Configuration

Single-file publishing is configured in `src/NuGetLicenseCore/NuGetLicenseCore.csproj`:

```xml
<!-- Enable single-file publishing for standalone executables -->
<PropertyGroup Condition="'$(RuntimeIdentifier)' != ''">
  <PublishSingleFile>true</PublishSingleFile>
  <!-- Disable trimming due to Microsoft.Build compatibility issues -->
  <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

The configuration is conditional on `RuntimeIdentifier` being set, which means:
- Normal builds and dotnet tool packaging remain unchanged (framework-dependent)
- Publishing with a runtime identifier (e.g., `linux-x64`, `win-x64`) produces a single-file executable

## Trimming Evaluation

We evaluated enabling IL trimming to reduce binary size, but encountered compatibility issues:

### Issues with Trimming

1. **Microsoft.Build compatibility**: The application uses Microsoft.Build to parse project files, which relies heavily on reflection and runtime type loading. Trimming causes `TypeLoadException` errors.

2. **Reflection-heavy dependencies**: Several dependencies use reflection extensively:
   - Microsoft.Build and Microsoft.Build.Framework
   - NuGet.ProjectModel, NuGet.Packaging, NuGet.Configuration
   - Newtonsoft.Json
   - System.Text.Json for custom converters

3. **Trim modes tested**:
   - `TrimMode=partial`: Failed with `TypeLoadException` for `System.Threading.LockRecursionPolicy`
   - `TrimMode=copyused`: Failed with `TypeLoadException` for `System.Object`

### Conclusion on Trimming

**Trimming is not compatible** with this application due to the extensive use of Microsoft.Build and reflection-based NuGet libraries. The application requires these types to be available at runtime for dynamic project analysis.

## Binary Size Comparison

| Build Type | Size | File Count | Notes |
|-----------|------|------------|-------|
| Framework-dependent (baseline) | 16 MB | 44 files | Requires .NET runtime installed |
| Self-contained, single-file | 85 MB | 1 file | Includes .NET runtime |
| Self-contained, single-file, trimmed (failed) | N/A | N/A | Runtime errors due to trimming |

The self-contained single-file executable is larger because it includes the entire .NET runtime. This is expected and provides the benefit of not requiring users to install .NET separately.

## Publishing Commands

### For release builds

The release workflow publishes single-file executables for multiple platforms:

```bash
# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained -f net10.0

# Linux ARM64
dotnet publish -c Release -r linux-arm64 --self-contained -f net10.0

# Windows x64
dotnet publish -c Release -r win-x64 --self-contained -f net10.0

# Windows ARM64
dotnet publish -c Release -r win-arm64 --self-contained -f net10.0

# macOS x64
dotnet publish -c Release -r osx-x64 --self-contained -f net10.0

# macOS ARM64 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -f net10.0
```

### For local testing

```bash
# Framework-dependent (smaller, requires .NET runtime)
dotnet publish -c Release -f net10.0

# Self-contained single-file for current platform
dotnet publish -c Release -r <RID> --self-contained -f net10.0
```

Where `<RID>` is your runtime identifier (e.g., `linux-x64`, `win-x64`, `osx-x64`, `osx-arm64`).

## Trade-offs

### Advantages of Single-File

- ✅ Simplified distribution and deployment
- ✅ Single file to manage and distribute
- ✅ No dependency on installed .NET runtime
- ✅ Easier to manage and distribute via GitHub Releases

### Disadvantages

- ❌ Larger file size (includes .NET runtime)
- ❌ Platform-specific builds required
- ❌ Cannot benefit from trimming due to compatibility issues
- ❌ Initial startup may involve extraction overhead

## Recommendations

1. **For end users**: Use the self-contained single-file executables from GitHub Releases
2. **For developers**: Use the dotnet tool (`dotnet tool install --global nuget-license`)
3. **For CI/CD**: Use the framework-dependent version or dotnet tool to avoid large binary sizes

## Future Improvements

Potential areas for improvement:

1. **NativeAOT**: Explore Native AOT compilation once Microsoft.Build adds support
2. **Modular architecture**: Split MSBuild functionality into an optional plugin to enable trimming for the core
3. **ReadyToRun**: Consider using ReadyToRun compilation for faster startup while maintaining compatibility
