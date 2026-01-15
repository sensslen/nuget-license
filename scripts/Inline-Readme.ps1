#!/usr/bin/env pwsh
# This script processes the README.md file to inline all documentation from docs/*.md files
# This is necessary because NuGet.org only displays the README and doesn't make other markdown files accessible

param(
    [Parameter(Mandatory=$true)]
    [string]$ReadmePath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [Parameter(Mandatory=$true)]
    [string]$DocsFolder
)

Write-Host "Inlining documentation from $DocsFolder into README..."
Write-Host "Reading from: $ReadmePath"
Write-Host "Writing to: $OutputPath"

# Read the README content
$readmeContent = Get-Content -Path $ReadmePath -Raw

# Find all links to docs/*.md files
# Pattern matches: [text](docs/filename.md)
$pattern = '\[([^\]]+)\]\(docs/([^)]+\.md)\)'

$matches = [regex]::Matches($readmeContent, $pattern)

Write-Host "Found $($matches.Count) documentation links"

# Process each match and build replacement map
$replacements = @{}

foreach ($match in $matches) {
    $fullMatch = $match.Value
    $linkText = $match.Groups[1].Value
    $docFileName = $match.Groups[2].Value
    $docPath = Join-Path $DocsFolder $docFileName
    
    if (Test-Path $docPath) {
        Write-Host "Processing: $docFileName"
        
        # Read the doc file content
        $docContent = Get-Content -Path $docPath -Raw
        
        # Extract the title from the doc (first line starting with #)
        $titleMatch = [regex]::Match($docContent, '^\s*#\s+(.+)$', [System.Text.RegularExpressions.RegexOptions]::Multiline)
        $docTitle = if ($titleMatch.Success) { $titleMatch.Groups[1].Value.Trim() } else { $linkText }
        
        # Remove the title from content since we'll use it as a section header
        $docContentWithoutTitle = [regex]::Replace($docContent, '^\s*#\s+.+$', '', [System.Text.RegularExpressions.RegexOptions]::Multiline)
        $docContentWithoutTitle = $docContentWithoutTitle.Trim()
        
        # Create an anchor link (GitHub-style)
        # GitHub converts to lowercase, removes non-alphanumeric except spaces and hyphens,
        # converts spaces to hyphens, and removes consecutive/trailing hyphens
        $anchorId = $docTitle.ToLower()
        $anchorId = $anchorId -replace '[^a-z0-9\s-]', ''  # Remove special chars except lowercase alphanumeric, spaces, hyphens
        $anchorId = $anchorId -replace '\s+', '-'          # Convert spaces to hyphens
        $anchorId = $anchorId -replace '-+', '-'           # Remove consecutive hyphens
        $anchorId = $anchorId.Trim('-')                    # Remove leading/trailing hyphens
        
        # Create inline replacement: link to anchor using the title as link text
        $linkReplacement = "[${docTitle}](#${anchorId})"
        
        # Store the section content to append at the end
        if (-not $replacements.ContainsKey($docFileName)) {
            $replacements[$docFileName] = @{
                OriginalLink = $fullMatch
                NewLink = $linkReplacement
                Title = $docTitle
                Content = $docContentWithoutTitle
            }
        }
    }
    else {
        Write-Warning "Documentation file not found: $docPath"
    }
}

# Replace all links in the README
$inlinedReadme = $readmeContent
foreach ($replacement in $replacements.Values) {
    # Build a regex pattern that matches any link to this specific doc file
    # Pattern: \[any text\](docs/filename.md)
    $docFileName = $replacement.OriginalLink -replace '^\[([^\]]+)\]\(docs/([^)]+\.md)\)$', '$2'
    $linkPattern = '\[[^\]]+\]\(docs/' + [regex]::Escape($docFileName) + '\)'
    
    # Escape special regex characters in the replacement text, especially '$'
    $safeReplacement = $replacement.NewLink -replace '[\$]', '$$$$'
    
    $inlinedReadme = $inlinedReadme -replace $linkPattern, $safeReplacement
}

# Append all documentation sections at the end
if ($replacements.Count -gt 0) {
    $inlinedReadme += "`n`n---`n`n"
    $inlinedReadme += "## Additional Documentation`n`n"
    $inlinedReadme += "*The following sections provide detailed documentation for the various configuration files and options.*`n`n"
    
    foreach ($replacement in $replacements.Values | Sort-Object { $_.Title }) {
        $inlinedReadme += "### $($replacement.Title)`n`n"
        $inlinedReadme += "$($replacement.Content)`n`n"
    }
}

# Write the output
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$inlinedReadme | Set-Content -Path $OutputPath -NoNewline

Write-Host "Successfully created inlined README at: $OutputPath"
Write-Host "Inlined $($replacements.Count) documentation files"
