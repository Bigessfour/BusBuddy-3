#Requires -Version 7.0
<#
.SYNOPSIS
    Adds current client IP to Azure SQL Server firewall rules
.DESCRIPTION
    Automatically detects your current public IP and adds it to the specified Azure SQL Server firewall.
    Requires Azure CLI to be installed and authenticated (az login).
.PARAMETER ResourceGroup
    Azure Resource Group containing the SQL Server
.PARAMETER ServerName
    Azure SQL Server name (without .database.windows.net)
.PARAMETER RuleName
    Name for the firewall rule (defaults to "AllowClientIP-{timestamp}")
.EXAMPLE
    .\Add-AzureSqlFirewallRule.ps1 -ResourceGroup "BusBuddy-RG" -ServerName "busbuddy-server-sm2"
.NOTES
    Docs: https://learn.microsoft.com/azure/azure-sql/database/firewall-configure
    This script supports confirmation prompts (WhatIf/Confirm) using ShouldProcess.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$ServerName,

    [Parameter()]
    [string]$RuleName = "AllowClientIP-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
)

# Check if Azure CLI is available
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) not found. Please install from https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
}

try {
    Write-Information "Getting current public IP address..." -InformationAction Continue
    $currentIP = (Invoke-RestMethod -Uri "https://api.ipify.org/?format=json" -TimeoutSec 10).ip

    if (-not $currentIP) {
        throw "Failed to retrieve public IP address"
    }

Write-Information "Current public IP: $currentIP" -InformationAction Continue

# Test if already authenticated with Azure
Write-Information "Checking Azure authentication..." -InformationAction Continue
az account show 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Not authenticated with Azure. Running 'az login'..."
    az login
    if ($LASTEXITCODE -ne 0) {
        throw "Azure authentication failed"
    }
}

    # Check if server exists
    Write-Information "Verifying SQL Server exists..." -InformationAction Continue
    $serverCheckError = az sql server show --resource-group $ResourceGroup --name $ServerName 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "az sql server show error: $serverCheckError"
        throw "SQL Server '$ServerName' not found in resource group '$ResourceGroup'"
    }

    # Check if rule already exists for this IP
    Write-Information "Checking existing firewall rules..." -InformationAction Continue
    $existingRules = az sql server firewall-rule list --resource-group $ResourceGroup --server $ServerName --output json | ConvertFrom-Json
    $existingRule = $existingRules | Where-Object { $_.startIpAddress -eq $currentIP -and $_.endIpAddress -eq $currentIP }

    if ($existingRule) {
        Write-Information "IP $currentIP is already allowed via rule: $($existingRule.name)" -InformationAction Continue
        return
    }

    if ($PSCmdlet.ShouldProcess("Azure SQL Server $ServerName", "Add firewall rule for IP $currentIP")) {
        Write-Information "Creating firewall rule '$RuleName' for IP $currentIP..." -InformationAction Continue

        $firewallRuleParams = @{
            "resource-group"    = $ResourceGroup
            "server"            = $ServerName
            "name"              = $RuleName
            "start-ip-address"  = $currentIP
            "end-ip-address"    = $currentIP
            "output"            = "json"
        }
        az sql server firewall-rule create @firewallRuleParams

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Firewall rule created successfully!" -InformationAction Continue
            Write-Information "Rule Name: $RuleName" -InformationAction Continue
            Write-Information "Allowed IP: $currentIP" -InformationAction Continue
            Write-Information "You should now be able to connect to $ServerName.database.windows.net" -InformationAction Continue
        } else {
            throw "Failed to create firewall rule"
        }
    }
}
catch {
    Write-Error "Error: $($_.Exception.Message)"
    Write-Information "Troubleshooting tips:" -InformationAction Continue
    Write-Information "1. Ensure you have Contributor/SQL Server Contributor role on the resource group" -InformationAction Continue
    Write-Information "2. Verify server name and resource group are correct" -InformationAction Continue
    Write-Information "3. Check if server uses Private Endpoint (public firewall rules won't work)" -InformationAction Continue
    exit 1
}
