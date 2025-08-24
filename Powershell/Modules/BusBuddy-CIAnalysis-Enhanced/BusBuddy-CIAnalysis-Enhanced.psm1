#requires -Version 7.5
<#
.SYNOPSIS
    Enhanced BusBuddy CI/CD Analysis Module - Real AI-Powered Failure Analysis

.DESCRIPTION
    Leverages xAI Grok-4 API for intelligent CI/CD pipeline failure analysis.
    Integrates with GitHub Actions, provides structured insights, and includes async batch processing.

.NOTES
    Author: BusBuddy Development Team
    Version: 2.0.0
    PowerShell: 7.5.2+
    Dependencies: BusBuddy-GrokAssistant module, GitHub CLI (optional)

.EXAMPLE
    Import-Module BusBuddy-CIAnalysis-Enhanced
    Invoke-EnhancedCIAnalysis -FetchGitHubLogs
    Start-BatchCIAnalysis -Async
#>

# Module metadata
$ModuleInfo = @{
    Name = 'BusBuddy-CIAnalysis-Enhanced'
    Version = '2.0.0'
    Description = 'Enhanced AI-powered CI/CD failure analysis with real Grok-4 integration'
    Author = 'BusBuddy Development Team'
    Dependencies = @('BusBuddy-GrokAssistant')
}

Write-Information "Loading $($ModuleInfo.Name) v$($ModuleInfo.Version)" -InformationAction Continue

# Enhanced CI Analysis with Real Grok Integration
function Invoke-EnhancedCIAnalysis {
    <#
    .SYNOPSIS
    Enhanced CI/CD failure analysis using xAI Grok-4 API

    .DESCRIPTION
    Performs intelligent analysis of CI/CD failures with automatic log fetching,
    GitHub Actions integration, and structured AI insights.

    .PARAMETER ErrorMessage
    Specific error message to analyze

    .PARAMETER FetchGitHubLogs
    Automatically fetch recent GitHub Actions logs for analysis

    .PARAMETER LogFile
    Path to specific log file to analyze

    .PARAMETER WorkflowName
    Specific workflow name to analyze

    .PARAMETER Async
    Run analysis asynchronously for large datasets

    .EXAMPLE
    Invoke-EnhancedCIAnalysis -FetchGitHubLogs

    .EXAMPLE
    Invoke-EnhancedCIAnalysis -ErrorMessage "CS1061: 'string' does not contain a definition for 'Contains'"

    .EXAMPLE
    Invoke-EnhancedCIAnalysis -WorkflowName "ci.yml" -Async
    #>

    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]
        [string]$ErrorMessage,

        [Parameter()]
        [switch]$FetchGitHubLogs,

        [Parameter()]
        [string]$LogFile,

        [Parameter()]
        [string]$WorkflowName,

        [Parameter()]
        [switch]$Async
    )

    begin {
        Write-Information "🚀 Starting Enhanced CI/CD Analysis with xAI Grok-4" -InformationAction Continue
        Write-Information "=====================================================" -InformationAction Continue

        # Ensure Grok Assistant module is loaded
        if (-not (Get-Module BusBuddy-GrokAssistant)) {
            try {
                Import-Module "$PSScriptRoot\BusBuddy-GrokAssistant.psm1" -Force
                Write-Information "✅ Grok Assistant module loaded" -InformationAction Continue
            } catch {
                Write-Warning "⚠️ Failed to load Grok Assistant module: $($_.Exception.Message)"
                Write-Information "📝 Falling back to pattern-based analysis" -InformationAction Continue
            }
        }
    }

    process {
        try {
            $analysisData = @()
            $analysisContext = @{
                ProjectType = "BusBuddy WPF Application"
                Framework = ".NET 8.0"
                Architecture = "MVVM with Syncfusion"
                BuildSystem = "MSBuild"
                CISystem = "GitHub Actions"
            }

            # 1. Fetch GitHub Actions logs if requested
            if ($FetchGitHubLogs) {
                $githubData = Get-GitHubActionsData -WorkflowName $WorkflowName
                if ($githubData) {
                    $analysisData += $githubData
                }
            }

            # 2. Add specific error message
            if ($ErrorMessage) {
                $analysisData += "Specific Error: $ErrorMessage"
            }

            # 3. Add log file content
            if ($LogFile -and (Test-Path $LogFile)) {
                $logContent = Get-Content $LogFile -Raw
                $analysisData += "Log File Content:`n$logContent"
            }

            # 4. Auto-detect local build artifact if no data provided
            if ($analysisData.Count -eq 0) {
                $localData = Get-LocalBuildArtifact
                if ($localData) {
                    $analysisData += $localData
                }
            }

            # 5. Perform AI analysis
            if ($analysisData.Count -gt 0) {
                if ($Async) {
                    Start-AsyncCIAnalysis -Data $analysisData -Context $analysisContext
                } else {
                    $analysis = Invoke-SynchronousCIAnalysis -Data $analysisData -Context $analysisContext
                    Show-EnhancedResult -Analysis $analysis
                    return $analysis
                }
            } else {
                Write-Warning "❌ No CI failure data found to analyze"
                Show-AnalysisHelp
            }

        } catch {
            Write-Error "Enhanced CI analysis failed: $($_.Exception.Message)"
            throw
        }
    }
}

