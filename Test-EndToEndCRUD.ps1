# Test-EndToEndCRUD.ps1 - Comprehensive CRUD and Foreign Key Testing
# Purpose: Validate database operations and detect subtle regressions

#Requires -Version 7.5

[CmdletBinding()]
param(
    [string]$Environment = "Development",
    [switch]$IncludeForeignKeyTests,
    [switch]$GenerateReport,
    [string]$ReportPath = ".\TestResults\CRUD-Test-Report.json"
)

# Script metadata for better integration
$Script:ScriptInfo = @{
    Name = "Test-EndToEndCRUD"
    Version = "1.0.0"
    Author = "BusBuddy Development Team"
    Description = "Comprehensive database CRUD validation and testing framework"
    RequiredModules = @("SqlServer")
}

# Import required modules with fallback
try {
    Import-Module SqlServer -ErrorAction SilentlyContinue
    $SqlModuleAvailable = $true
} catch {
    Write-Warning "SqlServer module not available. Some tests will use alternative methods."
    $SqlModuleAvailable = $false
}

# Test configuration
$TestConfig = @{
    ConnectionString = "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddyDb;Integrated Security=True"
    TestStudentPrefix = "CRUD_TEST"
    TestRoutePrefix = "ROUTE_TEST"
    CleanupAfterTests = $true
    MaxTestRecords = 10
}

# Test results tracking
$TestResults = @{
    StartTime = Get-Date
    Environment = $Environment
    Tests = @()
    Summary = @{
        Total = 0
        Passed = 0
        Failed = 0
        Skipped = 0
    }
}

# Enhanced logging function
function Write-TestLog {
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error", "Debug")]
        [string]$Level = "Info",
        [switch]$NoNewline
    )

    $timestamp = Get-Date -Format "HH:mm:ss"
    $color = switch ($Level) {
        "Success" { "Green" }
        "Warning" { "Yellow" }
        "Error" { "Red" }
        "Debug" { "Gray" }
        default { "White" }
    }

    $prefix = switch ($Level) {
        "Success" { "✅" }
        "Warning" { "⚠️" }
        "Error" { "❌" }
        "Debug" { "🔍" }
        default { "ℹ️" }
    }

    if ($NoNewline) {
        Write-Host "$prefix [$timestamp] $Message" -ForegroundColor $color -NoNewline
    } else {
        Write-Host "$prefix [$timestamp] $Message" -ForegroundColor $color
    }
}

# Execute SQL command with proper error handling
function Invoke-TestSQL {
    param(
        [string]$Query,
        [hashtable]$Parameters = @{},
        [switch]$ExpectRows
    )

    try {
        Write-TestLog "Executing SQL: $($Query.Substring(0, [Math]::Min(50, $Query.Length)))" -Level Debug

        $result = Invoke-Sqlcmd -Query $Query -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddyDb" -ErrorAction Stop

        if ($ExpectRows -and (-not $result -or $result.Count -eq 0)) {
            throw "Query executed successfully but returned no rows"
        }

        return $result
    }
    catch {
        Write-TestLog "SQL Error: $($_.Exception.Message)" -Level Error
        throw
    }
}

# Test framework functions
function Start-Test {
    param([string]$TestName, [string]$Description = "")

    Write-TestLog "🧪 Starting Test: $TestName" -Level Info
    if ($Description) {
        Write-TestLog "   Description: $Description" -Level Debug
    }

    $test = @{
        Name = $TestName
        Description = $Description
        StartTime = Get-Date
        Status = "Running"
        Error = $null
        Details = @()
    }

    $TestResults.Tests += $test
    $TestResults.Summary.Total++

    return $TestResults.Tests.Count - 1  # Return index for updates
}

function Complete-Test {
    param(
        [int]$TestIndex,
        [bool]$Success,
        [string]$ErrorMessage = "",
        [array]$Details = @()
    )

    $test = $TestResults.Tests[$TestIndex]
    $test.EndTime = Get-Date
    $test.Duration = ($test.EndTime - $test.StartTime).TotalSeconds
    $test.Details = $Details

    if ($Success) {
        $test.Status = "Passed"
        $TestResults.Summary.Passed++
        Write-TestLog "✅ Test Passed: $($test.Name)" -Level Success
    } else {
        $test.Status = "Failed"
        $test.Error = $ErrorMessage
        $TestResults.Summary.Failed++
        Write-TestLog "❌ Test Failed: $($test.Name) - $ErrorMessage" -Level Error
    }
}

