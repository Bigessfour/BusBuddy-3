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
function Test-GitHubCLI {
    try {
        $null = gh --version
        Write-Information "✅ GitHub CLI is available" -InformationAction Continue
        return $true
    } catch {
        Write-Error "❌ GitHub CLI (gh) is not installed or not in PATH. Please install it first."
        Write-Information "Download from: https://cli.github.com/" -InformationAction Continue
        return $false
    }
}

# Check authentication
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
function Test-GitHubAuth {
    try {
        $auth = gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ GitHub CLI is authenticated" -InformationAction Continue
            return $true
        } else {
            Write-Error "❌ GitHub CLI is not authenticated. Run 'gh auth login' first."
            return $false
        }
    } catch {
        Write-Error "❌ Unable to check GitHub authentication status."
        return $false
    }
}

# Get workflow runs with filtering
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER RepoName
${3:Parameter description}

.PARAMETER WorkflowFilter
${4:Parameter description}

.PARAMETER Status
${5:Parameter description}

.PARAMETER OlderThan
${6:Parameter description}

.EXAMPLE
${7:An example}

.NOTES
${8:General notes}
#>
function Get-WorkflowRun {
    param(
        [string]$RepoName,
        [string]$WorkflowFilter,
        [string]$Status,
        [DateTime]$OlderThan
    )

    Write-Information "📊 Fetching workflow runs..." -InformationAction Continue

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
                $createdAt = $_.createdAt
                $parsedDate = $null
                if ($createdAt -and [DateTime]::TryParse($createdAt, [ref]$parsedDate)) {
                    $parsedDate -lt $OlderThan
                } else {
                    $false
                }
            }
        }

        return $runs
    } catch {
        Write-Error "Failed to fetch workflow runs using command: gh $($cmd -join ' ')`nError: $_"
        return @()
    }
}

# Delete a single workflow run
functio Remonve-WorkflowRun {
    param(
        [string]$RepoName,
        [string]$RunId,
        [bool]$DryRun = $false
    )

    if ($DryRun) {
        Write-Information "🔄 [DRY RUN] Would delete run $RunId" -InformationAction Continue
        return $true
    }

    try {
        $null = gh run delete $RunId --repo $RepoName --confirm 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Deleted run $RunId" -InformationAction Continue
            return $true
        } else {
            Write-Warning "⚠️ Failed to delete run $RunId"
            return $false
        }
    } catch {
        Write-Warning "⚠️ Error deleting run $RunId : $_"
        return $false
    }
}

# Main execution
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
function Main {
    Write-Information "🚌 BusBuddy Workflow Cleanup Tool" -InformationAction Continue
    Write-Information "=================================" -InformationAction Continue

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

    Write-Information "🎯 Target: $Repository" -InformationAction Continue
    Write-Information "📋 Criteria: $($criteria -join ', ')" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    # Calculate date filter
    $olderThanDate = if ($OlderThanDays) { (Get-Date).AddDays(-$OlderThanDays) } else { $null }

    # Get workflow runs
    $status = if ($DeleteFailedOnly) { "failure" } else { $null }
    $workflowFilter = if ($WorkflowId) { $WorkflowId } else { $null }

    $runs = Get-WorkflowRuns -RepoName $Repository -WorkflowFilter $workflowFilter -Status $status -OlderThan $olderThanDate

    if ($runs.Count -eq 0) {
        Write-Information "✅ No workflow runs found matching the criteria." -InformationAction Continue
        return
    }

    # Display summary
    Write-Information "📊 Found $($runs.Count) workflow runs matching criteria:" -InformationAction Continue
    Write-Information "" -InformationAction Continue

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
    Write-Information "📝 Sample runs to be deleted:" -InformationAction Continue
    $runs | Select-Object -First 5 | ForEach-Object {
        $date = [DateTime]::Parse($_.createdAt).ToString("yyyy-MM-dd HH:mm")
        Write-Information "  • Run #$($_.runNumber) - $($_.conclusion) - $date - $($_.name)" -InformationAction Continue
    }

    if ($runs.Count -gt 5) {
        Write-Information "  ... and $($runs.Count - 5) more" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue

    # Confirmation
    if (-not $WhatIf -and -not $Force) {
        $confirm = Read-Host "❓ Are you sure you want to delete these $($runs.Count) workflow runs? (yes/no)"
        if ($confirm -ne "yes") {
            Write-Information "❌ Operation cancelled." -InformationAction Continue
            return
        }
    }

    if ($WhatIf) {
        Write-Information "🔍 WhatIf mode: No runs will actually be deleted." -InformationAction Continue
    }

    # Delete runs
    Write-Information "🚀 Starting deletion process..." -InformationAction Continue

    $deleted = 0
    $failed = 0
    $total = $runs.Count

    foreach ($run in $runs) {
        $progress = [math]::Round(($deleted + $failed) / $total * 100, 1)
        Write-Progress -Activity "Deleting workflow runs" -Status "$progress% Complete" -PercentComplete $progress

        if (Remove-WorkflowRun -RepoName $Repository -RunId $run.id -DryRun $WhatIf) {
            $deleted++
        } else {
            $failed++
        }

        # Rate limiting - don't overwhelm the API
        Start-Sleep -Milliseconds 200
    }

    Write-Progress -Activity "Deleting workflow runs" -Completed

    # Final summary
    Write-Information "" -InformationAction Continue
    Write-Information "📊 Cleanup Summary:" -InformationAction Continue
    Write-Information "  ✅ Successfully deleted: $deleted" -InformationAction Continue
    if ($failed -gt 0) {
        Write-Information "  ❌ Failed to delete: $failed" -InformationAction Continue
    }
    Write-Information "  📦 Total processed: $total" -InformationAction Continue

    if ($WhatIf) {
        Write-Information "" -InformationAction Continue
        Write-Information "💡 To actually delete these runs, run the command again without -WhatIf" -InformationAction Continue
    }
}

# Execute main function
Main
