# Exclude Projects JSON File Format (`--exclude-projects-matching`)

The exclude projects JSON file is used with the `-exclude-projects` or `--exclude-projects-matching` option to specify projects to exclude from analysis.

## Format

The file must contain a JSON array of project names or patterns. Wildcards (`*`) are supported:

```json
[
  "*Test*",
  "SampleProject",
  "Legacy*"
]
```

Each entry should be a string representing a project name or pattern.
