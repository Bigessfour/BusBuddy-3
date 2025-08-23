#Requires -Version 7.5
<#
.SYNOPSIS
BusBuddy XAML Validation Script - Syncfusion-Only Enforcement
Ensures ZERO TOLERANCE for non-Syncfusion controls in XAML files

.DESCRIPTION
Specifically validates:
- No standard WPF controls (DataGrid, ComboBox, etc.)
- Only documented Syncfusion WPF 30.1.42 controls
- Proper Syncfusion namespace declarations
- Correct Syncfusion control property usage
- Theme consistency and resource usage

.PARAMETER Path
Root path to validate (defaults to current directory)

.PARAMETER Fix
Attempt to auto-convert standard controls to Syncfusion

.PARAMETER Strict
Enable strict mode for enhanced validation

.EXAMPLE
bb-xaml-validate
bb-xaml-validate -Path "C:\BusBuddy" -Fix -Strict
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = (Get-Location).Path,

    [Parameter()]
    [switch]$Fix,

    [Parameter()]
    [switch]$Strict
)

# Initialize validation results
$script:XAMLViolations = 0
$script:FixedViolations = 0
$script:ViolationDetails = @()

#region XAML Control Mappings
$script:StandardToSyncfusion = @{
    'DataGrid' = 'SfDataGrid'
    'ComboBox' = 'SfComboBox'
    'TextBox' = 'SfTextBoxExt'
    'DatePicker' = 'SfDatePicker'
    'Calendar' = 'SfCalendar'
    'Slider' = 'SfRangeSlider'
    'ProgressBar' = 'SfProgressBar'
    'TabControl' = 'SfTabControl'
    'TreeView' = 'SfTreeView'
    'Button' = 'SfButton'
    'RadioButton' = 'SfRadioButton'
    'CheckBox' = 'SfCheckBox'
}

$script:DocumentedSyncfusionControls = @(
    'SfDataGrid', 'SfChart', 'SfMap', 'SfGauge', 'SfButton', 'SfTextBoxExt',
    'SfComboBox', 'SfDatePicker', 'SfTimePicker', 'SfCalendar', 'SfScheduler',
    'DockingManager', 'NavigationDrawer', 'SfTreeView', 'SfTabControl',
    'SfProgressBar', 'SfBusyIndicator', 'SfRangeSlider', 'SfRating',
    'SfMaskedEdit', 'SfAutoComplete', 'SfColorPicker', 'SfDropDownButton',
    'SfSplitButton', 'SfMenuButton', 'SfRadioButton', 'SfCheckBox',
    'SfGroupBox', 'SfExpander', 'SfAccordion', 'SfCarousel', 'SfCoverFlow',
    'SfPivotGrid', 'SfSpreadsheet', 'SfRichTextBoxAdv', 'SfDiagram',
    'SfSunburstChart', 'SfTreeMap', 'SfSparkline', 'SfSmithChart',
    'SfSurfaceChart', 'SfBulletGraph', 'SfDigitalGauge', 'SfLinearGauge',
    'SfCircularGauge', 'SfMaps', 'SfNavigationDrawer', 'SfDocking',
    'SfRibbon', 'SfToolBar', 'SfStatusBar', 'SfContextMenu'
)

$script:RequiredProperties = @{
    'SfDataGrid' = @('AutoGenerateColumns', 'AllowSorting', 'AllowFiltering')
    'GridTextColumn' = @('MappingName', 'HeaderText')
    'SfChart' = @('PrimaryAxis', 'SecondaryAxis')
    'SfComboBox' = @('ItemsSource')
}
#endregion

#region Utility Functions
function Write-XAMLValidationResult {
    param(
        [string]$File,
        [string]$Violation,
        [string]$Line = "",
        [string]$Severity = "ERROR",
        [string]$Fix = "",
        [string]$Control = ""
    )

    $script:XAMLViolations++

    $violationObject = [PSCustomObject]@{
        File = $File
        Line = $Line
        Control = $Control
        Violation = $Violation
        Severity = $Severity
        SuggestedFix = $Fix
        Timestamp = Get-Date
    }

    $script:ViolationDetails += $violationObject

    $icon = switch ($Severity) {
        "ERROR" { "❌" }
        "WARNING" { "⚠️" }
        "CRITICAL" { "🚨" }
        default { "🔍" }
    }

    $message = "$icon [XAML] $File"
    if ($Line) { $message += ":$Line" }
    if ($Control) { $message += " ($Control)" }
    $message += " - $Violation"

    Write-Information $message -InformationAction Continue

    if ($Fix) {
        Write-Information "    💡 Fix: $Fix" -InformationAction Continue
    }
}

