#requires -Version 7.5
<#
.SYNOPSIS
    Comprehensive remediation plan for BusBuddy anti-regression violations
.DESCRIPTION
    Addresses the violations found by bb-anti-regression check:
    - Microsoft.Extensions.Logging violations: 6 files
    - Standard WPF controls: 22 instances
    - PowerShell Write-Host violations: 57 instances
.NOTES
    Created: August 3, 2025
    Purpose: Follow BusBuddy coding instructions for zero-tolerance anti-regression
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Logging', 'UI', 'PowerShell', 'All')]
    [string]$ViolationType = 'All',

    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [switch]$Detailed
)

function Write-RemediationInfo {
    param([string]$Message, [string]$Color = 'Cyan')
    Write-Information "🛡️ $Message" -InformationAction Continue
}

function Write-RemediationError {
    param([string]$Message)
    Write-Error "❌ $Message"
}

function Write-RemediationSuccess {
    param([string]$Message)
    Write-Information "✅ $Message" -InformationAction Continue
}

function Resolve-LoggingViolations {
    Write-RemediationInfo "Addressing Microsoft.Extensions.Logging violations..."

    $violations = @(
        'BusBuddy.Core\Data\BusBuddyDbContext.cs',
        'BusBuddy.Tests\Phase3Tests\DataLayerTests.cs',
        'BusBuddy.Tests\Phase3Tests\XAIChatServiceTests.cs',
        'BusBuddy.WPF\Logging\UIPerformanceLogger.cs',
        'BusBuddy.WPF\ViewModels\GoogleEarth\GoogleEarthViewModel.cs',
        'BusBuddy.WPF\ViewModels\Vehicle\VehicleViewModel.cs'
    )

    foreach ($file in $violations) {
        $fullPath = Join-Path $PSScriptRoot "..\..\$file"
        if (Test-Path $fullPath) {
            Write-RemediationInfo "Processing: $file"

            if (-not $DryRun) {
                # Replace Microsoft.Extensions.Logging with Serilog
                $content = Get-Content $fullPath -Raw

                # Common replacements
                $content = $content -replace 'using Microsoft\.Extensions\.Logging;', 'using Serilog;'
                $content = $content -replace 'ILogger<([^>]+)>', 'ILogger'
                $content = $content -replace 'private readonly ILogger<[^>]+> _logger;', 'private static readonly ILogger Logger = Log.ForContext<$1>();'
                $content = $content -replace '_logger\.Log', 'Logger'
                $content = $content -replace 'LogLevel\.[A-Za-z]+', 'LogEventLevel.Information'

                Set-Content $fullPath -Value $content -Encoding UTF8
                Write-RemediationSuccess "Updated: $file"
            } else {
                Write-RemediationInfo "Would update: $file"
            }
        } else {
            Write-RemediationError "File not found: $file"
        }
    }
}

function Resolve-UIControlViolations {
    Write-RemediationInfo "Addressing standard WPF control violations..."

    $xamlFiles = @(
        'BusBuddy.WPF\Testing\TestSyncfusionControl.xaml',
        'BusBuddy.WPF\Views\Activities\ActivityScheduleEditDialog.xaml',
        'BusBuddy.WPF\Views\Activities\ActivityTimelineView.xaml',
        'BusBuddy.WPF\Views\Fuel\FuelDialog.xaml',
        'BusBuddy.WPF\Views\Fuel\FuelReconciliationDialog.xaml',
        'BusBuddy.WPF\Views\GoogleEarth\GoogleEarthView.xaml',
        'BusBuddy.WPF\Views\Students\StudentForm.xaml',
        'BusBuddy.WPF\Views\Vehicles\VehicleForm.xaml',
        'BusBuddy.WPF\Views\Vehicles\VehicleManagementView.xaml'
    )

    foreach ($file in $xamlFiles) {
        $fullPath = Join-Path $PSScriptRoot "..\..\$file"
        if (Test-Path $fullPath) {
            Write-RemediationInfo "Processing XAML: $file"

            if (-not $DryRun) {
                $content = Get-Content $fullPath -Raw

                # Add Syncfusion namespace if missing
                if ($content -notmatch 'xmlns:syncfusion="http://schemas\.syncfusion\.com/wpf"') {
                    $content = $content -replace '(xmlns:x="[^"]+")([^>]*>)', '$1 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"$2'
                }

                # Replace standard controls with Syncfusion equivalents
                $content = $content -replace '<DataGrid([^>]*)>', '<syncfusion:SfDataGrid$1>'
                $content = $content -replace '</DataGrid>', '</syncfusion:SfDataGrid>'
                $content = $content -replace '<ComboBox([^>]*)>', '<syncfusion:SfComboBox$1>'
                $content = $content -replace '</ComboBox>', '</syncfusion:SfComboBox>'
                $content = $content -replace '<Button([^>]*)>', '<syncfusion:SfButton$1>'
                $content = $content -replace '</Button>', '</syncfusion:SfButton>'

                Set-Content $fullPath -Value $content -Encoding UTF8
                Write-RemediationSuccess "Updated XAML: $file"
            } else {
                Write-RemediationInfo "Would update XAML: $file"
            }
        } else {
            Write-RemediationError "XAML file not found: $file"
        }
    }
}

