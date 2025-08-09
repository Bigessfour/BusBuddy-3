#Requires -Version 7.5
<#
.SYNOPSIS
    Debug BusBuddy DI Container Registration using Wintellect Tools

.DESCRIPTION
    Comprehensive debugging script to trace DI container setup and identify
    missing registrations using both BusBuddy utilities and Wintellect debugging tools.

.NOTES
    Requires WintellectPowerShell module and VS Code C# debugging capabilities
#>

[CmdletBinding()]
param(
    [switch]$DetailedOutput,
    [switch]$ExportToFile
)

function Invoke-BusBuddyDIContainerDiagnostic {
    <#
    .SYNOPSIS
        Run DI Container diagnostic analysis
    .PARAMETER DetailedOutput
        Show detailed output information
    .PARAMETER ExportToFile
        Export results to a timestamped file
    #>
    [CmdletBinding()]
    param(
        [switch]$DetailedOutput,
        [switch]$ExportToFile
    )

Write-Host "🔍 BusBuddy DI Container Diagnostic Report" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Gray

#region System Environment Check
Write-Host "`n📋 System Environment:" -ForegroundColor Yellow
$envInfo = @{
    "PowerShell Version" = $PSVersionTable.PSVersion
    ".NET Version" = (dotnet --version)
    "Current Directory" = $PWD.Path
    "BusBuddy Module" = (Get-Module BusBuddy -ErrorAction SilentlyContinue)?.Version ?? "Not Loaded"
    "Wintellect Module" = (Get-Module WintellectPowerShell -ErrorAction SilentlyContinue)?.Version ?? "Not Loaded"
}

$envInfo.GetEnumerator() | ForEach-Object {
    Write-Host "  ✓ $($_.Key): $($_.Value)" -ForegroundColor Green
}
#endregion

#region BusBuddy Health Check
Write-Host "`n🏥 BusBuddy Health Check:" -ForegroundColor Yellow
try {
    $healthResult = Test-BusBuddyHealth
    Write-Host "  ✅ Health check completed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
}
#endregion

#region Build Status Check
Write-Host "`n🏗️ Build Status Check:" -ForegroundColor Yellow
try {
    $buildOutput = dotnet build BusBuddy.sln --verbosity quiet --nologo 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Solution builds successfully" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        Write-Host "  📄 Build output: $buildOutput" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ❌ Build check failed: $($_.Exception.Message)" -ForegroundColor Red
}
#endregion

#region DI Container Analysis
Write-Host "`n🔧 DI Container Registration Analysis:" -ForegroundColor Yellow

# Check ServiceCollectionExtensions registration
$serviceExtensionsFile = "BusBuddy.Core\Extensions\ServiceCollectionExtensions.cs"
if (Test-Path $serviceExtensionsFile) {
    Write-Host "  📄 Analyzing ServiceCollectionExtensions.cs..." -ForegroundColor Cyan

    $content = Get-Content $serviceExtensionsFile -Raw

    # Check for IBusBuddyDbContextFactory registration
    if ($content -match "IBusBuddyDbContextFactory") {
        Write-Host "  ✅ IBusBuddyDbContextFactory registration found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ IBusBuddyDbContextFactory registration NOT found" -ForegroundColor Red
    }

    # Check for AddDataServices method
    if ($content -match "AddDataServices") {
        Write-Host "  ✅ AddDataServices method found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ AddDataServices method NOT found" -ForegroundColor Red
    }

    # Extract registered services
    $serviceRegistrations = $content | Select-String "services\.Add\w+<([^>]+)>" -AllMatches
    Write-Host "  📋 Registered Services:" -ForegroundColor Cyan
    foreach ($match in $serviceRegistrations.Matches) {
        Write-Host "    • $($match.Groups[1].Value)" -ForegroundColor Gray
    }
}

# Check App.xaml.cs DI setup
$appXamlFile = "BusBuddy.WPF\App.xaml.cs"
if (Test-Path $appXamlFile) {
    Write-Host "`n  📄 Analyzing App.xaml.cs DI setup..." -ForegroundColor Cyan

    $appContent = Get-Content $appXamlFile -Raw

    if ($appContent -match "AddDataServices") {
        Write-Host "  ✅ AddDataServices called in App.xaml.cs" -ForegroundColor Green
    } else {
        Write-Host "  ❌ AddDataServices NOT called in App.xaml.cs" -ForegroundColor Red
    }

    if ($appContent -match "ServiceProvider") {
        Write-Host "  ✅ ServiceProvider setup found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ ServiceProvider setup NOT found" -ForegroundColor Red
    }
}
#endregion

#region Application Runtime Test
Write-Host "`n🚀 Application Runtime Test:" -ForegroundColor Yellow
try {
    # Check if application is currently running
    $busBuddyProcess = Get-Process -Name "BusBuddy*" -ErrorAction SilentlyContinue
    if ($busBuddyProcess) {
        Write-Host "  ✅ BusBuddy application is currently running (PID: $($busBuddyProcess.Id))" -ForegroundColor Green

        # Use Wintellect tools to analyze the running process if available
        if (Get-Command Get-DumpAnalysis -ErrorAction SilentlyContinue) {
            Write-Host "  🔍 Wintellect analysis available for running process" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  ℹ️  BusBuddy application is not currently running" -ForegroundColor Yellow
        Write-Host "  💡 Try starting with: bb-run" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ❌ Runtime test failed: $($_.Exception.Message)" -ForegroundColor Red
}
#endregion

#region Recommendations
Write-Host "`n💡 Diagnostic Recommendations:" -ForegroundColor Yellow

$recommendations = @()

# Check for common DI issues
if (!(Test-Path $serviceExtensionsFile)) {
    $recommendations += "❌ ServiceCollectionExtensions.cs file not found"
}

if (!(Test-Path $appXamlFile)) {
    $recommendations += "❌ App.xaml.cs file not found"
}

# Add Wintellect-specific recommendations
if (Get-Module WintellectPowerShell -ErrorAction SilentlyContinue) {
    $recommendations += "✅ Wintellect debugging tools are available"
    $recommendations += "💡 Use Get-DumpAnalysis for deeper runtime analysis"
} else {
    $recommendations += "⚠️  Install WintellectPowerShell for advanced debugging"
}

# Add VS Code debugging recommendations
$recommendations += "💡 Use VS Code C# debugging with breakpoints in App.xaml.cs"
$recommendations += "💡 Use Roslynator extension for code analysis"
$recommendations += "💡 Check Output panel in VS Code for detailed error messages"

foreach ($rec in $recommendations) {
    Write-Host "  $rec" -ForegroundColor Gray
}
#endregion

#region Export Results
if ($ExportToFile) {
    $reportPath = "BusBuddy-DI-Diagnostic-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $report = @"
BusBuddy DI Container Diagnostic Report
Generated: $(Get-Date)
========================================

Environment Information:
$(($envInfo.GetEnumerator() | ForEach-Object { "  $($_.Key): $($_.Value)" }) -join "`n")

Health Check: $($healthResult ? "PASSED" : "FAILED")
Build Status: $(if ($LASTEXITCODE -eq 0) { "SUCCESS" } else { "FAILED" })

Recommendations:
$(($recommendations | ForEach-Object { "  $_" }) -join "`n")
"@

    $report | Out-File $reportPath -Encoding UTF8
    Write-Host "`n💾 Report saved to: $reportPath" -ForegroundColor Green
}
#endregion

Write-Host "`n✅ DI Container diagnostic completed!" -ForegroundColor Green
Write-Host "🔧 Next steps: Address any issues found above, then test with bb-run" -ForegroundColor Cyan
}

# Only run automatically if script is invoked directly (not dot-sourced)
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-BusBuddyDIContainerDiagnostic @PSBoundParameters
}
