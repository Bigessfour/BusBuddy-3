# 🚌 BusBuddy Environment Validation Script
# Validates that all components from the original comprehensive setup are properly configured

#Requires -Version 7.5

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Detailed
)

begin {
    Write-Information "🔍 BusBuddy Environment Validation" -InformationAction Continue
    $issues = @()
    $success = @()
}

process {
    # Check PowerShell version
    if ($PSVersionTable.PSVersion.Major -ge 7 -and $PSVersionTable.PSVersion.Minor -ge 5) {
        $success += "✅ PowerShell 7.5+ detected: $($PSVersionTable.PSVersion)"
    } else {
        $issues += "❌ PowerShell 7.5+ required. Current: $($PSVersionTable.PSVersion)"
    }

    # Check required modules from original setup
    $requiredModules = @(
        'Az.Accounts', 'Az.Sql', 'Az.Storage', 'Az.KeyVault', 'SqlServer',
        'PSScriptAnalyzer', 'Microsoft.PowerShell.SecretManagement',
        'Pester', 'PSDepend', 'PSRule.Azure', 'Plaster', 'PSFramework'
    )

    $optionalModules = @('dbatools', 'ImportExcel', 'PSGraph', 'NuGet.PackageManagement')

    Write-Information "🔍 Checking required modules..." -InformationAction Continue
    foreach ($module in $requiredModules) {
        if (Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue) {
            $success += "✅ Required module: $module"
        } else {
            $issues += "❌ Missing required module: $module"
        }
    }

    Write-Information "🔍 Checking optional modules..." -InformationAction Continue
    foreach ($module in $optionalModules) {
        if (Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue) {
            $success += "✅ Optional module: $module"
        } else {
            if ($Detailed) {
                $issues += "⚠️ Missing optional module: $module"
            }
        }
    }

    # Check VS Code extensions configuration
    $extensionsFile = Join-Path $PWD ".vscode\extensions.json"
    if (Test-Path $extensionsFile) {
        $success += "✅ VS Code extensions.json found"

        $extensionsContent = Get-Content $extensionsFile -Raw
        $requiredExtensions = @(
            'ms-vscode.powershell', 'ms-dotnettools.csharp', 'ms-mssql.mssql',
            'ms-azuretools.vscode-azuresql', 'formulahendry.code-runner'
        )

        foreach ($ext in $requiredExtensions) {
            if ($extensionsContent -match $ext) {
                $success += "✅ VS Code extension configured: $ext"
            } else {
                $issues += "❌ Missing VS Code extension: $ext"
            }
        }
    } else {
        $issues += "❌ VS Code extensions.json not found"
    }

    # Check VS Code settings
    $settingsFile = Join-Path $PWD ".vscode\settings.json"
    if (Test-Path $settingsFile) {
        $success += "✅ VS Code settings.json found"

        $settingsContent = Get-Content $settingsFile -Raw
        if ($settingsContent -match "PSModulePath") {
            $success += "✅ PSModulePath configured in VS Code"
        } else {
            $issues += "❌ PSModulePath not configured in VS Code settings"
        }

        if ($settingsContent -match "PSScriptAnalyzerSettings") {
            $success += "✅ PSScriptAnalyzer settings configured"
        } else {
            $issues += "❌ PSScriptAnalyzer settings not configured"
        }
    } else {
        $issues += "❌ VS Code settings.json not found"
    }

    # Check PowerShell profile
    $profilePath = Join-Path $PWD "PowerShell\Profiles\Microsoft.PowerShell_profile.ps1"
    if (Test-Path $profilePath) {
        $success += "✅ BusBuddy PowerShell profile found"

        $profileContent = Get-Content $profilePath -Raw
        $profileChecks = @(
            @{ Pattern = "ExtendedModules"; Description = "Extended modules configuration" },
            @{ Pattern = "Secret Management"; Description = "Secret management integration" },
            @{ Pattern = "dbatools"; Description = "Enhanced SQL functions" },
            @{ Pattern = "PSRule.Azure"; Description = "Azure compliance checking" },
            @{ Pattern = "NuGet.PackageManagement"; Description = "Package management functions" }
        )

        foreach ($check in $profileChecks) {
            if ($profileContent -match $check.Pattern) {
                $success += "✅ Profile includes: $($check.Description)"
            } else {
                $issues += "❌ Profile missing: $($check.Description)"
            }
        }
    } else {
        $issues += "❌ BusBuddy PowerShell profile not found"
    }

    # Check PSScriptAnalyzer configuration
    $psaFile = Join-Path $PWD "PSScriptAnalyzerSettings.psd1"
    if (Test-Path $psaFile) {
        $success += "✅ PSScriptAnalyzer settings file found"
    } else {
        $issues += "❌ PSScriptAnalyzer settings file missing"
    }

    # Summary
    Write-Output "`n📊 BusBuddy Environment Validation Summary:"
    Write-Output "✅ Successful checks: $($success.Count)"
    Write-Output "❌ Issues found: $($issues.Count)"

    if ($Detailed -or $issues.Count -gt 0) {
        if ($success.Count -gt 0) {
            Write-Output "`n✅ Successful items:"
            $success | ForEach-Object { Write-Output "  $_" }
        }

        if ($issues.Count -gt 0) {
            Write-Output "`n❌ Issues found:"
            $issues | ForEach-Object { Write-Output "  $_" }

            Write-Output "`n💡 To fix issues:"
            Write-Output "  • Run: .\PowerShell\Install-BusBuddyCompleteEnvironment.ps1"
            Write-Output "  • Install missing VS Code extensions via Extensions view"
            Write-Output "  • Restart VS Code and PowerShell sessions"
        }
    }

    if ($issues.Count -eq 0) {
        Write-Information "`n🎉 Environment validation successful! All components properly configured." -InformationAction Continue
    } else {
        Write-Warning "`n⚠️ Environment validation found $($issues.Count) issues that need attention."
    }
}

end {
    return $issues.Count -eq 0
}
