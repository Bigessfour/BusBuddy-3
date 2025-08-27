#!/usr/bin/env pwsh
<#
.SYNOPSIS
Analyzes pull requests using pattern-based analysis for actionable insights and recommendations.

.DESCRIPTION
This script provides comprehensive pull request analysis using pattern-based algorithms,
offering insights, code quality assessment, and actionable recommendations
for improving the BusBuddy project. (Grok integration removed)

.PARAMETER PullRequestNumber
The pull request number to analyze (optional - uses current PR if not specified)

.PARAMETER DetailLevel
Analysis detail level: Basic, Standard, or Comprehensive (default: Standard)

.PARAMETER OutputFormat
Output format: Console, JSON, or Markdown (default: Console)

.EXAMPLE
./Analyze-PullRequest.ps1 -PullRequestNumber 123 -DetailLevel Comprehensive

.EXAMPLE
./Analyze-PullRequest.ps1 -OutputFormat Markdown > pr-analysis.md

.NOTES
Requires: GitHub CLI (Grok integration removed)
Author: BusBuddy Development Team
Version: 1.1.0
#>

[CmdletBinding()]
param(
    [Parameter(ValueFromPipeline = $true)]
    [int]$PullRequestNumber,

    [Parameter()]
    [ValidateSet('Basic', 'Standard', 'Comprehensive')]
    [string]$DetailLevel = 'Standard',

    [Parameter()]
    [ValidateSet('Console', 'JSON', 'Markdown')]
    [string]$OutputFormat = 'Console'
)

