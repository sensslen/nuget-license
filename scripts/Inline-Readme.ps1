#!/usr/bin/env pwsh
# This script processes the README.md file to replace documentation links with GitHub URLs
# This is necessary because NuGet.org doesn't support anchor links in markdown

param(
    [Parameter(Mandatory=$true)]
    [string]$ReadmePath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [Parameter(Mandatory=$true)]
    [string]$DocsFolder,
    
    [Parameter(Mandatory=$false)]
    [string]$GitCommitHash
)

Write-Host "Replacing documentation links with GitHub URLs..."
Write-Host "Reading from: $ReadmePath"
Write-Host "Writing to: $OutputPath"
if ($GitCommitHash) {
    Write-Host "Using git commit hash: $GitCommitHash"
}

# Read the README content
$readmeContent = Get-Content -Path $ReadmePath -Raw

# Determine the base GitHub URL
# If we have a git commit hash, use it; otherwise use 'main' branch
# Trim the git commit hash to remove any trailing whitespace or newlines
$gitRef = if ($GitCommitHash) { $GitCommitHash.Trim() } else { "main" }
$baseGitHubUrl = "https://github.com/sensslen/nuget-license/blob/$gitRef"

Write-Host "Base GitHub URL: $baseGitHubUrl"

# Find all links to docs/*.md files
# Pattern matches: [text](docs/filename.md)
$pattern = '\[([^\]]+)\]\(docs/([^)]+\.md)\)'

$matches = [regex]::Matches($readmeContent, $pattern)

Write-Host "Found $($matches.Count) documentation links"

# Replace all documentation links with GitHub URLs
$processedReadme = $readmeContent

foreach ($match in $matches) {
    $fullMatch = $match.Value
    $linkText = $match.Groups[1].Value
    $docFileName = $match.Groups[2].Value
    
    Write-Host "Processing: $docFileName -> $linkText"
    
    # Create GitHub URL for the documentation file
    $gitHubUrl = "$baseGitHubUrl/docs/$docFileName"
    
    # Create replacement link
    $replacement = "[$linkText]($gitHubUrl)"
    
    # Use literal string replacement to avoid issues with special characters
    $processedReadme = $processedReadme.Replace($fullMatch, $replacement)
}

# Add a note at the end about documentation
if ($matches.Count -gt 0) {
    $processedReadme += "`n`n---`n`n"
    $processedReadme += "## Documentation`n`n"
    $processedReadme += "For detailed documentation on configuration files and advanced usage, please refer to the documentation files linked above in the GitHub repository."
    if ($GitCommitHash) {
        $trimmedHash = $GitCommitHash.Trim()
        $processedReadme += " The links point to the documentation that was available at the time this package version was created (commit: ``$trimmedHash``)."
    }
    $processedReadme += "`n"
}

# Write the output
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$processedReadme | Set-Content -Path $OutputPath -NoNewline

Write-Host "Successfully created README with GitHub documentation links at: $OutputPath"
Write-Host "Replaced $($matches.Count) documentation links"
