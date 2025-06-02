# Allowed Licenses JSON File Format (`--allowed-license-types`)

The allowed licenses JSON file is used with the `-a` or `--allowed-license-types` option to specify which license types are permitted.

## Format

The file must contain a JSON array of license identifiers (SPDX or custom):

```json
[
  "MIT",
  "Apache-2.0",
  "BSD-3-Clause"
]
```

Each entry should be a string representing a license type.