# Database connectivity test
function Test-DatabaseConnectivity {
    $testIndex = Start-Test "Database Connectivity" "Verify connection to BusBuddyDb"

    try {
        $result = Invoke-TestSQL "SELECT @@VERSION as Version, DB_NAME() as DatabaseName"

        $details = @(
            "SQL Server Version: $($result.Version)",
            "Connected Database: $($result.DatabaseName)"
        )

        Complete-Test $testIndex $true -Details $details
        return $true
    }
    catch {
        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Table existence validation
function Test-TableExistence {
    $testIndex = Start-Test "Table Existence" "Verify all required tables exist"

    try {
        $requiredTables = @("Students", "Vehicles", "Routes", "Drivers", "RouteAssignments", "Families")
        $existingTables = Invoke-TestSQL @"
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            AND TABLE_SCHEMA = 'dbo'
"@

        $missing = @()
        $found = @()

        foreach ($table in $requiredTables) {
            if ($existingTables.TABLE_NAME -contains $table) {
                $found += $table
            } else {
                $missing += $table
            }
        }

        $details = @(
            "Found Tables: $($found -join ', ')",
            "Missing Tables: $(if($missing) { $missing -join ', ' } else { 'None' })"
        )

        if ($missing.Count -eq 0) {
            Complete-Test $testIndex $true -Details $details
            return $true
        } else {
            Complete-Test $testIndex $false "Missing required tables: $($missing -join ', ')" -Details $details
            return $false
        }
    }
    catch {
        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Student CRUD Operations Test
function Test-StudentCRUD {
    $testIndex = Start-Test "Student CRUD Operations" "Create, Read, Update, Delete student record"

    try {
        $testStudentId = $null
        $testNumber = "$($TestConfig.TestStudentPrefix)_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

        # CREATE Test
        Write-TestLog "Testing CREATE operation..." -Level Debug
        $createResult = Invoke-TestSQL @"
            INSERT INTO Students (StudentNumber, StudentName, Grade, School, CreatedDate, CreatedBy)
            OUTPUT INSERTED.StudentId
            VALUES ('$testNumber', 'Test Student CRUD', '10', 'Test School', GETUTCDATE(), 'CRUD_TEST')
"@

        $testStudentId = $createResult.StudentId
        if (-not $testStudentId) {
            throw "CREATE failed: No StudentId returned"
        }

        # READ Test
        Write-TestLog "Testing READ operation..." -Level Debug
        $readResult = Invoke-TestSQL "SELECT * FROM Students WHERE StudentId = $testStudentId" -ExpectRows

        if ($readResult.StudentNumber -ne $testNumber) {
            throw "READ failed: Retrieved student number doesn't match"
        }

        # UPDATE Test
        Write-TestLog "Testing UPDATE operation..." -Level Debug
        $newGrade = "11"
        Invoke-TestSQL "UPDATE Students SET Grade = '$newGrade', UpdatedDate = GETUTCDATE(), UpdatedBy = 'CRUD_TEST_UPDATE' WHERE StudentId = $testStudentId"

        $updateVerify = Invoke-TestSQL "SELECT Grade FROM Students WHERE StudentId = $testStudentId" -ExpectRows
        if ($updateVerify.Grade -ne $newGrade) {
            throw "UPDATE failed: Grade not updated correctly"
        }

        # DELETE Test
        Write-TestLog "Testing DELETE operation..." -Level Debug
        Invoke-TestSQL "DELETE FROM Students WHERE StudentId = $testStudentId"

        $deleteVerify = Invoke-TestSQL "SELECT COUNT(*) as Count FROM Students WHERE StudentId = $testStudentId"
        if ($deleteVerify.Count -ne 0) {
            throw "DELETE failed: Student record still exists"
        }

        $details = @(
            "Test Student ID: $testStudentId",
            "Student Number: $testNumber",
            "CREATE: Success",
            "READ: Success",
            "UPDATE: Success (Grade: 10 → 11)",
            "DELETE: Success"
        )

        Complete-Test $testIndex $true -Details $details
        return $true
    }
    catch {
        # Cleanup on failure
        if ($testStudentId) {
            try {
                Invoke-TestSQL "DELETE FROM Students WHERE StudentId = $testStudentId"
                Write-TestLog "Cleanup: Removed test student $testStudentId" -Level Debug
            }
            catch {
                Write-TestLog "Cleanup failed for student $testStudentId" -Level Warning
            }
        }

        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Foreign Key Constraint Tests
function Test-ForeignKeyConstraints {
    if (-not $IncludeForeignKeyTests) {
        $testIndex = Start-Test "Foreign Key Constraints" "Skipped - Use -IncludeForeignKeyTests to enable"
        $TestResults.Tests[$testIndex].Status = "Skipped"
        $TestResults.Summary.Skipped++
        return $true
    }

    $testIndex = Start-Test "Foreign Key Constraints" "Validate referential integrity"

    try {
        # Test 1: Valid FK insertion
        Write-TestLog "Testing valid FK insertion..." -Level Debug

        # Create a test vehicle first
        $vehicleResult = Invoke-TestSQL @"
            INSERT INTO Vehicles (BusNumber, Make, Model, Year, Status, CreatedDate, CreatedBy)
            OUTPUT INSERTED.VehicleId
            VALUES ('FK_TEST_001', 'Test Make', 'Test Model', 2020, 'Active', GETUTCDATE(), 'FK_TEST')
"@

        $testVehicleId = $vehicleResult.VehicleId

        # Create a test route with FK reference
        $routeResult = Invoke-TestSQL @"
            INSERT INTO Routes (RouteName, AMVehicleID, School, CreatedDate, CreatedBy)
            OUTPUT INSERTED.RouteID
            VALUES ('FK_TEST_ROUTE', $testVehicleId, 'Test School', GETUTCDATE(), 'FK_TEST')
"@

        $testRouteId = $routeResult.RouteID

        # Test 2: Invalid FK insertion (should fail)
        Write-TestLog "Testing invalid FK insertion (should fail)..." -Level Debug

        $invalidFKTest = $false
        try {
            Invoke-TestSQL @"
                INSERT INTO Routes (RouteName, AMVehicleID, School, CreatedDate, CreatedBy)
                VALUES ('FK_INVALID_TEST', 99999, 'Test School', GETUTCDATE(), 'FK_TEST')
"@
            # If we get here, the FK constraint is not working
            $invalidFKTest = $false
        }
        catch {
            # This is expected - FK constraint should prevent this
            $invalidFKTest = $true
        }

        # Test 3: Cascade behavior
        Write-TestLog "Testing cascade behavior..." -Level Debug

        # Try to delete vehicle that's referenced by route (should fail or cascade)
        $cascadeTest = $false
        try {
            Invoke-TestSQL "DELETE FROM Vehicles WHERE VehicleId = $testVehicleId"
            # Check if route still exists
            $routeCheck = Invoke-TestSQL "SELECT COUNT(*) as Count FROM Routes WHERE RouteID = $testRouteId"
            if ($routeCheck.Count -eq 0) {
                $cascadeTest = $true  # Cascade delete worked
            } else {
                # Check if FK was set to NULL (also valid)
                $fkCheck = Invoke-TestSQL "SELECT AMVehicleID FROM Routes WHERE RouteID = $testRouteId"
                if ($fkCheck.AMVehicleID -eq $null) {
                    $cascadeTest = $true  # SET NULL worked
                }
            }
        }
        catch {
            # FK constraint prevented deletion (also valid)
            $cascadeTest = $true
        }

        # Cleanup
        try {
            Invoke-TestSQL "DELETE FROM Routes WHERE RouteID = $testRouteId"
            Invoke-TestSQL "DELETE FROM Vehicles WHERE VehicleId = $testVehicleId"
        }
        catch {
            Write-TestLog "FK test cleanup encountered issues" -Level Warning
        }

        $details = @(
            "Valid FK Insertion: Success",
            "Invalid FK Rejection: $(if($invalidFKTest) { 'Success' } else { 'Failed - FK constraint not enforced' })",
            "Cascade Behavior: $(if($cascadeTest) { 'Success' } else { 'Failed - Cascade not working' })",
            "Test Vehicle ID: $testVehicleId",
            "Test Route ID: $testRouteId"
        )

        $success = $invalidFKTest -and $cascadeTest
        Complete-Test $testIndex $success $(if(-not $success) { "FK constraint validation failed" } else { "" }) -Details $details

        return $success
    }
    catch {
        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Migration Status Validation
function Test-MigrationStatus {
    $testIndex = Start-Test "Migration Status" "Verify migration history consistency"

    try {
        # Check if migration history table exists
        $historyExists = Invoke-TestSQL @"
            SELECT COUNT(*) as Count
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = '__EFMigrationsHistory'
"@

        if ($historyExists.Count -eq 0) {
            Complete-Test $testIndex $false "Migration history table does not exist"
            return $false
        }

        # Get migration history
        $migrations = Invoke-TestSQL "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId"

        # Get expected EF version from project
        $efVersion = "9.0.8"  # Current expected version

        $details = @(
            "Migration History Table: Exists",
            "Applied Migrations: $($migrations.Count)",
            "Latest Migration: $($migrations[-1].MigrationId)",
            "EF Product Version: $($migrations[-1].ProductVersion)",
            "Expected EF Version: $efVersion"
        )

        # Check for version consistency
        $versionMismatch = $migrations | Where-Object { $_.ProductVersion -ne $efVersion }
        if ($versionMismatch) {
            $details += "Version Mismatches: $($versionMismatch.Count)"
            Complete-Test $testIndex $false "EF version mismatch detected" -Details $details
            return $false
        }

        Complete-Test $testIndex $true -Details $details
        return $true
    }
    catch {
        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Data Integrity Check
function Test-DataIntegrity {
    $testIndex = Start-Test "Data Integrity" "Check for orphaned records and constraint violations"

    try {
        $issues = @()

        # Check for orphaned students (invalid RouteAssignmentId)
        $orphanedStudents = Invoke-TestSQL @"
            SELECT COUNT(*) as Count
            FROM Students s
            WHERE s.RouteAssignmentId IS NOT NULL
            AND s.RouteAssignmentId NOT IN (SELECT RouteAssignmentId FROM RouteAssignments)
"@

        if ($orphanedStudents.Count -gt 0) {
            $issues += "Orphaned Students with invalid RouteAssignmentId: $($orphanedStudents.Count)"
        }

        # Check for orphaned route assignments (invalid RouteId or VehicleId)
        $orphanedRouteAssignments = Invoke-TestSQL @"
            SELECT COUNT(*) as Count
            FROM RouteAssignments ra
            WHERE ra.RouteId NOT IN (SELECT RouteID FROM Routes)
            OR ra.VehicleId NOT IN (SELECT VehicleId FROM Vehicles)
"@

        if ($orphanedRouteAssignments.Count -gt 0) {
            $issues += "Orphaned Route Assignments: $($orphanedRouteAssignments.Count)"
        }

        # Check for duplicate student numbers
        $duplicateStudents = Invoke-TestSQL @"
            SELECT StudentNumber, COUNT(*) as Count
            FROM Students
            GROUP BY StudentNumber
            HAVING COUNT(*) > 1
"@

        if ($duplicateStudents.Count -gt 0) {
            $issues += "Duplicate Student Numbers: $($duplicateStudents.Count)"
        }

        # Check for duplicate VIN numbers
        $duplicateVINs = Invoke-TestSQL @"
            SELECT VIN, COUNT(*) as Count
            FROM Vehicles
            WHERE VIN IS NOT NULL
            GROUP BY VIN
            HAVING COUNT(*) > 1
"@

        if ($duplicateVINs.Count -gt 0) {
            $issues += "Duplicate VIN Numbers: $($duplicateVINs.Count)"
        }

        $details = if ($issues.Count -eq 0) {
            @("No data integrity issues found")
        } else {
            $issues
        }

        $success = $issues.Count -eq 0
        Complete-Test $testIndex $success $(if(-not $success) { "Data integrity issues detected" } else { "" }) -Details $details

        return $success
    }
    catch {
        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Performance Baseline Test
function Test-PerformanceBaseline {
    $testIndex = Start-Test "Performance Baseline" "Measure basic query performance"

    try {
        $perfResults = @()

        # Test 1: Student count query
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $studentCount = Invoke-TestSQL "SELECT COUNT(*) as Count FROM Students"
        $stopwatch.Stop()
        $perfResults += "Student Count ($($studentCount.Count) records): $($stopwatch.ElapsedMilliseconds)ms"

        # Test 2: Complex join query
        $stopwatch.Restart()
        $complexQuery = Invoke-TestSQL @"
            SELECT
                s.StudentName,
                r.RouteName,
                v.BusNumber
            FROM Students s
            LEFT JOIN RouteAssignments ra ON s.RouteAssignmentId = ra.RouteAssignmentId
            LEFT JOIN Routes r ON ra.RouteId = r.RouteID
            LEFT JOIN Vehicles v ON ra.VehicleId = v.VehicleId
"@
        $stopwatch.Stop()
        $perfResults += "Complex Join Query ($($complexQuery.Count) rows): $($stopwatch.ElapsedMilliseconds)ms"

        # Test 3: Index usage check
        $stopwatch.Restart()
        $indexQuery = Invoke-TestSQL "SELECT * FROM Students WHERE StudentNumber = 'PERF_TEST_001'"
        $stopwatch.Stop()
        $perfResults += "Indexed Lookup: $($stopwatch.ElapsedMilliseconds)ms"

        Complete-Test $testIndex $true -Details $perfResults
        return $true
    }
    catch {
        Complete-Test $testIndex $false $_.Exception.Message
        return $false
    }
}

# Generate comprehensive test report
function Export-TestReport {
    if (-not $GenerateReport) {
        return
    }

    Write-TestLog "Generating test report..." -Level Info

    try {
        $TestResults.EndTime = Get-Date
        $TestResults.TotalDuration = ($TestResults.EndTime - $TestResults.StartTime).TotalSeconds

        # Ensure directory exists
        $reportDir = Split-Path $ReportPath -Parent
        if (!(Test-Path $reportDir)) {
            New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
        }

        # Export to JSON
        $TestResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $ReportPath -Encoding UTF8

        Write-TestLog "Test report saved to: $ReportPath" -Level Success

        # Generate summary
        $summary = @"
🧪 End-to-End CRUD Test Summary
========================================
Environment: $($TestResults.Environment)
Test Duration: $([math]::Round($TestResults.TotalDuration, 2)) seconds
Total Tests: $($TestResults.Summary.Total)
✅ Passed: $($TestResults.Summary.Passed)
❌ Failed: $($TestResults.Summary.Failed)
⏭️ Skipped: $($TestResults.Summary.Skipped)

Success Rate: $([math]::Round(($TestResults.Summary.Passed / $TestResults.Summary.Total) * 100, 1))%
"@

        Write-Host $summary -ForegroundColor Cyan

    }
    catch {
        Write-TestLog "Failed to generate test report: $($_.Exception.Message)" -Level Error
    }
}

# Main execution function
function Invoke-EndToEndTests {
    Write-TestLog "🚀 Starting End-to-End CRUD Testing" -Level Info
    Write-TestLog "Environment: $Environment" -Level Info
    Write-TestLog "Include FK Tests: $IncludeForeignKeyTests" -Level Info

    $allPassed = $true

    # Execute test suite
    $allPassed = (Test-DatabaseConnectivity) -and $allPassed
    $allPassed = (Test-TableExistence) -and $allPassed
    $allPassed = (Test-MigrationStatus) -and $allPassed
    $allPassed = (Test-DataIntegrity) -and $allPassed
    $allPassed = (Test-StudentCRUD) -and $allPassed
    $allPassed = (Test-ForeignKeyConstraints) -and $allPassed
    $allPassed = (Test-PerformanceBaseline) -and $allPassed

    # Generate report
    Export-TestReport

    # Final summary
    if ($allPassed) {
        Write-TestLog "🎉 All critical tests passed! Database is ready for production use." -Level Success
        exit 0
    } else {
        Write-TestLog "⚠️ Some tests failed. Review the results before proceeding to production." -Level Warning
        exit 1
    }
}

# Execute the test suite
Invoke-EndToEndTests
