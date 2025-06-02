# Input JSON File Format (`--json-input`)

The input JSON file is used with the `-ji` or `--json-input` option to specify multiple project or solution files for analysis.

## Format

The file must contain a JSON array of file paths (relative or absolute):

```json
[
  "src/ProjectA/ProjectA.csproj",
  "src/ProjectB/ProjectB.csproj",
  "MySolution.sln"
]
```

Each entry should point to a valid `.csproj`, `.vcxproj`, or `.sln` file.
