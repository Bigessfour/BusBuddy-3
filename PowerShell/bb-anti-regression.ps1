# bb-anti-regression.ps1 - BusBuddy Anti-Regression Compliance Check
# Refactored for direct import and profile integration (2025-08-08)

<#
.SYNOPSIS
    BusBuddy anti-regression check (PowerShell, WPF, Logging compliance)
.DESCRIPTION
    Checks for forbidden patterns (Write-Host, standard WPF controls, Microsoft.Extensions.Logging)
    and outputs detailed violation locations for remediation.
.EXAMPLE
    . ./PowerShell/bb-anti-regression.ps1 -Detailed
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
    $psFiles = Get-ChildItem -Recurse -Path $root -Include *.ps1,*.psm1 | Where-Object { $_.FullName -notmatch 'Archive|TestData|bin|obj' }
    $writeHostHits = foreach ($file in $psFiles) {
        Select-String -Path $file.FullName -Pattern 'Write-Host' | ForEach-Object {
            [PSCustomObject]@{ File = $_.Path; Line = $_.LineNumber; Text = $_.Line.Trim() }
        }
    }
    $summary['WriteHost'] = $writeHostHits.Count
    if ($Detailed -and $writeHostHits.Count) {
        Write-Information "❌ Write-Host violations: $($writeHostHits.Count)" -InformationAction Continue
        $writeHostHits | ForEach-Object { Write-Information "  $($_.File):$($_.Line): $($_.Text)" -InformationAction Continue }
    }

    # 2. Microsoft.Extensions.Logging violations
    $csFiles = Get-ChildItem -Recurse -Path $root/.. -Include *.cs | Where-Object { $_.FullName -notmatch 'bin|obj|TestData' }
    $loggingHits = foreach ($file in $csFiles) {
        Select-String -Path $file.FullName -Pattern 'Microsoft.Extensions.Logging' | ForEach-Object {
            [PSCustomObject]@{ File = $_.Path; Line = $_.LineNumber; Text = $_.Line.Trim() }
        }
    }
    $summary['Logging'] = $loggingHits.Count
    if ($Detailed -and $loggingHits.Count) {
        Write-Information "❌ Microsoft.Extensions.Logging violations: $($loggingHits.Count)" -InformationAction Continue
        $loggingHits | ForEach-Object { Write-Information "  $($_.File):$($_.Line): $($_.Text)" -InformationAction Continue }
    }

    # 3. Standard WPF controls (DataGrid, ComboBox, etc.)
    $xamlFiles = Get-ChildItem -Recurse -Path $root/.. -Include *.xaml | Where-Object { $_.FullName -notmatch 'bin|obj|TestData' }
    $wpfHits = foreach ($file in $xamlFiles) {
        Select-String -Path $file.FullName -Pattern '<DataGrid|<ComboBox|<TabControl|<ListView' | ForEach-Object {
            [PSCustomObject]@{ File = $_.Path; Line = $_.LineNumber; Text = $_.Line.Trim() }
        }
    }
    $summary['WPF'] = $wpfHits.Count
    if ($Detailed -and $wpfHits.Count) {
        Write-Information "❌ Standard WPF control violations: $($wpfHits.Count)" -InformationAction Continue
        $wpfHits | ForEach-Object { Write-Information "  $($_.File):$($_.Line): $($_.Text)" -InformationAction Continue }
    }

    # Summary
    Write-Information "🛡️ BusBuddy Anti-Regression Summary:" -InformationAction Continue
    Write-Information "  Write-Host: $($summary['WriteHost'])" -InformationAction Continue
    Write-Information "  Microsoft.Extensions.Logging: $($summary['Logging'])" -InformationAction Continue
    Write-Information "  Standard WPF controls: $($summary['WPF'])" -InformationAction Continue
    if ($summary.Values | Where-Object { $_ -gt 0 }) {
        Write-Information "❌ Violations found. See above for details." -InformationAction Continue
    } else {
        Write-Information "✅ No anti-regression violations detected." -InformationAction Continue
    }
}

# Export for profile import
Set-Alias bbAntiRegression bbAntiRegression
Export-ModuleMember -Function bbAntiRegression
