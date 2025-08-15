#requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy Environment Validation - Ensures consistent, reusable PowerShell setup
.DESCRIPTION
    Run this script to validate that the BusBuddy PowerShell environment is
    properly configured and ready for MVP development.
.NOTES
    This ensures the environment won't break and remains consistent across sessions.
#>

function Test-BusBuddyEnvironment {
    [CmdletBinding()]
    param()

    Write-Output "🔍 BusBuddy Environment Validation"
    Write-Output "================================="
    Write-Output ""

    $issues = @()
    $warnings = @()

    # 1. PowerShell Version Check
    Write-Output "1. PowerShell Version..."
    if ($PSVersionTable.PSVersion -ge [version]'7.5.0') {
        Write-Host "   ✅ PowerShell $($PSVersionTable.PSVersion) (Required: 7.5+)" -ForegroundColor Green
    } else {
        $issues += "PowerShell version $($PSVersionTable.PSVersion) is too old. Need 7.5+"
        Write-Host "   ❌ PowerShell $($PSVersionTable.PSVersion) - UPGRADE REQUIRED" -ForegroundColor Red
    }

    # 2. BusBuddy Workspace Detection
    Write-Host "2. Workspace Detection..." -ForegroundColor Yellow
    $workspaceFound = $false
    $possiblePaths = @(
        $PWD.Path,
        "$env:USERPROFILE\Desktop\BusBuddy",
        "$env:USERPROFILE\Documents\BusBuddy",
        "C:\BusBuddy"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\BusBuddy.sln" -ErrorAction SilentlyContinue) {
            Write-Host "   ✅ Workspace found: $path" -ForegroundColor Green
            $workspaceFound = $true
            break
        }
    }

    if (-not $workspaceFound) {
        $issues += "BusBuddy workspace not found in standard locations"
        Write-Host "   ❌ Workspace not found" -ForegroundColor Red
    }

    # 3. Module File Existence
    Write-Host "3. PowerShell Module..." -ForegroundColor Yellow
    $modulePath = ".\PowerShell\Modules\BusBuddy\BusBuddy.psm1"
    if (Test-Path $modulePath) {
        Write-Host "   ✅ Module file exists" -ForegroundColor Green

        # Check module can be imported
        try {
            Import-Module $modulePath -Force -ErrorAction Stop
            Write-Host "   ✅ Module imports successfully" -ForegroundColor Green
        } catch {
            $issues += "Module exists but fails to import: $($_.Exception.Message)"
            Write-Host "   ❌ Module import failed" -ForegroundColor Red
        }
    } else {
        $issues += "BusBuddy.psm1 module file not found"
        Write-Host "   ❌ Module file missing" -ForegroundColor Red
    }

    # 4. Essential Commands Test
    Write-Host "4. Essential Commands..." -ForegroundColor Yellow
    $essentialCommands = @('bb-build', 'bb-run', 'Test-BusBuddyHealth', 'bb-mvp', 'bb-mvp-check')
    $commandsWorking = 0

    foreach ($cmd in $essentialCommands) {
        if (Get-Command $cmd -ErrorAction SilentlyContinue) {
            $commandsWorking++
        }
    }

    if ($commandsWorking -eq $essentialCommands.Count) {
        Write-Host "   ✅ All $($essentialCommands.Count) essential commands available" -ForegroundColor Green
    } else {
        $issues += "Only $commandsWorking of $($essentialCommands.Count) commands available"
        Write-Host "   ❌ Missing commands ($commandsWorking/$($essentialCommands.Count))" -ForegroundColor Red
    }

    # 5. .NET SDK Check
    Write-Host "5. .NET SDK..." -ForegroundColor Yellow
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -and $dotnetVersion -match '^9\.') {
            Write-Host "   ✅ .NET $dotnetVersion" -ForegroundColor Green
        } else {
            $warnings += ".NET version $dotnetVersion - expected 9.x"
            Write-Host "   ⚠️ .NET $dotnetVersion (Expected: 9.x)" -ForegroundColor Yellow
        }
    } catch {
        $issues += ".NET SDK not found or not working"
        Write-Host "   ❌ .NET SDK not found" -ForegroundColor Red
    }

    # 6. Git Status
    Write-Host "6. Git Repository..." -ForegroundColor Yellow
    try {
        $gitStatus = & git status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0) {
            if ($gitStatus) {
                $warnings += "Git has uncommitted changes"
                Write-Host "   ⚠️ Uncommitted changes present" -ForegroundColor Yellow
            } else {
                Write-Host "   ✅ Git repository clean" -ForegroundColor Green
            }
        } else {
            $warnings += "Not in a Git repository or Git not available"
            Write-Host "   ⚠️ Git issues detected" -ForegroundColor Yellow
        }
    } catch {
        $warnings += "Git not available: $($_.Exception.Message)"
        Write-Host "   ⚠️ Git not available" -ForegroundColor Yellow
    }

    # 7. Grok Resources Check
    Write-Host "7. AI Assistant Resources..." -ForegroundColor Yellow
    if (Test-Path "Grok Resources\GROK-README.md") {
        Write-Output "   ✅ Grok Resources folder ready"
    } else {
        if ($env:BUSBUDDY_NO_XAI_WARN -ne '1') {
            # Removed per MVP request: avoid Grok/xAI warning noise in validation output
            # $warnings += "Grok Resources not found - AI assistance may be limited"
        }
        Write-Output "   ⚠️ Grok Resources missing"
    }

    # Summary
    Write-Output ""
    Write-Output "🎯 VALIDATION SUMMARY"
    Write-Output "====================="

    if ($issues.Count -eq 0) {
        Write-Output "✅ ENVIRONMENT READY FOR MVP DEVELOPMENT!"
        Write-Output "   All critical systems are operational"
        Write-Output ""
        Write-Output "🚀 Quick Start Commands:"
    Write-Output "   Test-BusBuddyHealth - System health check"
        Write-Output "   bb-mvp -JustShow - Show MVP priorities"
        Write-Output "   bb-build       - Build the solution"
        Write-Output "   bb-run         - Run the application"
        Write-Output ""
        Write-Output "🎯 MVP Focus:"
        Write-Output "   bb-mvp 'feature name' - Evaluate if feature is MVP-worthy"
        Write-Output "   bb-mvp-check          - Check MVP milestone readiness"

        if ($warnings.Count -gt 0) {
            Write-Output ""
            Write-Output "⚠️ WARNINGS (non-critical):"
            $warnings | ForEach-Object { Write-Output "   • $_" }
        }

        return $true
    } else {
        Write-Output "❌ ENVIRONMENT NOT READY"
        Write-Output "   Fix these issues before starting development:"
        Write-Output ""
        $issues | ForEach-Object { Write-Output "   • $_" }

        if ($warnings.Count -gt 0) {
            Write-Output ""
            Write-Output "⚠️ Additional warnings:"
            $warnings | ForEach-Object { Write-Output "   • $_" }
        }

        return $false
    }
}

# Export the function
Export-ModuleMember -Function Test-BusBuddyEnvironment

# Run validation if script is executed directly
if ($MyInvocation.InvocationName -eq $MyInvocation.MyCommand.Name) {
    Test-BusBuddyEnvironment
}
