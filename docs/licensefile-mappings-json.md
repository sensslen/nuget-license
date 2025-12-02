# License File to License Mappings JSON File Format (`--licensefile-to-license-mappings`)

The license file mappings JSON file is used with the `-file-mapping` or `--licensefile-to-license-mappings` option to map license files to license types.

## Format

The file must contain a JSON object where keys are license file paths and values are license identifiers:

```json
{
  "licenses/MIT.txt": "MIT",
  "licenses/Apache-2.0.txt": "Apache-2.0"
}
```

Each key is a path to a license file, and each value is the corresponding license type.

## Path Resolution

**Important:** License file paths in the JSON file are **relative to the directory containing the JSON file itself**.

### Example

If your JSON file is located at `/home/user/project/config/license-mappings.json`:

```json
{
  "licenses/MIT.txt": "MIT",
  "../external-licenses/Apache.txt": "Apache-2.0"
}
```

The tool will resolve the paths as:
- `licenses/MIT.txt` → `/home/user/project/config/licenses/MIT.txt`
- `../external-licenses/Apache.txt` → `/home/user/project/external-licenses/Apache.txt`

### Recommended Structure

For clarity and maintainability, it's recommended to place your license files in a directory relative to the JSON file:

```
project/
├── config/
│   └── license-mappings.json
└── licenses/
    ├── MIT.txt
    └── Apache-2.0.txt
```

Then reference them with simple relative paths in the JSON:

```json
{
  "../licenses/MIT.txt": "MIT",
  "../licenses/Apache-2.0.txt": "Apache-2.0"
}
```
