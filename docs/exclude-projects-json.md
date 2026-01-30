# Exclude Projects (`--exclude-projects-matching`)

The `-exclude-projects` or `--exclude-projects-matching` option is used to specify projects to exclude from analysis. This is useful for excluding test projects when analyzing a solution.

## Input Format

You can provide the excluded projects in two ways:

### 1. JSON File

Provide a path to a JSON file containing an array of project names or patterns. Wildcards (`*`) are supported:

```json
[
  "*Test*",
  "SampleProject",
  "Legacy*"
]
```

**Example usage:**
```bash
nuget-license -i MySolution.sln -exclude-projects exclude-projects.json
```

### 2. Inline Semicolon-Separated List

Provide a semicolon-separated list of project names or patterns directly on the command line. Wildcards (`*`) are supported:

**Example usage:**
```bash
nuget-license -i MySolution.sln -exclude-projects "*Test*;SampleProject;Legacy*"
```

**Note:** When using inline format, make sure to quote the entire list to prevent shell interpretation of wildcards.

## Project Names

Each entry should be a string representing a project name or pattern:
- Exact match: `"ProjectName"`
- Prefix wildcard: `"ProjectName*"`
- Suffix wildcard: `"*ProjectName"`
- Contains: `"*PartialName*"`
- Multiple wildcards: `"*Test*.csproj"`
