#requires -Version 7.5 -PSEdition Core
<#
.SYNOPSIS
BusBuddy CLI Integration Module - Integrates GitHub, Azure, and GitKraken CLIs
.DESCRIPTION
This module provides PowerShell wrappers for GitHub CLI (via PowerShellForGitHub),
Azure CLI (via Az module), and GitKraken CLI for enhanced repo scanning and CI/CD workflows.
Uses PowerShell Gallery modules where available for better integration.

.NOTES
Standards: PowerShell 7.5+, StrictMode 3.0, lazy loading for performance
Refs:
- PowerShellForGitHub: https://github.com/microsoft/PowerShellForGitHub
- Az Module: https://learn.microsoft.com/powershell/azure/
- GitHub CLI: https://cli.github.com
- Azure CLI: https://learn.microsoft.com/cli/azure/
- GitKraken CLI: https://www.gitkraken.com/cli
#>

Set-StrictMode -Version 3.0
# Don't use Stop for ErrorActionPreference in module - causes dependency issues
$ErrorActionPreference = 'Continue'

# Lazy loading flag to avoid repeated module imports
$script:ModulesLoaded = @{}

#region Helper Functions

function Initialize-CliModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('PowerShellForGitHub', 'Az', 'Native')]
        [string]$ModuleName
    )

    if ($script:ModulesLoaded[$ModuleName]) { return $true }

    try {
        switch ($ModuleName) {
            'PowerShellForGitHub' {
                if (-not (Get-Module PowerShellForGitHub -ListAvailable -ErrorAction SilentlyContinue)) {
                    Write-Warning "PowerShellForGitHub not installed. Run: Install-Module PowerShellForGitHub -Scope CurrentUser"
                    return $false
                }
                Write-Output "Loading PowerShellForGitHub module..."
                Import-Module PowerShellForGitHub -Force -ErrorAction SilentlyContinue
            }
            'Az' {
                if (-not (Get-Module Az -ListAvailable -ErrorAction SilentlyContinue)) {
                    Write-Warning "Az module not installed. Run: Install-Module Az -Scope CurrentUser"
                    return $false
                }
                Write-Output "Loading Az module (may take 10-15 seconds)..."
                # Import core Az modules only to avoid dependency conflicts
                Import-Module Az.Accounts, Az.Resources, Az.Sql -Force -ErrorAction SilentlyContinue
            }
            'Native' {
                # Check native CLI tools availability
                $clis = @('gh', 'az', 'gk')
                foreach ($cli in $clis) {
                    if (-not (Get-Command $cli -ErrorAction SilentlyContinue)) {
                        Write-Warning "$cli CLI not found. Install from: gh (https://cli.github.com), az (https://learn.microsoft.com/cli/azure/install-azure-cli), gk (https://www.gitkraken.com/cli)"
                        return $false
                    }
                }
            }
        }
        $script:ModulesLoaded[$ModuleName] = $true
        return $true
    }
    catch {
        Write-Warning "Failed to initialize $ModuleName`: $($_.Exception.Message)"
        return $false
    }
}function Get-BusBuddyRepoInfo {
    [CmdletBinding()]
    param()

    # Try to determine repo info from current directory or environment
    $repoPath = $PWD.Path
    if ($env:BUSBUDDY_REPO_ROOT) {
        $repoPath = $env:BUSBUDDY_REPO_ROOT
    }

    # Look for .git directory or BusBuddy.sln
    $gitPath = Join-Path $repoPath '.git'
    $slnPath = Join-Path $repoPath 'BusBuddy.sln'

    return @{
        Path = $repoPath
        IsGitRepo = Test-Path $gitPath
        IsBusBuddyRepo = Test-Path $slnPath
        Owner = 'Bigessfour'
        Repository = 'BusBuddy-3'
    }
}

#endregion

#region GitHub CLI Functions (PowerShellForGitHub)

function Invoke-BusBuddyGitHub {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    # Use native GitHub CLI directly (preferred)
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        & gh $Arguments
    } else {
    Write-Warning "GitHub CLI (gh) not found. Install from https://cli.github.com"
    Write-Output "Alternatively, install PowerShellForGitHub: Install-Module PowerShellForGitHub -Scope CurrentUser"
    }
}

