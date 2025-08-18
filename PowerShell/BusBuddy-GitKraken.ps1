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

# GitKraken Environment Initialization
function Initialize-GitKrakenEnvironment {
    <#
    .SYNOPSIS
        Initializes GitKraken environment with organization and provider sync
    .DESCRIPTION
        Sets up GitKraken CLI for AI features by configuring organization and syncing providers
    #>
    [CmdletBinding()]
    param()

    Write-Information "🚀 Initializing GitKraken Environment..." -InformationAction Continue
    
    try {
        # Step 1: Verify GitKraken CLI is available
        $gkVersion = & gk version 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "GitKraken CLI not found or not working"
        }
        Write-Verbose "GitKraken CLI found: $gkVersion"

        # Step 2: Check authentication
        $authStatus = & gk auth status 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "GitKraken not authenticated. Run 'gk auth login' to enable AI features."
            return $false
        }
        Write-Verbose "GitKraken authenticated successfully"

        # Step 3: Set organization (required for AI)
        $orgs = & gk organization list --format json 2>$null | ConvertFrom-Json
        if ($orgs -and $orgs.Count -gt 0) {
            $activeOrg = $orgs | Where-Object { $_.active -eq $true }
            if (-not $activeOrg) {
                # Set the first available organization
                $firstOrg = $orgs[0]
                Write-Information "Setting organization: $($firstOrg.name)" -InformationAction Continue
                & gk organization set $firstOrg.name | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "Failed to set GitKraken organization"
                    return $false
                }
            }
        }

        # Step 4: Sync providers for GitHub integration
        Write-Information "Syncing GitKraken providers..." -InformationAction Continue
        & gk provider list --sync | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to sync GitKraken providers"
        }

        # Step 5: Validate AI tokens
        $tokens = & gk ai tokens 2>$null
        if ($LASTEXITCODE -eq 0 -and $tokens) {
            Write-Information "✅ GitKraken AI ready with token allocation" -InformationAction Continue
            $script:GitKrakenConfig.AIEnabled = $true
            return $true
        } else {
            Write-Warning "GitKraken AI tokens not available"
            return $false
        }
    }
    catch {
        Write-Error "Failed to initialize GitKraken environment: $_"
        return $false
    }
}

# Decision Tree for GitKraken Integration
$script:GitKrakenDecisionTree = @{
    # Capability Detection
    HasCLI = $false
    HasAuthentication = $false
    HasOrganization = $false
    HasAITokens = $false

    # Available Commands (validated dynamically)
    ValidCommands = @{
        "gk ai explain branch" = $false
        "gk ai explain commit" = $false
        "gk ai pr create" = $false
        "gk ai commit" = $false
        "gk ai changelog" = $false
        "gk work list" = $false
        "gk work start" = $false
    }

    # Fallback Strategy
    FallbackStrategy = @{
        "AI Explain" = "git log + git diff analysis"
        "PR Creation" = "GitHub browser interface"
        "Commit Messages" = "Manual git commit"
        "Work Items" = "GitHub Issues browser"
        "CI Monitoring" = "GitHub Actions browser + gh CLI"
    }
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
            $script:GitKrakenDecisionTree.HasCLI = $true
            return $true
        }
    }
    catch {
        # GitKraken CLI not available
    }

    Write-Information "ℹ️  GitKraken CLI not detected. Desktop features will be used instead." -InformationAction Continue
    Write-Information "📥 Install GitKraken CLI: npm install -g @gitkraken/cli" -InformationAction Continue
    Write-Information "📥 Alternative: winget install gitkraken.cli" -InformationAction Continue
    $script:GitKrakenDecisionTree.HasCLI = $false
    return $false
}

