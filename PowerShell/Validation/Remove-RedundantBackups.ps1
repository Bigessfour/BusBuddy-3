#requires -Version 7.0
<#!
.SYNOPSIS
    Finds and (optionally) removes redundant backup/temp files like "* (1).*", "* (2).*", and "*.backup".

.DESCRIPTION
    Recursively scans a workspace and targets common duplicate/backup artifacts created by editors or downloads,
    specifically files matching the patterns:
      - * (1).*
      - * (2).*
      - *.backup

    The command supports -WhatIf/-Confirm via SupportsShouldProcess (about_ShouldProcess):
    https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_ShouldProcess

    Deletions use Remove-Item with -LiteralPath and -Force (Remove-Item docs):
    https://learn.microsoft.com/powershell/module/microsoft.powershell.management/remove-item

    Output streams follow Microsoft guidance — no Write-Host. See:
    https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

.PARAMETER Root
    Root directory to scan. Defaults to repository root (two levels up from this script).

.PARAMETER IncludePatterns
    Wildcard patterns to include. Defaults to the requested three patterns.

.PARAMETER ExcludePathRegex
    Regex to exclude typical build/output folders from the scan.

.PARAMETER PassThru
    Returns the list of matched files (objects) instead of only writing progress.

.EXAMPLE
    # Dry-run — list what would be removed
    .\Remove-RedundantBackups.ps1 -WhatIf

.EXAMPLE
    # Actually remove matching files (with confirmation prompts)
    .\Remove-RedundantBackups.ps1 -Confirm

#>
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string]$Root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..' '..')).Path,

    [string[]]$IncludePatterns = @(
        '* (1).*',
        '* (2).*',
        '*.backup'
    ),

    # Match either backslash or forward slash as path separators, and common build/output folders
    # Note: Using a character class [\\/] ensures proper matching of both separators in regex
    [string]$ExcludePathRegex = '[\\/](bin|obj|logs|TestResults|\.git|\.vs)(?=[\\/]|$)',

    [switch]$PassThru,

    # Include hidden/system files and directories in the scan (adds -Force to Get-ChildItem)
    [switch]$IncludeHidden
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Root directory not found: $Root"
    }

    Write-Information ("Scanning: {0}" -f $Root) -InformationAction Continue
    Write-Information ("Include patterns: {0}" -f ($IncludePatterns -join ', ')) -InformationAction Continue

    # Gather files efficiently, exclude build/system folders unless caller disabled via ExcludePathRegex
    # IncludeHidden adds -Force to traverse hidden/system items (e.g., .git, .vs) when ExcludePathRegex permits
    $gciParams = @{ LiteralPath = $Root; Recurse = $true; File = $true; ErrorAction = 'SilentlyContinue' }
    if ($IncludeHidden) { $gciParams['Force'] = $true }
    $allFiles = Get-ChildItem @gciParams |
        Where-Object { $_.FullName -notmatch $ExcludePathRegex }

    if (-not $allFiles) {
        Write-Output 'No files found to evaluate.'
        return
    }

    # Match any of the provided wildcard patterns using -like semantics per file name
    function Test-MatchesPattern {
        param([string]$Name)
        foreach ($p in $IncludePatterns) { if ($Name -like $p) { return $true } }
        return $false
    }

    $matchedFiles = $allFiles | Where-Object { Test-MatchesPattern -Name $_.Name }

    if (-not $matchedFiles -or $matchedFiles.Count -eq 0) {
        Write-Output 'No redundant backup/temp files matched.'
        return
    }

    Write-Output ("Matched files: {0}" -f $matchedFiles.Count)

    foreach ($file in $matchedFiles) {
        if ($PSCmdlet.ShouldProcess($file.FullName, 'Remove redundant backup/temp file')) {
            try {
                Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
                Write-Information ("Removed: {0}" -f $file.FullName) -InformationAction Continue
            } catch {
                Write-Error -ErrorRecord $_
            }
        } else {
            # -WhatIf path
            Write-Information ("Would remove: {0}" -f $file.FullName) -InformationAction Continue
        }
    }

    if ($PassThru) { return $matchedFiles }
}
catch {
    Write-Error -ErrorRecord $_
    throw
}
