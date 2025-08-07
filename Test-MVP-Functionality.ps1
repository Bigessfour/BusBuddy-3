# 🚌 BusBuddy MVP Test - Student Entry & Routes
# Quick test to verify core MVP functionality is working

Write-Host "🚌 BusBuddy MVP Test" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan

Write-Host ""
Write-Host "🔍 1. Testing database connection..." -ForegroundColor Yellow
if (-not ($env:AZURE_SQL_USER -and $env:AZURE_SQL_PASSWORD)) {
    Write-Host "   ❌ Environment variables not set" -ForegroundColor Red
    Write-Host "   💡 Run: ./Setup-Azure-SQL-Owner.ps1 first" -ForegroundColor Cyan
    exit 1
}

$ServerName = "busbuddy-server-sm2.database.windows.net"
$DatabaseName = "BusBuddyDB"
$ConnectionString = "Server=tcp:$ServerName,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$env:AZURE_SQL_USER;Password=$env:AZURE_SQL_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

try {
    Add-Type -AssemblyName System.Data
    $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $Connection.Open()
    Write-Host "   ✅ Database connection successful" -ForegroundColor Green
    $Connection.Close()
} catch {
    Write-Host "   ❌ Database connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔍 2. Checking MVP tables..." -ForegroundColor Yellow
try {
    $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $Connection.Open()

    # Check for Students table
    $Command = $Connection.CreateCommand()
    $Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Students'"
    $StudentsExists = $Command.ExecuteScalar()

    # Check for Routes table
    $Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Routes'"
    $RoutesExists = $Command.ExecuteScalar()


    # Check for Buses table (formerly Vehicles)
    $Command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Buses'"
    $BusesExists = $Command.ExecuteScalar()

    $Connection.Close()

    if ($StudentsExists -gt 0) {
        Write-Host "   ✅ Students table exists" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Students table missing" -ForegroundColor Red
    }

    if ($RoutesExists -gt 0) {
        Write-Host "   ✅ Routes table exists" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Routes table missing" -ForegroundColor Red
    }

    if ($VehiclesExists -gt 0) {
        Write-Host "   ✅ Vehicles table exists" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Vehicles table missing" -ForegroundColor Red
    }

    if ($StudentsExists -eq 0 -or $RoutesExists -eq 0 -or $VehiclesExists -eq 0) {
        Write-Host "   💡 Run migrations: ./Setup-Azure-SQL-Owner.ps1" -ForegroundColor Cyan
        exit 1
    }

} catch {
    Write-Host "   ❌ Table check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔍 3. Testing application startup..." -ForegroundColor Yellow
Write-Host "   🔧 Building solution..." -ForegroundColor Blue
dotnet build BusBuddy.sln --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "   ✅ Build successful" -ForegroundColor Green
Write-Host "   💡 Ready to test application!" -ForegroundColor Cyan

Write-Host ""
Write-Host "🎯 MVP Test Results:" -ForegroundColor Green
Write-Host "===================" -ForegroundColor Green
Write-Host "✅ Database connection working" -ForegroundColor White
Write-Host "✅ MVP tables (Students, Routes, Vehicles) exist" -ForegroundColor White
Write-Host "✅ Application builds successfully" -ForegroundColor White
Write-Host ""
Write-Host "📋 Manual Testing Steps:" -ForegroundColor Cyan
Write-Host "1. Run: bb-run (or dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj)" -ForegroundColor White
Write-Host "2. Navigate to Students tab" -ForegroundColor White
Write-Host "3. Add a test student (e.g., 'John Doe', Student ID: '12345')" -ForegroundColor White
Write-Host "4. Navigate to Routes tab" -ForegroundColor White
Write-Host "5. Create a test route" -ForegroundColor White
Write-Host "6. Assign student to route" -ForegroundColor White
Write-Host ""
Write-Host "🔍 Verify in Azure:" -ForegroundColor Cyan
Write-Host "• Open Azure Query Editor" -ForegroundColor White
Write-Host "• Run: SELECT * FROM Students;" -ForegroundColor White
Write-Host "• Run: SELECT * FROM Routes;" -ForegroundColor White
Write-Host ""
Write-Host "🚀 Ready for MVP testing!" -ForegroundColor Green
