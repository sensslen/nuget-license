# Allowed Licenses (`--allowed-license-types`)

The `-a` or `--allowed-license-types` option is used to specify which license types are permitted.

## Input Format

You can provide the allowed licenses in two ways:

### 1. JSON File

Provide a path to a JSON file containing an array of license identifiers (SPDX or custom):

```json
[
  "MIT",
  "Apache-2.0",
  "BSD-3-Clause"
]
```

**Example usage:**
```bash
nuget-license -i MyProject.csproj -a allowed-licenses.json
```

### 2. Inline Semicolon-Separated List

Provide a semicolon-separated list of license identifiers directly on the command line:

**Example usage:**
```bash
nuget-license -i MyProject.csproj -a "MIT;Apache-2.0;BSD-3-Clause"
```

**Note:** When using inline format, make sure to quote the entire list to prevent shell interpretation of special characters.

## Format Detection

The tool automatically detects whether the input is a file path or an inline list:
- If a file exists at the specified path, it will be read as a JSON file
- Otherwise, the input will be parsed as a semicolon-separated inline list

**Important:** If you have a file in your current directory with a name that matches your inline value (e.g., a file named "MIT"), the tool will read from the file instead of parsing it as an inline value. In such cases, use a different file name or provide a full/relative path to disambiguate.

## License Identifiers

Each entry should be a string representing a license type. License identifiers can be:
- SPDX license identifiers (e.g., `MIT`, `Apache-2.0`, `GPL-3.0`)
- Custom license names
- SPDX license expressions (e.g., `MIT OR Apache-2.0`, `GPL-2.0-or-later WITH Classpath-exception-2.0`)
