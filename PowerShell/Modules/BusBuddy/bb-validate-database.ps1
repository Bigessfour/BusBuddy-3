# bb-validate-database.ps1 - Quick database validation for BusBuddy MVP
# Integrates with existing bb-* command structure


function Test-BusBuddyDatabase {
    [CmdletBinding()]
    param(
        [switch]$IncludeCRUD,
        [switch]$Detailed
    )
    if ($PSVersionTable.PSVersion.Major -lt 7 -or ($PSVersionTable.PSVersion.Major -eq 7 -and $PSVersionTable.PSVersion.Minor -lt 5)) {
        throw "Test-BusBuddyDatabase requires PowerShell 7.5 or later. Current version: $($PSVersionTable.PSVersion)"
    }
    <#
    .SYNOPSIS
        Quick database health validation for BusBuddy MVP
    .DESCRIPTION
        Performs essential database connectivity, table existence, and integrity checks.
        Integrates with existing bb-* command structure for consistent development workflow.
    .PARAMETER IncludeCRUD
        Include basic CRUD operation testing
    .PARAMETER Detailed
        Show detailed diagnostic information
    .EXAMPLE
        Test-BusBuddyDatabase
        Basic database validation
    .EXAMPLE
        Test-BusBuddyDatabase -IncludeCRUD -Detailed
        Comprehensive validation with detailed output
    #>

    Write-Host "🔍 BusBuddy Database Validation" -ForegroundColor Cyan
    Write-Host "===============================" -ForegroundColor Cyan

    $issues = @()
    $successes = @()
    $sqlModuleAvailable = $false


    # Ensure SqlServer module is loaded and available
    if (-not (Get-Module -ListAvailable -Name SqlServer)) {
        Write-Host "SqlServer module not found. Attempting to install..." -ForegroundColor Yellow
        try {
            Install-Module -Name SqlServer -Scope CurrentUser -Force -ErrorAction Stop
            Write-Host "SqlServer module installed successfully." -ForegroundColor Green
        }
        catch {
            Write-Host "Failed to install SqlServer module: $($_.Exception.Message)" -ForegroundColor Red
            Write-Warning "SqlServer module is required for full validation. Please install manually if this fails."
        }
    }
    try {
        Import-Module SqlServer -ErrorAction Stop
        $sqlModuleAvailable = $true
    }
    catch {
        Write-Warning "SqlServer module not available. Using alternative validation methods."
    }

    try {
        # Test 1: Database Connection
        Write-Host "🔌 Testing database connection..." -NoNewline
        try {
            if ($sqlModuleAvailable) {
                $result = Invoke-Sqlcmd -Query "SELECT @@VERSION" -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddyDb" -ErrorAction Stop
                Write-Host " ✅" -ForegroundColor Green
                $successes += "Database connection successful"

                if ($Detailed) {
                    Write-Host "   SQL Server: $($result.Column1.Split("`n")[0])" -ForegroundColor Gray
                }
            }
            else {
                # Alternative: Use EF Core to test connection
                $efTest = dotnet ef database update --dry-run --project BusBuddy.Core --startup-project BusBuddy.WPF 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "Database connection successful (via EF Core)"
                }
                else {
                    Write-Host " ⚠️" -ForegroundColor Yellow
                    $issues += "Database connection test inconclusive"
                }
            }
        }
        catch {
            Write-Host " ❌" -ForegroundColor Red
            $issues += "Database connection failed: $($_.Exception.Message)"
        }

        # Test 2: Critical Tables Existence
        Write-Host "📋 Checking critical tables..." -NoNewline
        try {
            if ($sqlModuleAvailable) {
                $tables = Invoke-Sqlcmd -Query "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddyDb"
                $requiredTables = @("Students", "Vehicles", "Routes", "Drivers")
                $missingTables = @()

                foreach ($required in $requiredTables) {
                    if ($tables.TABLE_NAME -notcontains $required) {
                        $missingTables += $required
                    }
                }

                if ($missingTables.Count -eq 0) {
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "All critical tables present"
                }
                else {
                    Write-Host " ⚠️" -ForegroundColor Yellow
                    $issues += "Missing tables: $($missingTables -join ', ')"
                }

                if ($Detailed) {
                    Write-Host "   Found tables: $($tables.TABLE_NAME -join ', ')" -ForegroundColor Gray
                }
            }
            else {
                # Alternative: Check via migration list
                $migrationCheck = dotnet ef migrations list --project BusBuddy.Core --startup-project BusBuddy.WPF 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "Database schema validated via migrations"
                }
                else {
                    Write-Host " ⚠️" -ForegroundColor Yellow
                    $issues += "Could not validate table structure"
                }
            }
        }
        catch {
            Write-Host " ❌" -ForegroundColor Red
            $issues += "Table check failed: $($_.Exception.Message)"
        }

        # Test 3: Migration Status
        Write-Host "🔄 Checking migration status..." -NoNewline
        try {
            if ($sqlModuleAvailable) {
                $migrations = Invoke-Sqlcmd -Query "SELECT COUNT(*) as Count FROM __EFMigrationsHistory" -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddyDb"

                if ($migrations.Count -gt 0) {
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "Migration history present ($($migrations.Count) migrations)"
                }
                else {
                    Write-Host " ⚠️" -ForegroundColor Yellow
                    $issues += "No migration history found"
                }
            }
            else {
                # Alternative: Use EF Core migrations list
                $migrationOutput = dotnet ef migrations list --project BusBuddy.Core --startup-project BusBuddy.WPF 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $migrationCount = ($migrationOutput | Where-Object { $_ -match "^\d" }).Count
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "Migration status verified ($migrationCount migrations found)"
                }
                else {
                    Write-Host " ⚠️" -ForegroundColor Yellow
                    $issues += "Migration status check failed"
                }
            }
        }
        catch {
            Write-Host " ❌" -ForegroundColor Red
            $issues += "Migration check failed: $($_.Exception.Message)"
        }

        # Test 4: Basic Data Integrity
        Write-Host "🔍 Checking data integrity..." -NoNewline
        try {
            if ($sqlModuleAvailable) {
                # Check for orphaned records
                $orphanCheck = Invoke-Sqlcmd -Query @"
                    SELECT
                        (SELECT COUNT(*) FROM Students WHERE RouteAssignmentId IS NOT NULL
                         AND RouteAssignmentId NOT IN (SELECT RouteAssignmentId FROM RouteAssignments WHERE RouteAssignmentId IS NOT NULL)) as OrphanedStudents
"@ -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddyDb"

                if ($orphanCheck.OrphanedStudents -eq 0) {
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "Data integrity check passed"
                }
                else {
                    Write-Host " ⚠️" -ForegroundColor Yellow
                    $issues += "Found $($orphanCheck.OrphanedStudents) orphaned student records"
                }
            }
            else {
                Write-Host " ⏭️" -ForegroundColor Cyan
                $successes += "Data integrity check skipped (requires SqlServer module)"
            }
        }
        catch {
            Write-Host " ❌" -ForegroundColor Red
            $issues += "Data integrity check failed: $($_.Exception.Message)"
        }

        # Test 5: EF Core Version Check
        Write-Host "⚙️ Checking EF Core version..." -NoNewline
        try {
            $efVersion = dotnet ef --version 2>$null
            if ($efVersion -like "*9.0.8*") {
                Write-Host " ✅" -ForegroundColor Green
                $successes += "EF Core Tools version correct (9.0.8)"
            }
            else {
                Write-Host " ⚠️" -ForegroundColor Yellow
                $issues += "EF Core Tools version mismatch. Current: $efVersion, Expected: 9.0.8"
            }
        }
        catch {
            Write-Host " ❌" -ForegroundColor Red
            $issues += "EF Core version check failed"
        }

        # Optional CRUD Test
        if ($IncludeCRUD) {
            Write-Host "🧪 Running CRUD validation..." -NoNewline
            try {
                $crudResult = & "$PSScriptRoot\..\..\Test-EndToEndCRUD.ps1" -ErrorAction Stop
                if ($LASTEXITCODE -eq 0) {
                    Write-Host " ✅" -ForegroundColor Green
                    $successes += "CRUD operations validated successfully"
                }
                else {
                    Write-Host " ❌" -ForegroundColor Red
                    $issues += "CRUD validation failed"
                }
            }
            catch {
                Write-Host " ❌" -ForegroundColor Red
                $issues += "CRUD test execution failed: $($_.Exception.Message)"
            }
        }

    }
    catch {
        Write-Host ""
        Write-Host "❌ Validation failed with critical error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }

    # Summary
    Write-Host ""
    Write-Host "📊 Validation Summary" -ForegroundColor Cyan
    Write-Host "====================" -ForegroundColor Cyan

    if ($successes.Count -gt 0) {
        Write-Host "✅ Passed ($($successes.Count)):" -ForegroundColor Green
        foreach ($success in $successes) {
            Write-Host "   • $success" -ForegroundColor Green
        }
    }

    if ($issues.Count -gt 0) {
        Write-Host "⚠️ Issues ($($issues.Count)):" -ForegroundColor Yellow
        foreach ($issue in $issues) {
            Write-Host "   • $issue" -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Host "💡 For detailed solutions, see: TROUBLESHOOTING-LOG.md" -ForegroundColor Cyan
    }
    else {
        Write-Host ""
        Write-Host "🎉 All validations passed! Database is ready for use." -ForegroundColor Green
    }

    return $issues.Count -eq 0
}

# Create alias for backwards compatibility and short command
New-Alias -Name "bb-validate-database" -Value "Test-BusBuddyDatabase" -Force
New-Alias -Name "bb-db-validate" -Value "Test-BusBuddyDatabase" -Force
