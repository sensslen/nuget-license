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

## Format Detection

The tool automatically detects whether the input is a file path or an inline list:
- If a file exists at the specified path, it will be read as a JSON file
- Otherwise, the input will be parsed as a semicolon-separated inline list

**Important:** If you have a file in your current directory with a name that matches your inline value, the tool will read from the file instead of parsing it as an inline value. In such cases, use a different file name or provide a full/relative path to disambiguate.

## Package Names

Each entry should be a string representing a package name or pattern:
- Exact match: `"PackageName"`
- Prefix wildcard: `"PackageName*"`
- Suffix wildcard: `"*PackageName"`
- Contains: `"*PartialName*"`
