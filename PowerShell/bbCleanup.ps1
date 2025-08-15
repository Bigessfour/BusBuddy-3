param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$LogsDir = "logs",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$SummaryOut = "logs/log-summary.json"
)

# Microsoft PowerShell guidelines: prefer Write-Information over Write-Host
Write-Information "Starting BusBuddy log cleanup" -InformationAction Continue

# Ensure logs directory exists
try { if (-not (Test-Path -LiteralPath $LogsDir)) { New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null } } catch {}

# Build the WPF project if the assembly is missing (so Add-Type succeeds)
$wpfDllPath = Join-Path -Path "$PSScriptRoot\..\BusBuddy.WPF\bin\Debug\net9.0-windows" -ChildPath "BusBuddy.WPF.dll"
if (-not (Test-Path -LiteralPath $wpfDllPath)) {
    Write-Information "Building solution to load WPF utilities..." -InformationAction Continue
    & dotnet build (Join-Path $PSScriptRoot '..\BusBuddy.sln') -v m | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet build failed with exit code $LASTEXITCODE" -ErrorAction Stop
    }
}

# Load assembly to access LogLifecycleManager
Add-Type -Path $wpfDllPath -ErrorAction Stop

try {
    # Create and run cleanup
    $manager = New-Object BusBuddy.WPF.Utilities.LogLifecycleManager($LogsDir)
    $manager.PerformIntelligentCleanup()

    # Export summary for CI artifacts
    $null = $manager.ExportLogSummary($SummaryOut)
    Write-Information "Log cleanup finished. Summary at $SummaryOut" -InformationAction Continue
}
catch {
    Write-Error ("Log cleanup failed: {0}" -f $_.Exception.Message) -ErrorAction Stop
}