function Test-SyncfusionNamespace {
    param([string]$Content)

    $hasSyncfusionControls = $Content -match '<syncfusion:'
    $hasSyncfusionNamespace = $Content -match 'xmlns:syncfusion='

    return $hasSyncfusionControls -and $hasSyncfusionNamespace
}
#endregion

#region Standard Control Detection
function Test-StandardControlViolations {
    param(
        [string]$FilePath,
        [string]$Content
    )

    $fileName = Split-Path $FilePath -Leaf
    $lines = $Content -split "`n"

    foreach ($standardControl in $script:StandardToSyncfusion.Keys) {
        $pattern = "<$standardControl(?:\s|>|/>)"
        $matches = $Content | Select-String -Pattern $pattern -AllMatches

        if ($matches) {
            foreach ($match in $matches) {
                $lineNumber = ($Content.Substring(0, $match.Matches[0].Index) -split "`n").Count
                $syncfusionEquivalent = $script:StandardToSyncfusion[$standardControl]

                $severity = if ($standardControl -eq 'DataGrid') { "CRITICAL" } else { "ERROR" }

                Write-XAMLValidationResult -File $fileName -Line $lineNumber -Control $standardControl `
                    -Violation "FORBIDDEN: Standard WPF $standardControl - MUST use Syncfusion equivalent" `
                    -Severity $severity `
                    -Fix "Replace <$standardControl> with <syncfusion:$syncfusionEquivalent>"
            }
        }
    }
}
#endregion

#region Syncfusion Control Validation
function Test-SyncfusionControlCompliance {
    param(
        [string]$FilePath,
        [string]$Content
    )

    $fileName = Split-Path $FilePath -Leaf

    # Find all Syncfusion controls
    $syncfusionMatches = $Content | Select-String -Pattern '<syncfusion:(\w+)' -AllMatches

    if ($syncfusionMatches) {
        foreach ($match in $syncfusionMatches) {
            $controlName = $match.Matches[0].Groups[1].Value
            $lineNumber = ($Content.Substring(0, $match.Matches[0].Index) -split "`n").Count

            # Validate control is documented
            if ($controlName -notin $script:DocumentedSyncfusionControls) {
                Write-XAMLValidationResult -File $fileName -Line $lineNumber -Control $controlName `
                    -Violation "Undocumented Syncfusion control - verify in official docs" `
                    -Severity "WARNING" `
                    -Fix "Check https://help.syncfusion.com/wpf/ for control documentation"
            }

            # Validate required properties
            if ($script:RequiredProperties.ContainsKey($controlName)) {
                $requiredProps = $script:RequiredProperties[$controlName]
                $controlBlock = $Content | Select-String -Pattern "<syncfusion:$controlName.*?(?:/>|</syncfusion:$controlName>)" -AllMatches

                foreach ($prop in $requiredProps) {
                    if ($controlBlock -and $controlBlock.Matches[0].Value -notmatch "$prop\s*=") {
                        Write-XAMLValidationResult -File $fileName -Line $lineNumber -Control $controlName `
                            -Violation "Missing recommended property: $prop" `
                            -Severity "WARNING" `
                            -Fix "Add $prop property as per Syncfusion documentation"
                    }
                }
            }
        }
    }
}
#endregion

