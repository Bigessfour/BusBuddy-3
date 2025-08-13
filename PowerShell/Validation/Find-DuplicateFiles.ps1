<#!
.SYNOPSIS
    Finds duplicate file names in the BusBuddy workspace and classifies them as identical (same content hash) or different (same name, different content).

.DESCRIPTION
    Scans recursively (excluding common build/output folders) to locate files sharing the same name. For each name-collision group, a SHA256 hash
    (Get-FileHash per Microsoft PowerShell docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/get-filehash)
    is computed to distinguish identical vs differing content.

    Three output text files are generated in the chosen output directory (default: repository root):
      - duplicate_identical_list.txt   : List of full paths for files whose name-collision group all share the same hash.
      - duplicate_different_list.txt   : Lines of: <Hash>  <FullPath> for groups where at least two differing hashes exist.
      - phase_files_list.txt           : Aggregate list of all duplicate (by name) file paths, regardless of identical/different classification.

    All output streams use Write-Output / Write-Information / Write-Warning / Write-Error — no Write-Host (per BusBuddy standards & Microsoft guidelines:
    https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams).

.PARAMETER RootPath
    Root directory to scan. Defaults to repository root (resolved from this script's location two levels up).

.PARAMETER OutputDirectory
    Directory where the three report files will be written. Defaults to RootPath.

.PARAMETER ExcludeNameRegex
    Regex pattern (single) used to exclude file names (after grouping). Applied to names (not paths). Optional.

.PARAMETER ExcludePathRegex
    Regex pattern (single) used to exclude full paths prior to grouping. Default excludes bin|obj|logs|TestResults directories.

.PARAMETER Algorithm
    Hash algorithm for content comparison. Default: SHA256. (See Get-FileHash docs for other supported algorithms.)

.PARAMETER PassThru
    When set, emits a structured object with the results in addition to writing files.

.EXAMPLE
    ./Find-DuplicateFiles.ps1 -Verbose

.EXAMPLE
    ./Find-DuplicateFiles.ps1 -RootPath .. -OutputDirectory ..\reports -ExcludePathRegex '(bin|obj|TestResults|logs)' -PassThru

.OUTPUTS
    If -PassThru is specified: PSCustomObject with properties Identical, Different, AllDuplicateFiles.

.NOTES
    Safe for large trees: hashes computed only for groups with count > 1. Adds an initial size pre-filter before hashing to reduce unnecessary hashing.

#>
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [string]$RootPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..' '..')).Path,

    [Parameter(Position=1)]
    [string]$OutputDirectory = $null,

    [string]$ExcludeNameRegex,

    [string]$ExcludePathRegex = '(?:\\|/)(?:bin|obj|logs|TestResults)(?:\\|/)',

    [ValidateSet('SHA256','SHA1','MD5')]
    [string]$Algorithm = 'SHA256',

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) {
        throw "RootPath does not exist: $RootPath"
    }
    if (-not $OutputDirectory) { $OutputDirectory = $RootPath }
    if (-not (Test-Path -LiteralPath $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory | Out-Null }

    Write-Verbose "Scanning root: $RootPath"

    # Gather candidate files (excluding typical build/output paths early for performance)
    $allFiles = Get-ChildItem -LiteralPath $RootPath -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch $ExcludePathRegex }

    if ($ExcludeNameRegex) {
        $allFiles = $allFiles | Where-Object { $_.Name -notmatch $ExcludeNameRegex }
    }

    Write-Information ("Files scanned (post-exclusion): {0}" -f ($allFiles.Count)) -InformationAction Continue

    # Group by filename to locate duplicates
    # Ensure $groups is always an array (even when only one group) to allow safe .Count access under StrictMode
    $groups = @($allFiles | Group-Object -Property Name | Where-Object { $_.Count -gt 1 })
    if (-not $groups) {
        Write-Output 'No duplicate file names found.'
        # Still emit empty PassThru object if requested so callers don't break
        if ($PassThru) {
            return [PSCustomObject]@{
                Identical = @()
                Different = @()
                AllDuplicateFiles = @()
                OutputDirectory = $OutputDirectory
                Algorithm = $Algorithm
                Source = 'NoDuplicates'
            }
        }
        return
    }

    Write-Information ("Duplicate name groups: {0}" -f $groups.Count) -InformationAction Continue

    $identicalResults   = New-Object System.Collections.Generic.List[object]
    $differentResults   = New-Object System.Collections.Generic.List[object]
    $allDuplicateFiles  = New-Object System.Collections.Generic.List[string]

    foreach ($g in $groups) {
        # Add all file paths to unified list
        $g.Group | ForEach-Object { $allDuplicateFiles.Add($_.FullName) }

        # Optimization: compute hashes only when size groups align
        $sizeGroups = $g.Group | Group-Object Length
        $hashRecords = @()
        foreach ($sizeGroup in $sizeGroups) {
            if ($sizeGroup.Count -eq 1) {
                # Unique size ensures difference; mark distinct directly
                $file = $sizeGroup.Group[0]
                $hashRecords += [PSCustomObject]@{ Path = $file.FullName; Hash = "SIZE:{0}" -f $file.Length }
            } else {
                foreach ($file in $sizeGroup.Group) {
                    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm $Algorithm).Hash
                    $hashRecords += [PSCustomObject]@{ Path = $file.FullName; Hash = $hash }
                }
            }
        }

    # Force array to safely use .Count (strings lack Count under StrictMode)
    $distinctHashes = @($hashRecords.Hash | Select-Object -Unique)
    if ($distinctHashes.Count -eq 1 -and ($distinctHashes[0] -is [string]) -and -not ($distinctHashes[0] -like 'SIZE:*')) {
            # All truly identical content (same hash and not only size pseudo-hash)
            $hashRecords | ForEach-Object { $identicalResults.Add($_) }
        } else {
            $hashRecords | ForEach-Object { $differentResults.Add($_) }
        }
    }

    # Prepare output file paths
    $identicalFile  = Join-Path $OutputDirectory 'duplicate_identical_list.txt'
    $differentFile  = Join-Path $OutputDirectory 'duplicate_different_list.txt'
    $phaseFile      = Join-Path $OutputDirectory 'phase_files_list.txt'

    # Write identical list (paths only)
    $identicalResults | Sort-Object Hash, Path | ForEach-Object { $_.Path } | Set-Content -Encoding UTF8 -NoNewline:$false -Path $identicalFile

    # Write differing list (Hash  Path)
    $differentResults | Sort-Object Hash, Path | ForEach-Object { "{0}  {1}" -f $_.Hash, $_.Path } | Set-Content -Encoding UTF8 -NoNewline:$false -Path $differentFile

    # All duplicates (raw paths)
    $allDuplicateFiles | Sort-Object | Set-Content -Encoding UTF8 -NoNewline:$false -Path $phaseFile

    Write-Output ("Lists written: {0}, {1}, {2}" -f (Split-Path -Leaf $identicalFile),(Split-Path -Leaf $differentFile),(Split-Path -Leaf $phaseFile))
    Write-Output ("Identical duplicate file entries: {0}" -f $identicalResults.Count)
    Write-Output ("Different duplicate file entries: {0}" -f $differentResults.Count)
    Write-Output ("Total duplicate file paths recorded: {0}" -f $allDuplicateFiles.Count)

    if ($PassThru) {
        return [PSCustomObject]@{
            Identical          = @($identicalResults)
            Different          = @($differentResults)
            AllDuplicateFiles  = @($allDuplicateFiles)
            OutputDirectory    = $OutputDirectory
            Algorithm          = $Algorithm
            Source             = 'Direct'
        }
    }
}
catch {
    Write-Error -ErrorRecord $_
    throw
}
