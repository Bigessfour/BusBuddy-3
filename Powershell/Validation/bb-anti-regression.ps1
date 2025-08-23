#Requires -Version 7.5
<#
.SYNOPSIS
BusBuddy Anti-Regression Validation Script
Strictly enforces PowerShell, C#, XAML, and Syncfusion standards per copilot-instructions.md

.DESCRIPTION
ZERO TOLERANCE validation for:
- PowerShell 7.5.2 compliance (no Write-Host, proper output streams)
- C# .NET 8+ standards (no nullable violations, proper patterns)
- XAML Syncfusion-only controls (no standard WPF controls)
- Documented API usage only (official Microsoft/Syncfusion docs)

.PARAMETER Path
Root path to validate (defaults to current directory)

.PARAMETER Fix
Attempt to auto-fix violations where possible

.PARAMETER Detailed
Show detailed violation information

.EXAMPLE
bb-anti-regression
bb-anti-regression -Path "C:\BusBuddy" -Fix -Detailed
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = (Get-Location).Path,

    [Parameter()]
    [switch]$Fix,

    [Parameter()]
    [switch]$Detailed
)

# Initialize validation results
$script:TotalViolations = 0
$script:ViolationsByCategory = @{
    PowerShell = 0
    CSharp = 0
    XAML = 0
    Syncfusion = 0
    Documentation = 0
}

$script:ViolationDetails = @()

#region Utility Functions
function Write-ValidationHeader {
    param([string]$Title)
    $separator = "=" * 80
    Write-Information "" -InformationAction Continue
    Write-Information $separator -InformationAction Continue
    Write-Information "🔍 $Title" -InformationAction Continue
    Write-Information $separator -InformationAction Continue
}

function Write-ValidationResult {
    param(
        [string]$Category,
        [string]$File,
        [string]$Violation,
        [string]$Line = "",
        [string]$Severity = "ERROR",
        [string]$Fix = ""
    )

    $script:TotalViolations++
    $script:ViolationsByCategory[$Category]++

    $violationObject = [PSCustomObject]@{
        Category = $Category
        Severity = $Severity
        File = $File
        Line = $Line
        Violation = $Violation
        SuggestedFix = $Fix
        Timestamp = Get-Date
    }

    $script:ViolationDetails += $violationObject

    $icon = switch ($Severity) {
        "ERROR" { "❌" }
        "WARNING" { "⚠️" }
        "INFO" { "ℹ️" }
        default { "🔍" }
    }

    $message = "$icon [$Category] $File"
    if ($Line) { $message += ":$Line" }
    $message += " - $Violation"

    Write-Information $message -InformationAction Continue

    if ($Detailed -and $Fix) {
        Write-Information "    💡 Fix: $Fix" -InformationAction Continue
    }
}
#endregion

