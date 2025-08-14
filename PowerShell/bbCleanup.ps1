param(
    [string]$LogsDir = "logs",
    [string]$SummaryOut = "logs/log-summary.json"
)

# Microsoft PowerShell guidelines: use Write-Output/Write-Information
Write-Information "Starting BusBuddy log cleanup" -InformationAction Continue

# Build the project if needed so types are available
if (-not (Test-Path "$PSScriptRoot\..\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.dll")) {
    dotnet build "$PSScriptRoot\..\BusBuddy.sln" | Out-Null
}

# Load assembly to access LogLifecycleManager
Add-Type -Path "$PSScriptRoot\..\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.dll"

# Create and run cleanup
$manager = New-Object BusBuddy.WPF.Utilities.LogLifecycleManager($LogsDir)
$manager.PerformIntelligentCleanup()

# Export summary for CI artifacts
$null = $manager.ExportLogSummary($SummaryOut)
Write-Information "Log cleanup finished. Summary at $SummaryOut" -InformationAction Continue
