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
        $docContentWithoutTitle = $docContent -replace '^\s*#\s+.+$', ''
        $docContentWithoutTitle = $docContentWithoutTitle.Trim()
        
        # Create an anchor link (GitHub-style)
        $anchorId = $docTitle.ToLower() -replace '[^\w\s-]', '' -replace '\s+', '-'
        
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
    $inlinedReadme = $inlinedReadme -replace [regex]::Escape($replacement.OriginalLink), $replacement.NewLink
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
