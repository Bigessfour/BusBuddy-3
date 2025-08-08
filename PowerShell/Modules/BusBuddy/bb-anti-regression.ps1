# bb-anti-regression.ps1 - BusBuddy Anti-Regression Compliance Check
# Refactored for direct import and profile integration (2025-08-08)

<#!
.SYNOPSIS
    BusBuddy anti-regression check (PowerShell, WPF, Logging compliance)
.DESCRIPTION
    Checks for forbidden patterns (Write-Host, standard WPF controls, Microsoft.Extensions.Logging)
    and outputs detailed violation locations for remediation.
.EXAMPLE
    . ./PowerShell/Modules/BusBuddy/bb-anti-regression.ps1 -Detailed
    bbAntiRegression -Detailed
#>
[CmdletBinding()]
param(
    [switch]$Detailed
)

function bbAntiRegression {
    [CmdletBinding()]
    param(
        [switch]$Detailed
    )
    $root = $PSScriptRoot
    $summary = @{}
    $violations = @()

    # 1. PowerShell Write-Host violations
    $psFiles = Get-ChildItem -Recurse -Path $root/../.. -Include *.ps1,*.psm1 | Where-Object { $_.FullName -notmatch 'Archive|TestData|bin|obj' }
    $writeHostHits = foreach ($file in $psFiles) {
        Select-String -Path $file.FullName -Pattern 'Write-Host' | ForEach-Object {
            [PSCustomObject]@{ File = $_.Path; Line = $_.LineNumber; Text = $_.Line.Trim() }
        }
    }
    $summary['WriteHost'] = $writeHostHits.Count
    if ($Detailed -and $writeHostHits.Count) {
        Write-Output "❌ Write-Host violations: $($writeHostHits.Count)"
        $writeHostHits | ForEach-Object { Write-Output ("  $($_.File):$($_.Line): $($_.Text)") }
    }

    # 2. Microsoft.Extensions.Logging violations
    $csFiles = Get-ChildItem -Recurse -Path $root/../../../.. -Include *.cs | Where-Object { $_.FullName -notmatch 'bin|obj|TestData' }
    $loggingHits = foreach ($file in $csFiles) {
        Select-String -Path $file.FullName -Pattern 'Microsoft.Extensions.Logging' | ForEach-Object {
            [PSCustomObject]@{ File = $_.Path; Line = $_.LineNumber; Text = $_.Line.Trim() }
        }
    }
    $summary['Logging'] = $loggingHits.Count
    if ($Detailed -and $loggingHits.Count) {
        Write-Output "❌ Microsoft.Extensions.Logging violations: $($loggingHits.Count)"
        $loggingHits | ForEach-Object { Write-Output ("  $($_.File):$($_.Line): $($_.Text)") }
    }

    # 3. Standard WPF controls (DataGrid, ComboBox, etc.)
    $xamlFiles = Get-ChildItem -Recurse -Path $root/../../../.. -Include *.xaml | Where-Object { $_.FullName -notmatch 'bin|obj|TestData' }
    $wpfHits = foreach ($file in $xamlFiles) {
        Select-String -Path $file.FullName -Pattern '<DataGrid|<ComboBox|<TabControl|<ListView' | ForEach-Object {
            [PSCustomObject]@{ File = $_.Path; Line = $_.LineNumber; Text = $_.Line.Trim() }
        }
    }
    $summary['WPF'] = $wpfHits.Count
    if ($Detailed -and $wpfHits.Count) {
        Write-Output "❌ Standard WPF control violations: $($wpfHits.Count)"
        $wpfHits | ForEach-Object { Write-Output ("  $($_.File):$($_.Line): $($_.Text)") }
    }

    # Summary
    Write-Output "🛡️ BusBuddy Anti-Regression Summary:"
    Write-Output ("  Write-Host: $($summary['WriteHost'])")
    Write-Output ("  Microsoft.Extensions.Logging: $($summary['Logging'])")
    Write-Output ("  Standard WPF controls: $($summary['WPF'])")
    if ($summary.Values | Where-Object { $_ -gt 0 }) {
        Write-Output "❌ Violations found. See above for details."
    } else {
        Write-Output "✅ No anti-regression violations detected."
    }
}

# Export for profile import
Set-Alias bbAntiRegression bbAntiRegression
Export-ModuleMember -Function bbAntiRegression
