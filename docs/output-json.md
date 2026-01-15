# JSON Output Format (`--output Json` or `--output JsonPretty`)

The JSON output format provides machine-readable license validation results. Use `-o Json` for minified output or `-o JsonPretty` for formatted output.

## Format

The output is a JSON array of license validation result objects:

```json
[
  {
    "PackageId": "Newtonsoft.Json",
    "PackageVersion": "13.0.1",
    "PackageProjectUrl": "https://www.newtonsoft.com/json",
    "License": "MIT",
    "LicenseUrl": "https://licenses.nuget.org/MIT",
    "Copyright": "Copyright (c) 2007 James Newton-King",
    "Authors": "James Newton-King",
    "Description": "Json.NET is a popular high-performance JSON framework for .NET",
    "Summary": "JSON serialization library for .NET",
    "LicenseInformationOrigin": 0,
    "ValidationErrors": []
  }
]
```

## Fields

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `PackageId` | string | The NuGet package identifier |
| `PackageVersion` | string | The package version |
| `LicenseInformationOrigin` | number | How the license information was determined (see [License Information Origin](#license-information-origin)) |
| `ValidationErrors` | array | List of validation errors (empty if no errors) |

### Optional Fields

These fields may be `null` or omitted if not available in the package metadata:

| Field | Type | Description |
|-------|------|-------------|
| `PackageProjectUrl` | string | The project URL from package metadata |
| `License` | string | The license identifier (e.g., "MIT", "Apache-2.0") or expression |
| `LicenseUrl` | string | The license URL |
| `Copyright` | string | Copyright information from package metadata |
| `Authors` | string | Package authors (comma-separated) |
| `Description` | string | Package description from metadata |
| `Summary` | string | Package summary from metadata |

## License Information Origin

The `LicenseInformationOrigin` field indicates how the license was determined:

| Value | Name | Description |
|-------|------|-------------|
| `0` | Expression | License provided via SPDX expression in package metadata |
| `1` | Url | License determined by matching the package's license URL |
| `2` | Unknown | License origin unknown (usually with validation errors) |
| `3` | Ignored | Package was in the ignored packages list |
| `4` | Overwrite | License overridden via `--override-package-information` |
| `5` | File | License extracted from embedded license file in package |

## Validation Errors

When validation errors occur, the `ValidationErrors` array contains error objects:

```json
{
  "PackageId": "UnknownPackage",
  "PackageVersion": "1.0.0",
  "LicenseInformationOrigin": 2,
  "ValidationErrors": [
    {
      "Error": "No license information found",
      "Context": "ProjectName.csproj"
    }
  ]
}
```

Each error object has:
- `Error`: Description of the validation error
- `Context`: Where the error occurred (usually the project file path)

## Examples

### Valid Package with All Fields

```json
{
  "PackageId": "Newtonsoft.Json",
  "PackageVersion": "13.0.1",
  "PackageProjectUrl": "https://www.newtonsoft.com/json",
  "License": "MIT",
  "LicenseUrl": "https://licenses.nuget.org/MIT",
  "Copyright": "Copyright (c) 2007 James Newton-King",
  "Authors": "James Newton-King",
  "Description": "Json.NET is a popular high-performance JSON framework for .NET",
  "Summary": "JSON serialization library for .NET",
  "LicenseInformationOrigin": 0,
  "ValidationErrors": []
}
```

### Package with Validation Error

```json
{
  "PackageId": "UnlicensedPackage",
  "PackageVersion": "1.0.0",
  "LicenseInformationOrigin": 2,
  "ValidationErrors": [
    {
      "Error": "No license information found",
      "Context": "MyProject.csproj"
    }
  ]
}
```

### Package with License Expression (OR)

```json
{
  "PackageId": "DualLicensedPackage",
  "PackageVersion": "2.0.0",
  "License": "MIT OR Apache-2.0",
  "LicenseInformationOrigin": 0,
  "ValidationErrors": []
}
```

### Ignored Package

```json
{
  "PackageId": "MyCompany.InternalPackage",
  "PackageVersion": "1.0.0",
  "LicenseInformationOrigin": 3,
  "ValidationErrors": []
}
```

## Usage

### Generate JSON Output

```ps
nuget-license -i MyProject.csproj -o Json
```

### Generate Pretty-Printed JSON

```ps
nuget-license -i MyProject.csproj -o JsonPretty
```

### Save JSON to File

```ps
nuget-license -i MyProject.csproj -o JsonPretty -fo licenses.json
```

## Notes

- Fields with `null` values may be omitted from the JSON output
- The `PackageVersion` field is serialized as a string representation of the version
- License expressions follow [SPDX specification](https://spdx.org/licenses/) syntax
- Use `-err` or `--error-only` to include only packages with validation errors in the output
