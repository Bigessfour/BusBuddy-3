#Requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy PowerShell Profile with GitKraken AI Integration
.DESCRIPTION
    Comprehensive PowerShell profile for BusBuddy development with GitKraken AI,
    module management, and development workflow automation
.NOTES
    Author: BusBuddy Development Team
    Version: 2.0.0
    PowerShell: 7.5.2+
    GitKraken: Pro AI Features
#>

# =============================================================================
# BUSBUDDY DEVELOPMENT ENVIRONMENT INITIALIZATION
# =============================================================================

param(
    [switch]$SkipGitKraken,
    [switch]$Verbose,
    [switch]$Force
)

# Profile metadata
$script:ProfileVersion = "2.0.0"
$script:ProfileLoadTime = Get-Date

# Suppress welcome messages during automated loading
if ($env:BUSBUDDY_SILENT -or $env:BUSBUDDY_NO_WELCOME) {
    $InformationPreference = 'SilentlyContinue'
} else {
    Write-Host "🚌 " -ForegroundColor Blue -NoNewline
    Write-Host "BusBuddy Development Environment" -ForegroundColor Cyan -NoNewline
    Write-Host " v$script:ProfileVersion" -ForegroundColor Gray
}

# =============================================================================
# MODULE LOADING WITH PERFORMANCE OPTIMIZATION
# =============================================================================

try {
    # Load module manager first (import as module to handle Export-ModuleMember properly)
    $moduleManagerPath = Join-Path $PSScriptRoot "..\BusBuddy.ModuleManager.ps1"
    if (Test-Path $moduleManagerPath) {
        # Import as module instead of dot-sourcing to avoid Export-ModuleMember errors
        Import-Module $moduleManagerPath -Force -DisableNameChecking -WarningAction SilentlyContinue
        Initialize-BusBuddyModuleSystem -Verbose:$Verbose
    }

    # Load GitKraken integration
    $gitKrakenPath = Join-Path $PSScriptRoot "..\BusBuddy-GitKraken.ps1"
    # Parenthesize each expression so the parser doesn't treat -and as a parameter to Test-Path
    if ((Test-Path $gitKrakenPath) -and (-not $SkipGitKraken)) {
        . $gitKrakenPath

        # Initialize GitKraken environment
        if (-not $env:BUSBUDDY_SILENT) {
            Write-Information "🔧 Initializing GitKraken environment..." -InformationAction Continue
        }

        $gkResult = Initialize-GitKrakenEnvironment
        if ($gkResult) {
            if (-not $env:BUSBUDDY_SILENT) {
                Write-Host "✅ GitKraken AI ready" -ForegroundColor Green
            }

            # Set up GitKraken aliases
            Set-Alias -Name gkprs -Value Get-BusBuddyPullRequests -Force
            Set-Alias -Name gkprr -Value Invoke-BusBuddyPRReview -Force
            Set-Alias -Name gkprm -Value Merge-BusBuddyPullRequest -Force
            Set-Alias -Name gkpro -Value Open-BusBuddyPRs -Force
            Set-Alias -Name gkai -Value Invoke-GitKrakenAI -Force
        } else {
            if (-not $env:BUSBUDDY_SILENT) {
                Write-Warning "GitKraken AI not available. Using GitHub CLI fallback."
            }
        }
    }
} catch {
    Write-Error "Profile initialization error: $_"
}

# =============================================================================
# GITKRAKEN AI HELPER FUNCTIONS
# =============================================================================

function Invoke-GitKrakenAI {
    <#
    .SYNOPSIS
        Smart GitKraken AI command dispatcher
    .PARAMETER Command
        AI command to execute (explain-branch, explain-commit, pr-create, etc.)
    .PARAMETER Target
        Target for the command (branch name, commit SHA, etc.)
    .EXAMPLE
        gkai explain-branch feature/new-feature
        gkai explain-commit abc123
        gkai pr-create
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet("explain-branch", "explain-commit", "pr-create", "commit", "changelog")]
        [string]$Command,

        [Parameter()]
        [string]$Target
    )

    # Ensure GitKraken is initialized
    if (-not (Test-GitKrakenAvailability)) {
        Initialize-GitKrakenEnvironment | Out-Null
    }

    switch ($Command) {
        "explain-branch" {
            $branch = $Target ?? (git branch --show-current)
            Write-Information "🤖 GitKraken AI analyzing branch: $branch" -InformationAction Continue
            & gk ai explain branch --branch $branch
        }
        "explain-commit" {
            if (-not $Target) {
                $Target = git rev-parse HEAD
            }
            Write-Information "🤖 GitKraken AI analyzing commit: $Target" -InformationAction Continue
            & gk ai explain commit --commit $Target
        }
        "pr-create" {
            Write-Information "🤖 GitKraken AI creating PR..." -InformationAction Continue
            & gk ai pr create
        }
        "commit" {
            Write-Information "🤖 GitKraken AI generating commit message..." -InformationAction Continue
            & gk ai commit
        }
        "changelog" {
            $from = $Target ?? "HEAD~10"
            Write-Information "🤖 GitKraken AI generating changelog from $from..." -InformationAction Continue
            & gk ai changelog --from $from
        }
    }
}

