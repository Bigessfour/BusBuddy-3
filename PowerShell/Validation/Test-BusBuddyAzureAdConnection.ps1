<#
.SYNOPSIS
    Tests BusBuddy Azure SQL connectivity using Azure AD authentication without storing a password.

.DESCRIPTION
    Implements the recommended "no password" approach using Authentication=Active Directory Default (preferred)
    or Active Directory Interactive (fallback). Sets the BUSBUDDY_CONNECTION environment variable so the
    BusBuddyDbContextFactory and DbContext OnConfiguring logic use the correct connection string.

    References:
      Azure SQL & Entra ID Auth Overview:
        https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-overview
      SQL Connection Strings (Authentication keyword):
        https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax

.NOTES
    Follows BusBuddy PowerShell standards: no Write-Host, uses output objects, supports -Verbose.
    Does not persist secrets. For MFA-enabled accounts use Default (token-based) or Interactive.

.PARAMETER Mode
    Authentication mode: Default (token based) or Interactive (prompts). Default: Default.

.PARAMETER Server
    Azure SQL logical server hostname (without tcp: prefix). Default: busbuddy-server-sm2.database.windows.net

.PARAMETER Database
    Target database name. Default: BusBuddyDB

.PARAMETER ApplyMigrations
    When specified, runs EF Core migrations list and database update using the configured connection.

.EXAMPLE
    .\Test-BusBuddyAzureAdConnection.ps1 -Verbose

.EXAMPLE
    .\Test-BusBuddyAzureAdConnection.ps1 -Mode Interactive -ApplyMigrations

.OUTPUTS
    PSCustomObject with ConnectionTest, AuthenticationMode, Server, Database, EfMigrationsApplied
#>
[CmdletBinding()]
param(
    [ValidateSet('Default', 'Interactive')]
    [string]$Mode = 'Default',
    [string]$Server = 'busbuddy-server-sm2.database.windows.net',
    [string]$Database = 'BusBuddyDB',
    [switch]$ApplyMigrations
)

function New-ConnectionString {
    param(
        [string]$Server,
        [string]$Database,
        [string]$Mode
    )
    $authValue = if ($Mode -eq 'Interactive') { 'Active Directory Interactive' } else { 'Active Directory Default' }
    # No password included – relies on cached token / device flow.
    "Server=tcp:$Server,1433;Initial Catalog=$Database;Authentication=$authValue;Encrypt=True;TrustServerCertificate=False;"
}

Write-Verbose "Building connection string for $Server / $Database using mode $Mode"
$connectionString = New-ConnectionString -Server $Server -Database $Database -Mode $Mode

# Export required environment variables for DbContext resolution
$env:DatabaseProvider = 'Azure'
$env:BUSBUDDY_CONNECTION = $connectionString
Write-Verbose 'Set environment variables: DatabaseProvider=Azure and BUSBUDDY_CONNECTION (passwordless)'

$connectionResult = 'Unknown'
$exceptionMessage = $null

try {
    Add-Type -AssemblyName System.Data
    $conn = [System.Data.SqlClient.SqlConnection]::new($connectionString)
    $conn.Open()
    $serverVersion = $conn.ServerVersion
    $dbName = $conn.Database
    $connectionResult = 'Success'
    Write-Verbose "Connected. ServerVersion=$serverVersion Database=$dbName"
}
catch {
    $connectionResult = 'Failed'
    $exceptionMessage = $_.Exception.Message
    Write-Verbose "Connection failed: $exceptionMessage"
}
finally {
    if ($conn -and $conn.State -eq 'Open') { $conn.Close() }
    if ($conn) { $conn.Dispose() }
}

$efApplied = $false
if ($ApplyMigrations -and $connectionResult -eq 'Success') {
    Write-Verbose 'Running EF Core migrations (list + update)'
    $baseArgs = '--project BusBuddy.Core --startup-project BusBuddy.WPF'
    dotnet ef migrations list $baseArgs | Out-Null
    dotnet ef database update $baseArgs | Out-Null
    $efApplied = $true
}

[PSCustomObject]@{
    ConnectionTest      = $connectionResult
    AuthenticationMode  = $Mode
    Server              = $Server
    Database            = $Database
    EfMigrationsApplied = $efApplied
    Error               = $exceptionMessage
    TimestampUtc        = [DateTime]::UtcNow
}