function Resolve-PowerShellViolations {
    Write-RemediationInfo "Addressing PowerShell Write-Host violations..."

    $psFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "..") -Filter "*.ps1" -Recurse
    $psmFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "..") -Filter "*.psm1" -Recurse

    $allFiles = $psFiles + $psmFiles | Where-Object { $_.FullName -notlike "*BusBuddy.psm1*" }

    foreach ($file in $allFiles) {
        $violations = Select-String -Path $file.FullName -Pattern "Write-Host" -SimpleMatch
        if ($violations) {
            Write-RemediationInfo "Processing PowerShell: $($file.Name) ($($violations.Count) violations)"

            if (-not $DryRun) {
                $content = Get-Content $file.FullName -Raw

                # Replace Write-Host with appropriate output streams
                $content = $content -replace 'Write-Host\s+"([^"]+)"\s*$', 'Write-Output "$1"'
                $content = $content -replace 'Write-Host\s+"([^"]+)"\s+-ForegroundColor\s+\w+', 'Write-Information "$1" -InformationAction Continue'
                $content = $content -replace 'Write-Host\s+\$([a-zA-Z_][a-zA-Z0-9_]*)', 'Write-Output $$1'

                Set-Content $file.FullName -Value $content -Encoding UTF8
                Write-RemediationSuccess "Updated PowerShell: $($file.Name)"
            } else {
                Write-RemediationInfo "Would update PowerShell: $($file.Name)"
            }
        }
    }
}

function Show-RemediationSummary {
    Write-Information "=== Anti-Regression Remediation Summary ===" -InformationAction Continue
    Write-Information "Target violations identified from bb-anti-regression:" -InformationAction Continue
    Write-Information "  • Microsoft.Extensions.Logging violations: 6 files" -InformationAction Continue
    Write-Information "  • Standard WPF controls: 22 instances" -InformationAction Continue
    Write-Information "  • PowerShell Write-Host violations: 57 instances" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Recommended execution order:" -InformationAction Continue
    Write-Information "  1. .\Anti-Regression-Remediation-Plan.ps1 -ViolationType Logging -DryRun" -InformationAction Continue
    Write-Information "  2. .\Anti-Regression-Remediation-Plan.ps1 -ViolationType UI -DryRun" -InformationAction Continue
    Write-Information "  3. .\Anti-Regression-Remediation-Plan.ps1 -ViolationType PowerShell -DryRun" -InformationAction Continue
    Write-Information "  4. .\Anti-Regression-Remediation-Plan.ps1 -ViolationType All" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "After execution, verify with: bb-anti-regression" -InformationAction Continue
}

