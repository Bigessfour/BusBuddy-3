#requires -Version 7.0
<#
.SYNOPSIS
    Automatically updates Azure SQL firewall rules for dynamic IP addresses
.DESCRIPTION
    This script fetches your current public IP and adds it to Azure SQL firewall rules.
    Designed for BusBuddy project to handle Starlink and work ISP dynamic IP changes.
    Based on Azure SQL firewall configuration best practices.
.PARAMETER ResourceGroupName
    Azure resource group containing the SQL server
.PARAMETER ServerName
    Azure SQL server name (default: busbuddy-server-sm2)
.PARAMETER RuleName
    Firewall rule name prefix (default: DynamicIP)
.PARAMETER CleanupOldRules
    Remove old dynamic IP rules to keep firewall clean
.PARAMETER DatabaseName
    Optional: Create database-level rule instead of server-level
.EXAMPLE
    .\Update-AzureFirewall.ps1 -ResourceGroupName "busbuddy-rg" -ServerName "busbuddy-server-sm2"
.EXAMPLE
    .\Update-AzureFirewall.ps1 -CleanupOldRules -DatabaseName "BusBuddyDB"
.NOTES
    Reference: https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure
    Connect-AzAccount: https://learn.microsoft.com/powershell/module/az.accounts/connect-azaccount
    New-AzSqlServerFirewallRule: https://learn.microsoft.com/powershell/module/az.sql/new-azsqlserverfirewallrule
    Get-AzSqlServerFirewallRule: https://learn.microsoft.com/powershell/module/az.sql/get-azsqlserverfirewallrule
    Remove-AzSqlServerFirewallRule: https://learn.microsoft.com/powershell/module/az.sql/remove-azsqlserverfirewallrule
    Requires Az PowerShell module: Install-Module -Name Az -Scope CurrentUser
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [string]$ServerName = "busbuddy-server-sm2",

    [Parameter(Mandatory = $false)]
    [string]$RuleName = "DynamicIP",

    [Parameter(Mandatory = $false)]
    [switch]$CleanupOldRules,

    [Parameter(Mandatory = $false)]
    [string]$DatabaseName
    ,
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId
)

# Import required modules (Accounts + Sql)
foreach ($requiredModule in @('Az.Accounts', 'Az.Sql')) {
    if (-not (Get-Module -Name $requiredModule -ListAvailable)) {
        Write-Warning "$requiredModule module not found. Installing..."
        try {
            Install-Module -Name $requiredModule -Scope CurrentUser -Force -AllowClobber
            Write-Information "✅ $requiredModule module installed successfully" -InformationAction Continue
        }
        catch {
            Write-Error "Failed to install $requiredModule module: $($_.Exception.Message)"
            return
        }
    }
}

function Get-PublicIPAddress {
    <#
    .SYNOPSIS
        Gets current public IP using multiple reliable services
    #>
    $ipServices = @(
        @{ Name = "ipify.org"; Url = "https://api.ipify.org?format=json"; Property = "ip" },
        @{ Name = "AWS CheckIP"; Url = "http://checkip.amazonaws.com"; Property = $null },
        @{ Name = "CanHazIP"; Url = "https://icanhazip.com"; Property = $null }
    )

    foreach ($service in $ipServices) {
        try {
            Write-Verbose "Trying $($service.Name)..."
            $response = Invoke-RestMethod -Uri $service.Url -TimeoutSec 10

            if ($service.Property) {
                $ip = $response.($service.Property)
            }
            else {
                $ip = $response.Trim()
            }

            # Validate IP format
            if ($ip -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$') {
                Write-Information "✅ Current IP: $ip (via $($service.Name))" -InformationAction Continue
                return $ip
            }
        }
        catch {
            Write-Verbose "Failed to get IP from $($service.Name): $($_.Exception.Message)"
            continue
        }
    }

    throw "Could not determine public IP address from any service"
}

function Test-AzureConnection {
    <#
    .SYNOPSIS
        Tests if user is connected to Azure
    #>
    try {
        $context = Get-AzContext
        if (-not $context) {
            return $false
        }
        Write-Information "✅ Connected to Azure as: $($context.Account.Id)" -InformationAction Continue
        return $true
    }
    catch {
        return $false
    }
}

function Get-ResourceGroupFromConfig {
    <#
    .SYNOPSIS
        Attempts to determine resource group from BusBuddy configuration
    #>
    $configFiles = @(
        "appsettings.azure.json",
        "appsettings.json",
        ".env"
    )

    foreach ($configFile in $configFiles) {
        if (Test-Path $configFile) {
            try {
                $content = Get-Content $configFile -Raw
                if ($content -match '"ResourceGroup":\s*"([^"]+)"') {
                    return $matches[1]
                }
                if ($content -match 'AZURE_RESOURCE_GROUP=([^\r\n]+)') {
                    return $matches[1]
                }
            }
            catch {
                continue
            }
        }
    }

    return $null
}

