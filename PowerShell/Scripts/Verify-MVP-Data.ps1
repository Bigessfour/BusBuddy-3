#requires -Version 7.0
<#
    Verify-MVP-Data.ps1 — Validates presence of students, routes, vehicles, and drivers in Azure SQL

    Docs:
    - sqlcmd utility: https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility
#>
param(
    [string]$Server = "busbuddy-server-sm2.database.windows.net",
    [string]$Database = "BusBuddyDB"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$queries = @{
    Students = 'SELECT COUNT(1) FROM Students;'
    Routes   = 'SELECT COUNT(1) FROM Routes;'
    Vehicles = 'SELECT COUNT(1) FROM Vehicles;'
    Drivers  = 'SELECT COUNT(1) FROM Drivers;'
}

function Invoke-CountQuery([string]$name, [string]$q){
    $val = & sqlcmd -S $Server -d $Database -G -Q ("SET NOCOUNT ON; " + $q) -W -h -1 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("sqlcmd failed for {0}: {1}" -f $name, ($val -join ' ')) }
    $line = $val | Where-Object { $_ -match '^[0-9]+$' } | Select-Object -First 1
    if (-not $line) { return 0 }
    return [int]$line
}

try {
    $results = [ordered]@{}
    foreach($k in $queries.Keys){ $results[$k] = Invoke-CountQuery $k $queries[$k] }

    Write-Output (
        [PSCustomObject]@{
            Students = $results.Students
            Routes   = $results.Routes
            Vehicles = $results.Vehicles
            Drivers  = $results.Drivers
        }
    )
}
catch { Write-Error $_; exit 1 }
