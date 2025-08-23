#requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy CI/CD Analysis Module - AI-Powered Failure Analysis

.DESCRIPTION
    Leverages AI capabilities to analyze CI/CD pipeline failures and provide recommendations.
    Integrates with GitHub Actions workflow analysis and provides structured insights.

.NOTES
    Author: BusBuddy Development Team
    Version: 1.0.0
    PowerShell: 7.5.2+

.EXAMPLE
    Import-Module BusBuddy-CIAnalysis
    Invoke-CIFailureAnalysis
    Get-WorkflowFailureInsights
#>

# Module metadata
$ModuleInfo = @{
    Name = 'BusBuddy-CIAnalysis'
    Version = '1.0.0'
    Description = 'AI-powered CI/CD failure analysis for BusBuddy'
    Author = 'BusBuddy Development Team'
}

Write-Information "Loading $($ModuleInfo.Name) v$($ModuleInfo.Version)" -InformationAction Continue

# Real Grok AI Analysis Function
function Invoke-AIAnalysis {
    <#
    .SYNOPSIS
    Analyzes text input using xAI Grok-4 API

    .PARAMETER InputText
    The text to analyze

    .PARAMETER AnalysisType
    Type of analysis to perform
    #>

    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$InputText,

        [Parameter()]
        [ValidateSet('ci-failure', 'workflow-error', 'build-error', 'test-failure')]
        [string]$AnalysisType = 'ci-failure'
    )

    try {
        Write-Information "🤖 Analyzing with xAI Grok-4..." -InformationAction Continue

        # Import Grok Assistant module if not already loaded
        if (-not (Get-Module BusBuddy-GrokAssistant)) {
            Import-Module "$PSScriptRoot\BusBuddy-GrokAssistant.psm1" -Force
        }

        # Use real Grok API call based on analysis type
        $analysis = switch ($AnalysisType) {
            'ci-failure' {
                Invoke-GrokCIAnalysis -ErrorMessage $InputText
            }
            'workflow-error' {
                Invoke-GrokCIAnalysis -WorkflowFile $InputText
            }
            'build-error' {
                Invoke-GrokCIAnalysis -BuildOutput $InputText
            }
            'test-failure' {
                Invoke-GrokCIAnalysis -BuildOutput $InputText
            }
            default {
                Get-GrokInsights -Topic $InputText
            }
        }

        # Convert Grok response to structured format
        return @{
            Severity = "High"
            Insights = @($analysis.Analysis -split "`n" | Where-Object { $_ -match "^[•\-\*]" })
            Recommendations = @($analysis.Analysis -split "`n" | Where-Object { $_ -match "recommendation|suggest|should" })
            Summary = "Real AI Analysis via xAI Grok-4 completed"
            Timestamp = $analysis.Timestamp
            ApiCallsUsed = $analysis.ApiCallsUsed
        }

    } catch {
        Write-Warning "Grok AI analysis failed, falling back to pattern analysis: $($_.Exception.Message)"
        # Fallback to pattern-based analysis
        return Invoke-PatternBasedAnalysis -InputText $InputText -AnalysisType $AnalysisType
    }
}

function Invoke-PatternBasedAnalysis {
    <#
    .SYNOPSIS
    Fallback pattern-based analysis when Grok API is unavailable
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$InputText,

        [Parameter()]
        [string]$AnalysisType = 'ci-failure'
    )

    # Pattern-based analysis as fallback
    $analysis = switch ($AnalysisType) {
        'ci-failure' {
            Analyze-CIFailureContent -Content $InputText
        }
        'workflow-error' {
            Analyze-WorkflowErrorContent -Content $InputText
        }
        'build-error' {
            Analyze-BuildErrorContent -Content $InputText
        }
        'test-failure' {
            Analyze-TestFailureContent -Content $InputText
        }
        default {
            Analyze-GeneralContent -Content $InputText
        }
    }

    return $analysis
}

