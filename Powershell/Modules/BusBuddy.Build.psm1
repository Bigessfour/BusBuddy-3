# BusBuddy.Build.psm1
# Build and clean operations for BusBuddy
# Microsoft PowerShell Module Guidelines compliant

function Invoke-BusBuddyBuild {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Building BusBuddy solution..." -InformationAction Continue
        dotnet build "BusBuddy.sln" --configuration Debug
        Write-Output "Build completed successfully."
    } catch {
        Write-Error "Build failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

function Invoke-BusBuddyClean {
    [CmdletBinding()]
    param()
    try {
        Write-Information "Cleaning BusBuddy solution..." -InformationAction Continue
        dotnet clean "BusBuddy.sln"
        Write-Output "Clean completed successfully."
    } catch {
        Write-Error "Clean failed: $($_.Exception.Message)" -Category OperationStopped
    }
}

Export-ModuleMember -Function Invoke-BusBuddyBuild, Invoke-BusBuddyClean
