#Requires -Version 7.5

# Test BusBuddy module loading in isolation
Write-Host "Testing BusBuddy module loading..." -ForegroundColor Cyan

# Remove any existing instances
Remove-Module BusBuddy -Force -ErrorAction SilentlyContinue

# Test loading with verbose output
try {
    $modulePath = Join-Path $PSScriptRoot "PowerShell\Modules\BusBuddy\BusBuddy.psm1"
    Write-Host "Module path: $modulePath" -ForegroundColor Gray

    if (-not (Test-Path $modulePath)) {
        Write-Error "Module file not found at: $modulePath"
        exit 1
    }

    Import-Module $modulePath -Force -Verbose

    # Test basic functionality
    $module = Get-Module BusBuddy
    if ($module) {
        Write-Host "✅ Module loaded successfully" -ForegroundColor Green
        Write-Host "   Version: $($module.Version)" -ForegroundColor Gray
        Write-Host "   Functions: $($module.ExportedFunctions.Count)" -ForegroundColor Gray
        Write-Host "   Aliases: $($module.ExportedAliases.Count)" -ForegroundColor Gray

        # Test a simple command
        if (Get-Command 'Get-BusBuddyProjectRoot' -ErrorAction SilentlyContinue) {
            Write-Host "✅ Core functions available" -ForegroundColor Green
        } else {
            Write-Warning "Core functions not available"
        }
    } else {
        Write-Error "Module failed to load"
    }
} catch {
    Write-Error "Failed to load module: $_"
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
}