function Test-GitKrakenAvailability {
    <#
    .SYNOPSIS
        Tests if GitKraken AI is available and working
    #>
    try {
        & gk ai tokens 2>$null | Out-Null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

# =============================================================================
# BUSBUDDY DEVELOPMENT ALIASES AND SHORTCUTS
# =============================================================================

# GitKraken AI shortcuts (using approved verbs)
function Invoke-GitKrakenAIBranch { Invoke-GitKrakenAI -Command "explain-branch" -Target $args[0] }
function Invoke-GitKrakenAICommit { Invoke-GitKrakenAI -Command "explain-commit" -Target $args[0] }
function Invoke-GitKrakenAIPR { Invoke-GitKrakenAI -Command "pr-create" }
function Invoke-GitKrakenAIMessage { Invoke-GitKrakenAI -Command "commit" }
function Invoke-GitKrakenAIChangelog { Invoke-GitKrakenAI -Command "changelog" -Target $args[0] }

# Aliases for easier typing
Set-Alias -Name gkai-branch -Value Invoke-GitKrakenAIBranch -Force
Set-Alias -Name gkai-commit -Value Invoke-GitKrakenAICommit -Force
Set-Alias -Name gkai-pr -Value Invoke-GitKrakenAIPR -Force
Set-Alias -Name gkai-msg -Value Invoke-GitKrakenAIMessage -Force
Set-Alias -Name gkai-log -Value Invoke-GitKrakenAIChangelog -Force

# Enhanced build commands with GitKraken integration
function bbBuildWithAI {
    <#
    .SYNOPSIS
        Build BusBuddy with AI-generated commit message if changes detected
    #>
    dotnet build BusBuddy.sln
    if ($LASTEXITCODE -eq 0) {
        $status = git status --porcelain
        if ($status) {
            Write-Host "✨ Build successful! GitKraken AI can help with commit message:" -ForegroundColor Green
            Write-Host "Run: gkai-msg" -ForegroundColor Yellow
        }
    }
}

# Smart PR management
function bbPRStatus {
    <#
    .SYNOPSIS
        Get PR status with GitKraken AI analysis
    #>
    if (Test-GitKrakenAvailability) {
        Write-Host "🤖 GitKraken AI PR Analysis:" -ForegroundColor Cyan
        & gk ai pr list 2>$null
        if ($LASTEXITCODE -ne 0) {
            # Fallback to GitHub CLI
            gh pr list
        }
    } else {
        gh pr list
    }
}

# =============================================================================
# ENVIRONMENT HEALTH CHECK WITH GITKRAKEN
# =============================================================================

function bbHealthWithGitKraken {
    <#
    .SYNOPSIS
        Comprehensive health check including GitKraken AI status
    #>
    Write-Host "🏥 BusBuddy Environment Health Check" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan

    # PowerShell version
    $psVersion = $PSVersionTable.PSVersion
    Write-Host "PowerShell: " -NoNewline
    if ($psVersion.Major -ge 7) {
        Write-Host "$psVersion ✅" -ForegroundColor Green
    } else {
        Write-Host "$psVersion ❌ (7.5+ required)" -ForegroundColor Red
    }

    # .NET version
    $dotnetVersion = dotnet --version 2>$null
    Write-Host ".NET SDK: " -NoNewline
    if ($dotnetVersion) {
        Write-Host "$dotnetVersion ✅" -ForegroundColor Green
    } else {
        Write-Host "Not found ❌" -ForegroundColor Red
    }

    # GitKraken AI status
    Write-Host "GitKraken AI: " -NoNewline
    if (Test-GitKrakenAvailability) {
        $tokens = & gk ai tokens 2>$null
        Write-Host "Available ✅ ($tokens)" -ForegroundColor Green
    } else {
        Write-Host "Not available ❌" -ForegroundColor Red
        Write-Host "  Run: gk auth login && Initialize-GitKrakenEnvironment" -ForegroundColor Yellow
    }

    # GitHub CLI
    $ghVersion = gh --version 2>$null
    Write-Host "GitHub CLI: " -NoNewline
    if ($ghVersion) {
        Write-Host "Available ✅" -ForegroundColor Green
    } else {
        Write-Host "Not found ❌" -ForegroundColor Red
    }

    # Project build status
    Write-Host "Project Build: " -NoNewline
    dotnet build BusBuddy.sln --verbosity quiet 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Success ✅" -ForegroundColor Green
    } else {
        Write-Host "Failed ❌" -ForegroundColor Red
    }
}

# =============================================================================
# ENHANCED ALIASES
# =============================================================================

Set-Alias -Name bb-health -Value bbHealthWithGitKraken -Force
Set-Alias -Name bb-build-ai -Value bbBuildWithAI -Force
Set-Alias -Name bb-pr -Value bbPRStatus -Force

# Legacy aliases for compatibility
Set-Alias -Name bbHealth -Value bbHealthWithGitKraken -Force
Set-Alias -Name bbBuild -Value bbBuildWithAI -Force

# =============================================================================
# PROFILE COMPLETION MESSAGE
# =============================================================================

if (-not $env:BUSBUDDY_SILENT) {
    $loadTime = (Get-Date) - $script:ProfileLoadTime
    Write-Host "⚡ Profile loaded in $($loadTime.TotalMilliseconds)ms" -ForegroundColor Green

    # Show GitKraken status
    if (Test-GitKrakenAvailability) {
        Write-Host "🤖 GitKraken AI ready - Try: " -ForegroundColor Cyan -NoNewline
        Write-Host "gkai-branch" -ForegroundColor Yellow -NoNewline
        Write-Host " | " -ForegroundColor Gray -NoNewline
        Write-Host "gkai-commit" -ForegroundColor Yellow -NoNewline
        Write-Host " | " -ForegroundColor Gray -NoNewline
        Write-Host "gkai-pr" -ForegroundColor Yellow
    }

    Write-Host "Ready for BusBuddy development! 🚀" -ForegroundColor Green
}

# Profile loaded successfully - functions are automatically available in global scope