function Get-BusBuddyWorkflows {
    [CmdletBinding()]
    param(
        [string]$Owner = 'Bigessfour',
        [string]$Repository = 'BusBuddy-3'
    )

    # Prefer native GitHub CLI for reliability
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        Write-Output "Scanning workflows with GitHub CLI..."
        & gh workflow list --repo "$Owner/$Repository"
        return
    }

    # Fallback to PowerShellForGitHub if gh CLI not available
    if (Initialize-CliModule -ModuleName 'PowerShellForGitHub') {
        try {
            Write-Output "Using PowerShellForGitHub module..."
            Get-GitHubWorkflow -OwnerName $Owner -RepositoryName $Repository
        }
        catch {
            Write-Warning "PowerShellForGitHub failed: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "Neither GitHub CLI (gh) nor PowerShellForGitHub module available"
    }
}

function Start-BusBuddyCiScan {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [string]$Workflow = 'ci.yml',
        [string]$Ref = 'main',
        [string]$Owner = 'Bigessfour',
        [string]$Repository = 'BusBuddy-3'
    )

    if ($PSCmdlet.ShouldProcess("$Owner/$Repository", "Trigger workflow $Workflow on $Ref")) {
        # Prefer native GitHub CLI
        if (Get-Command gh -ErrorAction SilentlyContinue) {
            Write-Output "Triggering workflow with GitHub CLI..."
            & gh workflow run $Workflow --ref $Ref --repo "$Owner/$Repository"
            return
        }

        # Fallback to PowerShellForGitHub
        if (Initialize-CliModule -ModuleName 'PowerShellForGitHub') {
            try {
                Write-Output "Using PowerShellForGitHub to trigger workflow..."
                Invoke-GitHubWorkflow -OwnerName $Owner -RepositoryName $Repository -WorkflowFileName $Workflow -Ref $Ref
                Write-Output "Triggered workflow $Workflow on $Owner/$Repository"
            }
            catch {
                Write-Warning "PowerShellForGitHub failed: $($_.Exception.Message)"
            }
        } else {
            Write-Warning "Neither GitHub CLI (gh) nor PowerShellForGitHub module available"
        }
    }
}

#endregion

#region Azure CLI Functions (Az Module)

function Invoke-BusBuddyAzure {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    if (-not (Initialize-CliModule -ModuleName 'Az')) { return }

    # For native Azure CLI operations, use az CLI directly
    if (Get-Command az -ErrorAction SilentlyContinue) {
        & az $Arguments
    } else {
        Write-Warning "Azure CLI (az) not found. Install from https://learn.microsoft.com/cli/azure/install-azure-cli"
    }
}

function Get-BusBuddyAzureResources {
    [CmdletBinding()]
    param(
        [string]$ResourceGroupName = 'BusBuddy-RG',
        [string]$SubscriptionId = $env:AZURE_SUBSCRIPTION_ID
    )

    if (-not (Initialize-CliModule -ModuleName 'Az')) { return }

    try {
        # Use Az PowerShell module for rich object output
        if ($SubscriptionId) {
            Set-AzContext -SubscriptionId $SubscriptionId
        }

        $resources = @{
            ResourceGroup = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
            SqlServers = Get-AzSqlServer -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
            SqlDatabases = Get-AzSqlDatabase -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
            StorageAccounts = Get-AzStorageAccount -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
        }

        return $resources
    }
    catch {
        Write-Warning "Az PowerShell failed, falling back to az CLI"
        if (Get-Command az -ErrorAction SilentlyContinue) {
            & az resource list --resource-group $ResourceGroupName --output table
        }
    }
}

#endregion

#region GitKraken CLI Functions (Native)

function Invoke-BusBuddyGitKraken {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    if (-not (Initialize-CliModule -ModuleName 'Native')) { return }

    if (Get-Command gk -ErrorAction SilentlyContinue) {
        & gk $Arguments
    } else {
        Write-Error "GitKraken CLI (gk) not found. Install from https://www.gitkraken.com/cli"
    }
}

function Get-BusBuddyRepositories {
    [CmdletBinding()]
    param(
        [string]$Organization = 'Bigessfour'
    )

    # Check available CLI tools and use appropriate commands
    if (Get-Command gh -ErrorAction SilentlyContinue) {
        Write-Output "Scanning repositories with GitHub CLI..."
        & gh repo list $Organization
        return
    }

    if (Get-Command gk -ErrorAction SilentlyContinue) {
        Write-Output "Checking GitKraken CLI workspace info..."
        # GitKraken CLI focuses on workspaces rather than repo lists
        & gk workspace list
        return
    }

    Write-Warning "Neither GitHub CLI (gh) nor GitKraken CLI (gk) available for repository scanning"
    Write-Output "Install GitHub CLI from https://cli.github.com for repository management"
}

