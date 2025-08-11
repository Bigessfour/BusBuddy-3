#requires -Version 7.0
<#
    Seed-Mock-Routes.ps1 — Seeds Azure SQL with mock Routes, Vehicles, and Drivers to reach target counts.

    Usage examples:
    - Preview (no changes):
        pwsh -NoProfile -ExecutionPolicy Bypass -File ./PowerShell/Scripts/Seed-Mock-Routes.ps1 -WhatIf
    - Apply changes:
        pwsh -NoProfile -ExecutionPolicy Bypass -File ./PowerShell/Scripts/Seed-Mock-Routes.ps1

    Notes:
    - Uses sqlcmd with Azure AD integrated auth (-G). Ensure you are signed in (e.g., via Azure CLI/SSO).
    - Inserts minimal required fields based on current DB schema discovery.
    - Does NOT overwrite existing data; only tops up counts to targets.
#>
[CmdletBinding()]
param(
    [string]$Server = "tcp:busbuddy-server-sm2.database.windows.net,1433",
    [string]$Database = "BusBuddyDB",
    [int]$TargetVehicles = 10,
    [int]$TargetDrivers = 8,
    [int]$TargetRoutes = 5,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-ScalarInt([string]$query){
    $out = & sqlcmd -S $Server -d $Database -G -W -h -1 -Q ("SET NOCOUNT ON; " + $query) 2>&1
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $($out -join ' ')" }
    # Filter out empty lines and lines that are not pure integers
    $line = $out | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1
    if (-not $line) {
        Write-Warning "No valid integer result found in sqlcmd output: $($out -join '; ')"
        return 0
    }
    try {
        return [int]$line
    } catch {
        Write-Warning "Failed to parse integer from sqlcmd output: $line"
        return 0
    }
}

function Invoke-Sql([string]$tsql){
    if ($WhatIf) {
        # Use Write-Information instead of Write-Host per Microsoft guidance
        Write-Information "--- WHATIF T-SQL BEGIN ---" -InformationAction Continue
        Write-Information $tsql -InformationAction Continue
        Write-Information "--- WHATIF T-SQL END ---" -InformationAction Continue
        return
    }
    $out = & sqlcmd -S $Server -d $Database -G -b -Q "$tsql" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd nonquery failed: $($out -join ' ')" }
}

# Count current rows
$counts      = [PSCustomObject]@{ Vehicles = 0; Drivers = 0; Routes = 0 }
$countQuery  = @"
SELECT
    (SELECT COUNT(1) FROM Vehicles) AS Vehicles,
    (SELECT COUNT(1) FROM Drivers)  AS Drivers,
    (SELECT COUNT(1) FROM Routes)   AS Routes
"@
# Force a consistent separator (-s ",") to avoid space-separated output; suppress headers (-h -1) and trim whitespace (-W)
$out = & sqlcmd -S $Server -d $Database -G -W -h -1 -s "," -Q ("SET NOCOUNT ON; " + $countQuery) 2>&1
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $($out -join ' ')" }

# Robust parse — accept comma or whitespace separated output, or multi-line outputs
# Collect the first three integers found in the output
$nums = @()
foreach ($ln in $out) {
    foreach ($m in [regex]::Matches($ln, '\d+')) {
        $nums += $m.Value
        if ($nums.Count -ge 3) { break }
    }
    if ($nums.Count -ge 3) { break }
}
if ($nums.Count -ne 3) {
    throw "Unexpected output from sqlcmd count query: $($out -join '; ')"
}

$counts.Vehicles = [int]$nums[0]
$counts.Drivers  = [int]$nums[1]
$counts.Routes   = [int]$nums[2]

$needVehicles = [Math]::Max(0, $TargetVehicles - $counts.Vehicles)
$needDrivers  = [Math]::Max(0, $TargetDrivers  - $counts.Drivers)
$needRoutes   = [Math]::Max(0, $TargetRoutes   - $counts.Routes)

# Start indexes based on current totals to reduce collision w/ existing data
$seedStartVehicle = $counts.Vehicles + 1
$seedStartDriver  = $counts.Drivers + 1
$seedStartRoute   = $counts.Routes + 1

# Informational output (no Write-Host)
Write-Information ("Current -> Target (need): Vehicles={0} -> {1} (+{2}), Drivers={3} -> {4} (+{5}), Routes={6} -> {7} (+{8})" -f `
    $counts.Vehicles, $TargetVehicles, $needVehicles, `
    $counts.Drivers,  $TargetDrivers,  $needDrivers,  `
    $counts.Routes,   $TargetRoutes,   $needRoutes) -InformationAction Continue

function New-VehiclesSql([int]$n){
    if ($n -le 0) { return $null }
    $values = @()
    for($i=1; $i -le $n; $i++){
        $idx   = $seedStartVehicle + $i - 1
        $busNo = "SEED-BUS-{0:000}" -f $idx
        $vin   = "SEEDVIN{0:010}" -f $idx  # 17 chars total (7 + 10)
        $lic   = "SEEDLIC{0:0000}" -f $idx
        $values += @"
IF NOT EXISTS (SELECT 1 FROM Vehicles WHERE BusNumber = '$busNo')
BEGIN
    IF COL_LENGTH('Vehicles','VINNumber') IS NOT NULL
    BEGIN
        EXEC(N'INSERT INTO Vehicles (BusNumber, Year, Make, Model, SeatingCapacity, VINNumber, LicenseNumber, Status, GPSTracking)
              VALUES (''$busNo'', 2020, ''Blue Bird'', ''Vision'', 48, ''$vin'', ''$lic'', ''Active'', 0);');
    END
    ELSE IF COL_LENGTH('Vehicles','VIN') IS NOT NULL
    BEGIN
        EXEC(N'INSERT INTO Vehicles (BusNumber, Year, Make, Model, SeatingCapacity, VIN, LicenseNumber, Status, GPSTracking)
              VALUES (''$busNo'', 2020, ''Blue Bird'', ''Vision'', 48, ''$vin'', ''$lic'', ''Active'', 0);');
    END
    ELSE
    BEGIN
        EXEC(N'INSERT INTO Vehicles (BusNumber, Year, Make, Model, SeatingCapacity, LicenseNumber, Status, GPSTracking)
              VALUES (''$busNo'', 2020, ''Blue Bird'', ''Vision'', 48, ''$lic'', ''Active'', 0);');
    END
END
"@
    }
    return ($values -join "`n")
}

function New-DriversSql([int]$n){
    if ($n -le 0) { return $null }
    $first = @('John','Jane','Mike','Sarah','Tom','Lisa','Mark','Emily','Chris','Anna')
    $last  = @('Smith','Johnson','Williams','Brown','Davis','Miller','Wilson','Taylor','Anderson','Thomas')
    $sb = New-Object System.Text.StringBuilder
    for($i=1; $i -le $n; $i++){
        $idx = $seedStartDriver + $i - 1
        $fn = $first[($idx-1) % $first.Count]
        $ln = $last[($idx-1) % $last.Count]
        $name = "$fn $ln"
        $phone = ("555-100-{0:0000}" -f $idx)
        [void]$sb.AppendLine(@"
IF NOT EXISTS (SELECT 1 FROM Drivers WHERE DriverName = '$name')
BEGIN
    IF COL_LENGTH('Drivers','DriversLicenceType') IS NOT NULL
    BEGIN
        EXEC(N'INSERT INTO Drivers (DriverName, DriversLicenceType, TrainingComplete, Status, FirstName, LastName, DriverPhone)
              VALUES (''$name'', ''CDL'', 1, ''Active'', ''$fn'', ''$ln'', ''$phone'');');
    END
    ELSE IF COL_LENGTH('Drivers','DriversLicenseType') IS NOT NULL
    BEGIN
        EXEC(N'INSERT INTO Drivers (DriverName, DriversLicenseType, TrainingComplete, Status, FirstName, LastName, DriverPhone)
              VALUES (''$name'', ''CDL'', 1, ''Active'', ''$fn'', ''$ln'', ''$phone'');');
    END
    ELSE
    BEGIN
        EXEC(N'INSERT INTO Drivers (DriverName, TrainingComplete, Status, FirstName, LastName, DriverPhone)
              VALUES (''$name'', 1, ''Active'', ''$fn'', ''$ln'', ''$phone'');');
    END
END
"@)
    }
    return $sb.ToString()
}

function New-RoutesSql([int]$n){
    if ($n -le 0) { return $null }
    $sb = New-Object System.Text.StringBuilder
    for($i=1; $i -le $n; $i++){
        $idx = $seedStartRoute + $i - 1
        $rname = "Seed Route $idx"
        [void]$sb.AppendLine(@"
IF NOT EXISTS (SELECT 1 FROM Routes WHERE CAST([Date] as date) = CAST(GETDATE() as date) AND RouteName = '$rname')
BEGIN
    INSERT INTO Routes ([Date], RouteName, IsActive)
    VALUES (CAST(GETDATE() as date), '$rname', 1);
END
"@)
    }
    return $sb.ToString()
}

$tsql = @()
if ($needVehicles -gt 0) { $tsql += New-VehiclesSql -n $needVehicles }
if ($needDrivers  -gt 0) { $tsql += New-DriversSql  -n $needDrivers }
if ($needRoutes   -gt 0) { $tsql += New-RoutesSql   -n $needRoutes }

if (-not $tsql -or ($tsql -join '').Trim().Length -eq 0){
    Write-Information "No seeding required. Targets already satisfied." -InformationAction Continue
    return
}

# Execute each statement individually to avoid SQL Server batch size limits
foreach ($stmt in $tsql) {
    if ($stmt.Trim().Length -gt 0) {
        Invoke-Sql $stmt
    }
}

Write-Information "Seeding complete (or previewed with -WhatIf). Verifying..." -InformationAction Continue
$postVehicles = Invoke-ScalarInt 'SELECT COUNT(1) FROM Vehicles;'
$postDrivers  = Invoke-ScalarInt 'SELECT COUNT(1) FROM Drivers;'
$postRoutes   = Invoke-ScalarInt 'SELECT COUNT(1) FROM Routes;'

Write-Output ([PSCustomObject]@{
    VehiclesBefore = $counts.Vehicles
    DriversBefore  = $counts.Drivers
    RoutesBefore   = $counts.Routes
    VehiclesAfter  = $postVehicles
    DriversAfter   = $postDrivers
    RoutesAfter    = $postRoutes
})