# Main execution
try {
    $ErrorActionPreference = 'Stop'

    if ($Detailed) {
        Show-RemediationSummary
        return
    }

    Write-RemediationInfo "Starting Anti-Regression Remediation..." "Green"
    Write-RemediationInfo "Target: $ViolationType violations"
    Write-RemediationInfo "Mode: $(if ($DryRun) { 'DRY RUN (no changes)' } else { 'LIVE EXECUTION' })"
    Write-RemediationInfo ""

    switch ($ViolationType) {
        'Logging' { Resolve-LoggingViolations }
        'UI' { Resolve-UIControlViolations }
        'PowerShell' { Resolve-PowerShellViolations }
        'All' {
            Resolve-LoggingViolations
            Resolve-UIControlViolations
            Resolve-PowerShellViolations
        }
    }

    Write-RemediationInfo ""
    Write-RemediationSuccess "Remediation complete for: $ViolationType"
    Write-RemediationInfo "Next step: Run 'bb-anti-regression' to verify fixes"

    # --- Wiley Data Validation and Reporting ---
    $projectRoot = Split-Path $PSScriptRoot -Parent | Split-Path -Parent
    $reportDir = Join-Path $projectRoot 'Documentation/Reports'
    $reportFile = Join-Path $reportDir "Wiley-AntiRegression-Report-$(Get-Date -Format 'yyyy-MM-dd').md"

    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    $results = @()

    # 1. Validate Wiley Route Boundaries
    $routesPath = Join-Path $projectRoot 'BusBuddy.Core\Data\WileyRoutes.csv'
    if (Test-Path $routesPath) {
        $routes = Import-Csv $routesPath
        foreach ($route in $routes) {
            if ($route.Boundary -notmatch '^[A-Z]{2,}-[0-9]{2,}$') {
                $results += "❌ Route boundary format invalid: $($route.RouteName) ($($route.Boundary))"
            } else {
                $results += "✅ Route boundary valid: $($route.RouteName)"
            }
        }
    } else {
        $results += "⚠️ WileyRoutes.csv not found. Skipping route boundary validation."
    }

    # 2. Validate VIN Formats (Wiley Buses)
    $busesPath = Join-Path $projectRoot 'BusBuddy.Core\Data\WileyBuses.csv'
    if (Test-Path $busesPath) {
        $buses = Import-Csv $busesPath
        foreach ($bus in $buses) {
            if ($bus.VIN -notmatch '^[A-HJ-NPR-Z0-9]{17}$') {
                $results += "❌ Invalid VIN: $($bus.BusNumber) ($($bus.VIN))"
            } else {
                $results += "✅ VIN valid: $($bus.BusNumber)"
            }
        }
    } else {
        $results += "⚠️ WileyBuses.csv not found. Skipping VIN validation."
    }

    # 3. Validate Student Assignments (Wiley)
    $studentsPath = Join-Path $projectRoot 'BusBuddy.Core\Data\WileyStudents.csv'
    if (Test-Path $studentsPath) {
        $students = Import-Csv $studentsPath
        foreach ($student in $students) {
            if ([string]::IsNullOrWhiteSpace($student.RouteAssigned)) {
                $results += "❌ Student not assigned to route: $($student.StudentName)"
            } else {
                $results += "✅ Student assigned: $($student.StudentName) → $($student.RouteAssigned)"
            }
        }
    } else {
        $results += "⚠️ WileyStudents.csv not found. Skipping student assignment validation."
    }

    # Write results to report
    $header = @(
        "# Wiley Anti-Regression Validation Report",
        "_Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm')_",
        "",
        "## Validation Results",
        ""
    )
    $body = $results | Out-String
    $footer = @(
        "",
        "---",
        "_Generated by Anti-Regression-Remediation-Plan.ps1_"
    )

    $reportContent = ($header + $body + $footer) -join "`r`n"
    $reportContent | Set-Content -Path $reportFile -Encoding UTF8

    Write-RemediationSuccess "Wiley validation report generated: $reportFile"

    # Run bb-anti-regression post-integration
    try {
        Write-RemediationInfo "Running bb-anti-regression..."
        bb-anti-regression
    } catch {
        Write-RemediationError "bb-anti-regression failed: $($_.Exception.Message)"
    }
} catch {
    Write-RemediationError "Remediation failed: $($_.Exception.Message)"
    exit 1
}
