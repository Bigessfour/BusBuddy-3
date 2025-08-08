# BusBuddy UAT Test Automation Script
# Automates key UAT test scenarios for student entry and route design

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("All", "StudentManagement", "RouteDesign", "Integration")]
    [string]$TestSuite = "All",

    [Parameter(Mandatory=$false)]
    [string]$Environment = "Staging",

    [Parameter(Mandatory=$false)]
    [switch]$GenerateReport = $true
)

Write-Information "🧪 BusBuddy UAT Test Automation" -InformationAction Continue
Write-Information "📊 Test Suite: $TestSuite" -InformationAction Continue
Write-Information "🌍 Environment: $Environment" -InformationAction Continue

$testResults = @()
$startTime = Get-Date

function Test-StudentManagement {
    Write-Information "🎒 Testing Student Management Functionality" -InformationAction Continue

    $tests = @(
        @{ Name = "Application Startup"; Expected = "Dashboard loads successfully" },
        @{ Name = "Navigate to Students"; Expected = "Students view accessible" },
        @{ Name = "Student Data Display"; Expected = "Test students visible in grid" },
        @{ Name = "Add New Student"; Expected = "New student can be added with validation" },
        @{ Name = "Edit Student Details"; Expected = "Student information can be modified" },
        @{ Name = "Search Students"; Expected = "Search functionality works correctly" },
        @{ Name = "Student Data Persistence"; Expected = "Changes saved to database" }
    )

    $studentResults = @()
    foreach ($test in $tests) {
        $result = @{
            TestSuite = "StudentManagement"
            TestName = $test.Name
            Expected = $test.Expected
            Status = "Manual Verification Required"
            Timestamp = Get-Date
            Notes = "Automated validation not available for UI tests"
        }
        $studentResults += $result
        Write-Information "   ✓ $($test.Name): $($test.Expected)" -InformationAction Continue
    }

    return $studentResults
}

function Test-RouteDesign {
    Write-Information "🚌 Testing Route Design Functionality" -InformationAction Continue

    $tests = @(
        @{ Name = "Navigate to Routes"; Expected = "Routes view accessible" },
        @{ Name = "Route Data Display"; Expected = "Test routes visible in interface" },
        @{ Name = "Create New Route"; Expected = "New route can be created with stops" },
        @{ Name = "Assign Students to Route"; Expected = "Students can be assigned to routes" },
        @{ Name = "Modify Route Details"; Expected = "Route information can be updated" },
        @{ Name = "Route Optimization"; Expected = "Route suggestions work correctly" },
        @{ Name = "Route Data Persistence"; Expected = "Route changes saved to database" }
    )

    $routeResults = @()
    foreach ($test in $tests) {
        $result = @{
            TestSuite = "RouteDesign"
            TestName = $test.Name
            Expected = $test.Expected
            Status = "Manual Verification Required"
            Timestamp = Get-Date
            Notes = "Automated validation not available for UI tests"
        }
        $routeResults += $result
        Write-Information "   ✓ $($test.Name): $($test.Expected)" -InformationAction Continue
    }

    return $routeResults
}

function Test-Integration {
    Write-Information "🔗 Testing Integration Functionality" -InformationAction Continue

    $tests = @(
        @{ Name = "Database Connectivity"; Expected = "Azure SQL connection successful" },
        @{ Name = "Student-Route Assignment"; Expected = "Students properly assigned to routes" },
        @{ Name = "Data Consistency"; Expected = "Data remains consistent across operations" },
        @{ Name = "Performance"; Expected = "Response times under 3 seconds" },
        @{ Name = "Error Handling"; Expected = "Graceful error handling and recovery" },
        @{ Name = "Logging"; Expected = "Application Insights telemetry working" },
        @{ Name = "Security"; Expected = "Secure database connections maintained" }
    )

    $integrationResults = @()
    foreach ($test in $tests) {
        $result = @{
            TestSuite = "Integration"
            TestName = $test.Name
            Expected = $test.Expected
            Status = "Manual Verification Required"
            Timestamp = Get-Date
            Notes = "Some automated checks possible via health endpoints"
        }
        $integrationResults += $result
        Write-Information "   ✓ $($test.Name): $($test.Expected)" -InformationAction Continue
    }

    return $integrationResults
}