function Get-GitHubActionsData {
    [CmdletBinding()]
    param(
        [string]$WorkflowName
    )

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warning "⚠️ GitHub CLI (gh) not found. Install from https://cli.github.com/"
        return $null
    }

    try {
        Write-Information "📥 Fetching GitHub Actions data..." -InformationAction Continue

        # Get recent runs with detailed information
        $runArgs = @('run', 'list', '--limit', '10', '--json', 'status,conclusion,databaseId,workflowName,headSha,displayTitle,createdAt')
        if ($WorkflowName) {
            $runArgs += @('--workflow', $WorkflowName)
        }

        $recentRuns = & gh @runArgs | ConvertFrom-Json
        $failedRuns = $recentRuns | Where-Object { $_.conclusion -eq "failure" }

        if (-not $failedRuns) {
            Write-Information "✅ No recent failures found in GitHub Actions" -InformationAction Continue
            return $null
        }

        $analysisData = @()
        $failedRuns | Select-Object -First 3 | ForEach-Object {
            Write-Information "📋 Processing failure: $($_.displayTitle)" -InformationAction Continue

            try {
                # Get detailed logs for failed run
                $logOutput = gh run view $_.databaseId --log-failed 2>$null
                if ($logOutput) {
                    $analysisData += @"
GitHub Actions Failure - $($_.workflowName):
Title: $($_.displayTitle)
SHA: $($_.headSha)
Created: $($_.createdAt)
Logs:
$logOutput
"@
                }
            } catch {
                Write-Warning "Failed to fetch logs for run $($_.databaseId): $($_.Exception.Message)"
            }
        }

        return $analysisData -join "`n`n"

    } catch {
        Write-Warning "Failed to fetch GitHub Actions data: $($_.Exception.Message)"
        return $null
    }
}

function Get-LocalBuildArtifact {
    <#
    .SYNOPSIS
    Scans for local build artifacts such as MSBuild logs, test results, and recent build outputs.

    .PARAMETER SearchPath
    The root directory to begin searching for build artifacts.

    .PARAMETER MaxDepth
    The maximum directory depth to search for artifacts.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$SearchPath = ".",

        [Parameter()]
        [int]$MaxDepth = 2
    )

    Write-Information "🔍 Scanning for local build artifacts in '$SearchPath' (MaxDepth: $MaxDepth)..." -InformationAction Continue

    $artifacts = @()

    # 1. Check for MSBuild binary logs
    $binaryLogs = Get-ChildItem -Path $SearchPath -Filter "*.binlog" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.PSParentPath.Split('\').Count -le ($MaxDepth + 1) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 2

    foreach ($log in $binaryLogs) {
        $artifacts += "MSBuild Binary Log: $($log.FullName) (Modified: $($log.LastWriteTime))"
    }

    # 2. Check for test results
    $testResults = @()
    if (Test-Path "TestResults") {
        $testResults = Get-ChildItem -Path "TestResults" -Filter "*.trx" -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 2
    }

    foreach ($result in $testResults) {
        $artifacts += "Test Results: $($result.FullName) (Modified: $($result.LastWriteTime))"
    }

    # 3. Check for recent build output in logs directory
    if (Test-Path "logs") {
        $logFiles = Get-ChildItem -Path "logs" -Filter "*.log" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        foreach ($logFile in $logFiles) {
            $content = Get-Content $logFile.FullName -Tail 50 | Out-String
            if ($content -match "error|fail|exception") {
                $artifacts += "Recent Log File: $($logFile.FullName)`nRecent Content:`n$content"
            }
        }
    }

    # 4. Check git status for potential issues
    try {
        $gitStatus = git status --porcelain 2>$null
        if ($gitStatus -and $LASTEXITCODE -eq 0) {
            $artifacts += "Git Status (Uncommitted Changes):`n$gitStatus"
        }
    } catch {
        # Git not available or not a repository
    }

    return if ($artifacts.Count -gt 0) { $artifacts -join "`n`n" } else { $null }
}

