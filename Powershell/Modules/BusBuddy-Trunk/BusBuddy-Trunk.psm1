#region BusBuddy-Trunk Module
# Microsoft MCP-Compliant PowerShell Module for Trunk Integration
# Version: 1.0.0
# Purpose: Provides bb-trunk-* commands for code quality and formatting

#requires -Version 7.0

#region Module Functions

function Invoke-BusBuddyTrunkCheck {
    <#
    .SYNOPSIS
    Runs Trunk check on all files with auto-fix

    .DESCRIPTION
    Executes trunk check --all --fix for comprehensive code quality analysis

    .EXAMPLE
    Invoke-BusBuddyTrunkCheck
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Information "Running Trunk check on all files..." -InformationAction Continue

        $result = & trunk check --all --fix

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Trunk check completed successfully" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Trunk check found issues (exit code: $LASTEXITCODE)"
        }

        return $result
    }
    catch {
        Write-Error "Failed to run Trunk check: $_"
        throw
    }
}

function Invoke-BusBuddyTrunkFormat {
    <#
    .SYNOPSIS
    Formats all files using Trunk formatters

    .DESCRIPTION
    Executes trunk fmt --all for comprehensive code formatting

    .EXAMPLE
    Invoke-BusBuddyTrunkFormat
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Information "Formatting all files with Trunk..." -InformationAction Continue

        $result = & trunk fmt --all

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Trunk formatting completed successfully" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Trunk formatting encountered issues (exit code: $LASTEXITCODE)"
        }

        return $result
    }
    catch {
        Write-Error "Failed to run Trunk format: $_"
        throw
    }
}

function Invoke-BusBuddyTrunkPowerShell {
    <#
    .SYNOPSIS
    Runs PSScriptAnalyzer on PowerShell files via Trunk

    .DESCRIPTION
    Executes trunk check specifically for PowerShell files using PSScriptAnalyzer

    .PARAMETER Path
    Optional path to check. Defaults to PowerShell directory

    .EXAMPLE
    Invoke-BusBuddyTrunkPowerShell

    .EXAMPLE
    Invoke-BusBuddyTrunkPowerShell -Path "PowerShell/Modules"
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$Path = "PowerShell"
    )

    try {
        Write-Information "Running Trunk PowerShell analysis on: $Path" -InformationAction Continue

        $result = & trunk check --filter=psscriptanalyzer $Path

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ PowerShell analysis completed successfully" -InformationAction Continue
        } else {
            Write-Warning "⚠️ PowerShell analysis found issues (exit code: $LASTEXITCODE)"
        }

        return $result
    }
    catch {
        Write-Error "Failed to run Trunk PowerShell analysis: $_"
        throw
    }
}

function Get-BusBuddyTrunkStatus {
    <#
    .SYNOPSIS
    Gets Trunk status and configuration information

    .DESCRIPTION
    Displays Trunk version, enabled tools, and configuration status

    .EXAMPLE
    Get-BusBuddyTrunkStatus
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Information "Checking Trunk status..." -InformationAction Continue

        # Get Trunk version
        $version = & trunk version
        Write-Output "Trunk Version: $version"

        # Get enabled tools - use proper trunk command
        Write-Output "`nEnabled Tools:"
        $tools = & trunk check list
        Write-Output $tools

        return @{
            Version = $version
            EnabledTools = $tools
        }
    }
    catch {
        Write-Error "Failed to get Trunk status: $_"
        throw
    }
}

function Update-BusBuddyTrunk {
    <#
    .SYNOPSIS
    Updates Trunk to the latest version

    .DESCRIPTION
    Executes trunk upgrade to update Trunk and its tools

    .EXAMPLE
    Update-BusBuddyTrunk
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Information "Upgrading Trunk..." -InformationAction Continue

        $result = & trunk upgrade

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Trunk upgrade completed successfully" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Trunk upgrade encountered issues (exit code: $LASTEXITCODE)"
        }

        return $result
    }
    catch {
        Write-Error "Failed to upgrade Trunk: $_"
        throw
    }
}

#endregion

#region Aliases
New-Alias -Name "bb-trunk-check" -Value "Invoke-BusBuddyTrunkCheck" -Force
New-Alias -Name "bb-trunk-format" -Value "Invoke-BusBuddyTrunkFormat" -Force
New-Alias -Name "bb-trunk-ps" -Value "Invoke-BusBuddyTrunkPowerShell" -Force
New-Alias -Name "bb-trunk-status" -Value "Get-BusBuddyTrunkStatus" -Force
New-Alias -Name "bb-trunk-upgrade" -Value "Update-BusBuddyTrunk" -Force
#endregion

#region Module Exports
Export-ModuleMember -Function @(
    'Invoke-BusBuddyTrunkCheck',
    'Invoke-BusBuddyTrunkFormat',
    'Invoke-BusBuddyTrunkPowerShell',
    'Get-BusBuddyTrunkStatus',
    'Update-BusBuddyTrunk'
)

Export-ModuleMember -Alias @(
    'bb-trunk-check',
    'bb-trunk-format',
    'bb-trunk-ps',
    'bb-trunk-status',
    'bb-trunk-upgrade'
)
#endregion

#endregion