# Main execution
try {
    Write-Information "🚌 BusBuddy Azure SQL Firewall Updater" -InformationAction Continue
    Write-Information ("=" * 50) -InformationAction Continue

    # Get current IP
    $currentIP = Get-PublicIPAddress

    # Check Azure connection
    if (-not (Test-AzureConnection)) {
        Write-Information "🔐 Connecting to Azure..." -InformationAction Continue
        try {
            Connect-AzAccount -WarningAction SilentlyContinue
        }
        catch {
            Write-Error "Failed to connect to Azure: $($_.Exception.Message)"
            Write-Information "💡 Try running: Connect-AzAccount" -InformationAction Continue
            return
        }
    }

    # Switch context if SubscriptionId provided
    if ($SubscriptionId) {
        try {
            Set-AzContext -SubscriptionId $SubscriptionId | Out-Null
            Write-Information "📦 Using subscription: $SubscriptionId" -InformationAction Continue
        }
        catch {
            Write-Warning "Could not set subscription context to ${SubscriptionId}: $($_.Exception.Message)"
        }
    }

    # Determine resource group if not provided
    if (-not $ResourceGroupName) {
        $ResourceGroupName = Get-ResourceGroupFromConfig
        if (-not $ResourceGroupName) {
            Write-Warning "Resource group not specified and couldn't be determined from config"
            $ResourceGroupName = Read-Host "Enter Azure Resource Group name"
            if (-not $ResourceGroupName) {
                throw "Resource group is required"
            }
        }
    }

    Write-Information "🎯 Target: $ServerName in $ResourceGroupName" -InformationAction Continue

    # Create timestamped rule name
    $timestamp = Get-Date -Format "yyyyMMdd-HHmm"
    $fullRuleName = "$RuleName-$timestamp"

    if ($DatabaseName) {
        # Create database-level firewall rule (more secure)
        Write-Information "🛡️ Creating database-level firewall rule: $fullRuleName" -InformationAction Continue

        # Database-level rules require T-SQL execution
        $sqlQuery = @"
EXECUTE sp_set_database_firewall_rule N'$fullRuleName', '$currentIP', '$currentIP';
"@

        Write-Information "📝 T-SQL command to run in Azure portal or SQL client:" -InformationAction Continue
        Write-Information $sqlQuery -InformationAction Continue
        Write-Information "💡 Database-level rules provide finer security control" -InformationAction Continue
    }
    else {
        # Create server-level firewall rule
        Write-Information "🛡️ Creating server-level firewall rule: $fullRuleName" -InformationAction Continue

        try {
            $newRule = New-AzSqlServerFirewallRule `
                -ResourceGroupName $ResourceGroupName `
                -ServerName $ServerName `
                -FirewallRuleName $fullRuleName `
                -StartIpAddress $currentIP `
                -EndIpAddress $currentIP

            Write-Information "✅ Firewall rule created successfully" -InformationAction Continue
            Write-Information "   Rule Name: $($newRule.FirewallRuleName)" -InformationAction Continue
            Write-Information "   IP Range: $($newRule.StartIpAddress) - $($newRule.EndIpAddress)" -InformationAction Continue
        }
        catch {
            Write-Error "Failed to create firewall rule: $($_.Exception.Message)"
            return
        }
    }

    # Cleanup old dynamic rules if requested
    if ($CleanupOldRules) {
        Write-Information "🧹 Cleaning up old dynamic IP rules..." -InformationAction Continue

        try {
            $allRules = Get-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName -ServerName $ServerName
            $oldDynamicRules = $allRules | Where-Object {
                $_.FirewallRuleName -like "$RuleName-*" -and
                $_.StartIpAddress -ne $currentIP
            }

            foreach ($oldRule in $oldDynamicRules) {
                Write-Information "🗑️ Removing old rule: $($oldRule.FirewallRuleName)" -InformationAction Continue
                Remove-AzSqlServerFirewallRule `
                    -ResourceGroupName $ResourceGroupName `
                    -ServerName $ServerName `
                    -FirewallRuleName $oldRule.FirewallRuleName `
                    -Force
            }

            if ($oldDynamicRules.Count -eq 0) {
                Write-Information "✅ No old rules to clean up" -InformationAction Continue
            }
            else {
                Write-Information "✅ Cleaned up $($oldDynamicRules.Count) old rules" -InformationAction Continue
            }
        }
        catch {
            Write-Warning "Failed to clean up old rules: $($_.Exception.Message)"
        }
    }

    Write-Information "`n⏱️ Rule propagation takes up to 5 minutes" -InformationAction Continue
    Write-Information "🧪 Test connection with: Test-AzureConnection.ps1" -InformationAction Continue
    Write-Information "🚌 Run BusBuddy with: bb-run" -InformationAction Continue

    # Return success object for PowerShell automation
    return @{
        Success        = $true
        IPAddress      = $currentIP
        RuleName       = $fullRuleName
        ServerName     = $ServerName
        ResourceGroup  = $ResourceGroupName
        SubscriptionId = $SubscriptionId
        Timestamp      = Get-Date
    }
}
catch {
    Write-Error "Azure firewall update failed: $($_.Exception.Message)"

    Write-Information "`n🔧 Troubleshooting options:" -InformationAction Continue
    Write-Information "1. Check Azure credentials: Get-AzContext" -InformationAction Continue
    Write-Information "2. Verify resource group: Get-AzResourceGroup" -InformationAction Continue
    Write-Information "3. Manual portal fix: https://portal.azure.com" -InformationAction Continue
    Write-Information "4. Use local database temporarily: Set 'DatabaseProvider=Local' in config" -InformationAction Continue

    return @{
        Success   = $false
        Error     = $_.Exception.Message
        IPAddress = $currentIP
        Timestamp = Get-Date
    }
}
