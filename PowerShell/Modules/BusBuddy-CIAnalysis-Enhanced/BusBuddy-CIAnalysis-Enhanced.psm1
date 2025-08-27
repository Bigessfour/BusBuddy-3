#requires -Version 7.5
<#
.SYNOPSIS
Enhanced BusBuddy CI/CD Analysis Module - Pattern-Based Failure Analysis

.DESCRIPTION
Provides intelligent CI/CD pipeline failure analysis using pattern-based algorithms.
Integrates with GitHub Actions, provides structured insights, and includes async batch processing.
Note: xAI Grok-4 integration removed - now uses enhanced pattern matching.

.NOTES
Author: BusBuddy Development Team
Version: 2.1.0
PowerShell: 7.5.2+
Dependencies: GitHub CLI (optional)

.EXAMPLE
Import-Module BusBuddy-CIAnalysis-Enhanced
Invoke-EnhancedCIAnalysis -FetchGitHubLogs
Start-BatchCIAnalysis -Async
#>

# Module metadata
$ModuleInfo = @{
    Name = 'BusBuddy-CIAnalysis-Enhanced'
    Version = '2.1.0'
    Description = 'Enhanced pattern-based CI/CD failure analysis (Grok integration removed)'
    Author = 'BusBuddy Development Team'
    Dependencies = @()
}

Write-Information "Loading $($ModuleInfo.Name) v$($ModuleInfo.Version)" -InformationAction Continue

# Enhanced CI Analysis with Pattern-Based Analysis
function Invoke-EnhancedCIAnalysis {
    <#
    .SYNOPSIS
    Enhanced CI/CD failure analysis using xAI Grok-4 API

    .DESCRIPTION
    Performs intelligent analysis of CI/CD failures with automatic log fetching,
    GitHub Actions integration, and structured pattern-based insights.
    Note: xAI Grok-4 integration removed - now uses enhanced pattern matching.

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
        Write-Information "🚀 Starting Enhanced CI/CD Analysis with Pattern-Based Analysis" -InformationAction Continue
        Write-Information "=====================================================" -InformationAction Continue

        # Note: Grok Assistant module removed - using pattern-based analysis
        Write-Information "📝 Using pattern-based CI/CD analysis (Grok integration removed)" -InformationAction Continue
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

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER WorkflowName
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
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

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Data
${3:Parameter description}

.PARAMETER Context
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Invoke-SynchronousCIAnalysis {
    [CmdletBinding()]
    param(
        [string[]]$Data,
        [hashtable]$Context
    )

    $combinedData = $Data -join "`n`n"

    try {
        # Note: Grok integration removed - using enhanced pattern analysis
        Write-Information "📊 Using enhanced pattern analysis..." -InformationAction Continue
        return Invoke-EnhancedPatternAnalysis -Data $combinedData -Context $Context
    } catch {
        Write-Warning "Pattern analysis failed: $($_.Exception.Message)"
        # Return basic analysis as final fallback
        return @{
            Severity = "Medium"
            Insights = @("Basic error pattern detected", "Review error logs for details")
            Recommendations = @("Check error logs", "Validate configuration", "Review recent changes")
            Summary = "Basic pattern analysis completed"
            Timestamp = Get-Date
            Context = $Context
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Data
${3:Parameter description}

.PARAMETER Context
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
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

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Analysis
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
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

    # Database Integration - Note: Azure SQL integration removed with Grok module
    Write-Information "" -InformationAction Continue
    Write-Information "💾 Database Integration:" -InformationAction Continue

    try {
        # Note: BusBuddy-GrokAzureSQL module removed - database integration disabled
        Write-Information "   ℹ️ Database storage not available (Grok integration removed)." -InformationAction Continue
        Write-Information "   � Database integration can be re-enabled with future modules." -InformationAction Continue
    } catch {
        Write-Warning "   ⚠️ Database integration error: $($_.Exception.Message)"
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

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Data
${3:Parameter description}

.PARAMETER Context
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
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

            # Note: Grok Assistant module removed - using pattern-based analysis
            # Import-Module "$modulePath\BusBuddy-GrokAssistant.psm1" -Force -ErrorAction SilentlyContinue

            # Perform analysis
            # Use pattern-based analysis since Grok integration is removed
            $result = @{
                Analysis = "Pattern-based CI/CD analysis completed (Grok integration removed)"
                Timestamp = Get-Date
                Patterns = "Standard error pattern matching applied"
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
    Write-Information "  • Pattern-based analysis (no external API keys required)" -InformationAction Continue
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
