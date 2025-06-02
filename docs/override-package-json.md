# Override Package Information JSON File Format (`--override-package-information`)

The override package information JSON file is used with the `-override` or `--override-package-information` option to supply custom license and metadata for specific packages. This is useful when a package's license metadata is missing, incorrect, or needs to be supplemented.

## Format

The file must contain a JSON array of objects, each specifying a package and its information.

```json
[
  {
    "Id": "SomePackage",
    "Version": "1.2.3",
    "License": "MIT",
    "Copyright": "Copyright (c) 2023 SomeCompany",
    "Authors": "John Doe;Jane Smith",
    "Title": "Some Package Title",
    "ProjectUrl": "https://example.com/project",
    "Summary": "A short summary of the package.",
    "Description": "A longer description of the package.",
    "LicenseUrl": "https://opensource.org/licenses/MIT"
  },
  {
    "Id": "OtherPackage",
    "Version": "2.0.0",
    "License": "Apache-2.0"
  }
]
```

### Supported Fields

- `Id` (required): The NuGet package name.
- `Version` (required): The specific version to override (as a string, e.g., `"1.2.3"`).
- `License` (required): The license identifier (e.g., SPDX ID or license name).
- `Copyright` (optional): Copyright statement.
- `Authors` (optional): Author(s) of the package (semicolon-separated if multiple).
- `Title` (optional): Package title.
- `ProjectUrl` (optional): URL to the project site.
- `Summary` (optional): Short summary of the package.
- `Description` (optional): Detailed description of the package.
- `LicenseUrl` (optional): URL to the license text.

**Notes:**
- `Id`, `Version`, and `License` are required. All other fields are optional.
- `Version` must match the NuGet version format.
- `LicenseUrl` should be a valid URL string.

**Example:**

```json
[
  {
    "Id": "Example.Package",
    "Version": "3.1.0",
    "License": "BSD-3-Clause",
    "Title": "Example Package",
    "ProjectUrl": "https://example.com"
  }
]
```