function Test-HealthChecks {
    Write-Information "🔍 Running Automated Health Checks" -InformationAction Continue

    try {
        # Run bb health check
        $healthOutput = & pwsh -Command "bbHealth" 2>&1
        $healthStatus = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }

        $healthResult = @{
            TestSuite = "HealthCheck"
            TestName = "System Health Validation"
            Expected = "All health checks pass"
            Status = $healthStatus
            Timestamp = Get-Date
            Notes = "bbHealth command execution: $healthStatus"
        }

        Write-Information "   ✓ Health Check: $healthStatus" -InformationAction Continue
        return @($healthResult)

    } catch {
        $errorResult = @{
            TestSuite = "HealthCheck"
            TestName = "System Health Validation"
            Expected = "All health checks pass"
            Status = "ERROR"
            Timestamp = Get-Date
            Notes = "Health check failed: $($_.Exception.Message)"
        }

        Write-Warning "   ⚠️ Health Check: ERROR - $($_.Exception.Message)"
        return @($errorResult)
    }
}

try {
    # Run health checks first
    $testResults += Test-HealthChecks

    # Run selected test suites
    switch ($TestSuite) {
        "All" {
            $testResults += Test-StudentManagement
            $testResults += Test-RouteDesign
            $testResults += Test-Integration
        }
        "StudentManagement" {
            $testResults += Test-StudentManagement
        }
        "RouteDesign" {
            $testResults += Test-RouteDesign
        }
        "Integration" {
            $testResults += Test-Integration
        }
    }

    $endTime = Get-Date
    $duration = $endTime - $startTime

    # Generate test report
    if ($GenerateReport) {
        Write-Information "📊 Generating UAT Test Report" -InformationAction Continue

        $reportData = @{
            TestSession = @{
                Environment = $Environment
                TestSuite = $TestSuite
                StartTime = $startTime
                EndTime = $endTime
                Duration = $duration.ToString("hh\:mm\:ss")
                TotalTests = $testResults.Count
            }
            TestResults = $testResults
            Summary = @{
                Passed = ($testResults | Where-Object { $_.Status -eq "PASS" }).Count
                Failed = ($testResults | Where-Object { $_.Status -eq "FAIL" }).Count
                ManualVerification = ($testResults | Where-Object { $_.Status -eq "Manual Verification Required" }).Count
                Errors = ($testResults | Where-Object { $_.Status -eq "ERROR" }).Count
            }
        }

        $reportFileName = "UAT-Test-Report-$Environment-$TestSuite-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
        $reportData | ConvertTo-Json -Depth 5 | Out-File $reportFileName -Encoding UTF8

        Write-Information "📋 UAT Test Report Summary" -InformationAction Continue
        Write-Information "═══════════════════════════════════════" -InformationAction Continue
        Write-Information "🌍 Environment: $Environment" -InformationAction Continue
        Write-Information "📊 Test Suite: $TestSuite" -InformationAction Continue
        Write-Information "⏱️ Duration: $($duration.ToString('hh\:mm\:ss'))" -InformationAction Continue
        Write-Information "📈 Total Tests: $($testResults.Count)" -InformationAction Continue
        Write-Information "✅ Automated Passed: $($reportData.Summary.Passed)" -InformationAction Continue
        Write-Information "❌ Automated Failed: $($reportData.Summary.Failed)" -InformationAction Continue
        Write-Information "⚠️ Errors: $($reportData.Summary.Errors)" -InformationAction Continue
        Write-Information "🧪 Manual Verification: $($reportData.Summary.ManualVerification)" -InformationAction Continue
        Write-Information "📁 Report File: $reportFileName" -InformationAction Continue
        Write-Information "═══════════════════════════════════════" -InformationAction Continue

        Write-Information "🎯 Next Steps for UAT:" -InformationAction Continue
        Write-Information "1. Review automated test results" -InformationAction Continue
        Write-Information "2. Execute manual verification steps with test users" -InformationAction Continue
        Write-Information "3. Collect user feedback on usability and performance" -InformationAction Continue
        Write-Information "4. Document any issues found during testing" -InformationAction Continue
        Write-Information "5. Validate all critical MVP functionality works as expected" -InformationAction Continue
    }

    Write-Information "🎉 UAT test automation completed successfully!" -InformationAction Continue

} catch {
    Write-Error "❌ UAT test automation failed: $($_.Exception.Message)"
    exit 1
}
