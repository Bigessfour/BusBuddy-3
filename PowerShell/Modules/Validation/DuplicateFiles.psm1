<#
.SYNOPSIS
    High-level duplicate file scan wrapper for BusBuddy (module form) referencing Microsoft docs for Get-FileHash & output streams.

.DESCRIPTION
    Wraps existing Validation/Find-DuplicateFiles.ps1 script, adds manifest filtering, suggestion scoring, and structured output.
    Documentation-first: PowerShell output streams per https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
    File hashing via Get-FileHash per https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/get-filehash

.EXAMPLE
    Invoke-BusBuddyDuplicateScan -PassThru -GenerateSuggestions
#>
Set-StrictMode -Version Latest

function Invoke-BusBuddyDuplicateScan {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        # Default root: repository root (three levels up from this module folder).
        # Previous implementation used four levels which resolved to the user's Desktop, causing
        # $scriptPath (PowerShell/Validation/Find-DuplicateFiles.ps1) to be unresolved and the
        # function to throw before producing a PassThru object. (See issue: $out not set.)
        # Documentation: Resolve-Path per https://learn.microsoft.com/powershell/module/microsoft.powershell.management/resolve-path
        [string]$Root = (Join-Path $PSScriptRoot '..' '..' '..'),
        [string]$ManifestPath = (Join-Path $Root 'tooling' 'duplicate-resolution-manifest.json'),
        [switch]$IncludeResolved,
        [switch]$GenerateSuggestions,
        [switch]$PassThru,
        [ValidateRange(0.0,1.0)][double]$MinSuggestionScore = 0.65
    )
    # --- Repo root auto-discovery (robust against path depth changes) ---
    try {
        if (-not (Test-Path -LiteralPath $Root)) {
            $Root = (Resolve-Path -LiteralPath $Root -ErrorAction SilentlyContinue).Path
        }
    } catch { }

    if (-not (Test-Path -LiteralPath $Root)) {
        # Walk upward from module directory until we find BusBuddy.sln or stop at drive root
        $cursor = Get-Item -LiteralPath $PSScriptRoot
        while ($cursor -and $cursor.PSDrive -and $cursor.FullName) {
            $candidateSln = Join-Path $cursor.FullName 'BusBuddy.sln'
            $candidateScript = Join-Path $cursor.FullName 'PowerShell' 'Validation' 'Find-DuplicateFiles.ps1'
            if (Test-Path -LiteralPath $candidateSln -or Test-Path -LiteralPath $candidateScript) {
                $Root = $cursor.FullName
                break
            }
            $parent = $cursor.Parent
            if (-not $parent) { break }
            $cursor = $parent
        }
    }

    $Root = (Resolve-Path -LiteralPath $Root).Path

    $scriptPath = Join-Path $Root 'PowerShell' 'Validation' 'Find-DuplicateFiles.ps1'
    if (-not (Test-Path $scriptPath)) {
        # Fallback upward search if default root was misresolved.
        $cursor = Get-Item -LiteralPath $PSScriptRoot
        while ($cursor) {
            $candidate = Join-Path $cursor.FullName 'PowerShell' 'Validation' 'Find-DuplicateFiles.ps1'
            if (Test-Path $candidate) {
                $Root = $cursor.FullName
                $scriptPath = $candidate
                Write-Information "Adjusted Root during fallback discovery: $Root" -InformationAction Continue
                break
            }
            $cursor = $cursor.Parent
        }
    }
    if (-not (Test-Path $scriptPath)) { throw "Base duplicate script missing (after discovery attempts): $scriptPath" }

    if (-not (Test-Path (Split-Path $ManifestPath -Parent))) { New-Item -ItemType Directory -Path (Split-Path $ManifestPath -Parent) | Out-Null }

    Write-Information "Running base duplicate scan script ($scriptPath)" -InformationAction Continue
    $raw = $null
    try {
        $raw = & $scriptPath -RootPath $Root -OutputDirectory $Root -PassThru
    } catch {
        Write-Warning "Underlying duplicate script threw: $($_.Exception.Message)"
    }

    # Extract structured object (has Identical & Different)
    $result = $null
    if ($raw) {
        $candidates = if ($raw -is [System.Array]) { $raw } else { @($raw) }
        foreach ($c in $candidates) {
            if ($c -is [psobject] -and (Get-Member -InputObject $c -Name 'Identical' -ErrorAction SilentlyContinue) -and (Get-Member -InputObject $c -Name 'Different' -ErrorAction SilentlyContinue)) {
                $result = $c
            }
        }
    }
    if (-not $result) {
        # Fallback: parse files produced by the underlying script
        $identicalFile = Join-Path $Root 'duplicate_identical_list.txt'
        $differentFile = Join-Path $Root 'duplicate_different_list.txt'
        $parsedIdentical = if (Test-Path $identicalFile) { Get-Content $identicalFile -ErrorAction SilentlyContinue | Where-Object { $_ } | ForEach-Object { [PSCustomObject]@{ Path = $_; Hash = 'UNKNOWN' } } } else { @() }
        $parsedDifferent = if (Test-Path $differentFile) { Get-Content $differentFile -ErrorAction SilentlyContinue | Where-Object { $_ } | ForEach-Object { if ($_ -match '^(\S+)\s+(.+)$') { [PSCustomObject]@{ Hash = $Matches[1]; Path = $Matches[2] } } } } else { @() }
        $result = [PSCustomObject]@{
            Identical = $parsedIdentical
            Different = $parsedDifferent
            AllDuplicateFiles = @()
            OutputDirectory = $Root
            Algorithm = 'SHA256'
            Source = 'Fallback'
        }
        if (-not $raw) { Write-Warning 'Base script produced no PassThru object; using fallback parsed file results.' }
        else { Write-Warning 'PassThru object not detected in mixed output; using fallback parsed file results.' }
    }

    $manifest = $null
    if (Test-Path $ManifestPath) {
        try { $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json } catch { Write-Warning "Failed to parse manifest: $ManifestPath" }
    }

    $resolvedMap = @{}
    if ($manifest) {
        foreach ($r in @($manifest.identicalRemoved)) { $resolvedMap[$r.removed] = $true }
        foreach ($r in @($manifest.differingDecisions)) { $resolvedMap[$r.removed] = $true }
    }

    $identicalRaw = @($result.Identical)
    $differentRaw = @($result.Different)

    $identical = $identicalRaw
    $different = $differentRaw

    if (-not $IncludeResolved) {
        $identical = $identical | Where-Object { $_.Path -and (-not $resolvedMap.ContainsKey($_.Path)) }
        $different = $different | Where-Object { $_.Path -and (-not $resolvedMap.ContainsKey($_.Path)) }
    }

    $suggestions = @()
    if ($GenerateSuggestions) {
        # Group differing by filename
        $groups = $different | Group-Object { [IO.Path]::GetFileName($_.Path) }
        foreach ($g in $groups) {
            $items = $g.Group
            # Basic metrics: size, lastWrite, reference count
            $enriched = foreach ($i in $items) {
                $fi = Get-Item -LiteralPath $i.Path -ErrorAction SilentlyContinue
                $size = $fi.Length
                $lw = $fi.LastWriteTimeUtc
                $refCount = (Select-String -Path (Get-ChildItem -Path $Root -Recurse -Include *.cs,*.ps1 -File | ForEach-Object FullName) -Pattern ([Regex]::Escape($g.Name)) -SimpleMatch -ErrorAction SilentlyContinue | Measure-Object).Count
                [PSCustomObject]@{ Path=$i.Path; Size=$size; LastWrite=$lw; RefCount=$refCount }
            }
            if ($enriched.Count -lt 2) { continue }
            # Score: higher refcount, newer, smaller size diff cluster
            $maxRef = ($enriched.RefCount | Measure-Object -Maximum).Maximum
            $maxWrite = ($enriched.LastWrite | Measure-Object -Maximum).Maximum
            $minSize = ($enriched.Size | Measure-Object -Minimum).Minimum
            foreach ($e in $enriched) {
                $refScore = if ($maxRef -gt 0) { $e.RefCount / $maxRef } else { 0 }
                $freshScore = if ($maxWrite -gt 0) { ($e.LastWrite - ([datetime]'1970-01-01')) / ($maxWrite - ([datetime]'1970-01-01')) } else { 0 }
                $sizeScore = if ($e.Size -gt 0) { [Math]::Min(1, $minSize / $e.Size) } else { 1 }
                $score = [Math]::Round((0.4*$refScore + 0.3*$freshScore + 0.3*$sizeScore),3)
                if ($score -ge $MinSuggestionScore) {
                    $suggestions += [PSCustomObject]@{ File=$g.Name; Candidate=$e.Path; Score=$score; RefCount=$e.RefCount; Size=$e.Size; LastWrite=$e.LastWrite }
                }
            }
        }
    }

    $out = [PSCustomObject]@{
        # Backward compatible expected names
        Identical         = $identical
        Different         = $different
        # Raw (pre-filter) for transparency
        RawIdentical      = $identicalRaw
        RawDifferent      = $differentRaw
        # Pending names (legacy naming from draft)
        IdenticalPending  = $identical
        DifferentPending  = $different
        Suggestions       = $suggestions | Sort-Object Score -Descending
        ManifestPath      = $ManifestPath
        ManifestLoaded    = [bool]$manifest
        ScriptSource      = $result.Source
        Root              = $Root
    }
    if ($PassThru) { return $out }
    $out
}

Set-Alias -Name bb-dup-scan -Value Invoke-BusBuddyDuplicateScan -Scope Global
Export-ModuleMember -Function Invoke-BusBuddyDuplicateScan -Alias bb-dup-scan
