#Requires -Version 7.0
<#
.SYNOPSIS
BusBuddy Syncfusion Theme & Resource Validation Script
Ensures proper usage of Syncfusion themes and dynamic resources

.DESCRIPTION
Validates:
- DynamicResource vs StaticResource usage
- Hardcoded colors vs theme brushes
- Custom SolidColorBrush definitions
- Proper Syncfusion theme resource references
- Theme consistency across all XAML files

.PARAMETER Path
Root path to validate (defaults to current directory)

.PARAMETER Fix
Attempt to auto-convert hardcoded colors to theme resources

.PARAMETER ReportFile
Output file for detailed report

.EXAMPLE
bb-syncfusion-theme-checker
bb-syncfusion-theme-checker -Path "C:\BusBuddy" -Fix
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = (Get-Location).Path,

    [Parameter()]
    [switch]$Fix,

    [Parameter()]
    [string]$ReportFile = "syncfusion-theme-report.json"
)

# Initialize validation results
$script:ThemeViolations = @()
$script:ResourceViolations = @()
$script:ColorViolations = @()
$script:FixedIssues = @()

# Syncfusion FluentDark theme brush mappings
$script:SyncfusionThemeBrushes = @{
    # Primary theme brushes
    'PrimaryBackground' = 'Syncfusion.Themes.FluentDark.WPF.ControlBackground'
    'PrimaryForeground' = 'Syncfusion.Themes.FluentDark.WPF.ControlForeground'
    'SecondaryBackground' = 'Syncfusion.Themes.FluentDark.WPF.ContentBackground'
    'HeaderBackground' = 'Syncfusion.Themes.FluentDark.WPF.HeaderBackground'
    'HeaderForeground' = 'Syncfusion.Themes.FluentDark.WPF.HeaderForeground'
    'BorderBrush' = 'Syncfusion.Themes.FluentDark.WPF.BorderBrush'
    'AccentBrush' = 'Syncfusion.Themes.FluentDark.WPF.PrimaryBrush'
    'HoverBrush' = 'Syncfusion.Themes.FluentDark.WPF.HoverBrush'
    'SelectedBrush' = 'Syncfusion.Themes.FluentDark.WPF.SelectionBrush'
}

# Common hardcoded colors that should use theme resources
$script:ProblematicColors = @(
    '#FF2D3142', '#FF4F5D75', '#FF3A415A', '#FF007ACC', '#FF5A6B8C',
    '#FFFFFFFF', '#FF000000', '#FFEF233C', '#FF16A085', '#FFF0F0F0',
    'White', 'Black', 'Gray', 'DarkGray', 'LightGray'
)