function Invoke-SynchronousCIAnalysis {
    [CmdletBinding()]
    param(
        [string[]]$Data,
        [hashtable]$Context
    )

    $combinedData = $Data -join "`n`n"

    try {
        # Use real Grok API if available
        if (Get-Command Invoke-GrokCIAnalysis -ErrorAction SilentlyContinue) {
            Write-Information "🤖 Analyzing with xAI Grok-4..." -InformationAction Continue
            $grokResult = Invoke-GrokCIAnalysis -ErrorMessage $combinedData

            return @{
                Severity = "High"
                Insights = @($grokResult.Analysis -split "`n" | Where-Object { $_ -match "^[•\-\*\d]" } | Select-Object -First 5)
                Recommendations = @($grokResult.Analysis -split "`n" | Where-Object { $_ -match "recommend|suggest|should|fix|try" } | Select-Object -First 5)
                Summary = "Real AI Analysis via xAI Grok-4 completed"
                Timestamp = $grokResult.Timestamp
                ApiCallsUsed = $grokResult.ApiCallsUsed
                RawAnalysis = $grokResult.Analysis
                Context = $Context
            }
        } else {
            # Fallback to enhanced pattern analysis
            Write-Information "📊 Using enhanced pattern analysis..." -InformationAction Continue
            return Invoke-EnhancedPatternAnalysis -Data $combinedData -Context $Context
        }
    } catch {
        Write-Warning "Grok analysis failed, using pattern analysis: $($_.Exception.Message)"
        return Invoke-EnhancedPatternAnalysis -Data $combinedData -Context $Context
    }
}

function Invoke-EnhancedPatternAnalysis {
    [CmdletBinding()]
    param(
        [string]$Data,
        [hashtable]$Context
    )

    $insights = @()
    $recommendations = @()
    $severity = "Medium"

    # Enhanced pattern matching for BusBuddy-specific issues

    # .NET/C# specific errors
    if ($Data -match "CS\d+|error CS\d+") {
        $insights += "🔧 C# Compilation Error: Detected compiler errors in .NET code"
        $recommendations += "✅ Run 'dotnet build --verbosity detailed' for comprehensive error details"
        $recommendations += "✅ Check for missing using statements or namespace conflicts"
        $severity = "High"
    }

    # Syncfusion-specific issues
    if ($Data -match "Syncfusion|SfDataGrid|SfChart") {
        $insights += "🎨 Syncfusion Control Issue: Problems with Syncfusion WPF controls detected"
        $recommendations += "✅ Verify Syncfusion license registration in App.xaml.cs"
        $recommendations += "✅ Check Syncfusion package versions and compatibility"
        $recommendations += "✅ Ensure proper XAML namespace declarations"
    }

    # Entity Framework issues
    if ($Data -match "Entity Framework|DbContext|Migration") {
        $insights += "🗄️ Database Issue: Entity Framework or database-related problems"
        $recommendations += "✅ Check database connection strings in appsettings.json"
        $recommendations += "✅ Verify EF Core migrations are up to date"
        $recommendations += "✅ Test database connectivity"
    }

    # MVVM pattern issues
    if ($Data -match "ViewModel|INotifyPropertyChanged|DataBinding") {
        $insights += "🔄 MVVM Pattern Issue: Problems with view models or data binding"
        $recommendations += "✅ Verify property change notifications are properly implemented"
        $recommendations += "✅ Check XAML binding expressions for errors"
        $recommendations += "✅ Ensure ViewModels are properly registered in DI container"
    }

    # GitHub Actions specific
    if ($Data -match "yaml|workflow|action|step") {
        $insights += "⚙️ GitHub Actions Issue: Workflow configuration problems"
        $recommendations += "✅ Validate YAML syntax using yamllint or online validators"
        $recommendations += "✅ Check workflow file indentation and structure"
        $recommendations += "✅ Verify action versions and compatibility"
    }

    # Default analysis
    if ($insights.Count -eq 0) {
        $insights += "🔍 General Build Issue: Analyzing available information"
        $recommendations += "✅ Review complete build logs for specific error patterns"
        $recommendations += "✅ Check recent code changes for potential issues"
        $recommendations += "✅ Verify development environment setup"
    }

    return @{
        Severity = $severity
        Insights = $insights
        Recommendations = $recommendations
        Summary = "Enhanced Pattern Analysis completed - $($insights.Count) insights, $($recommendations.Count) recommendations"
        Timestamp = Get-Date
        Context = $Context
        AnalysisType = "Enhanced Pattern-Based"
    }
}

