<#
.SYNOPSIS
  Utilities to add/remove the caller's current public IP to an Azure SQL Server firewall.

.DESCRIPTION
  Provides parameterized functions that:
  - Discover current public IP (via ipinfo.io / api.ipify.org with fallback)
  - Create or update a firewall rule named for the environment/machine
  - Remove the firewall rule when done
  - Optionally set a short-lived TTL by encoding expiry in rule name

.REQUIREMENTS
  - Az.Accounts, Az.Resources, Az.Sql modules (Install-Module Az -Scope CurrentUser)
  - Logged into Azure (Connect-AzAccount) with permission to manage the SQL server

.REFERENCES
  - Microsoft Docs: https://learn.microsoft.com/azure/azure-sql/database/firewall-configure
  - Az.Sql Cmdlets: https://learn.microsoft.com/powershell/module/az.sql/
#>

# region: helpers
function Get-PublicIpAddress {
    [CmdletBinding()]
    param()
    try {
        $ip = (Invoke-RestMethod -Uri 'https://api.ipify.org?format=json' -TimeoutSec 5).ip
        if ($ip) { return $ip }
    } catch { }
    try {
        $ip = (Invoke-RestMethod -Uri 'https://ipinfo.io/json' -TimeoutSec 5).ip
        if ($ip) { return $ip }
    } catch { }
    throw 'Unable to determine public IP. Check internet connectivity or provide -StartIp/-EndIp.'
}

function Test-AzModulesInstalled {
    [CmdletBinding()] param(
        [switch] $AutoInstall
    )
    $required = 'Az.Accounts','Az.Resources','Az.Sql'
    $missing = @()
    foreach ($m in $required) {
        if (-not (Get-Module -ListAvailable -Name $m)) { $missing += $m }
    }
    if ($missing.Count -gt 0) {
        if ($AutoInstall) {
            Write-Verbose "Installing Az modules (requires PowerShellGet/NuGet gallery access)"
            try {
                Install-Module Az -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
            } catch {
                throw "Missing Az modules: $($missing -join ', '). Install with: Install-Module Az -Scope CurrentUser. Error: $($_.Exception.Message)"
            }
        } else {
            throw "Missing Az modules: $($missing -join ', '). Install with: Install-Module Az -Scope CurrentUser"
        }
    }
    # Ensure imported
    foreach ($m in $required) { Import-Module $m -ErrorAction SilentlyContinue | Out-Null }
}
# endregion

function Add-AzureSqlFirewallForCaller {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $ResourceGroupName,
        [Parameter(Mandatory)] [string] $SqlServerName,
        [string] $RuleName = $("bb-$(($env:COMPUTERNAME) -replace '[^a-zA-Z0-9-]', '-')-$(Get-Date -Format 'yyyyMMdd-HHmm')"),
        [string] $StartIp,
        [string] $EndIp,
        [switch] $SetAsClientIp
    )
    Test-AzModulesInstalled -AutoInstall

    # Ensure context
    $ctx = Get-AzContext -ErrorAction SilentlyContinue
    if (-not $ctx -or $ctx.Subscription.Id -ne $SubscriptionId) {
        Write-Verbose "Setting Az context to $SubscriptionId"
        try { Set-AzContext -Subscription $SubscriptionId | Out-Null }
        catch { throw "Failed to set Azure context. Ensure you're logged in: Connect-AzAccount; then Set-AzContext -Subscription $SubscriptionId. Error: $($_.Exception.Message)" }
    }

    if (-not $StartIp) { $StartIp = Get-PublicIpAddress }
    if (-not $EndIp)   { $EndIp   = $StartIp }

    if ($PSCmdlet.ShouldProcess("$SqlServerName", "Add/Update firewall rule $RuleName for $StartIp-$EndIp")) {
        if ($SetAsClientIp) {
            # Uses the special -ClientIpAddress switch (single IP)
            return New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName -ServerName $SqlServerName -FirewallRuleName $RuleName -ClientIpAddress $StartIp -ErrorAction Stop
        } else {
            return New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName -ServerName $SqlServerName -FirewallRuleName $RuleName -StartIpAddress $StartIp -EndIpAddress $EndIp -ErrorAction Stop
        }
    }
}

function Remove-AzureSqlFirewallRuleSafe {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)] [string] $SubscriptionId,
        [Parameter(Mandatory)] [string] $ResourceGroupName,
        [Parameter(Mandatory)] [string] $SqlServerName,
        [Parameter(Mandatory)] [string] $RuleName
    )
    Test-AzModulesInstalled

    $ctx = Get-AzContext -ErrorAction SilentlyContinue
    if (-not $ctx -or $ctx.Subscription.Id -ne $SubscriptionId) {
        try { Set-AzContext -Subscription $SubscriptionId | Out-Null }
        catch { throw "Failed to set Azure context. Ensure you're logged in: Connect-AzAccount; then Set-AzContext -Subscription $SubscriptionId. Error: $($_.Exception.Message)" }
    }

    try {
        if ($PSCmdlet.ShouldProcess("$SqlServerName", "Remove firewall rule $RuleName")) {
            Remove-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName -ServerName $SqlServerName -FirewallRuleName $RuleName -ErrorAction Stop
            Write-Output "Removed firewall rule: $RuleName"
        }
    } catch {
        Write-Warning "Failed to remove rule '$RuleName': $($_.Exception.Message)"
    }
}

# Note: Export-ModuleMember is only valid within modules (.psm1). These functions are provided via dot-sourcing.
