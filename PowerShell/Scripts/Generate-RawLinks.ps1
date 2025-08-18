#requires -Version 7.0
<#[
    .SYNOPSIS
    Generates comprehensive raw GitHub link indices (TXT, CSV, JSON) for all tracked files.

    .DESCRIPTION
    Produces the following at repo root:
      - RAW-LINKS.txt           -> branch-based raw URLs
      - RAW-LINKS-PINNED.txt    -> SHA-pinned raw URLs
      - raw-index.csv           -> path, url_branch, url_sha (CSV)
      - raw-index.json          -> path, url_branch, url_sha (JSON)

    Implements documentation-first and Microsoft PowerShell standards:
      - Output via Write-Information/Write-Output (no Write-Host)
      - Proper parameter validation and error handling
      - Reference: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/cmdlet-overview
                   https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

    .EXAMPLE
    pwsh -File Scripts/Generate-RawLinks.ps1 -Verbose
]#>

[CmdletBinding()]
param(
    [Parameter()] [string] $RepositoryFallback = 'Bigessfour/BusBuddy-3',
    [Parameter()] [string] $OutputRoot = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepositoryId {
    [CmdletBinding()] param()
    try {
        $remote = (git remote get-url origin).Trim()
        if ($remote -match 'github.com[:/]([^/]+)/([^/.]+)') {
            return "{0}/{1}" -f $Matches[1], $Matches[2]
        }
    }
    catch {
        Write-Verbose "Unable to read git remote origin: $($_.Exception.Message)"
    }
    return $RepositoryFallback
}

function Encode-PathSegment {
    [CmdletBinding()] param(
        [Parameter(Mandatory, ValueFromPipeline)] [string] $Path
    )
    process {
        # Encode each segment but preserve slashes for GitHub raw URLs
        # Note: Wrap the pipeline in parentheses before using -join to avoid it binding
        # to ForEach-Object's RemainingScripts parameter (PS 7.x parsing behavior).
        (( $Path -split '/' ) | ForEach-Object { [System.Uri]::EscapeDataString($_) }) -join '/'
    }
}

try {
    # Ensure we are at repo root (contains .git)
    if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot '.git'))) {
        throw "OutputRoot '$OutputRoot' does not appear to be a git repository root (missing .git)."
    }

    Push-Location -LiteralPath $OutputRoot
    try {
        $repo = Resolve-RepositoryId
        $branch = (git rev-parse --abbrev-ref HEAD).Trim()
        $sha = (git rev-parse HEAD).Trim()

        $baseRawBranch = "https://raw.githubusercontent.com/$repo/$branch"
        $baseRawSha = "https://raw.githubusercontent.com/$repo/$sha"

        $files = git ls-files | Where-Object { $_ }
        if (-not $files) { throw 'No tracked files found (git ls-files returned empty).' }

        $txt = foreach ($f in $files) {
            $ep = $f | Encode-PathSegment
            "{0} -> {1}/{2}" -f $f, $baseRawBranch, $ep
        }

        $pinnedTxt = foreach ($f in $files) {
            $ep = $f | Encode-PathSegment
            "{0} -> {1}/{2}" -f $f, $baseRawSha, $ep
        }

        $objs = foreach ($f in $files) {
            $ep = $f | Encode-PathSegment
            [PSCustomObject]@{
                path       = $f
                url_branch = "$baseRawBranch/$ep"
                url_sha    = "$baseRawSha/$ep"
            }
        }

        # Write outputs with UTF8
        Set-Content -LiteralPath (Join-Path $OutputRoot 'RAW-LINKS.txt') -Value $txt -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $OutputRoot 'RAW-LINKS-PINNED.txt') -Value $pinnedTxt -Encoding UTF8
        $objs | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath (Join-Path $OutputRoot 'raw-index.csv') -Encoding UTF8
        $objs | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $OutputRoot 'raw-index.json') -Encoding UTF8

        Write-Information (
            [string]::Format(
                "Generated {0} entries for repo {1}; branch={2}, sha={3}",
                $files.Count, $repo, $branch, $sha
            )
        ) -InformationAction Continue

        Write-Output ([PSCustomObject]@{
                Repository = $repo
                Branch     = $branch
                Sha        = $sha
                Count      = $files.Count
                Outputs    = @('RAW-LINKS.txt', 'RAW-LINKS-PINNED.txt', 'raw-index.csv', 'raw-index.json')
            })
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Error -Message ("Raw link index generation failed: {0}" -f $_.Exception.Message) -ErrorAction Stop
}
