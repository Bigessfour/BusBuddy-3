<#!
.SYNOPSIS
    Finds duplicate file names and classifies them as identical (same content hash) or different (same name, different content).
    Optionally emits a non-destructive cleanup plan for truly identical duplicates and a JSON summary for CI/MVP checks.

.DESCRIPTION
    Scans recursively (excluding common build/output folders) to locate files sharing the same name. For each name-collision group, a SHA256 hash
    (Get-FileHash — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/get-filehash)
    is computed to distinguish identical vs differing content.

    Outputs three text files and, when requested, a cleanup plan script:
      - duplicate_identical_list.txt   : Paths for files whose name-collision group all share the same hash.
      - duplicate_different_list.txt   : Lines of: <Hash>  <FullPath> for groups where at least two differing hashes exist.
      - phase_files_list.txt           : Aggregate list of all duplicate (by name) file paths.
      - duplicate_cleanup_plan.ps1     : Optional — non-destructive script proposing Remove-Item for truly identical duplicates.
        (Remove-Item — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.management/remove-item)

    All output streams use Write-Output / Write-Information / Write-Warning / Write-Error — no Write-Host
    (Microsoft docs — Output streams: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams).

#>
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]  # Enables -WhatIf/-Confirm — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_ShouldProcess
param(
    [Parameter(Position=0)]
    [string]$RootPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..' '..')).Path,

    [Parameter(Position=1)]
    [string]$OutputDirectory = $null,

    [string]$ExcludeNameRegex,

    [string]$ExcludePathRegex = '(?:\\|/)(?:bin|obj|logs|TestResults)(?:\\|/)',

    [ValidateSet('SHA256','SHA1','MD5')]
    [string]$Algorithm = 'SHA256',

    # New: Write a non-destructive cleanup plan script for truly identical duplicates
    [switch]$EmitCleanupScript,

    # New: Custom output path for the cleanup plan (defaults to OutputDirectory\duplicate_cleanup_plan.ps1)
    [string]$CleanupScriptPath,

    # New: Strategy for which file to keep per identical group
    [ValidateSet('First','Oldest','Newest','Largest','Smallest')]
    [string]$KeepStrategy = 'First',

    # New: Prefer keeping paths that match this regex; falls back to KeepStrategy when none match
    [string]$KeepPreferredPathRegex,

    # New: Add extra path exclusions without changing default ExcludePathRegex
    [string]$AdditionalExcludePathRegex,

    # New: Emit machine-readable summary JSON to OutputDirectory\duplicate_summary.json
    [switch]$EmitJsonSummary,

    # New: Execute deletions for truly identical duplicates (opt-in, honors -WhatIf/-Confirm)
    [switch]$ExecuteCleanup,

    # New: Explicit paths to delete (rooted or relative to RootPath). Deletion occurs only when -ExecuteCleanup is provided.
    [string[]]$DeleteLiteralPaths,

    # New: One-shot convenience — applies recommended defaults and performs safe cleanup
    # References:
    # - about_ShouldProcess: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_ShouldProcess
    # - Remove-Item: https://learn.microsoft.com/powershell/module/microsoft.powershell.management/remove-item
    [switch]$RecommendedCleanup,

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

    # New: Apply recommended cleanup defaults and explicit deletions
    if ($RecommendedCleanup) {
        if (-not $EmitCleanupScript)   { $EmitCleanupScript   = $true }
        if (-not $EmitJsonSummary)     { $EmitJsonSummary     = $true }
        if (-not $ExecuteCleanup)      { $ExecuteCleanup      = $true }
        # Prefer root BusBuddy.db and Scripts\Query-Students-Azure.ps1
        if (-not $KeepPreferredPathRegex) { $KeepPreferredPathRegex = '(?i)(\\BusBuddy(?!\.WPF)\\BusBuddy\.db$)|(\\PowerShell\\Scripts\\Query-Students-Azure\.ps1$)' }
        if (-not $AdditionalExcludePathRegex) { $AdditionalExcludePathRegex = '(?:\\|/)(?:\.git|\.vs|packages|node_modules)(?:\\|/)' }

        $explicit = [System.Collections.Generic.List[string]]::new()
        foreach ($p in @(
            'BusBuddy.WPF\BusBuddy.db',
            'BusBuddy.Core\Services\DataIntegrityService.cs',
            'BusBuddy.Core\Services\IBusRepository.cs',
            # Remove duplicate/stub Query-Students-Azure.ps1 copies — keep the Scripts version
            'Query-Students-Azure.ps1',
            'Documentation\Archive\LegacyScripts\Query-Students-Azure.ps1',
            # New explicit deletions per request
            'experiments\README.md',
            'BusBuddy.WPF\Documentation\README.md',
            'Documentation\Archive\LegacyScripts\*'  # wildcard intended — expand at runtime
        )) {
            $target = Join-Path $RootPath $p
            if ($p -match '[\*\?]') {
                # Resolve-Path with -Path to expand wildcards — Microsoft docs:
                # https://learn.microsoft.com/powershell/module/microsoft.powershell.management/resolve-path
                $matches = @(Resolve-Path -Path $target -ErrorAction SilentlyContinue)
                foreach ($m in $matches) { $explicit.Add($m.Path) }
            } else {
                if (Test-Path -LiteralPath $target) { $explicit.Add($target) }
            }
        }
        if ($explicit.Count -gt 0) {
            $DeleteLiteralPaths = @($DeleteLiteralPaths + $explicit.ToArray())
        }
        Write-Information "Recommended cleanup enabled — plan+json+execute with preferred keep regex and explicit deletions." -InformationAction Continue
    }

    Write-Verbose "Scanning root: $RootPath"

    # Gather candidate files (excluding typical build/output paths early for performance)
    # Where-Object -match/-notmatch — Microsoft docs (comparison operators): https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Comparison_Operators
    $combinedExclude = $ExcludePathRegex
    if ($AdditionalExcludePathRegex) { $combinedExclude = "(?:$ExcludePathRegex)|(?:$AdditionalExcludePathRegex)" }

    $allFiles = Get-ChildItem -LiteralPath $RootPath -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch $combinedExclude }

    if ($ExcludeNameRegex) {
        $allFiles = $allFiles | Where-Object { $_.Name -notmatch $ExcludeNameRegex }
    }

    Write-Information ("Files scanned (post-exclusion): {0}" -f ($allFiles.Count)) -InformationAction Continue

    # Group by filename to locate duplicates
    $groups = @($allFiles | Group-Object -Property Name | Where-Object { $_.Count -gt 1 })  # Group-Object docs:
    # https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/group-object
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

    # New: Initialize cleanup-plan script builder when requested
    $proposedDeleteCount = 0
    $executedDeleteCount = 0
    $requestedDeleteCount = 0
    if ($EmitCleanupScript) {
        if (-not $CleanupScriptPath) {
            $CleanupScriptPath = Join-Path $OutputDirectory 'duplicate_cleanup_plan.ps1'
        }
        $sb = [System.Text.StringBuilder]::new()
        [void]$sb.AppendLine("# Duplicate Cleanup Plan — generated by Find-DuplicateFiles.ps1")
        [void]$sb.AppendLine("# Review carefully before running. Use -Execute to perform deletions.")
        [void]$sb.AppendLine("# Microsoft docs: Remove-Item https://learn.microsoft.com/powershell/module/microsoft.powershell.management/remove-item")
        [void]$sb.AppendLine("param([switch]`$Execute)")
        [void]$sb.AppendLine("Set-StrictMode -Version Latest")
        [void]$sb.AppendLine("")
    }

    foreach ($g in $groups) {
        # Add all file paths to unified list
        $g.Group | ForEach-Object { $allDuplicateFiles.Add($_.FullName) }

        # Optimization: compute hashes only when size groups align
        $sizeGroups = $g.Group | Group-Object Length
        $hashRecords = @()
        foreach ($sizeGroup in $sizeGroups) {
            if ($sizeGroup.Count -eq 1) {
                $file = $sizeGroup.Group[0]
                # New: include metadata to support KeepStrategy
                $hashRecords += [PSCustomObject]@{
                    Path          = $file.FullName
                    Hash          = ("SIZE:{0}" -f $file.Length)
                    Name          = $g.Name
                    LastWriteTime = $file.LastWriteTimeUtc
                    Length        = $file.Length
                }
            } else {
                foreach ($file in $sizeGroup.Group) {
                    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm $Algorithm).Hash
                    # New: include metadata to support KeepStrategy
                    $hashRecords += [PSCustomObject]@{
                        Path          = $file.FullName
                        Hash          = $hash
                        Name          = $g.Name
                        LastWriteTime = $file.LastWriteTimeUtc
                        Length        = $file.Length
                    }
                }
            }
        }

        # Force array to safely use .Count (strings lack Count under StrictMode)
        $distinctHashes = @($hashRecords.Hash | Select-Object -Unique)
        if ($distinctHashes.Count -eq 1 -and ($distinctHashes[0] -is [string]) -and -not ($distinctHashes[0] -like 'SIZE:*')) {
            # All truly identical content — record as identical
            $hashRecords | ForEach-Object { $identicalResults.Add($_) }

            # New: choose keep candidate list honoring KeepPreferredPathRegex, then KeepStrategy
            $candidates = $hashRecords
            if ($KeepPreferredPathRegex) {
                $preferred = @($hashRecords | Where-Object { $_.Path -match $KeepPreferredPathRegex })
                if ($preferred.Count -gt 0) { $candidates = $preferred }
            }

            $keep = switch ($KeepStrategy) {
                'Oldest'   { $candidates | Sort-Object LastWriteTime | Select-Object -First 1 }
                'Newest'   { $candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
                'Largest'  { $candidates | Sort-Object Length -Descending | Select-Object -First 1 }
                'Smallest' { $candidates | Sort-Object Length | Select-Object -First 1 }
                default    { $candidates | Sort-Object Path | Select-Object -First 1 } # First
            }

            $toDelete = @($hashRecords | Where-Object { $_.Path -ne $keep.Path })

            if ($EmitCleanupScript -and $toDelete.Count -gt 0) {
                # New: Propose a cleanup plan honoring KeepPreferredPathRegex, then KeepStrategy
                if ($EmitCleanupScript) {
                    $candidates = $hashRecords
                    if ($KeepPreferredPathRegex) {
                        $preferred = @($hashRecords | Where-Object { $_.Path -match $KeepPreferredPathRegex })
                        if ($preferred.Count -gt 0) { $candidates = $preferred }
                    }

                    $keep = switch ($KeepStrategy) {
                        'Oldest'   { $candidates | Sort-Object LastWriteTime | Select-Object -First 1 }
                        'Newest'   { $candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
                        'Largest'  { $candidates | Sort-Object Length -Descending | Select-Object -First 1 }
                        'Smallest' { $candidates | Sort-Object Length | Select-Object -First 1 }
                        default    { $candidates | Sort-Object Path | Select-Object -First 1 } # First
                    }

                    $toDelete = @($hashRecords | Where-Object { $_.Path -ne $keep.Path })
                    if ($toDelete.Count -gt 0) {
                        [void]$sb.AppendLine("# Duplicate filename: $($g.Name)")
                        [void]$sb.AppendLine("# Keeping: $($keep.Path)")
                        foreach ($f in $toDelete) {
                            $escaped = $f.Path -replace "'", "''"
                            # Write-Information — Microsoft docs:
                            # https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/write-information
                            [void]$sb.AppendLine("if (`$Execute) { Remove-Item -LiteralPath '$escaped' -Force } else { Write-Information 'Would remove: $escaped' -InformationAction Continue }")
                            $proposedDeleteCount++
                        }
                        [void]$sb.AppendLine("")  # spacer
                    }
                }
            }

            # New: Optionally execute removals for identical duplicates (safe, opt-in)
            if ($ExecuteCleanup -and $toDelete.Count -gt 0) {
                foreach ($f in $toDelete) {
                    if ($PSCmdlet.ShouldProcess($f.Path, "Remove identical duplicate file")) {  # about_ShouldProcess
                        try {
                            # Remove-Item — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.management/remove-item
                            Remove-Item -LiteralPath $f.Path -Force -ErrorAction Stop
                            $executedDeleteCount++
                            Write-Information ("Removed: {0}" -f $f.Path) -InformationAction Continue
                        } catch {
                            Write-Error -ErrorRecord $_
                        }
                    }
                }
            }
        } else {
            # Different content or size — record as different
            $hashRecords | ForEach-Object { $differentResults.Add($_) }
        }
    }

    # Prepare output file paths
    $identicalFile  = Join-Path $OutputDirectory 'duplicate_identical_list.txt'
    $differentFile  = Join-Path $OutputDirectory 'duplicate_different_list.txt'
    $phaseFile      = Join-Path $OutputDirectory 'phase_files_list.txt'

    # Write identical list (paths only)
    $identicalResults | Sort-Object Hash, Path | ForEach-Object { $_.Path } | Set-Content -Encoding UTF8 -Path $identicalFile

    # Write differing list (Hash  Path)
    $differentResults | Sort-Object Hash, Path | ForEach-Object { "{0}  {1}" -f $_.Hash, $_.Path } | Set-Content -Encoding UTF8 -Path $differentFile

    # All duplicates (raw paths)
    $allDuplicateFiles | Sort-Object | Set-Content -Encoding UTF8 -Path $phaseFile

    Write-Output ("Lists written: {0}, {1}, {2}" -f (Split-Path -Leaf $identicalFile),(Split-Path -Leaf $differentFile),(Split-Path -Leaf $phaseFile))
    Write-Output ("Identical duplicate file entries: {0}" -f $identicalResults.Count)
    Write-Output ("Different duplicate file entries: {0}" -f $differentResults.Count)
    Write-Output ("Total duplicate file paths recorded: {0}" -f $allDuplicateFiles.Count)

    # New: Persist cleanup-plan script if requested
    if ($EmitCleanupScript) {
        # New: Add explicit deletions to plan, if provided
        if ($DeleteLiteralPaths) {
            [void]$sb.AppendLine("# Explicit deletions requested by caller")
            foreach ($p in $DeleteLiteralPaths) {
                $targetPath = if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $RootPath $p }
                $escaped = $targetPath -replace "'", "''"
                if ($p -match '[\*\?]') {
                    # Wildcard patterns — use -Path with -Recurse
                    [void]$sb.AppendLine("if (`$Execute) { Remove-Item -Path '$escaped' -Recurse -Force } else { Write-Information 'Would remove (wildcard): $escaped' -InformationAction Continue }")
                } else {
                    [void]$sb.AppendLine("if (`$Execute) { Remove-Item -LiteralPath '$escaped' -Force } else { Write-Information 'Would remove: $escaped' -InformationAction Continue }")
                }
                $proposedDeleteCount++
                $requestedDeleteCount++
            }
            [void]$sb.AppendLine("")
        }

        # Set-Content — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.management/set-content
        [System.IO.File]::WriteAllText($CleanupScriptPath, $sb.ToString(), [System.Text.Encoding]::UTF8) | Out-Null
        Write-Output ("Cleanup plan written: {0} (proposed deletions: {1})" -f (Split-Path -Leaf $CleanupScriptPath), $proposedDeleteCount)
    }

    # New: Execute explicit deletions if requested and -ExecuteCleanup is set
    if ($DeleteLiteralPaths -and $ExecuteCleanup) {
        foreach ($p in $DeleteLiteralPaths) {
            $targetPath = if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $RootPath $p }
            # Expand wildcards with -Path; exact paths with -LiteralPath — Microsoft docs: Resolve-Path
            $resolved = if ($p -match '[\*\?]') {
                @(Resolve-Path -Path $targetPath -ErrorAction SilentlyContinue)
            } else {
                @(Resolve-Path -LiteralPath $targetPath -ErrorAction SilentlyContinue)
            }
            if ($resolved) {
                foreach ($r in $resolved) {
                    $item = Get-Item -LiteralPath $r.Path -ErrorAction SilentlyContinue  # Microsoft docs: Get-Item
                    $isContainer = $item -and $item.PSIsContainer
                    if ($PSCmdlet.ShouldProcess($r.Path, "Remove explicit item")) {
                        try {
                            # Remove-Item with -Recurse for containers — Microsoft docs:
                            # https://learn.microsoft.com/powershell/module/microsoft.powershell.management/remove-item
                            if ($isContainer) {
                                Remove-Item -LiteralPath $r.Path -Recurse -Force -ErrorAction Stop
                            } else {
                                Remove-Item -LiteralPath $r.Path -Force -ErrorAction Stop
                            }
                            $executedDeleteCount++
                            Write-Information ("Removed (explicit): {0}" -f $r.Path) -InformationAction Continue
                        } catch {
                            Write-Error -ErrorRecord $_
                        }
                    }
                }
            } else {
                Write-Warning ("Requested delete path not found: {0}" -f $targetPath)
            }
        }
    }

    # New: Optional JSON summary (ConvertTo-Json — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/convertto-json)
    $summaryJsonPath = $null
    if ($EmitJsonSummary) {
        $summaryJsonPath = Join-Path $OutputDirectory 'duplicate_summary.json'

        # Keep JSON compact and CI-friendly — include counts and sample paths only
        $identicalByName = $identicalResults | Group-Object Name | ForEach-Object {
            [PSCustomObject]@{
                Name        = $_.Name
                Count       = $_.Count
                SamplePaths = ($_.Group | Select-Object -First 3 -ExpandProperty Path)
            }
        }
        $differentByName = $differentResults | Group-Object Name | ForEach-Object {
            $distinct = @($_.Group.Hash | Select-Object -Unique)
            [PSCustomObject]@{
                Name               = $_.Name
                Count              = $_.Count
                DistinctHashCount  = $distinct.Count
                SampleItems        = ($_.Group | Select-Object -First 3 Path,Hash,Length)
            }
        }

        $summary = [PSCustomObject]@{
            RootPath             = $RootPath
            ScannedCount         = $allFiles.Count
            GroupCount           = $groups.Count
            IdenticalCount       = $identicalResults.Count
            DifferentCount       = $differentResults.Count
            Algorithm            = $Algorithm
            OutputDirectory      = $OutputDirectory
            GeneratedOnUtc       = [DateTime]::UtcNow
            KeepStrategy         = $KeepStrategy
            KeepPreferredPathRegex = $KeepPreferredPathRegex
            ProposedDeletions    = $proposedDeleteCount
            IdenticalGroups      = $identicalByName
            DifferentGroups      = $differentByName
        }

        $summary | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -Path $summaryJsonPath
        Write-Output ("Summary written: {0}" -f (Split-Path -Leaf $summaryJsonPath))
    }

    if ($executedDeleteCount -gt 0) {
        Write-Output ("Executed deletions: {0}" -f $executedDeleteCount)
    }

    if ($PassThru) {
        return [PSCustomObject]@{
            Identical              = @($identicalResults)
            Different              = @($differentResults)
            AllDuplicateFiles      = @($allDuplicateFiles)
            OutputDirectory        = $OutputDirectory
            Algorithm              = $Algorithm
            Source                 = 'Direct'
            # New: expose cleanup script and summary for automation
            CleanupScriptPath      = $CleanupScriptPath
            ProposedDeletions      = $proposedDeleteCount
            SummaryJsonPath        = $summaryJsonPath
            ExecutedDeletions      = $executedDeleteCount
            RequestedDeletions     = $requestedDeleteCount
        }
    }
}
catch {
    # Write-Error — Microsoft docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/write-error
    Write-Error -ErrorRecord $_
    throw
}