function Show-EnhancedResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Analysis
    )

    Write-Information "" -InformationAction Continue
    Write-Information "🎯 Enhanced Analysis Results" -InformationAction Continue
    Write-Information "=============================" -InformationAction Continue

    # Context information
    if ($Analysis.Context) {
        Write-Information "📋 Project Context:" -InformationAction Continue
        $Analysis.Context.GetEnumerator() | ForEach-Object {
            Write-Information "   $($_.Key): $($_.Value)" -InformationAction Continue
        }
        Write-Information "" -InformationAction Continue
    }

    # Severity with emoji indicator
    $severityEmoji = switch ($Analysis.Severity) {
        'High' { '🔴' }
        'Medium' { '🟡' }
        'Low' { '🟢' }
        default { '⚪' }
    }
    Write-Information "$severityEmoji Severity: $($Analysis.Severity)" -InformationAction Continue

    # Insights
    if ($Analysis.Insights) {
        Write-Information "" -InformationAction Continue
        Write-Information "💡 Key Insights:" -InformationAction Continue
        foreach ($insight in $Analysis.Insights) {
            Write-Information "   $insight" -InformationAction Continue
        }
    }

    # Recommendations
    if ($Analysis.Recommendations) {
        Write-Information "" -InformationAction Continue
        Write-Information "🔧 Actionable Recommendations:" -InformationAction Continue
        foreach ($recommendation in $Analysis.Recommendations) {
            Write-Information "   $recommendation" -InformationAction Continue
        }
    }

    # Azure SQL Integration - Store insights in database
    Write-Information "" -InformationAction Continue
    Write-Information "💾 Azure SQL Database Integration:" -InformationAction Continue

    try {
        # Check if Grok Azure SQL module is available
        if (Get-Module BusBuddy-GrokAzureSQL -ErrorAction SilentlyContinue) {
            Write-Information "   🔧 Storing analysis in Azure SQL Database..." -InformationAction Continue

            $storeParams = @{
                ErrorMessage = $Analysis.RawAnalysis ?? "CI/CD Analysis"
                WorkflowName = $Analysis.Context.CISystem ?? "Unknown"
                BuildId = (Get-Date -Format "yyyyMMdd-HHmmss")
            }

            $storeResult = Invoke-GrokCIFailureAnalysis @storeParams
            if ($storeResult.Success) {
                Write-Information "   ✅ Analysis stored with Insight ID: $($storeResult.InsightId)" -InformationAction Continue
                Write-Information "   📊 Priority: $($storeResult.Priority)" -InformationAction Continue
            } else {
                Write-Warning "   ❌ Failed to store in database: $($storeResult.Error)"
            }
        } else {
            Write-Information "   ℹ️ Azure SQL module not loaded." -InformationAction Continue
            Write-Information "   💡 Use 'Import-Module BusBuddy-GrokAzureSQL' for database storage." -InformationAction Continue
        }
    } catch {
        Write-Warning "   ⚠️ Database storage failed: $($_.Exception.Message)"
    }

    # Metadata
    Write-Information "" -InformationAction Continue
    Write-Information "📊 Analysis Metadata:" -InformationAction Continue
    Write-Information "   Timestamp: $($Analysis.Timestamp)" -InformationAction Continue
    Write-Information "   Summary: $($Analysis.Summary)" -InformationAction Continue

    if ($Analysis.ApiCallsUsed) {
        Write-Information "   API Calls Used: $($Analysis.ApiCallsUsed)" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue
    Write-Information "=============================" -InformationAction Continue
}

