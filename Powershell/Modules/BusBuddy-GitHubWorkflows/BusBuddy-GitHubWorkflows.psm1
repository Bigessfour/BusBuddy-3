# Mass Delete GitHub Workflow Runs
# This script helps clean up old workflow runs after repository refactoring
# Uses GitHub CLI for secure API access

<#
.SYNOPSIS
    Mass delete GitHub workflow runs based on various criteria.

.DESCRIPTION
    This script provides different options for deleting workflow runs:
    - Delete all failed runs
    - Delete runs older than a specific date
    - Delete runs from specific workflows
    - Delete all runs (with confirmation)

.PARAMETER Repository
    The GitHub repository in format "owner/repo"

.PARAMETER WorkflowId
    Specific workflow ID to target (optional)

.PARAMETER DeleteFailedOnly
    Delete only failed workflow runs

.PARAMETER OlderThanDays
    Delete runs older than specified number of days

.PARAMETER DeleteAll
    Delete all workflow runs (requires confirmation)

.PARAMETER WhatIf
    Show what would be deleted without actually deleting

.PARAMETER Force
    Skip confirmation prompts

.EXAMPLE
    .\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -DeleteFailedOnly -WhatIf
    Shows what failed runs would be deleted

.EXAMPLE
    .\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -OlderThanDays 30
    Deletes runs older than 30 days

.EXAMPLE
    .\Delete-WorkflowRuns.ps1 -Repository "Bigessfour/BusBuddy-3" -WorkflowId 179562737 -DeleteFailedOnly
    Deletes failed runs from specific workflow
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,
    
    [Parameter()]
    [string]$WorkflowId,
    
    [Parameter()]
    [switch]$DeleteFailedOnly,
    
    [Parameter()]
    [int]$OlderThanDays,
    
    [Parameter()]
    [switch]$DeleteAll,
    
    [Parameter()]
    [switch]$WhatIf,
    
    [Parameter()]
    [switch]$Force
)

# Ensure GitHub CLI is available
function Test-GitHubCLI {
    try {
        $null = gh --version
        Write-Host "✅ GitHub CLI is available" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "❌ GitHub CLI (gh) is not installed or not in PATH. Please install it first."
        Write-Host "Download from: https://cli.github.com/" -ForegroundColor Yellow
        return $false
    }
}

# Check authentication
function Test-GitHubAuth {
    try {
        $auth = gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ GitHub CLI is authenticated" -ForegroundColor Green
            return $true
        }
        else {
            Write-Error "❌ GitHub CLI is not authenticated. Run 'gh auth login' first."
            return $false
        }
    }
    catch {
        Write-Error "❌ Unable to check GitHub authentication status."
        return $false
    }
}

# Get workflow runs with filtering
function Get-WorkflowRuns {
    param(
        [string]$RepoName,
        [string]$WorkflowFilter,
        [string]$Status,
        [DateTime]$OlderThan
    )
    
    Write-Host "📊 Fetching workflow runs..." -ForegroundColor Blue
    
    # Build the gh command
    $cmd = @("run", "list", "--repo", $RepoName, "--limit", "1000", "--json", "id,name,status,conclusion,createdAt,workflowId,runNumber")
    
    if ($WorkflowFilter) {
        $cmd += @("--workflow", $WorkflowFilter)
    }
    
    if ($Status) {
        $cmd += @("--status", $Status)
    }
    
    try {
        $runsJson = & gh @cmd
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to fetch workflow runs"
        }
        
        $runs = $runsJson | ConvertFrom-Json
        
        # Filter by date if specified
        if ($OlderThan) {
            $runs = $runs | Where-Object { 
                [DateTime]::Parse($_.createdAt) -lt $OlderThan 
            }
        }
        
        return $runs
    }
    catch {
        Write-Error "Failed to fetch workflow runs: $_"
        return @()
    }
}

# Delete a single workflow run
function Remove-WorkflowRun {
    param(
        [string]$RepoName,
        [string]$RunId,
        [bool]$DryRun = $false
    )
    
    if ($DryRun) {
        Write-Host "🔄 [DRY RUN] Would delete run $RunId" -ForegroundColor Yellow
        return $true
    }
    
    try {
        $null = gh run delete $RunId --repo $RepoName --confirm 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Deleted run $RunId" -ForegroundColor Green
            return $true
        }
        else {
            Write-Warning "⚠️ Failed to delete run $RunId"
            return $false
        }
    }
    catch {
        Write-Warning "⚠️ Error deleting run $RunId : $_"
        return $false
    }
}

