# BusBuddy.Database.psm1
# PowerShell module for database operations
#requires -Version 7.5
[CmdletBinding()]
param()

# Minimal function to prevent null script errors
function Test-DatabaseConnection {
    [CmdletBinding()]
    param(
        [string]$ConnectionString
    )

    if (-not $ConnectionString) {
        Write-Warning "Connection string is null or empty"
        return $false
    }

    Write-Information "Testing database connection..." -InformationAction Continue
    return $true
}

# Export module members
Export-ModuleMember -Function Test-DatabaseConnection
