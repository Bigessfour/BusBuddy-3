#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ConfigPath = "appsettings.json",
    [int]$Top = 50
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ConnectionString {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Config file not found: $Path"
    }
    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $cs = $json.ConnectionStrings
    if ($null -eq $cs) { throw "ConnectionStrings section missing in $Path" }

    $candidates = @(
        $cs.DefaultConnection,
        $cs.BusBuddyDb,
        $cs.LocalConnection
    ) | Where-Object { $_ -and $_ -is [string] -and $_.Trim().Length -gt 0 }

    if ($candidates.Count -eq 0) {
        throw "No SQL Server connection string found in $Path."
    }
    return $candidates[0]
}

function Invoke-Query {
    param(
        [string]$ConnectionString,
        [string]$Sql
    )
    Add-Type -AssemblyName System.Data
    $table = New-Object System.Data.DataTable
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        [void]$adapter.Fill($table)
        return $table
    }
    finally {
        if ($conn.State -ne 'Closed') { $conn.Close() }
        $conn.Dispose()
    }
}

# Resolve config path (prefer root), fallback to BusBuddy.WPF/appsettings.json if requested path not found
$resolvedConfig = if (Test-Path -LiteralPath $ConfigPath) {
    (Resolve-Path -LiteralPath $ConfigPath).Path
} elseif (Test-Path -LiteralPath "BusBuddy.WPF/appsettings.json") {
    (Resolve-Path -LiteralPath "BusBuddy.WPF/appsettings.json").Path
} else {
    throw "Could not find appsettings.json at '$ConfigPath' or 'BusBuddy.WPF/appsettings.json'"
}

$connectionString = Get-ConnectionString -Path $resolvedConfig
Write-Information "Using config: $resolvedConfig" -InformationAction Continue
Write-Information "Connecting with: $connectionString" -InformationAction Continue

$topN = [Math]::Max(1, $Top)
$sql = @"
SELECT TOP ($topN)
    StudentId,
    StudentName,
    StudentNumber,
    Grade,
    Active,
    CreatedDate
FROM dbo.Students
ORDER BY StudentId DESC;
"@

try {
    $dt = Invoke-Query -ConnectionString $connectionString -Sql $sql
    if ($dt.Rows.Count -eq 0) {
        Write-Output "No students found."
        return
    }
    $dt | Select-Object StudentId, StudentName, StudentNumber, Grade, Active, CreatedDate |
        Sort-Object StudentId -Descending |
        Format-Table -AutoSize | Out-Host
}
catch {
    Write-Error $_
    throw
}
