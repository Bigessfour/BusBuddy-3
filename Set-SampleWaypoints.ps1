param(
    [string]$ConnectionString,
    [switch]$Force,
    [switch]$UseAzCliAuth
)

# Applies EF Core migrations and sets sample WaypointsJson on all eligible routes for map validation.
# Usage: pwsh -File ./Set-SampleWaypoints.ps1 [-ConnectionString <connStr>] [-Force]

$ErrorActionPreference = 'Stop'

function Get-ConnectionStringFromAppSettings {
    param(
        [string]$Path
    )
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "appsettings.json path parameter was null or empty"
    }
    if (!(Test-Path -LiteralPath $Path)) {
        throw "appsettings.json not found at '$Path'"
    }
    try {
        $json = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        throw "Failed to parse appsettings.json at '$Path': $($_.Exception.Message)"
    }
    if ($null -eq $json.ConnectionStrings -or $null -eq $json.ConnectionStrings.BusBuddyDb) {
        throw "ConnectionStrings:BusBuddyDb not found in appsettings.json at '$Path'"
    }
    $cs = [string]$json.ConnectionStrings.BusBuddyDb
    if ([string]::IsNullOrWhiteSpace($cs)) {
        throw "ConnectionStrings:BusBuddyDb is empty in appsettings.json at '$Path'"
    }
    return $cs
}

function Apply-Migrations {
    Write-Information "Applying EF Core migrations..." -InformationAction Continue
    $root = if ($PSScriptRoot) { $PSScriptRoot } elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path } else { (Get-Location).Path }
    $coreProj = Join-Path $root 'BusBuddy.Core\BusBuddy.Core.csproj'
    $wpfProj  = Join-Path $root 'BusBuddy.WPF\BusBuddy.WPF.csproj'

    if (!(Test-Path -LiteralPath $coreProj)) { throw "Core project not found at '$coreProj'" }
    if (!(Test-Path -LiteralPath $wpfProj))  { throw "WPF startup project not found at '$wpfProj'" }

    # First, try to add a migration automatically to satisfy any pending model changes
    $migrationName = "Auto_" + (Get-Date -Format 'yyyyMMddHHmmss')
    Write-Information "Checking for pending model changes (attempting: migrations add $migrationName)..." -InformationAction Continue
    $addOutput = & dotnet 'ef' 'migrations' 'add' $migrationName '--project' $coreProj '--startup-project' $wpfProj 2>&1
    $addExit = $LASTEXITCODE
    if ($addExit -ne 0) {
        $addText = ($addOutput | Out-String)
        if ($addText -match 'No changes were found' -or $addText -match 'No changes were found in the model') {
            Write-Information "No pending model changes detected — proceeding to database update." -InformationAction Continue
        }
        else {
            Write-Error $addText
            throw "EF Core migrations add failed with exit code $addExit"
        }
    }
    else {
        Write-Information "Created migration: $migrationName" -InformationAction Continue
    }

    # Apply migrations to database
    & dotnet 'ef' 'database' 'update' '--project' $coreProj '--startup-project' $wpfProj
    if ($LASTEXITCODE -ne 0) {
        throw "EF Core database update failed with exit code $LASTEXITCODE"
    }
    Write-Information "EF Core migrations applied." -InformationAction Continue
}