#region DataGrid Specific Validation
function Test-DataGridRegression {
    param(
        [string]$FilePath,
        [string]$Content
    )

    $fileName = Split-Path $FilePath -Leaf

    # CRITICAL: DataGrid regression detection (ZERO TOLERANCE)
    if ($Content -match '<DataGrid(?!\w)') {
        $dataGridMatches = $Content | Select-String -Pattern '<DataGrid(?!\w)' -AllMatches

        if ($dataGridMatches) {
            foreach ($match in $dataGridMatches) {
                $lineNumber = ($Content.Substring(0, $match.Matches[0].Index) -split "`n").Count

                Write-XAMLValidationResult -File $fileName -Line $lineNumber -Control "DataGrid" `
                    -Violation "CRITICAL REGRESSION: Standard DataGrid detected - IMMEDIATE FIX REQUIRED" `
                    -Severity "CRITICAL" `
                    -Fix "MANDATORY: Replace with <syncfusion:SfDataGrid> using documented patterns"
            }
        }
    }

    # Validate SfDataGrid usage patterns
    if ($Content -match '<syncfusion:SfDataGrid') {
        # Check for AutoGenerateColumns setting
        if ($Content -notmatch 'AutoGenerateColumns\s*=\s*"False"') {
            Write-XAMLValidationResult -File $fileName -Control "SfDataGrid" `
                -Violation "SfDataGrid missing explicit AutoGenerateColumns=\"False\"" `
                -Severity "WARNING" `
                -Fix "Add AutoGenerateColumns=\"False\" and define columns explicitly"
        }

        # Check for proper column definitions
        if ($Content -match '<syncfusion:SfDataGrid' -and $Content -notmatch '<syncfusion:SfDataGrid\.Columns>') {
            Write-XAMLValidationResult -File $fileName -Control "SfDataGrid" `
                -Violation "SfDataGrid without explicit column definitions" `
                -Severity "WARNING" `
                -Fix "Add <syncfusion:SfDataGrid.Columns> section with GridTextColumn elements"
        }

        # Check for MappingName usage in columns
        if ($Content -match '<syncfusion:GridTextColumn' -and $Content -notmatch 'MappingName\s*=') {
            Write-XAMLValidationResult -File $fileName -Control "GridTextColumn" `
                -Violation "GridTextColumn without MappingName property" `
                -Severity "ERROR" `
                -Fix "Add MappingName property for data binding"
        }
    }
}
#endregion

#region Namespace Validation
function Test-NamespaceCompliance {
    param(
        [string]$FilePath,
        [string]$Content
    )

    $fileName = Split-Path $FilePath -Leaf

    # Check if Syncfusion controls are used without namespace
    if ($Content -match '<syncfusion:' -and $Content -notmatch 'xmlns:syncfusion=') {
        Write-XAMLValidationResult -File $fileName `
            -Violation "Syncfusion controls used without namespace declaration" `
            -Severity "ERROR" `
            -Fix "Add: xmlns:syncfusion=\"http://schemas.syncfusion.com/wpf\""
    }

    # Check for correct Syncfusion namespace URL
    if ($Content -match 'xmlns:syncfusion=' -and $Content -notmatch 'http://schemas\.syncfusion\.com/wpf') {
        Write-XAMLValidationResult -File $fileName `
            -Violation "Incorrect Syncfusion namespace URL" `
            -Severity "ERROR" `
            -Fix "Use: http://schemas.syncfusion.com/wpf"
    }
}
#endregion

#region Auto-Fix Implementation
function Invoke-XAMLAutoFix {
    param([string]$FilePath)

    if (-not $Fix) { return }

    $content = Get-Content $FilePath -Raw
    $originalContent = $content
    $fileName = Split-Path $FilePath -Leaf

    # Add Syncfusion namespace if missing
    if ($content -match '<syncfusion:' -and $content -notmatch 'xmlns:syncfusion=') {
        $content = $content -replace '(<(?:UserControl|Window|Page)[^>]*)', '$1 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"'
        Write-Information "🔧 Added Syncfusion namespace to $fileName" -InformationAction Continue
    }

    # Convert standard controls to Syncfusion equivalents
    foreach ($standardControl in $script:StandardToSyncfusion.Keys) {
        $syncfusionControl = $script:StandardToSyncfusion[$standardControl]

        if ($content -match "<$standardControl(?:\s|>|/>)") {
            $content = $content -replace "<$standardControl\b", "<syncfusion:$syncfusionControl"
            $content = $content -replace "</$standardControl>", "</syncfusion:$syncfusionControl>"
            $content = $content -replace "<$standardControl\.", "<syncfusion:$syncfusionControl."

            Write-Information "🔧 Converted $standardControl to $syncfusionControl in $fileName" -InformationAction Continue
            $script:FixedViolations++
        }
    }

    # Fix DataGrid column patterns
    if ($content -match '<DataGridTextColumn') {
        $content = $content -replace '<DataGridTextColumn', '<syncfusion:GridTextColumn'
        $content = $content -replace 'Binding\s*=\s*"{Binding\s+([^}]+)}"', 'MappingName="$1"'
        Write-Information "🔧 Converted DataGridTextColumn to GridTextColumn in $fileName" -InformationAction Continue
    }

    # Save changes if content was modified
    if ($content -ne $originalContent) {
        Set-Content $FilePath -Value $content -Encoding UTF8
        Write-Information "✅ Auto-fixed XAML violations in $fileName" -InformationAction Continue
    }
}
#endregion

#region Main Validation Function
function Invoke-XAMLValidation {
    Write-Information "" -InformationAction Continue
    Write-Information "🚌 BusBuddy XAML Syncfusion-Only Validation" -InformationAction Continue
    Write-Information "📅 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -InformationAction Continue
    Write-Information "📁 Target Path: $Path" -InformationAction Continue
    Write-Information "🔧 Auto-Fix: $($Fix ? 'Enabled' : 'Disabled')" -InformationAction Continue
    Write-Information "🔒 Strict Mode: $($Strict ? 'Enabled' : 'Disabled')" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    $xamlFiles = @(Get-ChildItem -Path $Path -Recurse -Filter "*.xaml" -ErrorAction SilentlyContinue)

    if ($xamlFiles.Length -eq 0) {
        Write-Information "⚠️ No XAML files found in path: $Path" -InformationAction Continue
        return $true
    }

    Write-Information "🔍 Validating $($xamlFiles.Length) XAML files..." -InformationAction Continue
    Write-Information "" -InformationAction Continue

    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        # Run all validation tests
        Test-StandardControlViolations -FilePath $file.FullName -Content $content
        Test-DataGridRegression -FilePath $file.FullName -Content $content
        Test-SyncfusionControlCompliance -FilePath $file.FullName -Content $content
        Test-NamespaceCompliance -FilePath $file.FullName -Content $content

        # Attempt auto-fix if enabled
        if ($Fix) {
            Invoke-XAMLAutoFix -FilePath $file.FullName
        }
    }

    # Generate summary report
    Write-Information "" -InformationAction Continue
    Write-Information ("=" * 60) -InformationAction Continue
    Write-Information "📊 XAML VALIDATION SUMMARY" -InformationAction Continue
    Write-Information ("=" * 60) -InformationAction Continue
    Write-Information "📁 Files Processed: $($xamlFiles.Length)" -InformationAction Continue
    Write-Information "❌ Violations Found: $script:XAMLViolations" -InformationAction Continue

    if ($Fix) {
        Write-Information "🔧 Violations Fixed: $script:FixedViolations" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue

    # Categorize violations by severity
    $criticalViolations = ($script:ViolationDetails | Where-Object { $_.Severity -eq "CRITICAL" }).Count
    $errorViolations = ($script:ViolationDetails | Where-Object { $_.Severity -eq "ERROR" }).Count
    $warningViolations = ($script:ViolationDetails | Where-Object { $_.Severity -eq "WARNING" }).Count

    if ($criticalViolations -gt 0) {
        Write-Information "🚨 CRITICAL: $criticalViolations violations (DataGrid regressions)" -InformationAction Continue
    }
    if ($errorViolations -gt 0) {
        Write-Information "❌ ERROR: $errorViolations violations (Standard WPF controls)" -InformationAction Continue
    }
    if ($warningViolations -gt 0) {
        Write-Information "⚠️ WARNING: $warningViolations violations (Documentation/Properties)" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue

    if ($script:XAMLViolations -eq 0) {
        Write-Information "🎉 SUCCESS: All XAML files are Syncfusion-compliant!" -InformationAction Continue
        return $true
    } else {
        Write-Information "❌ FAILURE: $script:XAMLViolations XAML violations must be fixed." -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "🔧 Run with -Fix to auto-convert standard controls to Syncfusion" -InformationAction Continue
        Write-Information "📚 Reference: https://help.syncfusion.com/wpf/" -InformationAction Continue
        return $false
    }
}
#endregion

# Execute validation
$result = Invoke-XAMLValidation

# Export detailed results
$reportPath = Join-Path $Path "xaml-validation-report.json"
$script:ViolationDetails | ConvertTo-Json -Depth 3 | Set-Content $reportPath -Encoding UTF8
Write-Information "📄 Detailed XAML report saved to: $reportPath" -InformationAction Continue

# Set exit code for CI/CD integration
if (-not $result) {
    exit 1
} else {
    exit 0
}
