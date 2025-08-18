#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$RepoSlug = 'Bigessfour/BusBuddy-3',
    [string]$ReadmePath = 'GROK-README.md',
    [string]$IndexJson = 'raw-index.json',
    [switch]$RegenerateIndex,
    [switch]$NoGit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CurrentBranch {
    try { (git rev-parse --abbrev-ref HEAD).Trim() } catch { 'master' }
}

function Get-CurrentSha {
    try { (git rev-parse HEAD).Trim() } catch { '' }
}

Write-Verbose "Repo root: $(Get-Location)"

if ($RegenerateIndex -or -not (Test-Path -LiteralPath $IndexJson)) {
    $generator = Join-Path -Path (Get-Location) -ChildPath 'PowerShell/Scripts/Generate-RawLinks.ps1'
    if (Test-Path -LiteralPath $generator) {
        Write-Verbose 'Generating RAW-LINKS and raw-index artifacts...'
        & pwsh -File $generator -Verbose
    }
}

if (-not (Test-Path -LiteralPath $IndexJson)) {
    throw "Index file not found: $IndexJson"
}

$meta = Get-Content -LiteralPath $IndexJson -Raw | ConvertFrom-Json
if (-not $meta -or -not $meta.Count) {
    throw "Index file appears empty or invalid: $IndexJson"
}

$branch = Get-CurrentBranch
$sha = Get-CurrentSha

$rawLinks = "https://raw.githubusercontent.com/$RepoSlug/$branch/RAW-LINKS.txt"
$rawPinned = "https://raw.githubusercontent.com/$RepoSlug/$branch/RAW-LINKS-PINNED.txt"
$rawIdxJson = "https://raw.githubusercontent.com/$RepoSlug/$branch/raw-index.json"
$rawIdxCsv = "https://raw.githubusercontent.com/$RepoSlug/$branch/raw-index.csv"

Write-Verbose "Appending index for $($meta.Count) files to $ReadmePath..."

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Full file fetch index (raw.githubusercontent.com)')
[void]$sb.AppendLine()
[void]$sb.AppendLine(("Generated: {0}" -f (Get-Date -Format o)))
[void]$sb.AppendLine(("Repo: https://github.com/{0}" -f $RepoSlug))
[void]$sb.AppendLine(("Branch: {0}" -f $branch))
[void]$sb.AppendLine(("Commit: {0}" -f $sha))
[void]$sb.AppendLine()
[void]$sb.AppendLine(("Quick links: [RAW-LINKS.txt]({0}) · [RAW-LINKS-PINNED.txt]({1}) · [raw-index.json]({2}) · [raw-index.csv]({3})" -f $rawLinks, $rawPinned, $rawIdxJson, $rawIdxCsv))
[void]$sb.AppendLine()
[void]$sb.AppendLine('<details>')
[void]$sb.AppendLine(("<summary>All raw links (branch {0}) - {1} files</summary>" -f $branch, $meta.Count))
[void]$sb.AppendLine()
foreach ($f in $meta) {
    [void]$sb.AppendLine(("- [{0}]({1})" -f $f.path, $f.url_branch))
}
[void]$sb.AppendLine()
[void]$sb.AppendLine('</details>')

Add-Content -LiteralPath $ReadmePath -Value $sb.ToString() -Encoding UTF8

if (-not $NoGit) {
    Write-Verbose 'Staging and committing changes...'
    git add -- $ReadmePath 'RAW-LINKS.txt' 'RAW-LINKS-PINNED.txt' 'raw-index.csv' 'raw-index.json'
    if ($null -ne (git diff --cached --name-only)) {
        git commit -m "docs: append comprehensive raw fetch index to GROK-README.md; update raw link artifacts"
        git push origin $branch
    }
    else {
        Write-Verbose 'No changes detected to commit.'
    }
}

Write-Verbose 'Done.'
