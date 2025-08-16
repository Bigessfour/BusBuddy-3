# Update raw index files with new entries
$newSha = $env:NEW_SHA
$baseSha = $env:BASE_SHA

Write-Output "Updating index files..."
Write-Output "Base SHA: $baseSha"
Write-Output "New SHA: $newSha"

# Get changed files
$changedFiles = git diff --name-only "$baseSha..$newSha"
Write-Output "Found $($changedFiles.Count) changed files"

# Generate new entries
$newEntries = @()
foreach ($file in $changedFiles) {
    $encodedPath = $file -replace ' ', '%20'  # Simple URL encoding for spaces
    $entry = [PSCustomObject]@{
        'path' = $file
        'url_branch' = "https://raw.githubusercontent.com/Bigessfour/BusBuddy-3/master/$encodedPath"
        'url_sha' = "https://raw.githubusercontent.com/Bigessfour/BusBuddy-3/$newSha/$encodedPath"
    }
    $newEntries += $entry
}

# Save new entries to a temp file for manual review
$newEntries | ConvertTo-Json -Depth 3 | Out-File -FilePath 'new-index-entries.json' -Encoding UTF8
Write-Output "Generated new entries in: new-index-entries.json"
Write-Output "Total new entries: $($newEntries.Count)"

# Show first few entries as sample
Write-Output "`nSample entries:"
$newEntries | Select-Object -First 5 | Format-Table path, url_sha
