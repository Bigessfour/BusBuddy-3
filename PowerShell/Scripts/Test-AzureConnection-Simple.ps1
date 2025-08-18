<#
Delegates to canonical script under PowerShell\Azure to avoid duplication.
Usage:
	pwsh -NoProfile -File .\PowerShell\Test-AzureConnection-Simple.ps1 -Server "..." -Database "..."
#>
[CmdletBinding()] param(
    [Parameter()] [string] $Server = "busbuddy-server-sm2.database.windows.net",
    [Parameter()] [string] $Database = "BusBuddyDB",
    [Parameter()] [switch] $UseAzureAD,
    [Parameter()] [string] $TenantId
)

$delegate = Join-Path $PSScriptRoot 'Azure\Invoke-AzureSqlConnectionTest.ps1'
if (-not (Test-Path $delegate)) { Write-Error "Delegate script not found: $delegate"; exit 1 }

& $delegate -Server $Server -Database $Database -UseAzureAD:$UseAzureAD -TenantId $TenantId
$LASTEXITCODE
