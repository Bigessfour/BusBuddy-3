
# Azure-SQL-Diagnostic.ps1
# Provides a function to validate Azure SQL configuration and test connectivity.
# Microsoft PowerShell 7.5.2 standards: https://docs.microsoft.com/en-us/powershell/

function Test-BusBuddyAzureSql {
    <#
    .SYNOPSIS
        Validates Azure SQL configuration and tests connectivity for BusBuddy.
    .DESCRIPTION
        Checks required environment variables, prints current configuration, and runs Test-AzureConnection.ps1 if available.
    .EXAMPLE
        Test-BusBuddyAzureSql
    #>
    [CmdletBinding()]
    param()

    $user = [Environment]::GetEnvironmentVariable('AZURE_SQL_USER', 'User')
    $password = [Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD', 'User')
    $server = 'busbuddy-server-sm2.database.windows.net'
    $database = 'BusBuddyDB'

    Write-Information "[BusBuddy Azure SQL Diagnostic]" -InformationAction Continue
    Write-Information "Server: $server" -InformationAction Continue
    Write-Information "Database: $database" -InformationAction Continue
    Write-Information "User: $user" -InformationAction Continue
    if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($password)) {
        Write-Warning "AZURE_SQL_USER or AZURE_SQL_PASSWORD environment variable is not set."
        return
    }

    $connectionString = "Server=tcp:$server,1433;Initial Catalog=$database;Persist Security Info=False;User ID=$user;Password=********;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    Write-Information "Connection String (masked): $connectionString" -InformationAction Continue

    $testScript = Join-Path $PSScriptRoot 'Test-AzureConnection.ps1'
    if (Test-Path $testScript) {
        Write-Information "Running Test-AzureConnection.ps1..." -InformationAction Continue
        try {
            . $testScript | Write-Output
        } catch {
            Write-Error "Test-AzureConnection.ps1 failed: $_"
        }
    } else {
        Write-Warning "Test-AzureConnection.ps1 not found in $PSScriptRoot. Skipping connectivity test."
    }
}

# Export for profile auto-loading (if dot-sourced)
Set-Alias -Name bb-azure-diagnostic -Value Test-BusBuddyAzureSql -Option AllScope
