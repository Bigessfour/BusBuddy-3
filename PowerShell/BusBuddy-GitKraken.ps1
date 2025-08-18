#Requires -Version 7.5

<#
.SYNOPSIS
    BusBuddy GitKraken Pro Integration Module
.DESCRIPTION
    Enhanced GitKraken integration for BusBuddy project with Pro features
    Supports both GitKraken Desktop and CLI workflows for comprehensive Git management
.NOTES
    Author: BusBuddy Development Team
    Version: 1.0.0
    PowerShell: 7.5.2+
    GitKraken Pro Features: Launchpad, Advanced Workflows, Team Collaboration
#>

# Global variables for GitKraken integration
$script:GitKrakenConfig = @{
    RepoUrl = "https://github.com/Bigessfour/BusBuddy-3"
    ProjectName = "BusBuddy"
    DefaultBranch = "main"
    LaunchpadUrl = "https://gitkraken.dev/launchpad/personal?groupBy=none&prs=github&issues=github"
    SyncfusionDocsUrl = "https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf"
    AzureSqlDocsUrl = "https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql"
}

function Start-GitKrakenDesktop {
    <#
    .SYNOPSIS
        Launches GitKraken Desktop with the BusBuddy repository
    #>
    [CmdletBinding()]
    param()

    Write-Information "🚀 Starting GitKraken Desktop with BusBuddy repository..." -InformationAction Continue

    $gitKrakenPath = Get-GitKrakenPath
    if ($gitKrakenPath) {
        try {
            $currentPath = Get-Location
            Start-Process -FilePath $gitKrakenPath -ArgumentList "--path", $currentPath -WindowStyle Normal
            Write-Information "✅ GitKraken Desktop launched successfully" -InformationAction Continue
        }
        catch {
            Write-Warning "Failed to launch GitKraken Desktop: $($_.Exception.Message)"
            Write-Information "💡 Alternative: Open GitKraken manually and clone: $($script:GitKrakenConfig.RepoUrl)" -InformationAction Continue
        }
    }
    else {
        Write-Warning "GitKraken Desktop not found. Please install from: https://help.gitkraken.com/gitkraken-desktop/gitkraken-desktop-home/"
        Write-Information "📋 Manual setup steps:" -InformationAction Continue
        Write-Information "  1. Download and install GitKraken Pro" -InformationAction Continue
        Write-Information "  2. Clone repository: $($script:GitKrakenConfig.RepoUrl)" -InformationAction Continue
        Write-Information "  3. Configure authentication for GitHub" -InformationAction Continue
    }
}