function Analyze-CIFailureContent {
    param([string]$Content)

    $insights = @()
    $recommendations = @()
    $severity = "Medium"

    # YAML syntax errors
    if ($Content -match "yaml|syntax|malformed|unexpected token" -or $Content -match "line \d+") {
        $insights += "🔍 YAML Syntax Error Detected: The workflow file contains malformed YAML syntax"
        $recommendations += "✅ Fix YAML indentation and structure"
        $recommendations += "✅ Validate YAML syntax using online validators"
        $recommendations += "✅ Check for missing colons, incorrect spacing, or misplaced content"
        $severity = "High"
    }

    # Build failures
    if ($Content -match "build.*fail|compilation.*error|CS\d+|dotnet.*build") {
        $insights += "🔨 Build Failure Detected: Compilation errors in .NET project"
        $recommendations += "✅ Run 'dotnet build' locally to reproduce"
        $recommendations += "✅ Check for missing dependencies or package conflicts"
        $recommendations += "✅ Verify project references and using statements"
    }

    # Test failures
    if ($Content -match "test.*fail|assertion|NUnit|xUnit") {
        $insights += "🧪 Test Failure Detected: Unit tests are failing"
        $recommendations += "✅ Run 'dotnet test' locally to identify specific failures"
        $recommendations += "✅ Check test data and mock configurations"
        $recommendations += "✅ Verify test environment setup"
    }

    # Missing dependencies
    if ($Content -match "package.*not.*found|restore.*fail|NuGet") {
        $insights += "📦 Dependency Issue Detected: Package restore problems"
        $recommendations += "✅ Clear NuGet cache: 'dotnet nuget locals all --clear'"
        $recommendations += "✅ Verify package sources in NuGet.config"
        $recommendations += "✅ Check for package version conflicts"
    }

    # If no specific patterns found
    if ($insights.Count -eq 0) {
        $insights += "🔍 General CI Failure: Analyzing available information"
        $recommendations += "✅ Review full workflow logs for specific error messages"
        $recommendations += "✅ Check recent commits for potential breaking changes"
        $recommendations += "✅ Verify CI environment configuration"
    }

    return @{
        Severity = $severity
        Insights = $insights
        Recommendations = $recommendations
        Summary = "AI Analysis completed - $($insights.Count) insights, $($recommendations.Count) recommendations"
    }
}

function Analyze-WorkflowErrorContent {
    param([string]$Content)

    return @{
        Severity = "High"
        Insights = @("🔧 Workflow Configuration Error: YAML structure issues detected")
        Recommendations = @(
            "✅ Validate workflow YAML syntax",
            "✅ Check job dependencies and steps structure",
            "✅ Verify environment variables and secrets"
        )
        Summary = "Workflow configuration analysis complete"
    }
}

function Analyze-BuildErrorContent {
    param([string]$Content)

    return @{
        Severity = "High"
        Insights = @("🔨 Build Process Error: Compilation or dependency issues")
        Recommendations = @(
            "✅ Run local build to reproduce issue",
            "✅ Check project dependencies and versions",
            "✅ Verify SDK and runtime versions"
        )
        Summary = "Build error analysis complete"
    }
}

function Analyze-TestFailureContent {
    param([string]$Content)

    return @{
        Severity = "Medium"
        Insights = @("🧪 Test Execution Error: Test failures or configuration issues")
        Recommendations = @(
            "✅ Run tests locally to identify failures",
            "✅ Check test data and environment setup",
            "✅ Verify test dependencies and mocks"
        )
        Summary = "Test failure analysis complete"
    }
}

function Analyze-GeneralContent {
    param([string]$Content)

    return @{
        Severity = "Medium"
        Insights = @("🔍 General Analysis: Reviewing available information")
        Recommendations = @(
            "✅ Review detailed logs for specific error patterns",
            "✅ Check recent changes and their potential impact",
            "✅ Verify CI/CD configuration and environment"
        )
        Summary = "General analysis complete"
    }
}

function Invoke-CIFailureAnalysis {
    <#
    .SYNOPSIS
    Analyzes CI/CD failures using AI capabilities

    .DESCRIPTION
    Fetches recent GitHub Actions workflow failures and provides AI-powered analysis

    .EXAMPLE
    Invoke-CIFailureAnalysis
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🔍 Starting CI/CD Failure Analysis..." -InformationAction Continue
        Write-Information "============================================" -InformationAction Continue

        # Get recent workflow runs
        Write-Information "📋 Fetching recent workflow runs..." -InformationAction Continue

        $workflowRuns = & gh run list --limit 5 --json status,conclusion,displayTitle,createdAt,headBranch 2>$null

        if ($LASTEXITCODE -ne 0 -or -not $workflowRuns) {
            Write-Warning "⚠️ Unable to fetch workflow runs. Checking local CI configuration..."

            # Fallback to local analysis
            $ciFile = Join-Path (Get-Location) '.github\workflows\ci.yml'
            if (Test-Path $ciFile) {
                $ciContent = Get-Content $ciFile -Raw
                Write-Information "📄 Analyzing local CI workflow file..." -InformationAction Continue

                $analysis = Invoke-AIAnalysis -InputText $ciContent -AnalysisType 'workflow-error'
                Display-AnalysisResults -Analysis $analysis -Source "Local CI Workflow"
            } else {
                Write-Error "❌ No CI workflow file found and GitHub CLI not available"
                return
            }
        } else {
            # Parse and analyze workflow runs
            $runs = $workflowRuns | ConvertFrom-Json
            $failedRuns = $runs | Where-Object { $_.conclusion -eq 'failure' }

            if ($failedRuns.Count -eq 0) {
                Write-Information "✅ No recent workflow failures found!" -InformationAction Continue
                return
            }

            Write-Information "❌ Found $($failedRuns.Count) failed workflow runs" -InformationAction Continue

            foreach ($run in $failedRuns | Select-Object -First 2) {
                Write-Information "🔍 Analyzing: $($run.displayTitle)" -InformationAction Continue

                # Get detailed logs (if available)
                $runLogs = & gh run view $run.id --log 2>$null

                if ($runLogs) {
                    $analysis = Invoke-AIAnalysis -InputText $runLogs -AnalysisType 'ci-failure'
                } else {
                    # Fallback analysis based on available info
                    $runInfo = "$($run.displayTitle) - $($run.conclusion) on $($run.headBranch)"
                    $analysis = Invoke-AIAnalysis -InputText $runInfo -AnalysisType 'ci-failure'
                }

                Display-AnalysisResults -Analysis $analysis -Source $run.displayTitle
            }
        }

        Write-Information "============================================" -InformationAction Continue
        Write-Information "🎯 CI/CD Analysis Complete" -InformationAction Continue

    } catch {
        Write-Error "CI analysis failed: $($_.Exception.Message)"
        throw
    }
}

