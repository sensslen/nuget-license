# Nuget License Utility [![Tests](https://github.com/sensslen/nuget-license/actions/workflows/action.yml/badge.svg)](https://github.com/sensslen/nuget-license/actions/workflows/action.yml) [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=sensslen_nuget-license&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=sensslen_nuget-license) [![NuGet](https://img.shields.io/nuget/v/nuget-license.svg)](https://www.nuget.org/packages/nuget-license)

**Nuget License Utility** is a tool to analyze, print, and validate the licenses of dependencies in .NET and C++ projects. It supports .NET (Core), .NET Standard, .NET Framework, and native C++ projects.

## Features

- Analyze project or solution files for NuGet package licenses
- Validate licenses against an allowed list
- Download license files for auditing
- Supports transitive dependencies, custom mappings, and overrides
- Flexible output: table or JSON (pretty/minified)
- Exclude or ignore specific packages or projects
- Works with .NET Core, .NET Framework, and native C++ projects

## Project Structure

This repository provides two main tools:

| Tool | Description | Supported Project Types |
|------|-------------|------------------------|
| **NuGetLicenseCore**<br/>(dotnet tool) | Cross-platform .NET Core global tool, installed via `dotnet tool install`. | .NET Core, .NET Standard, partial .NET Framework<sup>1</sup> |
| **NuGetLicenseFramework.exe** | Standalone .NET Framework executable. | .NET Core, .NET Standard, .NET Framework, native C++ |

<sup>1</sup> .NET Framework support via the dotnet tool may vary due to MSBuild/environment differences.

## Compatibility Matrix

| Tool | .NET Core | .NET Standard | .NET Framework | Native C++ |
|------|:---------:|:-------------:|:--------------:|:----------:|
| **NuGetLicenseCore**<br/>(dotnet tool) | ✔️ | ✔️ | ⚠️<br/>Partial support | ❌ |
| **NuGetLicenseFramework.exe** | ✔️ | ✔️ | ✔️ | ✔️ |

## Installation

### NuGetLicenseCore (dotnet tool)

```ps
dotnet tool install --global nuget-license
```

### NuGetLicenseFramework.exe

Download the latest release from [GitHub Releases](https://github.com/sensslen/nuget-license/releases) and run the executable directly.

## Usage

### Basic Command

```ps
nuget-license [options]
```

### Common Options

| Option | Description |
| ------ | ----------- |
| `--version` | Show version information. |
| `-i`, `--input <FILE>` | Project or solution file to analyze. |
| `-ji`, `--json-input <FILE>` | JSON file with an array of project/solution files to analyze. See [docs/input-json.md](docs/input-json.md). |
| `-t`, `--include-transitive` | Include transitive dependencies. |
| `-a`, `--allowed-license-types <FILE>` | JSON file listing allowed license types. See [docs/allowed-licenses-json.md](docs/allowed-licenses-json.md). |
| `-ignore`, `--ignored-packages <FILE>` | JSON file listing package names to ignore (supports wildcards). See [docs/ignored-packages-json.md](docs/ignored-packages-json.md). |
| `-mapping`, `--licenseurl-to-license-mappings <FILE>` | JSON dictionary mapping license URLs to license types. See [docs/licenseurl-mappings-json.md](docs/licenseurl-mappings-json.md). |
| `-override`, `--override-package-information <FILE>` | JSON list to override package/license info. See [docs/override-package-json.md](docs/override-package-json.md). |
| `-d`, `--license-information-download-location <FOLDER>` | Download all license files to the specified folder. |
| `-o`, `--output <TYPE>` | Output format: `Table`, `Json`, or `JsonPretty` (default: Table). |
| `-err`, `--error-only` | Only show validation errors. |
| `-include-ignored`, `--include-ignored-packages` | Include ignored packages in output. |
| `-exclude-projects`, `--exclude-projects-matching <PATTERN\|FILE>` | Exclude projects by name or pattern (supports wildcards or JSON file). See [docs/exclude-projects-json.md](docs/exclude-projects-json.md). |
| `-isp`, `--include-shared-projects` | Include shared projects (`.shproj`). |
| `-f`, `--target-framework <TFM>` | Analyze for a specific Target Framework Moniker. |
| `-fo`, `--file-output <FILE>` | Write output to a file instead of console. |
| `-?`, `-h`, `--help` | Show help information. |

## Examples

### Show Help

```ps
nuget-license --help
```

### Validate licenses for a project

```ps
nuget-license -i MyProject.csproj
```

### Validate licenses for a solution

```ps
nuget-license -i MySolution.sln
```

### Use a custom allowed license list

```ps
nuget-license -i MyProject.csproj -a allowed-licenses.json
```

### Generate pretty JSON output

```ps
nuget-license -i MyProject.csproj -o JsonPretty
```

### Download all license files

```ps
nuget-license -i MyProject.csproj -d licenses/
```

## Advanced Usage

- **Multiple projects:** Use `-ji` with a JSON file listing multiple projects/solutions.
- **Override package info:** Use `-override` to supply custom license info for specific packages.
- **Ignore packages:** Use `-ignore` to skip in-house or known packages.
- **Exclude projects:** Use `-exclude-projects` to skip test or sample projects.

## Building from Source

1. Clone the repository.
2. Build with your preferred .NET SDK.
3. For the dotnet tool: `dotnet pack NuGetLicenseCore`
4. For the framework exe: build `NuGetLicenseFramework` and use the resulting `.exe`.

## License

See [LICENSE](LICENSE) for details.
