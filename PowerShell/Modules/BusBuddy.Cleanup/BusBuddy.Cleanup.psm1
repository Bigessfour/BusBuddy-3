# BusBuddy.Cleanup Module
# Implements repository hygiene and artifact cleanup per project guidelines.

function Invoke-BusBuddyCleanup {
    <#
    .SYNOPSIS
        Remove build artifacts and old log files.
    .DESCRIPTION
        Deletes bin/, obj/, TestResults/, and logs older than a retention window (default 7 days).
    #>
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [int]$LogRetentionDays = 7,
        [switch]$IncludeNodeModules
    )
    $paths = @('bin','obj','TestResults')
    if ($IncludeNodeModules) { $paths += 'node_modules' }
    foreach ($p in $paths) {
        Get-ChildItem -Recurse -Directory -Filter $p -ErrorAction SilentlyContinue | ForEach-Object {
            if ($PSCmdlet.ShouldProcess($_.FullName,'Remove directory')) {
                try { Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop; Write-Information "🗑️ Removed $($_.FullName)" -InformationAction Continue } catch { Write-Warning "Failed to remove $($_.FullName): $($_.Exception.Message)" }
            }
        }
    }
    if (Test-Path logs) {
        $threshold = (Get-Date).AddDays(-$LogRetentionDays)
        Get-ChildItem logs -File -Include *.log -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt $threshold } | ForEach-Object {
            if ($PSCmdlet.ShouldProcess($_.FullName,'Remove old log')) {
                try { Remove-Item $_.FullName -Force; Write-Information "🧹 Removed old log $($_.Name)" -InformationAction Continue } catch { Write-Warning "Failed to remove log $($_.Name): $($_.Exception.Message)" }
            }
        }
    }
}

function Get-BusBuddyUnusedFiles {
    <#
    .SYNOPSIS
        Identify candidate unused .cs/.xaml files not referenced in solution or project includes.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [string]$Root = '.'
    )
    $solution = Get-ChildItem -Path $Root -Filter *.sln -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $solution) { Write-Warning 'Solution file not found.'; return @() }
    $solutionText = Get-Content $solution.FullName -Raw
    $candidates = Get-ChildItem $Root -Recurse -Include *.cs,*.xaml -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch 'bin\\|obj\\|TestResults\\|\.git' }
    $unused = @()
    foreach ($f in $candidates) {
        if ($solutionText -notmatch [regex]::Escape($f.Name)) { $unused += $f.FullName }
    }
    return $unused
}

function Remove-BusBuddyUnusedFiles {
    <#
    .SYNOPSIS
        Remove unused files interactively (WhatIf supported).
    #>
    [CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='Medium')]
    param(
        [string]$Root='.'
    )
    $unused = Get-BusBuddyUnusedFiles -Root $Root
    if (-not $unused -or $unused.Count -eq 0) { Write-Information 'No unused files detected.' -InformationAction Continue; return }
    foreach ($file in $unused) {
        if ($PSCmdlet.ShouldProcess($file,'Remove unused file')) {
            try { Remove-Item $file -Force; Write-Information "Removed unused file $file" -InformationAction Continue } catch { $warn = 'Failed to remove ' + $file + ' -> ' + $_.Exception.Message; Write-Warning $warn }
        }
    }
}

Export-ModuleMember -Function Invoke-BusBuddyCleanup,Get-BusBuddyUnusedFiles,Remove-BusBuddyUnusedFiles