function Test-GitKrakenCli {
    <#
    .SYNOPSIS
        Checks if GitKraken CLI is available and functional
    #>
    [CmdletBinding()]
    param()
    
    try {
        # Test for actual GitKraken CLI, not just git
        $gkHelp = & gk --help 2>$null
        if ($LASTEXITCODE -eq 0 -and $gkHelp -match "GitKraken CLI") {
            Write-Information "✅ GitKraken CLI detected and functional" -InformationAction Continue
            return $true
        }
    }
    catch {
        # GitKraken CLI not available
    }
    
    Write-Information "ℹ️  GitKraken CLI not detected. Desktop features will be used instead." -InformationAction Continue
    Write-Information "📥 Install GitKraken CLI: npm install -g @gitkraken/cli" -InformationAction Continue
    Write-Information "📥 Alternative: winget install gitkraken.cli" -InformationAction Continue
    return $false
}function Get-GitKrakenPath {
    <#
    .SYNOPSIS
        Locates GitKraken Desktop installation path
    #>
    [CmdletBinding()]
    param()

    $possiblePaths = @(
        "$env:LOCALAPPDATA\GitKraken\GitKraken.exe",
        "$env:PROGRAMFILES\GitKraken\GitKraken.exe",
        "${env:PROGRAMFILES(x86)}\GitKraken\GitKraken.exe"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

function Invoke-GitKrakenWorkflow {
    <#
    .SYNOPSIS
        Executes common GitKraken workflows for BusBuddy development
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Status', 'BranchAnalysis', 'CreatePR', 'LaunchLaunchpad', 'SetupCI')]
        [string]$Workflow
    )

    $hasGkCli = Test-GitKrakenCli

    switch ($Workflow) {
        'Status' {
            Write-Information "📊 Checking repository status..." -InformationAction Continue
            if ($hasGkCli) {
                & gk status
                & gk workflow list --limit 5
            }
            else {
                git status --porcelain
                git log --oneline -10
                Write-Information "💡 For enhanced status, use GitKraken Desktop or install CLI" -InformationAction Continue
            }
        }

        'BranchAnalysis' {
            Write-Information "🔍 Analyzing current branch..." -InformationAction Continue
            if ($hasGkCli) {
                & gk ai explain branch
                & gk stats
            }
            else {
                $currentBranch = git branch --show-current
                $commits = git log --oneline -5
                Write-Information "Current branch: $currentBranch" -InformationAction Continue
                Write-Information "Recent commits:" -InformationAction Continue
                $commits | ForEach-Object { Write-Information "  $_" -InformationAction Continue }
            }
        }

        'CreatePR' {
            Write-Information "🔄 Creating Pull Request workflow..." -InformationAction Continue
            $currentBranch = git branch --show-current

            if ($currentBranch -eq $script:GitKrakenConfig.DefaultBranch) {
                Write-Warning "Cannot create PR from main branch. Create a feature branch first."
                Write-Information "💡 Suggested workflow:" -InformationAction Continue
                Write-Information "  git checkout -b feature/your-feature-name" -InformationAction Continue
                Write-Information "  # Make your changes" -InformationAction Continue
                Write-Information "  git add . && git commit -m 'Your changes'" -InformationAction Continue
                Write-Information "  git push -u origin feature/your-feature-name" -InformationAction Continue
                return
            }

            if ($hasGkCli) {
                Write-Information "Creating PR via GitKraken CLI..." -InformationAction Continue
                & gk pr create --base $script:GitKrakenConfig.DefaultBranch --head $currentBranch
            }
            else {
                $repoUrl = $script:GitKrakenConfig.RepoUrl
                $prUrl = "$repoUrl/compare/$currentBranch"
                Write-Information "🌐 Opening PR creation in browser: $prUrl" -InformationAction Continue
                Start-Process $prUrl
            }
        }

        'LaunchLaunchpad' {
            Write-Information "🚀 Opening GitKraken Launchpad..." -InformationAction Continue
            Start-Process $script:GitKrakenConfig.LaunchpadUrl
            Write-Information "📋 Launchpad features:" -InformationAction Continue
            Write-Information "  • GitHub Issues integration" -InformationAction Continue
            Write-Information "  • Pull Request management" -InformationAction Continue
            Write-Information "  • Workflow monitoring" -InformationAction Continue
        }

        'SetupCI' {
            Write-Information "⚙️  Setting up CI/CD monitoring..." -InformationAction Continue
            if ($hasGkCli) {
                & gk workflow list
                Write-Information "Monitoring GitHub Actions workflows..." -InformationAction Continue
            }
            else {
                $actionsUrl = "$($script:GitKrakenConfig.RepoUrl)/actions"
                Write-Information "🌐 Opening GitHub Actions: $actionsUrl" -InformationAction Continue
                Start-Process $actionsUrl
            }
        }
    }
}

function New-BusBuddyBranch {
    <#
    .SYNOPSIS
        Creates a new branch following BusBuddy naming conventions
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$BranchName,

        [ValidateSet('feature', 'bugfix', 'hotfix', 'chore', 'docs')]
        [string]$BranchType = 'feature'
    )

    $fullBranchName = "$BranchType/$BranchName"

    Write-Information "🌿 Creating new branch: $fullBranchName" -InformationAction Continue

    try {
        git checkout -b $fullBranchName
        Write-Information "✅ Branch created successfully" -InformationAction Continue
        Write-Information "💡 Common next steps:" -InformationAction Continue
        Write-Information "  • Make your changes" -InformationAction Continue
        Write-Information "  • bbBuild (test build)" -InformationAction Continue
        Write-Information "  • bbTest (run tests)" -InformationAction Continue
        Write-Information "  • git add . && git commit -m 'Your changes'" -InformationAction Continue
        Write-Information "  • git push -u origin $fullBranchName" -InformationAction Continue
    }
    catch {
        Write-Error "Failed to create branch: $($_.Exception.Message)"
    }
}

