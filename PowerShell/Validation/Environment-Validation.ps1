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
        Write-Information "   ✅ PowerShell $($PSVersionTable.PSVersion) (Required: 7.5+)" -InformationAction Continue
    } else {
        $issues += "PowerShell version $($PSVersionTable.PSVersion) is too old. Need 7.5+"
        Write-Error "   ❌ PowerShell $($PSVersionTable.PSVersion) - UPGRADE REQUIRED"
    }

    # 2. BusBuddy Workspace Detection
    Write-Information "2. Workspace Detection..." -InformationAction Continue
    $workspaceFound = $false
    $possiblePaths = @(
        $PWD.Path,
        "$env:USERPROFILE\Desktop\BusBuddy",
        "$env:USERPROFILE\Documents\BusBuddy",
        "C:\BusBuddy"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\BusBuddy.sln" -ErrorAction SilentlyContinue) {
            Write-Information "   ✅ Workspace found: $path" -InformationAction Continue
            $workspaceFound = $true
            break
        }
    }

    if (-not $workspaceFound) {
        $issues += "BusBuddy workspace not found in standard locations"
    Write-Error "   ❌ Workspace not found"
    }

    # 3. Module File Existence
    Write-Information "3. PowerShell Module..." -InformationAction Continue
    $modulePath = ".\PowerShell\Modules\BusBuddy\BusBuddy.psm1"
    if (Test-Path $modulePath) {
    Write-Information "   ✅ Module file exists" -InformationAction Continue

        # Check module can be imported
        try {
            Import-Module $modulePath -Force -ErrorAction Stop
            Write-Information "   ✅ Module imports successfully" -InformationAction Continue
        } catch {
            $issues += "Module exists but fails to import: $($_.Exception.Message)"
            Write-Error "   ❌ Module import failed"
        }
    } else {
        $issues += "BusBuddy.psm1 module file not found"
        Write-Error "   ❌ Module file missing"
    }

    # 4. Essential Commands Test
    Write-Information "4. Essential Commands..." -InformationAction Continue
    $essentialCommands = @('bb-build', 'bb-run', 'Test-BusBuddyHealth', 'bb-mvp', 'bb-mvp-check')
    $commandsWorking = 0

    foreach ($cmd in $essentialCommands) {
        if (Get-Command $cmd -ErrorAction SilentlyContinue) {
            $commandsWorking++
        }
    }

    if ($commandsWorking -eq $essentialCommands.Count) {
        Write-Information "   ✅ All $($essentialCommands.Count) essential commands available" -InformationAction Continue
    } else {
        $issues += "Only $commandsWorking of $($essentialCommands.Count) commands available"
        Write-Error "   ❌ Missing commands ($commandsWorking/$($essentialCommands.Count))"
    }

    # 5. .NET SDK Check
    Write-Information "5. .NET SDK..." -InformationAction Continue
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -and $dotnetVersion -match '^9\.') {
            Write-Information "   ✅ .NET $dotnetVersion" -InformationAction Continue
        } else {
            $warnings += ".NET version $dotnetVersion - expected 9.x"
            Write-Warning "   ⚠️ .NET $dotnetVersion (Expected: 9.x)"
        }
    } catch {
        $issues += ".NET SDK not found or not working"
        Write-Error "   ❌ .NET SDK not found"
    }

    # 6. Git Status
    Write-Information "6. Git Repository..." -InformationAction Continue
    try {
        $gitStatus = & git status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0) {
            if ($gitStatus) {
                $warnings += "Git has uncommitted changes"
                Write-Warning "   ⚠️ Uncommitted changes present"
            } else {
                Write-Information "   ✅ Git repository clean" -InformationAction Continue
            }
        } else {
            $warnings += "Not in a Git repository or Git not available"
            Write-Warning "   ⚠️ Git issues detected"
        }
    } catch {
        $warnings += "Git not available: $($_.Exception.Message)"
        Write-Warning "   ⚠️ Git not available"
    }

    # 7. Grok Resources Check
    Write-Information "7. AI Assistant Resources..." -InformationAction Continue
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
