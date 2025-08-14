<#
.SYNOPSIS
Compares SHAs found in raw-index.entries.json to the remote HEAD (master/main) to determine if the index is up-to-date.

.DESCRIPTION
Reads .\raw-index.entries.json, extracts commit SHAs robustly, fetches origin, resolves the default remote branch (origin/HEAD),
and compares against origin/master and origin/main. Outputs a concise status and optional environment variable.

.PARAMETER RawIndexPath
Path to the raw-index entries JSON file. Defaults to .\raw-index.entries.json.

.PARAMETER FailOnMismatch
If supplied, exits with code 1 when SHAs do not match any remote head.

.PARAMETER SetEnv
If supplied, sets the RAW_INDEX_STATUS environment variable to MATCH or MISMATCH.

.OUTPUTS
Writes human-readable status lines to the output stream.

.NOTES
PowerShell standards: Uses Write-Output/Write-Error and avoids Write-Host.

.LINK
https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
https://git-scm.com/docs/git-symbolic-ref
https://git-scm.com/docs/git-rev-parse
#>

[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string]$RawIndexPath = ".\raw-index.entries.json",

    [switch]$FailOnMismatch,
    [switch]$SetEnv
)

try {
    if (-not (Test-Path -LiteralPath $RawIndexPath)) {
        throw "File not found: $RawIndexPath"
    }

    $json   = Get-Content -Raw -LiteralPath $RawIndexPath | ConvertFrom-Json
    $shas   = $json | ForEach-Object { $_.url_sha } | ForEach-Object {
        if ($_ -match '[0-9a-fA-F]{40}') { $Matches[0] } else { ($_ -split '/')[6] }
    }
    $unique = $shas | Where-Object { $_ } | Sort-Object -Unique

    # Fetch remotes quietly
    git fetch origin --quiet | Out-Null

    # Determine default remote branch (origin/HEAD)
    $default = (git symbolic-ref -q --short refs/remotes/origin/HEAD 2>$null) -replace '^origin/',''
    if (-not $default) { $default = 'master' }

    $branches = @($default, 'master', 'main') | Select-Object -Unique
    $remotes  = [System.Collections.Generic.List[string]]::new()
    foreach ($b in $branches) {
        try {
            $rev = git rev-parse "origin/$b" 2>$null
            if ($LASTEXITCODE -eq 0 -and $rev) { $null = $remotes.Add($rev) }
        } catch { }
    }

    Write-Output ("Index SHAs:    " + ($unique  -join ', '))
    Write-Output ("Remote heads:  " + ($remotes -join ', '))

    $status = if ($remotes | Where-Object { $unique -contains $_ }) { 'MATCH' } else { 'MISMATCH' }
    Write-Output ("RAW_INDEX_STATUS: " + $status)

    if ($SetEnv) { $env:RAW_INDEX_STATUS = $status }

    if ($FailOnMismatch -and $status -ne 'MATCH') { exit 1 }
}
catch {
    Write-Error $_.Exception.Message
    exit 2
}