# Main execution
function Main {
    Write-Host "🚌 BusBuddy Workflow Cleanup Tool" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan
    
    # Prerequisites check
    if (-not (Test-GitHubCLI)) { return }
    if (-not (Test-GitHubAuth)) { return }
    
    # Determine criteria
    $criteria = @()
    if ($DeleteFailedOnly) { $criteria += "failed runs" }
    if ($OlderThanDays) { $criteria += "runs older than $OlderThanDays days" }
    if ($WorkflowId) { $criteria += "workflow ID $WorkflowId" }
    if ($DeleteAll) { $criteria += "ALL runs" }
    
    if ($criteria.Count -eq 0) {
        Write-Error "❌ No deletion criteria specified. Use -DeleteFailedOnly, -OlderThanDays, -DeleteAll, or -WorkflowId"
        return
    }
    
    Write-Host "🎯 Target: $Repository" -ForegroundColor White
    Write-Host "📋 Criteria: $($criteria -join ', ')" -ForegroundColor White
    Write-Host ""
    
    # Calculate date filter
    $olderThanDate = if ($OlderThanDays) { (Get-Date).AddDays(-$OlderThanDays) } else { $null }
    
    # Get workflow runs
    $status = if ($DeleteFailedOnly) { "failure" } else { $null }
    $workflowFilter = if ($WorkflowId) { $WorkflowId } else { $null }
    
    $runs = Get-WorkflowRuns -RepoName $Repository -WorkflowFilter $workflowFilter -Status $status -OlderThan $olderThanDate
    
    if ($runs.Count -eq 0) {
        Write-Host "✅ No workflow runs found matching the criteria." -ForegroundColor Green
        return
    }
    
    # Display summary
    Write-Host "📊 Found $($runs.Count) workflow runs matching criteria:" -ForegroundColor Yellow
    Write-Host ""
    
    # Group by workflow and status for summary
    $summary = $runs | Group-Object workflowId, conclusion | ForEach-Object {
        [PSCustomObject]@{
            WorkflowId = $_.Name.Split(', ')[0]
            Status = $_.Name.Split(', ')[1]
            Count = $_.Count
        }
    }
    
    $summary | Format-Table -AutoSize
    
    # Show sample runs
    Write-Host "📝 Sample runs to be deleted:" -ForegroundColor Yellow
    $runs | Select-Object -First 5 | ForEach-Object {
        $date = [DateTime]::Parse($_.createdAt).ToString("yyyy-MM-dd HH:mm")
        Write-Host "  • Run #$($_.runNumber) - $($_.conclusion) - $date - $($_.name)" -ForegroundColor Gray
    }
    
    if ($runs.Count -gt 5) {
        Write-Host "  ... and $($runs.Count - 5) more" -ForegroundColor Gray
    }
    
    Write-Host ""
    
    # Confirmation
    if (-not $WhatIf -and -not $Force) {
        $confirm = Read-Host "❓ Are you sure you want to delete these $($runs.Count) workflow runs? (yes/no)"
        if ($confirm -ne "yes") {
            Write-Host "❌ Operation cancelled." -ForegroundColor Red
            return
        }
    }
    
    if ($WhatIf) {
        Write-Host "🔍 WhatIf mode: No runs will actually be deleted." -ForegroundColor Magenta
    }
    
    # Delete runs
    Write-Host "🚀 Starting deletion process..." -ForegroundColor Blue
    
    $deleted = 0
    $failed = 0
    $total = $runs.Count
    
    foreach ($run in $runs) {
        $progress = [math]::Round(($deleted + $failed) / $total * 100, 1)
        Write-Progress -Activity "Deleting workflow runs" -Status "$progress% Complete" -PercentComplete $progress
        
        if (Remove-WorkflowRun -RepoName $Repository -RunId $run.id -DryRun $WhatIf) {
            $deleted++
        }
        else {
            $failed++
        }
        
        # Rate limiting - don't overwhelm the API
        Start-Sleep -Milliseconds 200
    }
    
    Write-Progress -Activity "Deleting workflow runs" -Completed
    
    # Final summary
    Write-Host ""
    Write-Host "📊 Cleanup Summary:" -ForegroundColor Cyan
    Write-Host "  ✅ Successfully deleted: $deleted" -ForegroundColor Green
    if ($failed -gt 0) {
        Write-Host "  ❌ Failed to delete: $failed" -ForegroundColor Red
    }
    Write-Host "  📦 Total processed: $total" -ForegroundColor White
    
    if ($WhatIf) {
        Write-Host ""
        Write-Host "💡 To actually delete these runs, run the command again without -WhatIf" -ForegroundColor Yellow
    }
}

# Execute main function
Main