# Officially documented Syncfusion WPF controls (as per help.syncfusion.com)
$script:DocumentedSyncfusionControls = @(
    # Buttons and Input
    'ButtonAdv',           # Official Syncfusion WPF Button control
    'SfButton',            # Modern button control
    'SfTextInputLayout',   # Text input with floating labels
    'SfTextBoxExt',        # Extended text box
    'SfMaskedEdit',        # Masked text input
    'SfNumericTextBox',    # Numeric input
    'SfNumericUpDown',     # Numeric up/down control
    'SfColorPicker',       # Color selection control
    'SfDatePicker',        # Date picker control
    'SfTimePicker',        # Time picker control
    'SfDateTimePicker',    # Date and time picker
    'SfDropDownButton',    # Drop-down button
    'SfSplitButton',       # Split button control

    # Data Controls
    'SfDataGrid',          # Primary data grid control
    'SfGridSplitter',      # Grid splitter
    'SfTreeGrid',          # Tree grid control
    'SfListView',          # List view control
    'SfComboBox',          # Combo box control
    'SfMultiColumnDropDownControl', # Multi-column dropdown
    'SfTreeView',          # Tree view control
    'SfDataPager',         # Data paging control

    # Charts and Visualization
    'SfChart',             # Chart control
    'SfChart3D',           # 3D chart control
    'SfSparkline',         # Sparkline chart
    'SfBarcode',           # Barcode generation
    'SfBulletGraph',       # Bullet graph
    'SfGauge',             # Gauge controls
    'SfDigitalGauge',      # Digital gauge
    'SfMap',               # Map control

    # Layout and Navigation
    'DockingManager',      # Docking layout manager
    'SfTabControl',        # Tab control
    'SfAccordion',         # Accordion control
    'SfCarousel',          # Carousel control
    'SfTileView',          # Tile view layout
    'SfNavigationDrawer',  # Navigation drawer
    'SfTreeNavigator',     # Tree navigation
    'SfGroupBar',          # Group bar control
    'SfTabSplitter',       # Tab splitter

    # Scheduling and Calendar
    'SfScheduler',         # Scheduler control
    'SfCalendar',          # Calendar control
    'SfDateTimeRangeNavigator', # Date range navigator

    # File and Document
    'PdfViewer',           # PDF viewer control
    'SfSpreadsheet',       # Spreadsheet control
    'SfRichTextBoxAdv',    # Rich text editor
    'SfSyntaxEditor',      # Syntax highlighting editor

    # Notification and Progress
    'SfProgressBar',       # Progress bar control
    'SfCircularProgressBar', # Circular progress
    'SfBusyIndicator',     # Busy indicator
    'SfHubTile',           # Hub tile control
    'SfNotificationBox',   # Notification control

    # Media and Imaging
    'SfImageEditor',       # Image editing control
    'SfRotator',           # Image rotator
    'SfBackStage'          # Backstage view (no comma on last item)

    # Validation note: All controls listed above are officially documented
    # at https://help.syncfusion.com/wpf/ and should NOT be flagged as undocumented
)<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Write-ThemeValidationHeader {
    Write-Host @"
🚌 BusBuddy Syncfusion Theme & Resource Validation
📅 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
📁 Target Path: $Path
🔧 Auto-Fix: $(if($Fix) { 'Enabled' } else { 'Disabled' })
"@ -ForegroundColor Cyan
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER FilePath
${3:Parameter description}

.PARAMETER Content
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Test-DynamicResourceUsage {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Write-Verbose "Checking DynamicResource usage in $FilePath"

    # Find StaticResource usage that should be DynamicResource for themes
    $staticResourceMatches = [regex]::Matches($Content, '\{StaticResource\s+([^}]+)\}')

    foreach ($match in $staticResourceMatches) {
        $resourceKey = $match.Groups[1].Value.Trim()
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

        # Check if it's a theme-related resource that should be dynamic
        if ($resourceKey -match 'Background|Foreground|Brush|Color|Theme') {
            $script:ResourceViolations += [PSCustomObject]@{
                File = (Split-Path $FilePath -Leaf)
                Line = $lineNumber
                Issue = "StaticResource should be DynamicResource for theme support"
                ResourceKey = $resourceKey
                Severity = "Warning"
                FixSuggestion = "Change {StaticResource $resourceKey} to {DynamicResource $resourceKey}"
            }
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER FilePath
${3:Parameter description}

.PARAMETER Content
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Test-HardcodedColor {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Write-Verbose "Checking for hardcoded colors in $FilePath"

    # Find hardcoded Color attributes
    $colorMatches = [regex]::Matches($Content, 'Color\s*=\s*["'']([^"'']+)["'']')

    foreach ($match in $colorMatches) {
        $colorValue = $match.Groups[1].Value
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

        if ($colorValue -in $script:ProblematicColors -or $colorValue -match '^#[0-9A-Fa-f]{6,8}$') {
            $script:ColorViolations += [PSCustomObject]@{
                File = (Split-Path $FilePath -Leaf)
                Line = $lineNumber
                Issue = "Hardcoded color should use Syncfusion theme brush"
                ColorValue = $colorValue
                Severity = "Error"
                FixSuggestion = "Replace with {DynamicResource [ThemeBrush]} or remove to use theme default"
            }
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER FilePath
${3:Parameter description}

.PARAMETER Content
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Test-CustomSolidColorBrush {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Write-Verbose "Checking for custom SolidColorBrush definitions in $FilePath"

    # Find SolidColorBrush definitions with hardcoded colors
    $brushMatches = [regex]::Matches($Content, '<SolidColorBrush\s+[^>]*Color\s*=\s*["'']([^"'']+)["'']', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    foreach ($match in $brushMatches) {
        $colorValue = $match.Groups[1].Value
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

        $script:ThemeViolations += [PSCustomObject]@{
            File = (Split-Path $FilePath -Leaf)
            Line = $lineNumber
            Issue = "Custom SolidColorBrush conflicts with Syncfusion theme"
            ColorValue = $colorValue
            Severity = "Error"
            FixSuggestion = "Remove custom brush and use Syncfusion theme brushes via DynamicResource"
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER FilePath
${3:Parameter description}

.PARAMETER Content
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Test-ThemeResourceReference {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Write-Verbose "Checking theme resource references in $FilePath"

    # Find custom resource references that might conflict
    $resourceMatches = [regex]::Matches($Content, '\{(?:Static|Dynamic)Resource\s+([^}]+)\}')

    foreach ($match in $resourceMatches) {
        $resourceKey = $match.Groups[1].Value.Trim()
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

        # Check for non-Syncfusion theme resources
        if ($resourceKey -match '^(BusBuddy\.|Custom|Header|Content|Primary|Secondary)' -and
            $resourceKey -notmatch 'Syncfusion\.Themes\.FluentDark') {

            $script:ThemeViolations += [PSCustomObject]@{
                File = (Split-Path $FilePath -Leaf)
                Line = $lineNumber
                Issue = "Custom theme resource may conflict with Syncfusion theme"
                ResourceKey = $resourceKey
                Severity = "Warning"
                FixSuggestion = "Consider using Syncfusion theme brushes instead of custom resources"
            }
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER FilePath
${3:Parameter description}

.PARAMETER Content
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Test-ThemeConsistency {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Write-Verbose "Checking theme consistency in $FilePath"

    # Check for inconsistent theme applications
    if ($Content -match 'Theme\s*=\s*["'']([^"'']+)["'']') {
        $themeValue = $Matches[1]

        if ($themeValue -ne "FluentDark" -and $themeValue -ne "FluentLight") {
            $lineNumber = ($Content.Substring(0, $Matches.Index) -split "`n").Count

            $script:ThemeViolations += [PSCustomObject]@{
                File = (Split-Path $FilePath -Leaf)
                Line = $lineNumber
                Issue = "Non-standard Syncfusion theme applied"
                ThemeValue = $themeValue
                Severity = "Warning"
                FixSuggestion = "Use Theme='FluentDark' or Theme='FluentLight' for consistency"
            }
        }
    }
}

<#
.SYNOPSIS
Test if Syncfusion controls used are officially documented

.DESCRIPTION
Validates that all Syncfusion controls used in XAML are from the official
documented control list to prevent usage of deprecated or undocumented controls

.PARAMETER FilePath
Path to the XAML file being validated

.PARAMETER Content
Content of the XAML file

.EXAMPLE
Test-DocumentedSyncfusionControls -FilePath $file.FullName -Content $content

.NOTES
References official Syncfusion documentation at https://help.syncfusion.com/wpf/
#>
function Test-DocumentedSyncfusionControl {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Write-Verbose "Checking for documented Syncfusion controls in $FilePath"

    # Find all syncfusion: control references
    $syncfusionMatches = [regex]::Matches($Content, '<syncfusion:([A-Za-z0-9]+)')

    foreach ($match in $syncfusionMatches) {
        $controlName = $match.Groups[1].Value
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count

        # Check if control is in documented list
        if ($controlName -notin $script:DocumentedSyncfusionControls) {
            $script:ThemeViolations += [PSCustomObject]@{
                File = (Split-Path $FilePath -Leaf)
                Line = $lineNumber
                Issue = "Potentially undocumented Syncfusion control used"
                ControlName = $controlName
                Severity = "Info"
                FixSuggestion = "Verify control exists in official Syncfusion WPF documentation at https://help.syncfusion.com/wpf/"
            }
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER FilePath
${3:Parameter description}

.PARAMETER Violations
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Invoke-ThemeAutoFix {
    param(
        [string]$FilePath,
        [array]$Violations
    )

    if (-not $Fix) { return }

    Write-Verbose "Attempting auto-fix for $FilePath"

    $content = Get-Content $FilePath -Raw
    $modified = $false

    foreach ($violation in $Violations | Where-Object { $_.File -eq (Split-Path $FilePath -Leaf) }) {
        switch ($violation.Issue) {
            "StaticResource should be DynamicResource for theme support" {
                $oldPattern = "\{StaticResource\s+$([regex]::Escape($violation.ResourceKey))\}"
                $newPattern = "{DynamicResource $($violation.ResourceKey)}"
                if ($content -match $oldPattern) {
                    $content = $content -replace $oldPattern, $newPattern
                    $modified = $true
                    $script:FixedIssues += "Fixed StaticResource to DynamicResource: $($violation.ResourceKey)"
                }
            }
            "Hardcoded color should use Syncfusion theme brush" {
                # Remove Color attribute to use theme default
                $oldPattern = "Color\s*=\s*[`"']$([regex]::Escape($violation.ColorValue))[`"']"
                if ($content -match $oldPattern) {
                    $content = $content -replace $oldPattern, ""
                    $modified = $true
                    $script:FixedIssues += "Removed hardcoded color: $($violation.ColorValue)"
                }
            }
        }
    }

    if ($modified) {
        Set-Content $FilePath -Value $content -Encoding UTF8
        Write-Information "Auto-fixed theme issues in $FilePath" -InformationAction Continue
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Write-ThemeValidationReport {
    $report = [PSCustomObject]@{
        Timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        Path = $Path
        Summary = [PSCustomObject]@{
            FilesProcessed = $script:FilesProcessed
            ThemeViolations = $script:ThemeViolations.Count
            ResourceViolations = $script:ResourceViolations.Count
            ColorViolations = $script:ColorViolations.Count
            TotalIssues = ($script:ThemeViolations + $script:ResourceViolations + $script:ColorViolations).Count
            FixedIssues = $script:FixedIssues.Count
        }
        Violations = [PSCustomObject]@{
            ThemeViolations = $script:ThemeViolations
            ResourceViolations = $script:ResourceViolations
            ColorViolations = $script:ColorViolations
        }
        FixedIssues = $script:FixedIssues
    }

    # Save detailed report
    $report | ConvertTo-Json -Depth 10 | Set-Content $ReportFile -Encoding UTF8

    # Display summary
    Write-Host @"

============================================================
📊 SYNCFUSION THEME VALIDATION SUMMARY
============================================================
📁 Files Processed: $($report.Summary.FilesProcessed)
🎨 Theme Violations: $($report.Summary.ThemeViolations)
📋 Resource Violations: $($report.Summary.ResourceViolations)
🎨 Color Violations: $($report.Summary.ColorViolations)
📊 Total Issues: $($report.Summary.TotalIssues)
🔧 Fixed Issues: $($report.Summary.FixedIssues)

"@ -ForegroundColor $(if ($report.Summary.TotalIssues -eq 0) { 'Green' } else { 'Yellow' })

    # Show top violations
    if ($script:ColorViolations.Count -gt 0) {
        Write-Host "🚨 TOP COLOR VIOLATIONS:" -ForegroundColor Red
        $script:ColorViolations | Select-Object File, Line, ColorValue, FixSuggestion | Format-Table -AutoSize
    }

    if ($script:ThemeViolations.Count -gt 0) {
        Write-Host "⚠️ TOP THEME VIOLATIONS:" -ForegroundColor Yellow
        $script:ThemeViolations | Select-Object File, Line, Issue, FixSuggestion | Format-Table -AutoSize
    }

    Write-Host "📄 Detailed report saved to: $ReportFile" -ForegroundColor Gray

    # Return success/failure
    return $report.Summary.TotalIssues -eq 0
}

#region Main Execution
Write-ThemeValidationHeader

$xamlFiles = Get-ChildItem -Path $Path -Filter "*.xaml" -Recurse |
    Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\packages\\' }

$script:FilesProcessed = $xamlFiles.Count

Write-Host "🔍 Validating $($xamlFiles.Count) XAML files for theme compliance..." -ForegroundColor Yellow

foreach ($file in $xamlFiles) {
    try {
        $content = Get-Content $file.FullName -Raw -ErrorAction Stop

        # Run all validation tests
        Test-DynamicResourceUsage -FilePath $file.FullName -Content $content
        Test-HardcodedColor -FilePath $file.FullName -Content $content
        Test-CustomSolidColorBrush -FilePath $file.FullName -Content $content
        Test-ThemeResourceReference -FilePath $file.FullName -Content $content
        Test-ThemeConsistency -FilePath $file.FullName -Content $content
        Test-DocumentedSyncfusionControl -FilePath $file.FullName -Content $content

        # Attempt auto-fix if enabled
        if ($Fix) {
            $allViolations = $script:ThemeViolations + $script:ResourceViolations + $script:ColorViolations
            Invoke-ThemeAutoFix -FilePath $file.FullName -Violations $allViolations
        }

        Write-Verbose "Processed: $($file.Name)"
    }
    catch {
        Write-Warning "Failed to process $($file.Name): $($_.Exception.Message)"
    }
}

# Generate and display report
$success = Write-ThemeValidationReport

# Exit with appropriate code
if ($success) {
    Write-Host "✅ All XAML files are properly using Syncfusion themes!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Theme violations found. Run with -Fix to attempt auto-correction." -ForegroundColor Red
    exit 1
}
#endregion