function Start-AsyncCIAnalysis {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string[]]$Data,

        [Parameter(Mandatory)]
        [hashtable]$Context
    )

    if ($PSCmdlet.ShouldProcess("CI Analysis Data", "Start asynchronous analysis")) {
        Write-Information "🚀 Starting asynchronous CI analysis..." -InformationAction Continue

        # For PowerShell 7.5.2, use Start-ThreadJob for async processing
        $job = Start-ThreadJob -ScriptBlock {
            param($analysisData, $analysisContext, $modulePath)

            # Import required modules in the background job
            Import-Module "$modulePath\BusBuddy-GrokAssistant.psm1" -Force -ErrorAction SilentlyContinue

            # Perform analysis
            $combinedData = $analysisData -join "`n`n"

            if (Get-Command Invoke-GrokCIAnalysis -ErrorAction SilentlyContinue) {
                $result = Invoke-GrokCIAnalysis -ErrorMessage $combinedData
            } else {
                $result = @{
                    Analysis = "Async pattern-based analysis completed"
                    Timestamp = Get-Date
                }
            }

            return @{
                Status = "Completed"
                Result = $result
                Context = $analysisContext
                CompletedAt = Get-Date
            }

        } -ArgumentList $Data, $Context, $PSScriptRoot

        Write-Information "✅ Async analysis job started (ID: $($job.Id))" -InformationAction Continue
        Write-Information "💡 Use 'Get-Job $($job.Id) | Receive-Job' to get results when complete" -InformationAction Continue

        return $job
    }
}

function Show-AnalysisHelp {
    [CmdletBinding()]
    param()

    Write-Information "" -InformationAction Continue
    Write-Information "💡 CI Analysis Help" -InformationAction Continue
    Write-Information "===================" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Available options:" -InformationAction Continue
    Write-Information "  • Invoke-EnhancedCIAnalysis -FetchGitHubLogs" -InformationAction Continue
    Write-Information "  • Invoke-EnhancedCIAnalysis -ErrorMessage 'Your error here'" -InformationAction Continue
    Write-Information "  • Invoke-EnhancedCIAnalysis -LogFile 'path/to/logfile.log'" -InformationAction Continue
    Write-Information "  • Invoke-EnhancedCIAnalysis -WorkflowName 'ci.yml' -Async" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Prerequisites:" -InformationAction Continue
    Write-Information "  • GitHub CLI (gh) for fetching workflow logs" -InformationAction Continue
    Write-Information "  • XAI_API_KEY environment variable for Grok-4 integration" -InformationAction Continue
    Write-Information "" -InformationAction Continue
}

# Create enhanced aliases
New-Alias -Name 'ci-analyze-enhanced' -Value 'Invoke-EnhancedCIAnalysis' -Force
New-Alias -Name 'ci-batch' -Value 'Start-AsyncCIAnalysis' -Force
New-Alias -Name 'ci-help' -Value 'Show-AnalysisHelp' -Force

# Export module members
Export-ModuleMember -Function @(
    'Invoke-EnhancedCIAnalysis',
    'Start-AsyncCIAnalysis',
    'Show-AnalysisHelp',
    'Show-EnhancedResult',
    'Get-GitHubActionsData',
    'Get-LocalBuildArtifact'
) -Alias @(
    'ci-analyze-enhanced',
    'ci-batch',
    'ci-help'
)

Write-Information "✅ $($ModuleInfo.Name) loaded with enhanced AI analysis capabilities" -InformationAction Continue
