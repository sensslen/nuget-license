# Ignored Packages JSON File Format (`--ignored-packages`)

The ignored packages JSON file is used with the `-ignore` or `--ignored-packages` option to specify NuGet packages to skip during validation.

## Format

The file must contain a JSON array of package names. Wildcards (`*`) are supported:

```json
[
  "MyCompany.*",
  "TestPackage",
  "LegacyLib*"
]
```

Each entry should be a string representing a package name or pattern.
