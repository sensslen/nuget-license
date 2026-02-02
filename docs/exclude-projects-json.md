# Exclude Projects (`--exclude-projects-matching`)

The `-exclude-projects` or `--exclude-projects-matching` option is used to specify projects to exclude from analysis. Common use cases include excluding test projects, sample projects, or build tools from the analysis when working with a solution file.

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

## Format Detection

The tool automatically detects whether the input is a file path or an inline list:
- If a file exists at the specified path, it will be read as a JSON file
- Otherwise, the input will be parsed as a semicolon-separated inline list

**Important:** If you have a file in your current directory with a name that matches your inline value, the tool will read from the file instead of parsing it as an inline value. In such cases, use a different file name or provide a full/relative path to disambiguate.

## Project Names

Each entry should be a string representing a project name or pattern:
- Exact match: `"ProjectName"`
- Prefix wildcard: `"ProjectName*"`
- Suffix wildcard: `"*ProjectName"`
- Contains: `"*PartialName*"`
- Multiple wildcards: `"*Test*.csproj"`
