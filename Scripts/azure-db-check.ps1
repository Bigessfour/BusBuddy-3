<#
.SYNOPSIS
Azure SQL inspection helper for BusBuddy CI/diagnostics.

.DESCRIPTION
Attempts to list Azure SQL servers and databases in the current Az context.
If Az modules or authentication are missing, guides the user how to connect.
#>

if (-not (Get-Module -ListAvailable -Name Az.Accounts)) {
    Write-Host "Az module not installed. Install with: Install-Module -Name Az -Scope CurrentUser" -ForegroundColor Yellow
    return
}

Import-Module Az.Accounts -ErrorAction Stop
Import-Module Az.Sql -ErrorAction SilentlyContinue

try {
    $ctx = Get-AzContext -ErrorAction Stop
} catch {
    Write-Warning "Not authenticated to Azure. Run Connect-AzAccount to authenticate in this session."
    return
}

Write-Host "Authenticated to subscription: $($ctx.Subscription.Name) ($($ctx.Subscription.Id))" -ForegroundColor Green

if (-not (Get-Module -ListAvailable -Name Az.Sql)) {
    Write-Warning "Az.Sql module not installed. Installing now..."
    Install-Module -Name Az.Sql -Scope CurrentUser -Force -AllowClobber
    Import-Module Az.Sql -ErrorAction Stop
}

Write-Host "Listing SQL servers in subscription..." -ForegroundColor Cyan
$servers = Get-AzSqlServer -ErrorAction SilentlyContinue
if (-not $servers) {
    Write-Warning "No SQL servers found in this subscription or permission issue."
    return
}

foreach ($s in $servers) {
    Write-Host "Server: $($s.ServerName) - Location: $($s.Location)" -ForegroundColor White
    $dbs = Get-AzSqlDatabase -ServerName $s.ServerName -ResourceGroupName $s.ResourceGroupName -ErrorAction SilentlyContinue
    foreach ($d in $dbs) {
        Write-Host "  - Database: $($d.DatabaseName) (Edition: $($d.Edition))" -ForegroundColor Gray
    }
}
