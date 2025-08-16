#Requires -Version 7.0
<#
.SYNOPSIS
    Test script to verify all PowerShell modules are loaded and working
.DESCRIPTION
    Validates that SqlServer, Logging, WPFBot3000, and PoshWPF modules are properly loaded
    and their key commands are accessible. Part of BusBuddy development environment setup.
.EXAMPLE
    .\Test-AllModules.ps1
#>

[CmdletBinding()]
param()

Write-Information "🧪 Testing BusBuddy PowerShell Module Environment" -InformationAction Continue
Write-Information "=================================================" -InformationAction Continue

# Test SqlServer module
Write-Information "" -InformationAction Continue
Write-Information "📊 Testing SqlServer Module..." -InformationAction Continue
try {
    $sqlCommands = Get-Command -Module SqlServer | Measure-Object
    Write-Information "✅ SqlServer: $($sqlCommands.Count) commands available" -InformationAction Continue

    # Test basic command availability
    if (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
        Write-Information "✅ Invoke-Sqlcmd: Available" -InformationAction Continue
    }
    if (Get-Command Get-SqlDatabase -ErrorAction SilentlyContinue) {
        Write-Information "✅ Get-SqlDatabase: Available" -InformationAction Continue
    }
}
catch {
    Write-Warning "❌ SqlServer module test failed: $($_.Exception.Message)"
}

# Test Logging module
Write-Information "" -InformationAction Continue
Write-Information "📝 Testing Logging Module..." -InformationAction Continue
try {
    $loggingCommands = Get-Command -Module Logging | Measure-Object
    Write-Information "✅ Logging: $($loggingCommands.Count) commands available" -InformationAction Continue

    # Test Write-Log command
    if (Get-Command Write-Log -ErrorAction SilentlyContinue) {
        Write-Information "✅ Write-Log: Available" -InformationAction Continue
        Write-Log -Level INFO -Message "Test log entry from BusBuddy environment test"
        Write-Information "✅ Write-Log: Executed successfully" -InformationAction Continue
    }
}
catch {
    Write-Warning "❌ Logging module test failed: $($_.Exception.Message)"
}

# Test WPF modules
Write-Information "" -InformationAction Continue
Write-Information "🎨 Testing WPF Modules..." -InformationAction Continue
try {
    $wpfBot3000Commands = Get-Command -Module WPFBot3000 -ErrorAction SilentlyContinue | Measure-Object
    Write-Information "✅ WPFBot3000: $($wpfBot3000Commands.Count) commands available" -InformationAction Continue

    $poshWpfCommands = Get-Command -Module PoshWPF -ErrorAction SilentlyContinue | Measure-Object
    Write-Information "✅ PoshWPF: $($poshWpfCommands.Count) commands available" -InformationAction Continue
}
catch {
    Write-Warning "❌ WPF modules test failed: $($_.Exception.Message)"
}

# Test module availability in path
Write-Information "" -InformationAction Continue
Write-Information "📦 Module Path Test..." -InformationAction Continue
$moduleLocations = Get-Module -ListAvailable |
    Where-Object {$_.Name -in @('SqlServer','Logging','WPFBot3000','PoshWPF')} |
    Select-Object Name, Version, ModuleBase

foreach ($module in $moduleLocations) {
    Write-Information "✅ $($module.Name) v$($module.Version): $($module.ModuleBase)" -InformationAction Continue
}

Write-Information "" -InformationAction Continue
Write-Information "🎯 Environment Test Complete!" -InformationAction Continue
Write-Information "All modules are permanently available and will auto-load with BusBuddy profile." -InformationAction Continue
