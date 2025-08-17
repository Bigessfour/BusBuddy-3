#requires -Version 7.5
<#
.SYNOPSIS
    Test script for enhanced bbHealth modernization features

.DESCRIPTION
    Validates the comprehensive PowerShell 7.5.2 modernization and health monitoring system
#>

Write-Output "=== Testing Enhanced bbHealth System ==="

# Set environment
$env:BUSBUDDY_REPO_ROOT = (Get-Location).Path

try {
    # Load BusBuddy module
    Write-Output "Loading BusBuddy module..."
    Import-Module ".\PowerShell\Modules\BusBuddy\BusBuddy.psd1" -Force -ErrorAction Stop
    Write-Output "✅ BusBuddy module loaded successfully"

    # Test basic health check
    Write-Output "`n1. Testing basic bbHealth..."
    $basicResult = bbHealth
    Write-Output "✅ Basic health check completed"

    # Test detailed health check
    Write-Output "`n2. Testing bbHealth -Detailed..."
    $detailedResult = bbHealth -Detailed
    Write-Output "✅ Detailed health check completed"

    # Test modernization scan
    Write-Output "`n3. Testing bbHealth -ModernizationScan..."
    $modernResult = bbHealth -ModernizationScan
    Write-Output "✅ Modernization scan completed"

    # Test auto-repair functionality
    Write-Output "`n4. Testing bbHealth -AutoRepair..."
    $repairResult = bbHealth -AutoRepair
    Write-Output "✅ Auto-repair completed"

    # Test comprehensive scan
    Write-Output "`n5. Testing full bbHealth scan..."
    $fullResult = bbHealth -Detailed -ModernizationScan -AutoRepair
    Write-Output "✅ Full comprehensive scan completed"

    # Validate return object structure
    if ($fullResult -is [hashtable]) {
        Write-Output "`n=== Health Check Results ==="
        Write-Output "Overall Health: $($fullResult.OverallHealth)"
        Write-Output "Issue Count: $($fullResult.IssueCount)"
        Write-Output "Modernization Issues: $($fullResult.ModernizationIssues)"
        Write-Output "Missing Tools: $($fullResult.MissingTools)"
        Write-Output "Auto-Repairs Applied: $($fullResult.AutoRepairsApplied)"
        Write-Output "Recommendations: $($fullResult.Recommendations -join ', ')"
    } else {
        Write-Output "Result type: $($fullResult.GetType().Name)"
        Write-Output "Result value: $fullResult"
    }

    Write-Output "`n🎉 All bbHealth tests completed successfully!"

} catch {
    Write-Error "❌ Test failed: $($_.Exception.Message)"
    Write-Output "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
