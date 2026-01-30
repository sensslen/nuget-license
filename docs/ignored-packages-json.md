# Ignored Packages (`--ignored-packages`)

The `-ignore` or `--ignored-packages` option is used to specify NuGet packages to skip during validation.

**Note:** Even though packages are ignored, their transitive dependencies are not ignored unless explicitly listed.

## Input Format

You can provide the ignored packages in two ways:

### 1. JSON File

Provide a path to a JSON file containing an array of package names. Wildcards (`*`) are supported:

```json
[
  "MyCompany.*",
  "TestPackage",
  "LegacyLib*"
]
```

**Example usage:**
```bash
nuget-license -i MyProject.csproj -ignore ignored-packages.json
```

### 2. Inline Semicolon-Separated List

Provide a semicolon-separated list of package names directly on the command line. Wildcards (`*`) are supported:

**Example usage:**
```bash
nuget-license -i MyProject.csproj -ignore "MyCompany.*;TestPackage;LegacyLib*"
```

**Note:** When using inline format, make sure to quote the entire list to prevent shell interpretation of wildcards.

## Package Names

Each entry should be a string representing a package name or pattern:
- Exact match: `"PackageName"`
- Prefix wildcard: `"PackageName*"`
- Suffix wildcard: `"*PackageName"`
- Contains: `"*PartialName*"`