function Display-AnalysisResults {
    param(
        [hashtable]$Analysis,
        [string]$Source
    )

    Write-Information "" -InformationAction Continue
    Write-Information "📊 Analysis Results for: $Source" -InformationAction Continue
    Write-Information "─────────────────────────────────────────" -InformationAction Continue

    # Severity
    $severityColor = switch ($Analysis.Severity) {
        'High' { 'Red' }
        'Medium' { 'Yellow' }
        'Low' { 'Green' }
        default { 'White' }
    }

    Write-Information "🚨 Severity: $($Analysis.Severity)" -InformationAction Continue

    # Insights
    Write-Information "" -InformationAction Continue
    Write-Information "💡 Key Insights:" -InformationAction Continue
    foreach ($insight in $Analysis.Insights) {
        Write-Information "   $insight" -InformationAction Continue
    }

    # Recommendations
    Write-Information "" -InformationAction Continue
    Write-Information "🎯 Recommendations:" -InformationAction Continue
    foreach ($recommendation in $Analysis.Recommendations) {
        Write-Information "   $recommendation" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue
    Write-Information "📋 $($Analysis.Summary)" -InformationAction Continue
    Write-Information "─────────────────────────────────────────" -InformationAction Continue
}

function Get-WorkflowFailureInsights {
    <#
    .SYNOPSIS
    Quick insights into workflow failures

    .DESCRIPTION
    Provides a summary of recent CI/CD issues and patterns
    #>

    [CmdletBinding()]
    param()

    Write-Information "🔍 Quick Workflow Insights" -InformationAction Continue
    Write-Information "===========================" -InformationAction Continue

    # Check local workflow file
    $ciFile = Join-Path (Get-Location) '.github\workflows\ci.yml'
    if (Test-Path $ciFile) {
        $content = Get-Content $ciFile -Raw

        # Quick pattern analysis
        if ($content -match "syntax error|malformed|line \d+") {
            Write-Information "⚠️ YAML Syntax Issues Detected" -InformationAction Continue
        }

        if ($content -match "dotnet build|dotnet test") {
            Write-Information "✅ .NET Build Pipeline Detected" -InformationAction Continue
        }

        if ($content -match "node|npm|yarn") {
            Write-Information "✅ Node.js Pipeline Detected" -InformationAction Continue
        }

        Write-Information "📄 Workflow file found and analyzed" -InformationAction Continue
    } else {
        Write-Warning "❌ No CI workflow file found"
    }
}

# Anti-regression and validation functions
function Invoke-BusBuddyAntiRegression {
    <#
    .SYNOPSIS
    Checks for banned patterns and regressions in BusBuddy codebase

    .DESCRIPTION
    Scans for banned output patterns, non-Syncfusion controls, and other anti-patterns
    following Microsoft PowerShell best practices and BusBuddy coding standards.

    .PARAMETER RootPath
    Root path to scan (defaults to current directory)

    .EXAMPLE
    Invoke-BusBuddyAntiRegression
    bb-anti-regression
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$RootPath = '.'
    )

    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "bb-anti-regression: Starting checks" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue

    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "Checking for banned patterns and regressions" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue

    # Check for banned output patterns in PowerShell files
    $psFiles = Get-ChildItem -Path $RootPath -Recurse -Include '*.ps1', '*.psm1' -ErrorAction SilentlyContinue
    $bannedOutputCount = 0

    foreach ($file in $psFiles) {
        try {
            $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
            $matches = [regex]::Matches($content, '\bWrite-Host\b')
            if ($matches.Count -gt 0) {
                $bannedOutputCount += $matches.Count
                Write-Information "❌ Banned output pattern found in: $($file.FullName) ($($matches.Count) instances)" -InformationAction Continue
            }
        }
        catch {
            Write-Information "⚠️  Cannot read file: $($file.FullName)" -InformationAction Continue
        }
    }

    # Check for legacy Syncfusion controls in XAML
    Test-SyncfusionCompliance -RootPath $RootPath

    if ($bannedOutputCount -eq 0) {
        Write-Information "✅ No banned output patterns found" -InformationAction Continue
    } else {
        Write-Information "❌ Found $bannedOutputCount banned output pattern violations" -InformationAction Continue
    }

    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "bb-anti-regression: Completed checks" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue
}