#region PowerShell Validation
function Test-PowerShellCompliance {
    Write-ValidationHeader "PowerShell 7.5.2 Compliance Validation"

    $psFiles = Get-ChildItem -Path $Path -Recurse -Filter "*.ps1" -ErrorAction SilentlyContinue
    $psFiles += Get-ChildItem -Path $Path -Recurse -Filter "*.psm1" -ErrorAction SilentlyContinue

    foreach ($file in $psFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue

        # CRITICAL: Write-Host violations (ZERO TOLERANCE)
        $writeHostLines = $lines | Select-String -Pattern "^\s*Write-Host\s" -AllMatches
        foreach ($match in $writeHostLines) {
            Write-ValidationResult -Category "PowerShell" -File $file.Name -Line $match.LineNumber `
                -Violation "FORBIDDEN Write-Host usage - violates Microsoft standards" `
                -Fix "Replace with Write-Information, Write-Output, or Write-Verbose"
        }

        # Console.WriteLine violations
        if ($content -match '\[Console\]::WriteLine|Console\.WriteLine') {
            Write-ValidationResult -Category "PowerShell" -File $file.Name `
                -Violation "Console.WriteLine usage - use PowerShell output streams" `
                -Fix "Replace with Write-Output or Write-Information"
        }

        # echo command violations
        if ($content -match '\becho\s') {
            Write-ValidationResult -Category "PowerShell" -File $file.Name `
                -Violation "echo command usage - use Write-Output" `
                -Fix "Replace 'echo' with 'Write-Output'"
        }

        # Improper parameter validation
        if ($content -match 'param\s*\(' -and $content -notmatch '\[CmdletBinding\(\)\]') {
            Write-ValidationResult -Category "PowerShell" -File $file.Name `
                -Violation "Missing [CmdletBinding()] attribute for advanced function" `
                -Fix "Add [CmdletBinding()] before param block"
        }

        # Missing Export-ModuleMember in modules
        if ($file.Extension -eq '.psm1' -and $content -notmatch 'Export-ModuleMember') {
            Write-ValidationResult -Category "PowerShell" -File $file.Name `
                -Violation "Missing Export-ModuleMember in module" `
                -Fix "Add Export-ModuleMember -Function FunctionName at end of module"
        }

        # Improper error handling patterns
        if ($content -match 'try\s*\{.*\}\s*catch\s*\{[^}]*\$_[^}]*\}' -and $content -notmatch 'throw') {
            Write-ValidationResult -Category "PowerShell" -File $file.Name `
                -Violation "Silent error swallowing in catch block" `
                -Fix "Add proper error handling with Write-Error or throw"
        }

        # Non-approved PowerShell verbs
        $functionMatches = $content | Select-String -Pattern 'function\s+(\w+)-(\w+)' -AllMatches
        foreach ($match in $functionMatches) {
            $verb = $match.Matches[0].Groups[1].Value
            $approvedVerbs = Get-Verb | Select-Object -ExpandProperty Verb
            if ($verb -notin $approvedVerbs) {
                Write-ValidationResult -Category "PowerShell" -File $file.Name `
                    -Violation "Non-approved PowerShell verb: $verb" `
                    -Fix "Use approved verb from Get-Verb cmdlet"
            }
        }
    }
}
#endregion

#region C# Validation
function Test-CSharpCompliance {
    Write-ValidationHeader "C# .NET 8+ Standards Validation"

    $csFiles = Get-ChildItem -Path $Path -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue

    foreach ($file in $csFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        # Microsoft.Extensions.Logging violations (must use Serilog)
        if ($content -match 'Microsoft\.Extensions\.Logging|ILogger<.*>.*Microsoft\.Extensions') {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Microsoft.Extensions.Logging usage - must use Serilog only" `
                -Fix "Replace with Serilog: private static readonly ILogger Logger = Log.ForContext<ClassName>();"
        }

        # Console.WriteLine in C#
        if ($content -match 'Console\.WriteLine') {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Console.WriteLine usage - use Serilog logging" `
                -Fix "Replace with Logger.Information() or Logger.Debug()"
        }

        # Debug.WriteLine violations
        if ($content -match 'Debug\.WriteLine') {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Debug.WriteLine usage - use Serilog logging" `
                -Fix "Replace with Logger.Debug()"
        }

        # Nullable reference type violations in new code
        if ($content -match '\?\s*\w+\s*[=;]' -and $file.CreationTime -gt (Get-Date).AddDays(-30)) {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Nullable reference types in new code - avoid if possible" `
                -Fix "Initialize with default values or use non-nullable patterns"
        }

        # Missing async/await patterns
        if ($content -match 'Task\.Result|\.Wait\(\)') {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Blocking async operations - use await" `
                -Fix "Replace .Result/.Wait() with proper async/await pattern"
        }

        # Missing using statements for IDisposable
        if ($content -match 'new\s+\w+Connection\(' -and $content -notmatch 'using\s*\(') {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Database connection without using statement" `
                -Fix "Wrap in using statement for proper disposal"
        }

        # Hard-coded connection strings
        if ($content -match 'Data Source=|Server=.*Initial Catalog=' -and $content -notmatch '\$\{|\$env:') {
            Write-ValidationResult -Category "CSharp" -File $file.Name `
                -Violation "Hard-coded connection string - use environment variables" `
                -Fix "Use configuration or environment variables"
        }
    }
}
#endregion

