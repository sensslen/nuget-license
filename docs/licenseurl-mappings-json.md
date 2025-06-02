# License URL to License Mappings JSON File Format (`--licenseurl-to-license-mappings`)

The license URL mappings JSON file is used with the `-mapping` or `--licenseurl-to-license-mappings` option to map license URLs to license types.

## Format

The file must contain a JSON object where keys are license URLs and values are license identifiers:

```json
{
  "https://opensource.org/licenses/MIT": "MIT",
  "https://www.apache.org/licenses/LICENSE-2.0": "Apache-2.0"
}
```

Each key is a license URL, and each value is the corresponding license type.
