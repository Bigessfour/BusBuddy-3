# Applies EF Core migrations to Azure SQL using dotnet-ef
[CmdletBinding()] param(
    [Parameter()] [string] $Project = "BusBuddy.Core/BusBuddy.Core.csproj",
    [Parameter()] [string] $StartupProject = "BusBuddy.WPF/BusBuddy.WPF.csproj",
    [Parameter()] [switch] $AddInitialIfMissing,
    [Parameter()] [string] $Server = "busbuddy-server-sm2.database.windows.net",
    [Parameter()] [string] $Database = "BusBuddyDB"
)

Write-Information "Applying EF Core migrations to Azure SQL..." -InformationAction Continue

$azureUser = $env:AZURE_SQL_USER
$azurePassword = $env:AZURE_SQL_PASSWORD
if (-not $azureUser -or -not $azurePassword) { Write-Error "AZURE_SQL_USER/AZURE_SQL_PASSWORD not set"; exit 1 }

# Force EF to use Azure connection for this session
$env:DatabaseProvider = 'Azure'
$env:ConnectionStrings__DefaultConnection = "Server=tcp:$Server,1433;Initial Catalog=$Database;Persist Security Info=False;User ID=$azureUser;Password=$azurePassword;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
Write-Information "Session DB provider set to Azure; DefaultConnection configured for $Server/$Database" -InformationAction Continue

# Ensure dotnet-ef is available
$dotnetEf = (& dotnet tool list -g | Select-String -SimpleMatch "dotnet-ef").Length -gt 0
if (-not $dotnetEf) {
    Write-Information "Installing dotnet-ef..." -InformationAction Continue
    dotnet tool install -g dotnet-ef | Out-Null
}

# If no migrations exist and flag set, add an initial one
$migrationsPath = Join-Path (Split-Path $Project) "Migrations"
$hasMigrations = Test-Path $migrationsPath -PathType Container -and (Get-ChildItem $migrationsPath -Filter "*.cs" -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
if (-not $hasMigrations -and $AddInitialIfMissing) {
    Write-Information "No migrations found — creating InitialCreate" -InformationAction Continue
    dotnet ef migrations add InitialCreate --project $Project --startup-project $StartupProject --verbose
}

# Update database
$update = dotnet ef database update --project $Project --startup-project $StartupProject --verbose
$LASTEXITCODE
