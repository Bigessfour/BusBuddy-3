# BusBuddy.Database.psm1
# Database operations for BusBuddy
# Microsoft PowerShell Module Guidelines compliant

function Invoke-BusBuddyDbTest {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Testing BusBuddy database connection..." -InformationAction Continue
        bb-sql-test
        Write-Output "Database test completed."
    } catch {
        Write-Error "Database test failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

Export-ModuleMember -Function Invoke-BusBuddyDbTest