function Show-BusBuddyGitKrakenHelp {
    <#
    .SYNOPSIS
        Displays comprehensive GitKraken integration help for BusBuddy
    #>
    [CmdletBinding()]
    param()
    
    Write-Information @"
🎯 BusBuddy GitKraken Pro Integration Guide

=== Quick Start Commands ===
Start-GitKrakenDesktop              - Launch GitKraken Desktop
Invoke-GitKrakenWorkflow -Workflow Status      - Check repo status
Invoke-GitKrakenWorkflow -Workflow BranchAnalysis  - Analyze current branch
New-BusBuddyBranch -BranchName "azure-sql-integration"  - Create feature branch

=== AI-Powered Commands (Pro/Advanced Required) ===
Invoke-GitKrakenAI -Command Commit              - AI-generated commit messages
Invoke-GitKrakenAI -Command ExplainBranch       - AI branch analysis
Invoke-GitKrakenAI -Command ExplainCommit -SHA <commit>  - AI commit explanation
Invoke-GitKrakenAI -Command CreatePR            - AI-powered PR creation
Invoke-GitKrakenAI -Command Changelog           - AI changelog generation

=== Project-Specific Workflows ===
Repository: $($script:GitKrakenConfig.RepoUrl)
Launchpad: $($script:GitKrakenConfig.LaunchpadUrl)

Branch Types:
  • feature/ - New features (Syncfusion controls, Azure SQL integration)
  • bugfix/  - Bug fixes
  • chore/   - Maintenance tasks
  • docs/    - Documentation updates

=== Integration with BusBuddy bb* Commands ===
Recommended workflow:
  1. Start-GitKrakenDesktop                    # Open GitKraken
  2. New-BusBuddyBranch -BranchName "my-feature"  # Create branch
  3. bbHealth                                  # Verify environment
  4. bbBuild                                   # Build project
  5. bbTest                                    # Run tests
  6. Invoke-GitKrakenAI -Command Commit        # AI commit message
  7. Invoke-GitKrakenAI -Command CreatePR      # AI PR creation

=== Troubleshooting ===
If AI commands fail:
  • Check authentication: gk auth login
  • Verify organization: gk organization list
  • Check AI tokens: gk ai tokens
  • Use fallback: Invoke-GitKrakenWorkflow commands

=== Documentation References ===
• Syncfusion WPF: $($script:GitKrakenConfig.SyncfusionDocsUrl)
• Azure SQL: $($script:GitKrakenConfig.AzureSqlDocsUrl)
• GitKraken Help: https://help.gitkraken.com/gitkraken-desktop/gitkraken-desktop-home/

=== Advanced Features (Pro) ===
• Launchpad: Issue tracking and PR management
• AI-powered branch analysis and commit generation
• Advanced workflow monitoring with AI insights
• Team collaboration features with AI assistance
"@ -InformationAction Continue
}

function Invoke-GitKrakenAI {
    <#
    .SYNOPSIS
        Executes GitKraken AI commands with proper error handling and fallbacks
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Commit', 'ExplainBranch', 'ExplainCommit', 'CreatePR', 'Changelog', 'TokenStatus')]
        [string]$Command,
        
        [string]$SHA,
        [string]$BaseBranch = 'main',
        [string]$HeadBranch,
        [switch]$IncludeDescription
    )
    
    # First verify GitKraken CLI is available
    $hasGkCli = Test-GitKrakenCli
    if (-not $hasGkCli) {
        Write-Warning "GitKraken CLI not available. Please install: npm install -g @gitkraken/cli"
        return $false
    }
    
    # Check authentication
    try {
        $authCheck = & gk auth status 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "GitKraken not authenticated. Run: gk auth login"
            return $false
        }
    }
    catch {
        Write-Warning "Unable to check GitKraken authentication status"
        return $false
    }
    
    switch ($Command) {
        'Commit' {
            Write-Information "🤖 Generating AI-powered commit message..." -InformationAction Continue
            try {
                if ($IncludeDescription) {
                    & gk ai commit -d
                }
                else {
                    & gk ai commit
                }
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "AI commit generation failed. Using fallback..."
                    Write-Information "💡 Manual commit suggested:" -InformationAction Continue
                    $status = git status --porcelain
                    if ($status) {
                        Write-Information "Files changed: $($status.Count) files" -InformationAction Continue
                        Write-Information "Use: git commit -m 'Your commit message'" -InformationAction Continue
                    }
                    return $false
                }
                return $true
            }
            catch {
                Write-Warning "GitKraken AI commit failed: $($_.Exception.Message)"
                return $false
            }
        }
        
        'ExplainBranch' {
            Write-Information "🔍 Getting AI analysis of current branch..." -InformationAction Continue
            try {
                & gk ai explain branch
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "AI branch explanation failed. Using fallback analysis..."
                    Invoke-GitKrakenWorkflow -Workflow BranchAnalysis
                    return $false
                }
                return $true
            }
            catch {
                Write-Warning "GitKraken AI branch explain failed: $($_.Exception.Message)"
                Invoke-GitKrakenWorkflow -Workflow BranchAnalysis
                return $false
            }
        }
        
        'ExplainCommit' {
            if (-not $SHA) {
                Write-Warning "SHA parameter required for ExplainCommit command"
                return $false
            }
            
            Write-Information "🔍 Getting AI analysis of commit $SHA..." -InformationAction Continue
            try {
                & gk ai explain commit $SHA
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "AI commit explanation failed. Using fallback..."
                    git show --stat $SHA
                    git log --oneline -1 $SHA
                    return $false
                }
                return $true
            }
            catch {
                Write-Warning "GitKraken AI commit explain failed: $($_.Exception.Message)"
                git show --stat $SHA
                return $false
            }
        }
        
        'CreatePR' {
            Write-Information "🔄 Creating AI-powered Pull Request..." -InformationAction Continue
            try {
                & gk ai pr create
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "AI PR creation failed. Using fallback..."
                    Invoke-GitKrakenWorkflow -Workflow CreatePR
                    return $false
                }
                return $true
            }
            catch {
                Write-Warning "GitKraken AI PR creation failed: $($_.Exception.Message)"
                Invoke-GitKrakenWorkflow -Workflow CreatePR
                return $false
            }
        }
        
        'Changelog' {
            Write-Information "📝 Generating AI-powered changelog..." -InformationAction Continue
            try {
                if ($HeadBranch -and $BaseBranch) {
                    & gk ai changelog --base $BaseBranch --head $HeadBranch
                }
                else {
                    & gk ai changelog
                }
                
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "AI changelog generation failed. Using fallback..."
                    $currentBranch = git branch --show-current
                    $commits = git log --oneline "$BaseBranch..$currentBranch"
                    Write-Information "Recent commits:" -InformationAction Continue
                    $commits | ForEach-Object { Write-Information "  $_" -InformationAction Continue }
                    return $false
                }
                return $true
            }
            catch {
                Write-Warning "GitKraken AI changelog failed: $($_.Exception.Message)"
                return $false
            }
        }
        
        'TokenStatus' {
            Write-Information "🎫 Checking AI token usage..." -InformationAction Continue
            try {
                & gk ai tokens
                return $true
            }
            catch {
                Write-Warning "Unable to check AI token status: $($_.Exception.Message)"
                return $false
            }
        }
    }
}