function Initialize-GitKrakenCapabilities {
    <#
    .SYNOPSIS
        Performs comprehensive capability detection for GitKraken integration
    .DESCRIPTION
        Tests all GitKraken features and populates the decision tree for intelligent fallbacks
    #>
    [CmdletBinding()]
    param()

    Write-Information "🔍 Initializing GitKraken capabilities assessment..." -InformationAction Continue

    # Test CLI availability
    $hasCli = Test-GitKrakenCli
    if (-not $hasCli) {
        Write-Information "⚠️  GitKraken CLI unavailable - using fallback strategies for all operations" -InformationAction Continue
        return $false
    }

    # Test authentication
    try {
        & gk auth status 2>$null | Out-Null
        $script:GitKrakenDecisionTree.HasAuthentication = ($LASTEXITCODE -eq 0)
        if ($script:GitKrakenDecisionTree.HasAuthentication) {
            Write-Information "✅ GitKraken authentication: OK" -InformationAction Continue
        } else {
            Write-Warning "⚠️  GitKraken not authenticated - AI features unavailable"
        }
    }
    catch {
        $script:GitKrakenDecisionTree.HasAuthentication = $false
        Write-Warning "⚠️  Authentication check failed"
    }

    # Test organization setup
    if ($script:GitKrakenDecisionTree.HasAuthentication) {
        try {
            $orgList = & gk organization list 2>$null
            $script:GitKrakenDecisionTree.HasOrganization = ($LASTEXITCODE -eq 0 -and $orgList)
            if ($script:GitKrakenDecisionTree.HasOrganization) {
                Write-Information "✅ GitKraken organization: Configured" -InformationAction Continue
            } else {
                Write-Warning "⚠️  Organization not configured - some AI features may be limited"
            }
        }
        catch {
            $script:GitKrakenDecisionTree.HasOrganization = $false
        }
    }

    # Test AI token availability
    if ($script:GitKrakenDecisionTree.HasAuthentication) {
        try {
            $tokenStatus = & gk ai tokens 2>$null
            $script:GitKrakenDecisionTree.HasAITokens = ($LASTEXITCODE -eq 0)
            if ($script:GitKrakenDecisionTree.HasAITokens) {
                Write-Information "✅ GitKraken AI tokens: Available" -InformationAction Continue

                # Check for token limits
                if ($tokenStatus -match "low|limit|exhausted") {
                    Write-Warning "⚠️  AI token usage approaching limits"
                }
            } else {
                Write-Warning "⚠️  AI tokens unavailable - AI features disabled"
            }
        }
        catch {
            $script:GitKrakenDecisionTree.HasAITokens = $false
        }
    }

    # Test specific command availability
    $commandTests = @{
        "gk ai explain branch" = { & gk ai explain branch --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
        "gk ai explain commit" = { & gk ai explain commit --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
        "gk ai pr create" = { & gk ai pr create --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
        "gk ai commit" = { & gk ai commit --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
        "gk ai changelog" = { & gk ai changelog --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
        "gk work list" = { & gk work list --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
        "gk work start" = { & gk work start --help 2>$null | Out-Null; $LASTEXITCODE -eq 0 }
    }

    foreach ($command in $commandTests.Keys) {
        try {
            $script:GitKrakenDecisionTree.ValidCommands[$command] = & $commandTests[$command]
        }
        catch {
            $script:GitKrakenDecisionTree.ValidCommands[$command] = $false
        }
    }

    # Report capability summary
    $availableCommands = ($script:GitKrakenDecisionTree.ValidCommands.Values | Where-Object { $_ }).Count
    $totalCommands = $script:GitKrakenDecisionTree.ValidCommands.Count

    Write-Information "📊 GitKraken Capability Summary:" -InformationAction Continue
    Write-Information "  • CLI Available: $($script:GitKrakenDecisionTree.HasCLI)" -InformationAction Continue
    Write-Information "  • Authenticated: $($script:GitKrakenDecisionTree.HasAuthentication)" -InformationAction Continue
    Write-Information "  • Organization: $($script:GitKrakenDecisionTree.HasOrganization)" -InformationAction Continue
    Write-Information "  • AI Tokens: $($script:GitKrakenDecisionTree.HasAITokens)" -InformationAction Continue
    Write-Information "  • Available Commands: $availableCommands/$totalCommands" -InformationAction Continue

    return $script:GitKrakenDecisionTree.HasCLI
}

function Get-GitKrakenPath {
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
        Executes common GitKraken workflows for BusBuddy development with intelligent fallbacks
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Status', 'BranchAnalysis', 'CreatePR', 'LaunchLaunchpad', 'SetupCI', 'SmartCommit')]
        [string]$Workflow
    )

    # Initialize capabilities if not done already
    if (-not $script:GitKrakenDecisionTree.HasCLI) {
        Initialize-GitKrakenCapabilities | Out-Null
    }

    switch ($Workflow) {
        'Status' {
            Write-Information "📊 Checking repository status..." -InformationAction Continue

            if ($script:GitKrakenDecisionTree.HasCLI -and $script:GitKrakenDecisionTree.ValidCommands["gk work list"]) {
                try {
                    Write-Information "🔍 GitKraken work items:" -InformationAction Continue
                    & gk work list 2>$null
                    if ($LASTEXITCODE -ne 0) {
                        Write-Information "💡 No active work items" -InformationAction Continue
                    }
                }
                catch {
                    Write-Information "💡 Work items unavailable, using git status..." -InformationAction Continue
                }
            }

            # Always show git status for immediate repository state
            Write-Information "📋 Repository status:" -InformationAction Continue
            git status --short
            Write-Information "� Recent commits:" -InformationAction Continue
            git log --oneline -5
        }

        'BranchAnalysis' {
            Write-Information "🔍 Analyzing current branch..." -InformationAction Continue

            # Try GitKraken AI first if available
            if ($script:GitKrakenDecisionTree.HasAITokens -and $script:GitKrakenDecisionTree.ValidCommands["gk ai explain branch"]) {
                try {
                    Write-Information "🤖 GitKraken AI branch analysis:" -InformationAction Continue
                    & gk ai explain branch 2>$null
                    if ($LASTEXITCODE -eq 0) {
                        return  # Success - no need for fallback
                    }
                }
                catch {
                    Write-Warning "GitKraken AI analysis failed, using fallback..."
                }
            }

            # Fallback to comprehensive git analysis
            Write-Information "💡 Using git-based analysis:" -InformationAction Continue
            $currentBranch = git branch --show-current
            $commits = git log --oneline -5
            $changes = git diff --name-only HEAD~1..HEAD

            Write-Information "Current branch: $currentBranch" -InformationAction Continue
            Write-Information "Recent commits:" -InformationAction Continue
            $commits | ForEach-Object { Write-Information "  $_" -InformationAction Continue }
            Write-Information "Files changed in latest commit:" -InformationAction Continue
            $changes | ForEach-Object { Write-Information "  $_" -InformationAction Continue }
            git status --short
        }

        'CreatePR' {
            Write-Information "🔄 Creating Pull Request..." -InformationAction Continue
            $currentBranch = git branch --show-current

            if ($currentBranch -eq $script:GitKrakenConfig.DefaultBranch) {
                Write-Warning "Cannot create PR from main branch. Create a feature branch first."
                Write-Information "💡 Suggested workflow:" -InformationAction Continue
                Write-Information "  New-BusBuddyBranch -BranchName 'your-feature' -BranchType 'feature'" -InformationAction Continue
                return
            }

            # Check if branch is pushed
            $remoteExists = git ls-remote --exit-code --heads origin $currentBranch 2>$null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Branch not pushed to remote. Pushing now..."
                git push -u origin $currentBranch
            }

            # Try GitKraken AI PR creation
            if ($script:GitKrakenDecisionTree.HasAITokens -and $script:GitKrakenDecisionTree.ValidCommands["gk ai pr create"]) {
                try {
                    Write-Information "🤖 Creating AI-powered PR..." -InformationAction Continue
                    & gk ai pr create 2>$null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Information "✅ PR created successfully via GitKraken AI" -InformationAction Continue
                        return
                    }
                }
                catch {
                    Write-Warning "GitKraken AI PR creation failed, using fallback..."
                }
            }

            # Fallback to browser-based PR creation
            $repoUrl = $script:GitKrakenConfig.RepoUrl
            $prUrl = "$repoUrl/compare/$currentBranch"
            Write-Information "🌐 Opening GitHub PR creation: $prUrl" -InformationAction Continue
            Start-Process $prUrl
        }

        'SmartCommit' {
            Write-Information "🤖 Smart commit workflow..." -InformationAction Continue

            # Check for staged changes first
            $stagedFiles = git diff --cached --name-only
            $modifiedFiles = git diff --name-only
            $untrackedFiles = git ls-files --others --exclude-standard

            if (-not $stagedFiles) {
                Write-Warning "No staged changes found." 
                
                if ($modifiedFiles -or $untrackedFiles) {
                    Write-Information "📋 Available files to stage:" -InformationAction Continue
                    if ($modifiedFiles) {
                        Write-Information "  Modified files:" -InformationAction Continue
                        $modifiedFiles | ForEach-Object { Write-Information "    $_" -InformationAction Continue }
                    }
                    if ($untrackedFiles) {
                        Write-Information "  Untracked files:" -InformationAction Continue
                        $untrackedFiles | ForEach-Object { Write-Information "    $_" -InformationAction Continue }
                    }
                    
                    Write-Information "💡 Stage files with one of these commands:" -InformationAction Continue
                    Write-Information "  git add <specific-files>     # Stage specific files" -InformationAction Continue
                    Write-Information "  git add .                    # Stage all changes" -InformationAction Continue
                    Write-Information "  git add -A                   # Stage all including deletions" -InformationAction Continue
                } else {
                    Write-Information "✅ Working directory is clean - no changes to commit" -InformationAction Continue
                }
                return
            }

            Write-Information "📋 Staged files:" -InformationAction Continue
            $stagedFiles | ForEach-Object { Write-Information "  $_" -InformationAction Continue }

            # Try GitKraken AI commit if available
            if ($script:GitKrakenDecisionTree.HasAITokens -and $script:GitKrakenDecisionTree.ValidCommands["gk ai commit"]) {
                try {
                    Write-Information "🤖 Generating AI commit message..." -InformationAction Continue
                    & gk ai commit
                    if ($LASTEXITCODE -eq 0) {
                        Write-Information "✅ Commit created with AI-generated message" -InformationAction Continue
                        return
                    }
                }
                catch {
                    Write-Warning "AI commit failed, using manual approach..."
                }
            }

            # Fallback to guided manual commit
            Write-Information "💡 Manual commit guidance:" -InformationAction Continue

            $commitType = if ($stagedFiles -match "\.feature|\.cs|\.xaml") { "feat" }
                         elseif ($stagedFiles -match "test|spec") { "test" }
                         elseif ($stagedFiles -match "doc|readme|\.md") { "docs" }
                         elseif ($stagedFiles -match "PowerShell|\.ps1") { "chore" }
                         else { "chore" }

            Write-Information "💡 Suggested commit format:" -InformationAction Continue
            Write-Information "  git commit -m '${commitType}: brief description of changes'" -InformationAction Continue
            
            # Provide specific suggestions based on file types
            if ($stagedFiles -match "GitKraken") {
                Write-Information "💡 GitKraken-specific suggestion:" -InformationAction Continue
                Write-Information "  git commit -m 'feat: enhance GitKraken integration with improved workflows'" -InformationAction Continue
            }
        }

        'LaunchLaunchpad' {
            Write-Information "🚀 Opening GitKraken Launchpad..." -InformationAction Continue
            Start-Process $script:GitKrakenConfig.LaunchpadUrl
        }

        'SetupCI' {
            Write-Information "⚙️  Setting up CI/CD monitoring..." -InformationAction Continue

            # Try GitKraken work integration first
            if ($script:GitKrakenDecisionTree.HasCLI -and $script:GitKrakenDecisionTree.ValidCommands["gk work list"]) {
                try {
                    Write-Information "🔍 GitKraken work items:" -InformationAction Continue
                    & gk work list 2>$null
                }
                catch {
                    Write-Information "💡 Work items unavailable" -InformationAction Continue
                }
            }

            # Always provide GitHub Actions integration
            Write-Information "📊 Checking CI status via GitHub Actions..." -InformationAction Continue

            # Use our enhanced workflow runs function
            try {
                Get-GitHubWorkflowRuns -Limit 5
            }
            catch {
                Write-Warning "GitHub workflow check failed, opening browser..."
                $actionsUrl = "$($script:GitKrakenConfig.RepoUrl)/actions"
                Start-Process $actionsUrl
            }
        }
    }
}

function Start-BusBuddyCommitWorkflow {
    <#
    .SYNOPSIS
        Complete commit workflow with intelligent file staging and AI commit messages
    #>
    [CmdletBinding()]
    param(
        [switch]$StageAll,
        [string[]]$Files,
        [switch]$Interactive
    )

    Write-Information "🚀 Starting BusBuddy commit workflow..." -InformationAction Continue

    # Check repository status
    $stagedFiles = git diff --cached --name-only
    $modifiedFiles = git diff --name-only
    $untrackedFiles = git ls-files --others --exclude-standard

    if ($stagedFiles) {
        Write-Information "📋 Files already staged:" -InformationAction Continue
        $stagedFiles | ForEach-Object { Write-Information "  ✅ $_" -InformationAction Continue }
    }

    # Handle file staging
    if (-not $stagedFiles) {
        if ($StageAll) {
            Write-Information "📁 Staging all changes..." -InformationAction Continue
            git add -A
        }
        elseif ($Files) {
            Write-Information "📁 Staging specified files..." -InformationAction Continue
            foreach ($file in $Files) {
                if (Test-Path $file) {
                    git add $file
                    Write-Information "  ✅ Staged: $file" -InformationAction Continue
                } else {
                    Write-Warning "File not found: $file"
                }
            }
        }
        elseif ($Interactive) {
            Write-Information "📋 Available files:" -InformationAction Continue
            $allFiles = @()
            if ($modifiedFiles) {
                $allFiles += $modifiedFiles | ForEach-Object { @{ File = $_; Type = "Modified" } }
            }
            if ($untrackedFiles) {
                $allFiles += $untrackedFiles | ForEach-Object { @{ File = $_; Type = "Untracked" } }
            }

            for ($i = 0; $i -lt $allFiles.Count; $i++) {
                Write-Information "  [$($i + 1)] $($allFiles[$i].Type): $($allFiles[$i].File)" -InformationAction Continue
            }

            $choice = Read-Host "Enter file numbers to stage (comma-separated) or 'all' for all files"
            if ($choice -eq "all") {
                git add -A
                Write-Information "✅ All files staged" -InformationAction Continue
            }
            else {
                $indices = $choice.Split(',') | ForEach-Object { [int]$_.Trim() - 1 }
                foreach ($index in $indices) {
                    if ($index -ge 0 -and $index -lt $allFiles.Count) {
                        $file = $allFiles[$index].File
                        git add $file
                        Write-Information "  ✅ Staged: $file" -InformationAction Continue
                    }
                }
            }
        }
        else {
            Write-Information "💡 No files staged. Use one of these options:" -InformationAction Continue
            Write-Information "  Start-BusBuddyCommitWorkflow -StageAll" -InformationAction Continue
            Write-Information "  Start-BusBuddyCommitWorkflow -Files 'file1.cs','file2.xaml'" -InformationAction Continue
            Write-Information "  Start-BusBuddyCommitWorkflow -Interactive" -InformationAction Continue
            return
        }
    }

    # Now proceed with smart commit
    Invoke-GitKrakenWorkflow -Workflow SmartCommit
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
Get-GitHubWorkflowRuns              - Fetch GitHub Actions workflow runs
Start-BusBuddyCommitWorkflow        - Enhanced commit workflow with staging

=== Enhanced Commit Workflows ===
Start-BusBuddyCommitWorkflow -StageAll          - Stage all files and commit with AI
Start-BusBuddyCommitWorkflow -Interactive       - Interactive file staging
Start-BusBuddyCommitWorkflow -Files 'file1','file2'  - Stage specific files
Invoke-GitKrakenWorkflow -Workflow SmartCommit  - AI commit (requires pre-staged files)
gkcommit -StageAll                              - Alias for enhanced workflow

=== CI/CD Integration ===
Get-GitHubWorkflowRuns -Status failure -Limit 5    - Show recent failed runs
Get-GitHubWorkflowRuns -Branch main -LatestOnly    - Latest run on main branch
Invoke-GitKrakenWorkflow -Workflow SetupCI         - Setup CI monitoring (opens browser)

Note: GitKraken CLI does not support GitHub Actions workflow reports directly.
Use Get-GitHubWorkflowRuns for programmatic access to run data via GitHub API/CLI.

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
        & gk auth status 2>$null | Out-Null
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
        & gk auth status 2>$null | Out-Null
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
        $tokenStatus = & gk ai tokens 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ AI Tokens: Available" -InformationAction Continue

            # Check for low token warning
            if ($tokenStatus -match "low|limit|exhausted") {
                Write-Warning "⚠️  AI token usage may be approaching limits"
                Write-Information "💡 Consider upgrading plan or reducing AI command usage" -InformationAction Continue
            }
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
}

function Get-GitHubWorkflowRuns {
    <#
    .SYNOPSIS
        Retrieves GitHub Actions workflow runs for the BusBuddy repository
    .DESCRIPTION
        Uses GitHub API to fetch workflow run data since GitKraken CLI doesn't support this
    #>
    [CmdletBinding()]
    param(
        [int]$Limit = 10,
        [string]$Status = "all",  # all, success, failure, in_progress
        [string]$Branch,
        [switch]$LatestOnly
    )

    # Extract owner and repo from URL
    $repoUrl = $script:GitKrakenConfig.RepoUrl
    if ($repoUrl -match "github\.com/([^/]+)/([^/]+)") {
        $owner = $matches[1]
        $repo = $matches[2] -replace "\.git$", ""
    }
    else {
        Write-Warning "Could not parse repository URL: $repoUrl"
        return
    }

    try {
        # Check if GitHub CLI is available first
        & gh version 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "🔍 Using GitHub CLI to fetch workflow runs..." -InformationAction Continue

            $ghArgs = @("run", "list", "--limit", $Limit)
            if ($Status -ne "all") { $ghArgs += @("--status", $Status) }
            if ($Branch) { $ghArgs += @("--branch", $Branch) }
            if ($LatestOnly) { $ghArgs += "--limit", "1" }

            & gh @ghArgs
            return
        }

        # Fallback to REST API
        Write-Information "🌐 Using GitHub API to fetch workflow runs..." -InformationAction Continue

        $params = @{
            per_page = $Limit
        }
        if ($Status -ne "all") { $params.status = $Status }
        if ($Branch) { $params.branch = $Branch }

        $queryString = ($params.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
        $fullUrl = "https://api.github.com/repos/$owner/$repo/actions/runs?$queryString"

        $response = Invoke-RestMethod -Uri $fullUrl -Method Get

        Write-Information "📊 Recent workflow runs:" -InformationAction Continue
        foreach ($run in $response.workflow_runs) {
            $status = switch ($run.status) {
                "completed" {
                    switch ($run.conclusion) {
                        "success" { "✅ Success" }
                        "failure" { "❌ Failed" }
                        "cancelled" { "⏹️  Cancelled" }
                        default { "⚠️  $($run.conclusion)" }
                    }
                }
                "in_progress" { "🔄 Running" }
                "queued" { "⏳ Queued" }
                default { "❓ $($run.status)" }
            }

            $duration = if ($run.updated_at -and $run.created_at) {
                $start = [DateTime]::Parse($run.created_at)
                $end = [DateTime]::Parse($run.updated_at)
                $span = $end - $start
                "$($span.Minutes)m $($span.Seconds)s"
            } else { "N/A" }

            Write-Information "  $status | $($run.name) | $($run.head_branch) | $duration" -InformationAction Continue
        }

        Write-Information "🔗 View all runs: $($script:GitKrakenConfig.RepoUrl)/actions" -InformationAction Continue
    }
    catch {
        Write-Warning "Failed to fetch workflow runs: $($_.Exception.Message)"
        Write-Information "💡 Install GitHub CLI for better integration: winget install GitHub.cli" -InformationAction Continue

        # Final fallback - open browser
        $actionsUrl = "$($script:GitKrakenConfig.RepoUrl)/actions"
        Write-Information "🌐 Opening GitHub Actions in browser: $actionsUrl" -InformationAction Continue
        Start-Process $actionsUrl
    }
}# Functions are automatically available when script is dot-sourced
# No Export-ModuleMember needed for .ps1 files

# Aliases for convenience
Set-Alias -Name gkstart -Value Start-GitKrakenDesktop
Set-Alias -Name gkworkflow -Value Invoke-GitKrakenWorkflow
Set-Alias -Name gkbranch -Value New-BusBuddyBranch
Set-Alias -Name gkhelp -Value Show-BusBuddyGitKrakenHelp
Set-Alias -Name gkai -Value Invoke-GitKrakenAI
Set-Alias -Name gkaitest -Value Test-GitKrakenAiAvailability
Set-Alias -Name gkruns -Value Get-GitHubWorkflowRuns
Set-Alias -Name gkcommit -Value Start-BusBuddyCommitWorkflow

# Module initialization message
Write-Information "🎯 BusBuddy GitKraken integration loaded successfully" -InformationAction Continue
Write-Information "   Available commands: Start-GitKrakenDesktop, Invoke-GitKrakenWorkflow, New-BusBuddyBranch, Get-GitHubWorkflowRuns, Start-BusBuddyCommitWorkflow" -InformationAction Continue
Write-Information "   Available aliases: gkstart, gkworkflow, gkbranch, gkhelp, gkai, gkaitest, gkruns, gkcommit" -InformationAction Continue
Write-Information "   🔧 Updated: Enhanced commit workflow with intelligent file staging and better guidance" -InformationAction Continue
