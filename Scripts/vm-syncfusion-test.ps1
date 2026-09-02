#Requires -Version 5.1
$ErrorActionPreference = "Continue"
[Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", $null, "User")
Set-Location "C:\dev\BusBuddy-3"

Write-Host "=== Verify ==="
& "C:\dev\BusBuddy-3\Scripts\Verify-SyncfusionLicense.ps1"

Write-Host "`n=== Build ==="
dotnet build BusBuddy.WPF\BusBuddy.WPF.csproj -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Launch (8s) ==="
$p = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", "BusBuddy.WPF\BusBuddy.WPF.csproj", "-c", "Debug", "--no-build") `
    -WorkingDirectory "C:\dev\BusBuddy-3" `
    -PassThru -WindowStyle Normal
Start-Sleep -Seconds 8
if (-not $p.HasExited) {
    Write-Host "App PID $($p.Id) still running (expected)"
}

Write-Host "`n=== Startup diagnostic ==="
$diag = "C:\dev\logs\busbuddy-license-startup.txt"
if (Test-Path $diag) {
    Get-Content $diag
} else {
    Write-Host "(not found at $diag)"
}