function Test-GitKrakenAiAvailability {
    <#
    .SYNOPSIS
        Tests if GitKraken AI features are available and working
    #>
    [CmdletBinding()]
    param()
    
    Write-Information "🤖 Testing GitKraken AI availability..." -InformationAction Continue
    
    # Check CLI availability
    $hasGkCli = Test-GitKrakenCli
    if (-not $hasGkCli) {
        Write-Warning "GitKraken CLI not available"
        return $false
    }
    
    # Check authentication
    try {
        $authCheck = & gk auth status 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "GitKraken not authenticated"
            return $false
        }
        Write-Information "✅ Authentication: OK" -InformationAction Continue
    }
    catch {
        Write-Warning "Authentication check failed"
        return $false
    }
    
    # Check organization
    try {
        $orgList = & gk organization list 2>$null
        if ($LASTEXITCODE -eq 0 -and $orgList) {
            Write-Information "✅ Organization: Configured" -InformationAction Continue
        }
        else {
            Write-Warning "Organization not properly configured"
            Write-Information "💡 Run: gk organization set <YOUR_ORG_NAME>" -InformationAction Continue
            return $false
        }
    }
    catch {
        Write-Warning "Organization check failed"
        return $false
    }
    
    # Test AI token status
    try {
        & gk ai tokens | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ AI Tokens: Available" -InformationAction Continue
        }
        else {
            Write-Warning "AI tokens may not be available"
            return $false
        }
    }
    catch {
        Write-Warning "AI token check failed"
        return $false
    }
    
    Write-Information "🎉 GitKraken AI is ready to use!" -InformationAction Continue
    return $true
}# Functions are automatically available when script is dot-sourced
# No Export-ModuleMember needed for .ps1 files

# Aliases for convenience
Set-Alias -Name gkstart -Value Start-GitKrakenDesktop
Set-Alias -Name gkworkflow -Value Invoke-GitKrakenWorkflow
Set-Alias -Name gkbranch -Value New-BusBuddyBranch
Set-Alias -Name gkhelp -Value Show-BusBuddyGitKrakenHelp
Set-Alias -Name gkai -Value Invoke-GitKrakenAI
Set-Alias -Name gkaitest -Value Test-GitKrakenAiAvailability

# Module initialization message
Write-Information "🎯 BusBuddy GitKraken integration loaded successfully" -InformationAction Continue
Write-Information "   Available commands: Start-GitKrakenDesktop, Invoke-GitKrakenWorkflow, New-BusBuddyBranch" -InformationAction Continue
Write-Information "   Available aliases: gkstart, gkworkflow, gkbranch, gkhelp" -InformationAction Continue