#region XAML Validation
function Test-XAMLCompliance {
    Write-ValidationHeader "XAML Syncfusion-Only Controls Validation"

    $xamlFiles = Get-ChildItem -Path $Path -Recurse -Filter "*.xaml" -ErrorAction SilentlyContinue

    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        # CRITICAL: Standard WPF DataGrid usage (ZERO TOLERANCE REGRESSION)
        if ($content -match '<DataGrid(?!\w)' -and $content -notmatch 'syncfusion:') {
            Write-ValidationResult -Category "XAML" -File $file.Name `
                -Violation "FORBIDDEN: Standard WPF DataGrid - must use Syncfusion SfDataGrid" `
                -Severity "ERROR" `
                -Fix "Replace <DataGrid> with <syncfusion:SfDataGrid>"
        }

        # Standard WPF ComboBox violations
        if ($content -match '<ComboBox(?!\w)' -and $content -notmatch 'syncfusion:') {
            Write-ValidationResult -Category "XAML" -File $file.Name `
                -Violation "Standard WPF ComboBox - prefer Syncfusion controls" `
                -Fix "Consider using syncfusion:SfComboBox"
        }

        # Standard WPF TextBox for enhanced scenarios
        if ($content -match '<TextBox(?!\w).*(?:AutoComplete|Mask|Validation)' -and $content -notmatch 'syncfusion:') {
            Write-ValidationResult -Category "XAML" -File $file.Name `
                -Violation "Standard TextBox with advanced features - use Syncfusion alternatives" `
                -Fix "Use syncfusion:SfTextBoxExt or syncfusion:SfMaskedEdit"
        }

        # Missing Syncfusion namespace
        if ($content -match '<syncfusion:' -and $content -notmatch 'xmlns:syncfusion=') {
            Write-ValidationResult -Category "XAML" -File $file.Name `
                -Violation "Syncfusion controls used without namespace declaration" `
                -Fix "Add: xmlns:syncfusion=\"http://schemas.syncfusion.com/wpf\""
        }

        # Standard WPF Chart/Graph controls
        if ($content -match '<(?:Chart|Graph|Plot)(?!\w)' -and $content -notmatch 'syncfusion:') {
            Write-ValidationResult -Category "XAML" -File $file.Name `
                -Violation "Standard WPF charting controls - use Syncfusion SfChart" `
                -Fix "Replace with syncfusion:SfChart for professional visualization"
        }

        # Hard-coded styles without theme consistency
        if ($content -match 'Background="#[0-9A-Fa-f]{6}"' -and $content -notmatch 'StaticResource|DynamicResource') {
            Write-ValidationResult -Category "XAML" -File $file.Name `
                -Violation "Hard-coded colors - use resource dictionaries" `
                -Fix "Use StaticResource for consistent theming"
        }
    }
}
#endregion

#region Syncfusion Documentation Compliance
function Test-SyncfusionCompliance {
    Write-ValidationHeader "Syncfusion Documentation Compliance"

    $xamlFiles = Get-ChildItem -Path $Path -Recurse -Filter "*.xaml" -ErrorAction SilentlyContinue
    $csFiles = Get-ChildItem -Path $Path -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue

    # Known documented Syncfusion WPF 30.1.42 controls
    $documentedControls = @(
        'SfDataGrid', 'SfChart', 'SfMap', 'SfGauge', 'SfButton', 'SfTextBoxExt',
        'SfComboBox', 'SfDatePicker', 'SfTimePicker', 'SfCalendar', 'SfScheduler',
        'DockingManager', 'NavigationDrawer', 'SfTreeView', 'SfTabControl',
        'SfProgressBar', 'SfBusyIndicator', 'SfRangeSlider', 'SfRating'
    )

    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        # Find Syncfusion control usage
        $syncfusionMatches = $content | Select-String -Pattern 'syncfusion:(\w+)' -AllMatches
        foreach ($match in $syncfusionMatches) {
            $controlName = $match.Matches[0].Groups[1].Value
            if ($controlName -notin $documentedControls) {
                Write-ValidationResult -Category "Syncfusion" -File $file.Name `
                    -Violation "Potentially undocumented Syncfusion control: $controlName" `
                    -Severity "WARNING" `
                    -Fix "Verify control exists in Syncfusion WPF 30.1.42 documentation"
            }
        }

        # Check for proper column definitions in SfDataGrid
        if ($content -match '<syncfusion:SfDataGrid' -and $content -notmatch 'AutoGenerateColumns="False"') {
            Write-ValidationResult -Category "Syncfusion" -File $file.Name `
                -Violation "SfDataGrid without explicit AutoGenerateColumns setting" `
                -Fix "Add AutoGenerateColumns=\"False\" and define columns explicitly"
        }

        # Check for MappingName usage (documented pattern)
        if ($content -match '<syncfusion:GridTextColumn' -and $content -notmatch 'MappingName=') {
            Write-ValidationResult -Category "Syncfusion" -File $file.Name `
                -Violation "GridTextColumn without MappingName property" `
                -Fix "Add MappingName property as per Syncfusion documentation"
        }
    }
}
#endregion

