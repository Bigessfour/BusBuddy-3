#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes pull requests using Grok assistant for actionable insights and recommendations.

.DESCRIPTION
    This script provides comprehensive pull request analysis using the Grok assistant,
    offering intelligent insights, code quality assessment, and actionable recommendations
    for improving the BusBuddy project.

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
    Requires: GitHub CLI, Grok API access, BusBuddy-GrokAssistant module
    Author: BusBuddy Development Team
    Version: 1.0.0
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
Import-Module "$PSScriptRoot\..\PowerShell\Modules\BusBuddy-GrokAssistant.psm1" -Force

function Get-PullRequestInfo {
    param([int]$PRNumber)

    Write-Information "📊 Retrieving pull request information..." -InformationAction Continue

    try {
        if ($PRNumber) {
            $prInfo = gh pr view $PRNumber --json title,body,author,createdAt,headRefName,baseRefName,files,commits
        } else {
            $prInfo = gh pr view --json title,body,author,createdAt,headRefName,baseRefName,files,commits
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

function Get-ChangedFiles {
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

function Invoke-GrokPRAnalysis {
    param(
        [object]$PRInfo,
        [array]$ChangedFiles,
        [string]$DetailLevel
    )

    Write-Information "🤖 Invoking Grok assistant for PR analysis..." -InformationAction Continue

    $analysisPrompt = @"
Please analyze this pull request for the BusBuddy project and provide actionable insights:

**Pull Request Details:**
- Title: $($PRInfo.title)
- Author: $($PRInfo.author.login)
- Branch: $($PRInfo.headRefName) → $($PRInfo.baseRefName)
- Created: $($PRInfo.createdAt)

**Description:**
$($PRInfo.body)

**Changed Files ($($ChangedFiles.Count) files):**
$($ChangedFiles | ForEach-Object { "- $($_.Path) (+$($_.Additions)/-$($_.Deletions))" } | Out-String)

**Analysis Level:** $DetailLevel

Please provide:
1. **Code Quality Assessment** - Overall quality, potential issues, best practices
2. **Security Analysis** - Security implications, vulnerabilities, recommendations
3. **Performance Impact** - Performance considerations, optimizations
4. **Architecture Review** - Architectural patterns, design decisions
5. **Testing Coverage** - Test adequacy, missing test scenarios
6. **Documentation Review** - Documentation quality, completeness
7. **CI/CD Impact** - Pipeline considerations, deployment implications
8. **Action Items** - Specific, prioritized recommendations for improvement

Focus on actionable insights that will improve code quality, security, and maintainability.
"@

    try {
        $grokResponse = Invoke-GrokAssistant -Prompt $analysisPrompt -SystemMessage "You are an expert code reviewer specializing in .NET/WPF applications, CI/CD pipelines, and software architecture. Provide thorough, actionable analysis."

        return $grokResponse
    }
    catch {
        Write-Error "Grok analysis failed: $($_.Exception.Message)"
        return "❌ Unable to complete Grok analysis. Please check your configuration and try again."
    }
}

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

## 🤖 Grok Assistant Analysis

$GrokAnalysis

---
*Analysis generated by BusBuddy Grok Assistant*
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

🤖 Grok Assistant Analysis:
$('-' * 50)
$GrokAnalysis
$('-' * 50)

✨ Analysis completed at $timestamp
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

    # Perform Grok analysis
    $grokAnalysis = Invoke-GrokPRAnalysis -PRInfo $prInfo -ChangedFiles $changedFiles -DetailLevel $DetailLevel

    # Format and output results
    $output = Format-AnalysisOutput -PRInfo $prInfo -ChangedFiles $changedFiles -GrokAnalysis $grokAnalysis -Format $OutputFormat

    Write-Output $output

    Write-Information "✅ Pull request analysis completed successfully!" -InformationAction Continue
}
catch {
    Write-Error "❌ Pull request analysis failed: $($_.Exception.Message)"
    exit 1
}
