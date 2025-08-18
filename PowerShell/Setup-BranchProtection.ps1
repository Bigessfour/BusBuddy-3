#!/usr/bin/env pwsh
# 🛡️ BusBuddy Branch Protection Setup
# This script configures GitHub branch protection rules to enforce CI approval before merging

param(
    [Parameter(Mandatory = $false)]
    [string]$BranchPattern = "main",

    [Parameter(Mandatory = $false)]
    [switch]$DryRun = $false,

    [Parameter(Mandatory = $false)]
    [switch]$Verbose = $false
)

# Required status checks that must pass before merging
$RequiredChecks = @(
    "build-test-x64",
    "build-test-x86",
    "quality-gate",
    "security-scan",
    "pr-status-summary"
)

# Additional required checks for comprehensive validation
$AdditionalChecks = @(
    "pr-analysis"
)

function Write-StatusMessage {
    param([string]$Message, [string]$Type = "Info")

    $timestamp = Get-Date -Format "HH:mm:ss"
    switch ($Type) {
        "Success" { Write-Host "[$timestamp] ✅ $Message" -ForegroundColor Green }
        "Warning" { Write-Host "[$timestamp] ⚠️  $Message" -ForegroundColor Yellow }
        "Error" { Write-Host "[$timestamp] ❌ $Message" -ForegroundColor Red }
        "Info" { Write-Host "[$timestamp] ℹ️  $Message" -ForegroundColor Cyan }
        default { Write-Host "[$timestamp] $Message" }
    }
}

function Test-GitHubCLI {
    try {
        gh auth status 2>&1 | Out-Null
        if ($?) {
            Write-StatusMessage "GitHub CLI authenticated successfully" "Success"
            return $true
        }
        else {
            Write-StatusMessage "GitHub CLI not authenticated. Please run 'gh auth login'" "Error"
            return $false
        }
    }
    catch {
        Write-StatusMessage "GitHub CLI not found. Please install GitHub CLI first." "Error"
        return $false
    }
}

function Get-CurrentBranchProtection {
    param([string]$Branch)

    Write-StatusMessage "Checking current branch protection for '$Branch'..."

    try {
        $protection = gh api "repos/{owner}/{repo}/branches/$Branch/protection" 2>$null | ConvertFrom-Json
        if ($protection) {
            Write-StatusMessage "Branch protection already exists for '$Branch'" "Info"
            if ($Verbose) {
                Write-Host "Current protection settings:"
                $protection | ConvertTo-Json -Depth 3 | Write-Host
            }
            return $protection
        }
    }
    catch {
        Write-StatusMessage "No existing branch protection found for '$Branch'" "Info"
    }

    return $null
}

function Set-BranchProtection {
    param(
        [string]$Branch,
        [string[]]$StatusChecks,
        [bool]$DryRunMode = $false
    )

    $protectionConfig = @{
        required_status_checks           = @{
            strict = $true
            checks = @()
        }
        enforce_admins                   = $true
        required_pull_request_reviews    = @{
            dismiss_stale_reviews           = $true
            require_code_owner_reviews      = $false
            required_approving_review_count = 1
            require_last_push_approval      = $true
        }
        restrictions                     = $null
        allow_force_pushes               = $false
        allow_deletions                  = $false
        block_creations                  = $false
        required_conversation_resolution = $true
    }

    # Add status checks
    foreach ($check in $StatusChecks) {
        $protectionConfig.required_status_checks.checks += @{
            context = $check
            app_id  = -1  # -1 means any app can provide this status
        }
    }

    $configJson = $protectionConfig | ConvertTo-Json -Depth 4 -Compress

    if ($DryRunMode) {
        Write-StatusMessage "DRY RUN: Would apply the following protection to '$Branch':" "Warning"
        $protectionConfig | ConvertTo-Json -Depth 4 | Write-Host
        return $true
    }

    Write-StatusMessage "Applying branch protection to '$Branch'..."

    try {
        # Use temporary file for input since PowerShell doesn't support here-strings with external commands
        $tempFile = New-TemporaryFile
        $configJson | Out-File -FilePath $tempFile.FullName -Encoding utf8
        gh api -X PUT "repos/{owner}/{repo}/branches/$Branch/protection" --input $tempFile.FullName
        Remove-Item $tempFile.FullName -Force
        Write-StatusMessage "Branch protection applied successfully to '$Branch'" "Success"
        return $true
    }
    catch {
        Write-StatusMessage "Failed to apply branch protection: $($_.Exception.Message)" "Error"
        return $false
    }
}

function Show-ProtectionSummary {
    param([string]$Branch, [string[]]$StatusChecks)

    Write-Host ""
    Write-StatusMessage "🛡️ Branch Protection Summary for '$Branch'" "Info"
    Write-Host "  📋 Required Status Checks:" -ForegroundColor Cyan
    foreach ($check in $StatusChecks) {
        Write-Host "    • $check" -ForegroundColor White
    }
    Write-Host "  🔒 Protection Rules:" -ForegroundColor Cyan
    Write-Host "    • Require pull request reviews (1 approval minimum)" -ForegroundColor White
    Write-Host "    • Dismiss stale reviews on new commits" -ForegroundColor White
    Write-Host "    • Require branches to be up to date before merging" -ForegroundColor White
    Write-Host "    • Enforce for administrators" -ForegroundColor White
    Write-Host "    • Require conversation resolution before merging" -ForegroundColor White
    Write-Host "    • Block force pushes and deletions" -ForegroundColor White
    Write-Host ""
}

# Main execution
Write-StatusMessage "🚌 BusBuddy Branch Protection Setup" "Info"
Write-StatusMessage "Configuring branch protection for: $BranchPattern" "Info"

# Check prerequisites
if (-not (Test-GitHubCLI)) {
    exit 1
}

# Combine all required checks
$AllRequiredChecks = $RequiredChecks + $AdditionalChecks

# Show what will be configured
Show-ProtectionSummary -Branch $BranchPattern -StatusChecks $AllRequiredChecks

# Check current protection
$existingProtection = Get-CurrentBranchProtection -Branch $BranchPattern

if ($existingProtection -and -not $DryRun) {
    $response = Read-Host "Branch protection already exists. Do you want to update it? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-StatusMessage "Operation cancelled by user" "Warning"
        exit 0
    }
}

# Apply branch protection
$success = Set-BranchProtection -Branch $BranchPattern -StatusChecks $AllRequiredChecks -DryRunMode $DryRun

if ($success) {
    if ($DryRun) {
        Write-StatusMessage "Dry run completed successfully" "Success"
        Write-StatusMessage "Run without -DryRun to actually apply the protection" "Info"
    }
    else {
        Write-StatusMessage "Branch protection configured successfully!" "Success"
        Write-StatusMessage "PRs to '$BranchPattern' now require CI approval before merging" "Info"
    }
}
else {
    Write-StatusMessage "Failed to configure branch protection" "Error"
    exit 1
}

# Show next steps
Write-Host ""
Write-StatusMessage "📋 Next Steps:" "Info"
Write-Host "  1. Test the new workflow by creating a PR" -ForegroundColor White
Write-Host "  2. Verify all status checks appear and must pass" -ForegroundColor White
Write-Host "  3. Confirm PR requires approval before merging" -ForegroundColor White
Write-Host "  4. Update team on new protection requirements" -ForegroundColor White
Write-Host ""
Write-StatusMessage "Branch protection setup complete! 🎉" "Success"