function Set-Sample-Waypoints {
    param(
        [string]$ConnStr,
        [switch]$Force,
        [switch]$UseAzCliAuth
    )
    if ([string]::IsNullOrWhiteSpace($ConnStr)) {
        throw "Connection string was null or empty when updating WaypointsJson"
    }
    $sample = @(
        @{ Latitude = 38.1527; Longitude = -102.7204 },
        @{ Latitude = 38.1560; Longitude = -102.7150 },
        @{ Latitude = 38.1605; Longitude = -102.7090 },
        @{ Latitude = 38.1660; Longitude = -102.7030 },
        @{ Latitude = 38.1720; Longitude = -102.6970 }
    )
    $json = ($sample | ConvertTo-Json -Compress)

    Write-Information "Updating all routes' WaypointsJson..." -InformationAction Continue

    try { Add-Type -AssemblyName System.Data -ErrorAction Stop } catch { }
    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnStr
    try {
        if ($UseAzCliAuth.IsPresent) {
            # Acquire Azure AD access token for Azure SQL using Azure CLI
            $azCmd = Get-Command az -ErrorAction SilentlyContinue
            if (-not $azCmd) { throw "Azure CLI (az) not found. Install Azure CLI or run without -UseAzCliAuth." }
            $tokenJson = & az account get-access-token --resource "https://database.windows.net/" -o json 2>&1
            if ($LASTEXITCODE -ne 0) {
                $msg = ($tokenJson | Out-String)
                throw "Failed to get Azure AD token via az: $msg"
            }
            try {
                $token = ($tokenJson | ConvertFrom-Json).accessToken
            } catch {
                throw "Unable to parse Azure AD token JSON: $($_.Exception.Message)"
            }
            if ([string]::IsNullOrWhiteSpace($token)) { throw "Azure AD token was empty" }
            $conn.AccessToken = $token
        }

        $conn.Open()

        # Step 1: Check if column exists
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COL_LENGTH('Routes', 'WaypointsJson')"
        $colLength = $cmd.ExecuteScalar()
        $hasColumn = ($null -ne $colLength -and -not ($colLength -is [System.DBNull]))

        if (-not $hasColumn) {
            # Step 2: Add column if missing
            Write-Information "WaypointsJson column not found — adding it now." -InformationAction Continue
            $cmd.Parameters.Clear()
            $cmd.CommandText = "ALTER TABLE Routes ADD WaypointsJson nvarchar(4000) NULL;"
            $null = $cmd.ExecuteNonQuery()
            Write-Information "Column added successfully." -InformationAction Continue
        }
        else {
            Write-Information "WaypointsJson column already exists." -InformationAction Continue
        }

    # Step 3: Update data (column is guaranteed to exist now)
    $cmd.Parameters.Clear()
    $cmd.CommandText = @"
SET NOCOUNT OFF;
UPDATE Routes
SET WaypointsJson = @json
WHERE @force = 1 OR WaypointsJson IS NULL OR LEN(LTRIM(RTRIM(WaypointsJson))) = 0 OR WaypointsJson = '[]';
SELECT @@ROWCOUNT;
"@
    # Use -1 to indicate NVARCHAR(MAX) parameter size
    $null = $cmd.Parameters.Add('@json', [System.Data.SqlDbType]::NVarChar, -1)
    $cmd.Parameters['@json'].Value = $json
    $null = $cmd.Parameters.Add('@force', [System.Data.SqlDbType]::Bit)
    $cmd.Parameters['@force'].Value = [int]$Force.IsPresent
    $rows = $cmd.ExecuteScalar()
    if ($rows -is [System.DBNull]) { $rows = 0 }
    Write-Information "WaypointsJson update affected $rows row(s)." -InformationAction Continue
    }
    finally {
        $conn.Dispose()
    }
}

try {
    $root = if ($PSScriptRoot) { $PSScriptRoot } elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path } else { (Get-Location).Path }
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        # Try common locations for appsettings.json
        $candidates = @(
            (Join-Path $root 'appsettings.json'),
            (Join-Path $root 'BusBuddy.WPF\appsettings.json'),
            (Join-Path $root 'BusBuddy.WPF/appsettings.json')
        )
        $appsettings = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $appsettings) {
            throw "Could not locate appsettings.json in expected locations under '$root'"
        }
        $ConnectionString = Get-ConnectionStringFromAppSettings -Path $appsettings
    }

    Apply-Migrations
    Set-Sample-Waypoints -ConnStr $ConnectionString -Force:$Force -UseAzCliAuth:$UseAzCliAuth

    Write-Information "Done. You can now toggle overlays and print the route map in the UI." -InformationAction Continue
}
catch {
    Write-Error $_
    exit 1
}
