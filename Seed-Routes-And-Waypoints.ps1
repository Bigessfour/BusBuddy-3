#!/usr/bin/env pwsh
# Seed-Routes-And-Waypoints.ps1
# Seeds Routes table and then applies waypoints

param(
    [switch]$UseAzCliAuth = $true
)

Write-Host "🚌 Seeding Routes and Applying Waypoints" -ForegroundColor Cyan

try {
    # Set Azure environment
    $env:ASPNETCORE_ENVIRONMENT = "Production"

    # Get Azure CLI access token
    if ($UseAzCliAuth) {
        Write-Host "🔐 Getting Azure CLI access token..." -ForegroundColor Yellow
        $accessToken = az account get-access-token --resource https://database.windows.net/ --query accessToken --output tsv
        if (-not $accessToken) {
            Write-Host "❌ Failed to get Azure access token" -ForegroundColor Red
            exit 1
        }
        Write-Host "✅ Azure access token obtained" -ForegroundColor Green
    }

    # First, create some sample routes directly via SQL
    Write-Host "🛣️ Creating sample routes..." -ForegroundColor Yellow

    $routesSql = @"
IF NOT EXISTS (SELECT 1 FROM Routes)
BEGIN
    INSERT INTO Routes (RouteName, Description, CreatedDate, CreatedBy, IsActive, AMRiders, PMRiders)
    VALUES
    ('North Elementary', 'North side elementary school route', GETUTCDATE(), 'SeedScript', 1, 15, 18),
    ('South Elementary', 'South side elementary school route', GETUTCDATE(), 'SeedScript', 1, 12, 14),
    ('Middle School Express', 'Express route to middle school', GETUTCDATE(), 'SeedScript', 1, 25, 28),
    ('High School Route A', 'High school route A', GETUTCDATE(), 'SeedScript', 1, 35, 32),
    ('High School Route B', 'High school route B', GETUTCDATE(), 'SeedScript', 1, 28, 30),
    ('Elementary East', 'East side elementary route', GETUTCDATE(), 'SeedScript', 1, 20, 22),
    ('Elementary West', 'West side elementary route', GETUTCDATE(), 'SeedScript', 1, 16, 19),
    ('Special Needs Route', 'Special needs transportation', GETUTCDATE(), 'SeedScript', 1, 8, 8);

    PRINT 'Created ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' routes';
END
ELSE
BEGIN
    PRINT 'Routes already exist, skipping creation';
END
"@

    # Execute the routes creation
    if ($UseAzCliAuth) {
        $routesSql | sqlcmd -S busbuddy-server-sm2.database.windows.net -d BusBuddyDB -G -P $accessToken
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Routes created successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to create routes" -ForegroundColor Red
        exit 1
    }

    # Now run the waypoints update
    Write-Host "🗺️ Applying waypoints to routes..." -ForegroundColor Yellow

    $waypointsSql = @"
DECLARE @force BIT = 1;

UPDATE Routes
SET WaypointsJson = CASE
    WHEN RouteName LIKE '%Elementary%' THEN
        '[{"lat":40.7128,"lng":-74.0060,"type":"pickup","description":"Elementary pickup point"},{"lat":40.7589,"lng":-73.9851,"description":"Elementary school"}]'
    WHEN RouteName LIKE '%Middle%' THEN
        '[{"lat":40.7282,"lng":-73.9942,"type":"pickup","description":"Middle school pickup"},{"lat":40.7505,"lng":-73.9934,"description":"Middle school campus"}]'
    WHEN RouteName LIKE '%High%' THEN
        '[{"lat":40.7614,"lng":-73.9776,"type":"pickup","description":"High school pickup"},{"lat":40.7831,"lng":-73.9712,"description":"High school"}]'
    ELSE
        '[{"lat":40.7484,"lng":-73.9857,"type":"pickup","description":"General pickup point"},{"lat":40.7580,"lng":-73.9855,"description":"School destination"}]'
END
WHERE @force = 1 OR WaypointsJson IS NULL OR WaypointsJson = '' OR WaypointsJson = '[]';

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' routes with waypoints';
"@

    # Execute the waypoints update
    if ($UseAzCliAuth) {
        $waypointsSql | sqlcmd -S busbuddy-server-sm2.database.windows.net -d BusBuddyDB -G -P $accessToken
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Waypoints applied successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to apply waypoints" -ForegroundColor Red
        exit 1
    }

    # Verify the results
    Write-Host "🔍 Verifying results..." -ForegroundColor Yellow

    $verifySQL = @"
SELECT
    RouteId,
    RouteName,
    CASE
        WHEN WaypointsJson IS NULL OR WaypointsJson = '' THEN 'No waypoints'
        WHEN LEN(WaypointsJson) > 50 THEN 'Has waypoints (' + CAST(LEN(WaypointsJson) AS VARCHAR(10)) + ' chars)'
        ELSE WaypointsJson
    END as WaypointStatus
FROM Routes
ORDER BY RouteName;
"@

    if ($UseAzCliAuth) {
        Write-Host "Routes and Waypoints Status:" -ForegroundColor Cyan
        $verifySQL | sqlcmd -S busbuddy-server-sm2.database.windows.net -d BusBuddyDB -G -P $accessToken
    }

    Write-Host "🎉 Routes seeding and waypoints application completed!" -ForegroundColor Green

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