#endregion

#region Combined Scanning Functions

function Invoke-BusBuddyFullScan {
    [CmdletBinding()]
    param(
        [switch]$IncludeWorkflows,
        [switch]$IncludeAzureResources,
        [switch]$IncludeRepositories,
        [switch]$IncludeFetchabilityIndex
    )

    Write-Output "🔍 Starting comprehensive BusBuddy environment scan..."

    $repoInfo = Get-BusBuddyRepoInfo
    Write-Output "📁 Repository Info: $($repoInfo.Path)"
    Write-Output "   Git Repo: $($repoInfo.IsGitRepo), BusBuddy Repo: $($repoInfo.IsBusBuddyRepo)"

    if ($IncludeWorkflows -or $PSBoundParameters.Count -eq 0) {
    Write-Output "🔍 Scanning GitHub Workflows..."
        try {
            Get-BusBuddyWorkflows
        }
        catch {
            Write-Warning "Workflow scan failed: $($_.Exception.Message)"
        }
    }

    if ($IncludeAzureResources -or $PSBoundParameters.Count -eq 0) {
    Write-Output "🔍 Scanning Azure Resources..."
        try {
            Get-BusBuddyAzureResources
        }
        catch {
            Write-Warning "Azure resource scan failed: $($_.Exception.Message)"
        }
    }

    if ($IncludeRepositories -or $PSBoundParameters.Count -eq 0) {
    Write-Output "🔍 Scanning Repositories..."
        try {
            Get-BusBuddyRepositories
        }
        catch {
            Write-Warning "Repository scan failed: $($_.Exception.Message)"
        }
    }

    if ($IncludeFetchabilityIndex -or $PSBoundParameters.Count -eq 0) {
    Write-Output "🔍 Checking Fetchability Index..."
        $indexFiles = @('raw-index.entries.json', 'raw-index.json')
        foreach ($indexFile in $indexFiles) {
            if (Test-Path $indexFile) {
                try {
                    # Try to parse JSON with error handling
                    $content = Get-Content $indexFile -Raw
                    if ($content) {
                        $indexData = $content | ConvertFrom-Json -ErrorAction Stop
                        $count = if ($indexData -is [array]) { $indexData.Count } else { 1 }
                        Write-Output "   $indexFile`: $count entries"
                    } else {
                        Write-Warning "   $indexFile`: file is empty"
                    }
                }
                catch {
                    Write-Warning "Failed to parse $indexFile`: $($_.Exception.Message)"
                    # Try to provide helpful info about the file
                    try {
                        $fileSize = (Get-Item $indexFile).Length
                        $lineCount = (Get-Content $indexFile | Measure-Object -Line).Lines
                        Write-Output "   $indexFile`: $fileSize bytes, $lineCount lines (malformed JSON)"
                    }
                    catch {
                        Write-Warning "   $indexFile`: unable to read file details"
                    }
                }
            } else {
                Write-Output "   $indexFile`: not found"
            }
        }
    }

    Write-Output "✅ BusBuddy environment scan completed"
}

#endregion

#region Aliases

Set-Alias -Name 'bbGh' -Value 'Invoke-BusBuddyGitHub'
Set-Alias -Name 'bbAz' -Value 'Invoke-BusBuddyAzure'
Set-Alias -Name 'bbGk' -Value 'Invoke-BusBuddyGitKraken'
Set-Alias -Name 'bbWorkflows' -Value 'Get-BusBuddyWorkflows'
Set-Alias -Name 'bbCiScan' -Value 'Start-BusBuddyCiScan'
Set-Alias -Name 'bbAzResources' -Value 'Get-BusBuddyAzureResources'
Set-Alias -Name 'bbRepos' -Value 'Get-BusBuddyRepositories'
Set-Alias -Name 'bbFullScan' -Value 'Invoke-BusBuddyFullScan'

#endregion

#region Exports

Export-ModuleMember -Function @(
    'Invoke-BusBuddyGitHub',
    'Invoke-BusBuddyAzure',
    'Invoke-BusBuddyGitKraken',
    'Get-BusBuddyWorkflows',
    'Start-BusBuddyCiScan',
    'Get-BusBuddyAzureResources',
    'Get-BusBuddyRepositories',
    'Invoke-BusBuddyFullScan'
) -Alias @(
    'bbGh', 'bbAz', 'bbGk',
    'bbWorkflows', 'bbCiScan', 'bbAzResources', 'bbRepos', 'bbFullScan'
)

#endregion