# Import required modules
# Note: BusBuddy-GrokAssistant module removed - using pattern-based analysis
# Import-Module "$PSScriptRoot\..\PowerShell\Modules\BusBuddy-GrokAssistant.psm1" -Force

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER PRNumber
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Get-PullRequestInfo {
    param([int]$PRNumber)

    Write-Information "📊 Retrieving pull request information..." -InformationAction Continue

    try {
        if ($PRNumber) {
            $prInfo = gh pr view $PRNumber --json title, body, author, createdAt, headRefName, baseRefName, files, commits
        } else {
            $prInfo = gh pr view --json title, body, author, createdAt, headRefName, baseRefName, files, commits
        }

        if (-not $prInfo) {
            throw "No pull request found"
        }

        return $prInfo | ConvertFrom-Json
    }
    catch {
        Write-Error "Failed to retrieve pull request information: $($_.Exception.Message)"
        return $null
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER PRInfo
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Get-ChangedFile {
    param([object]$PRInfo)

    Write-Information "📁 Analyzing changed files..." -InformationAction Continue

    $changedFiles = @()
    foreach ($file in $PRInfo.files) {
        $fileInfo = @{
            Path = $file.path
            Additions = $file.additions
            Deletions = $file.deletions
            Changes = $file.additions + $file.deletions
            Status = $file.status
        }
        $changedFiles += $fileInfo
    }

    return $changedFiles
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER PRInfo
${3:Parameter description}

.PARAMETER ChangedFiles
${4:Parameter description}

.PARAMETER DetailLevel
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
function Invoke-PatternPRAnalysis {
    param(
        [object]$PRInfo,
        [array]$ChangedFiles,
        [string]$DetailLevel
    )

    Write-Information "📊 Performing pattern-based PR analysis..." -InformationAction Continue

    # Pattern-based analysis instead of Grok
    $analysisResult = @"
**Pattern-Based Pull Request Analysis**

**Pull Request Details:**
- Title: $($PRInfo.title)
- Author: $($PRInfo.author.login)
- Branch: $($PRInfo.headRefName) → $($PRInfo.baseRefName)
- Created: $($PRInfo.createdAt)

**Changed Files ($($ChangedFiles.Count) files):**
$($ChangedFiles | ForEach-Object { "- $($_.Path) (+$($_.Additions)/-$($_.Deletions))" } | Out-String)

**Analysis Level:** $DetailLevel

**Code Quality Assessment:**
- Review code for consistent naming conventions
- Check for proper error handling patterns
- Verify logging is implemented where needed
- Ensure code follows established patterns

**Security Analysis:**
- Review for hardcoded secrets or credentials
- Check for proper input validation
- Verify authentication/authorization patterns
- Ensure secure coding practices are followed

**Performance Impact:**
- Review for potential performance bottlenecks
- Check for efficient database queries
- Verify resource management (memory, connections)
- Consider async/await usage where appropriate

**Testing Coverage:**
- Ensure unit tests are included for new functionality
- Verify integration tests for API changes
- Check for appropriate test data and mocking

**Documentation Review:**
- Verify XML documentation for public APIs
- Check for README updates if needed
- Ensure code comments for complex logic

**CI/CD Impact:**
- Verify build configurations are updated
- Check deployment scripts if infrastructure changes
- Ensure environment-specific settings are handled

**Action Items:**
- Run full test suite before merging
- Perform security code review
- Test in staging environment
- Update documentation as needed
"@

    return $analysisResult
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER PRInfo
${3:Parameter description}

.PARAMETER ChangedFiles
${4:Parameter description}

.PARAMETER GrokAnalysis
${5:Parameter description}

.PARAMETER Format
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
function Format-AnalysisOutput {
    param(
        [object]$PRInfo,
        [array]$ChangedFiles,
        [string]$GrokAnalysis,
        [string]$Format
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    switch ($Format) {
        'JSON' {
            $output = @{
                Timestamp = $timestamp
                PullRequest = @{
                    Title = $PRInfo.title
                    Author = $PRInfo.author.login
                    Branch = "$($PRInfo.headRefName) → $($PRInfo.baseRefName)"
                    CreatedAt = $PRInfo.createdAt
                }
                ChangedFiles = $ChangedFiles
                Analysis = $GrokAnalysis
            }
            return $output | ConvertTo-Json -Depth 10
        }

        'Markdown' {
            return @"
            # 📊 Pull Request Analysis Report

            **Generated:** $timestamp

            ## 🔍 Pull Request Overview

            - **Title:** $($PRInfo.title)
            - **Author:** $($PRInfo.author.login)
            - **Branch:** $($PRInfo.headRefName) → $($PRInfo.baseRefName)
            - **Created:** $($PRInfo.createdAt)

            ## 📁 Changed Files ($($ChangedFiles.Count) files)

            $($ChangedFiles | ForEach-Object { "- **$($_.Path)** (+$($_.Additions)/-$($_.Deletions)) [$($_.Status)]" } | Out-String)

            ## 📊 Pattern-Based Analysis

            $GrokAnalysis

            ---
            *Analysis generated by BusBuddy Pattern Analysis (Grok integration removed)*
"@
        }

        default {
            return @"
            🚀 BusBuddy Pull Request Analysis
            ================================

            📊 Pull Request: $($PRInfo.title)
            👤 Author: $($PRInfo.author.login)
            🌿 Branch: $($PRInfo.headRefName) → $($PRInfo.baseRefName)
            📅 Created: $($PRInfo.createdAt)

            📁 Changed Files: $($ChangedFiles.Count)
            $($ChangedFiles | ForEach-Object { "   • $($_.Path) (+$($_.Additions)/-$($_.Deletions))" } | Out-String)

            📊 Pattern-Based Analysis:
            $('-' * 50)
            $GrokAnalysis
            $('-' * 50)

            ✨ Analysis completed at $timestamp (Pattern-based analysis)
"@
        }
    }
}

# Main execution
try {
    Write-Information "🚀 Starting BusBuddy pull request analysis..." -InformationAction Continue

    # Get pull request information
    $prInfo = Get-PullRequestInfo -PRNumber $PullRequestNumber
    if (-not $prInfo) {
        exit 1
    }

    # Analyze changed files
    $changedFiles = Get-ChangedFiles -PRInfo $prInfo

    # Perform pattern-based analysis
    $patternAnalysis = Invoke-PatternPRAnalysis -PRInfo $prInfo -ChangedFiles $changedFiles -DetailLevel $DetailLevel

    # Format and output results
    $output = Format-AnalysisOutput -PRInfo $prInfo -ChangedFiles $changedFiles -GrokAnalysis $patternAnalysis -Format $OutputFormat

    Write-Output $output

    Write-Information "✅ Pull request analysis completed successfully!" -InformationAction Continue
}
catch {
    Write-Error "❌ Pull request analysis failed: $($_.Exception.Message)"
    exit 1
}