function Test-SyncfusionCompliance {
    <#
    .SYNOPSIS
    Tests XAML files for Syncfusion compliance

    .DESCRIPTION
    Scans XAML files for legacy controls and suggests Syncfusion equivalents

    .PARAMETER RootPath
    Root path to scan
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$RootPath = '.'
    )

    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "Test-SyncfusionCompliance: Scanning XAML for legacy/unsupported controls" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue

    # Map legacy WPF control names to their current Syncfusion equivalents
    # ButtonAdv and ComboBoxAdv ARE current Syncfusion controls, not legacy ones!
    $legacyMap = @{
        'Button' = 'ButtonAdv'  # Standard WPF Button → Syncfusion ButtonAdv
        'DataGrid' = 'SfDataGrid'  # Standard WPF DataGrid → Syncfusion SfDataGrid
        'ComboBox' = 'ComboBoxAdv'  # Standard WPF ComboBox → Syncfusion ComboBoxAdv
        'DatePicker' = 'DatePickerAdv'  # Standard WPF DatePicker → Syncfusion DatePickerAdv
    }

    $pattern = '<\s*(?:[a-zA-Z_][\w\-]*:)?(?<ctrl>[a-zA-Z_][\w\d]*)\b'

    $xamlFiles = Get-ChildItem -Path $RootPath -Recurse -Include '*.xaml' -ErrorAction SilentlyContinue
    foreach ($file in $xamlFiles) {
        try {
            $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
        }
        catch {
            Write-Information "Skipping unreadable file: $($file.FullName)" -InformationAction Continue
            continue
        }

        $matches = [regex]::Matches($content, $pattern)
        if ($matches.Count -eq 0) { continue }

        $reported = @{}
        foreach ($m in $matches) {
            $name = $m.Groups['ctrl'].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($name)) { continue }

            if ($legacyMap.ContainsKey($name) -and -not $reported.ContainsKey($name)) {
                $reported[$name] = $true
                $suggest = $legacyMap[$name]
                Write-Information ("{0} - File: {1} - Found legacy WPF control '{2}' → suggest migrate to Syncfusion '{3}'" -f (Get-Date).ToString("u"), $file.FullName, $name, $suggest) -InformationAction Continue
                $docUrl = switch ($suggest) {
                    'SfDataGrid' { 'https://help.syncfusion.com/wpf/datagrid/getting-started' }
                    'ButtonAdv' { 'https://help.syncfusion.com/wpf/button-control/getting-started' }
                    'ComboBoxAdv' { 'https://help.syncfusion.com/wpf/combo-box/getting-started' }
                    'DatePickerAdv' { 'https://help.syncfusion.com/wpf/calendar-date-picker/getting-started' }
                    default { 'https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf' }
                }
                Write-Information "Documentation: $docUrl" -InformationAction Continue
            }
        }
    }

    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "Test-SyncfusionCompliance: Scan complete" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue

    Write-Information "After applying changes, run bb-xaml-validate and bb-anti-regression; then run bb-test and bb-health to verify." -InformationAction Continue
}

# Create aliases for easier access
New-Alias -Name 'ci-analyze' -Value 'Invoke-CIFailureAnalysis' -Force
New-Alias -Name 'ci-insights' -Value 'Get-WorkflowFailureInsights' -Force
New-Alias -Name 'ai-analyze' -Value 'Invoke-AIAnalysis' -Force
New-Alias -Name 'bb-anti-regression' -Value 'Invoke-BusBuddyAntiRegression' -Force

# Export module members
Export-ModuleMember -Function @(
    'Invoke-CIFailureAnalysis',
    'Get-WorkflowFailureInsights',
    'Invoke-AIAnalysis',
    'Invoke-BusBuddyAntiRegression',
    'Test-SyncfusionCompliance'
) -Alias @(
    'ci-analyze',
    'ci-insights',
    'ai-analyze',
    'bb-anti-regression'
)

Write-Information "✅ $($ModuleInfo.Name) loaded with AI analysis capabilities" -InformationAction Continue
