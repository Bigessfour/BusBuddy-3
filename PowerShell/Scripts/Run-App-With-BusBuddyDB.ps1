# Run the WPF app with BUSBUDDY_CONNECTION override pointing to BusBuddyDB (SQL auth)
# Docs: .NET CLI run — https://learn.microsoft.com/dotnet/core/tools/dotnet-run
# PowerShell Output Streams — https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$user = $env:AZURE_SQL_USER
$pass = $env:AZURE_SQL_PASSWORD
if (-not $user -or -not $pass) {
    Write-Error "AZURE_SQL_USER/AZURE_SQL_PASSWORD environment variables are not set."
}

$connection = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID=$user;Password=$pass;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"

# Export for child process only (current session)
$env:BUSBUDDY_CONNECTION = $connection

Write-Information "Launching BusBuddy.WPF with BUSBUDDY_CONNECTION override to BusBuddyDB" -InformationAction Continue

& dotnet run --project "c:\Users\biges\Desktop\BusBuddy\BusBuddy.WPF\BusBuddy.WPF.csproj"