#region Documentation Reference Validation
function Test-DocumentationCompliance {
    Write-ValidationHeader "Official Documentation Reference Validation"

    $allFiles = Get-ChildItem -Path $Path -Recurse -Include "*.cs", "*.ps1", "*.psm1", "*.xaml" -ErrorAction SilentlyContinue

    # Required documentation references
    $requiredRefs = @{
        'PowerShell' = 'docs.microsoft.com/powershell'
        'Syncfusion' = 'help.syncfusion.com'
        'EFCore' = 'docs.microsoft.com/ef/core'
        'AzureSQL' = 'learn.microsoft.com/azure/azure-sql'
    }

    foreach ($file in $allFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        # Check for custom PowerShell functions without documentation
        if ($file.Extension -in @('.ps1', '.psm1') -and $content -match 'function\s+\w+-\w+' -and $content -notmatch '\.SYNOPSIS|\.DESCRIPTION') {
            Write-ValidationResult -Category "Documentation" -File $file.Name `
                -Violation "PowerShell function without proper documentation" `
                -Fix "Add .SYNOPSIS and .DESCRIPTION comment-based help"
        }

        # Check for public C# methods without XML documentation
        if ($file.Extension -eq '.cs' -and $content -match 'public\s+\w+\s+\w+\(' -and $content -notmatch '///\s*<summary>') {
            Write-ValidationResult -Category "Documentation" -File $file.Name `
                -Violation "Public C# method without XML documentation" `
                -Fix "Add /// <summary> XML documentation"
        }

        # Check for hardcoded API patterns without documentation reference
        if ($content -match 'new\s+Syncfusion\.' -and $content -notmatch 'help\.syncfusion\.com') {
            Write-ValidationResult -Category "Documentation" -File $file.Name `
                -Violation "Syncfusion API usage without documentation reference" `
                -Severity "WARNING" `
                -Fix "Add comment referencing help.syncfusion.com documentation"
        }
    }
}
#endregion

#region Auto-Fix Functions
function Invoke-AutoFix {
    if (-not $Fix) { return }

    Write-ValidationHeader "Auto-Fix Attempts"

    $fixableViolations = $script:ViolationDetails | Where-Object { $_.SuggestedFix -and $_.Severity -eq "ERROR" }

    foreach ($violation in $fixableViolations) {
        Write-Information "🔧 Attempting to fix: $($violation.File) - $($violation.Violation)" -InformationAction Continue

        # Auto-fix Write-Host violations
        if ($violation.Violation -match "Write-Host") {
            $filePath = Join-Path $Path $violation.File
            if (Test-Path $filePath) {
                $content = Get-Content $filePath -Raw
                $content = $content -replace 'Write-Host\s+([^-]+)(?:\s+-ForegroundColor\s+\w+)?', 'Write-Information $1 -InformationAction Continue'
                Set-Content $filePath -Value $content -Encoding UTF8
                Write-Information "✅ Fixed Write-Host violation in $($violation.File)" -InformationAction Continue
            }
        }

        # Auto-fix standard DataGrid to SfDataGrid
        if ($violation.Violation -match "Standard WPF DataGrid") {
            $filePath = Join-Path $Path $violation.File
            if (Test-Path $filePath) {
                $content = Get-Content $filePath -Raw
                if ($content -notmatch 'xmlns:syncfusion=') {
                    $content = $content -replace '(<UserControl[^>]*)', '$1 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"'
                }
                $content = $content -replace '<DataGrid\b', '<syncfusion:SfDataGrid'
                $content = $content -replace '</DataGrid>', '</syncfusion:SfDataGrid>'
                $content = $content -replace '<DataGrid\.', '<syncfusion:SfDataGrid.'
                Set-Content $filePath -Value $content -Encoding UTF8
                Write-Information "✅ Fixed DataGrid regression in $($violation.File)" -InformationAction Continue
            }
        }
    }
}
#endregion

#region Main Execution
function Invoke-AntiRegression {
    Write-Information "" -InformationAction Continue
    Write-Information "🚌 BusBuddy Anti-Regression Validation" -InformationAction Continue
    Write-Information "📅 $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -InformationAction Continue
    Write-Information "📁 Target Path: $Path" -InformationAction Continue
    Write-Information "🔧 Auto-Fix: $($Fix ? 'Enabled' : 'Disabled')" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    # Execute all validation tests
    Test-PowerShellCompliance
    Test-CSharpCompliance
    Test-XAMLCompliance
    Test-SyncfusionCompliance
    Test-DocumentationCompliance

    # Attempt auto-fixes if requested
    if ($Fix) {
        Invoke-AutoFix
    }

    # Generate summary report
    Write-ValidationHeader "VALIDATION SUMMARY"

    Write-Information "📊 Total Violations Found: $script:TotalViolations" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    foreach ($category in $script:ViolationsByCategory.Keys) {
        $count = $script:ViolationsByCategory[$category]
        $icon = if ($count -eq 0) { "✅" } else { "❌" }
        Write-Information "$icon $category`: $count violations" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue

    if ($script:TotalViolations -eq 0) {
        Write-Information "🎉 SUCCESS: No violations found! Code is compliant with BusBuddy standards." -InformationAction Continue
        return $true
    } else {
        Write-Information "❌ FAILURE: $script:TotalViolations violations must be fixed before commit." -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "📋 To see detailed violations, run with -Detailed parameter" -InformationAction Continue
        Write-Information "🔧 To attempt auto-fixes, run with -Fix parameter" -InformationAction Continue
        return $false
    }
}

# Export detailed results if requested
if ($Detailed -and $script:ViolationDetails) {
    $reportPath = Join-Path $Path "anti-regression-report.json"
    $script:ViolationDetails | ConvertTo-Json -Depth 3 | Set-Content $reportPath -Encoding UTF8
    Write-Information "📄 Detailed report saved to: $reportPath" -InformationAction Continue
}
#endregion

# Execute the validation
$result = Invoke-AntiRegression

# Set exit code for CI/CD integration
if (-not $result) {
    exit 1
} else {
    exit 0
}
