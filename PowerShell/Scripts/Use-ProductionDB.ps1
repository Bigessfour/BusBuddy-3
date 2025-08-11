param(
    [switch]$Clean,
    [ValidateSet('AAD','SQL')]
    [string]$Auth = 'AAD'
)

# Purpose: Set BUSBUDDY_CONNECTION to the Production DB and optionally run the seeder
# Docs: PowerShell environment variables https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Environment_Variables
#       .NET connection strings https://learn.microsoft.com/dotnet/framework/data/adonet/connection-strings

$ErrorActionPreference = 'Stop'

# Build production connection string
if ($Auth -eq 'AAD') {
    $connection = 'Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;MultipleActiveResultSets=True;'
    Write-Information 'Using Azure AD Default authentication (requires az/VS sign-in)' -InformationAction Continue
}
else {
    # Use placeholders and let the app expand them; requires AZURE_SQL_USER and AZURE_SQL_PASSWORD set
    $connection = 'Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;MultipleActiveResultSets=True;'
    if (-not $env:AZURE_SQL_USER -or -not $env:AZURE_SQL_PASSWORD) {
        Write-Warning 'SQL auth selected but AZURE_SQL_USER/AZURE_SQL_PASSWORD are not set. Set them and re-run.'
    }
}

$env:BUSBUDDY_CONNECTION = $connection
$env:ASPNETCORE_ENVIRONMENT = 'Production'

if ($Clean) { $env:BUSBUDDY_CLEAN_BEFORE_SEED = '1' } else { $env:BUSBUDDY_CLEAN_BEFORE_SEED = $null }

Write-Information "BUSBUDDY_CONNECTION set for production. Clean=$Clean Auth=$Auth" -InformationAction Continue
Write-Information 'Next: dotnet run --project TestDataSeeding/TestDataSeeding.csproj -- --clean' -InformationAction Continue
