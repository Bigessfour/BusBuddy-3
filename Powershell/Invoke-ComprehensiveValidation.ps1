# 🚌 BusBuddy Comprehensive PowerShell Environment Validator
# Created: August 22, 2025
# Purpose: Complete validation of PowerShell environment, modules, profiles, and terminal persistence
# Standards: PowerShell 7.5+ compliance, Microsoft best practices

#Requires -Version 7.5
#Requires -Module PSScriptAnalyzer

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$FixIssues,

    [Parameter()]
    [switch]$Detailed,

    [Parameter()]
    [switch]$ExportReport,

    [Parameter()]
    [string]$ReportPath = "validation-report.json"
)

begin {
    Write-Information "🔍 BusBuddy Comprehensive Environment Validation" -InformationAction Continue
    Write-Information "PowerShell Version: $($PSVersionTable.PSVersion)" -InformationAction Continue
    Write-Information "Host: $($Host.Name)" -InformationAction Continue
    Write-Information "Edition: $($PSVersionTable.PSEdition)" -InformationAction Continue

    $validationReport = @{
        Timestamp = Get-Date
        Environment = @{
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            Host = $Host.Name
            Edition = $PSVersionTable.PSEdition
            ProcessCount = (Get-Process -Name pwsh* -ErrorAction SilentlyContinue).Count
        }
        Modules = @{}
        Profiles = @{}
        EnvironmentVariables = @{}
        VSCodeIntegration = @{}
        AzureIntegration = @{}
        Issues = @()
        Recommendations = @()
        TotalScore = 0
    }
}

process {
    Write-Information "🧪 Testing PowerShell processes..." -InformationAction Continue

    # Check PowerShell processes
    $processes = Get-Process -Name pwsh* -ErrorAction SilentlyContinue
    if ($processes) {
        $validationReport.Environment.Processes = $processes | ForEach-Object {
            @{
                Id = $_.Id
                StartTime = $_.StartTime
                MemoryMB = [math]::Round($_.WorkingSet64/1MB, 2)
                ProcessName = $_.ProcessName
            }
        }
        Write-Information "✅ Found $($processes.Count) PowerShell processes" -InformationAction Continue
    } else {
        $validationReport.Issues += "❌ No PowerShell processes found"
    }

    Write-Information "🔍 Validating BusBuddy environment variables..." -InformationAction Continue

    # Check BusBuddy environment variables
    $busBuddyEnvVars = Get-ChildItem env:BUSBUDDY_* -ErrorAction SilentlyContinue
    if ($busBuddyEnvVars) {
        $validationReport.EnvironmentVariables.BusBuddy = @{}
        foreach ($env in $busBuddyEnvVars) {
            $validationReport.EnvironmentVariables.BusBuddy[$env.Name] = $env.Value
        }
        Write-Information "✅ Found $($busBuddyEnvVars.Count) BusBuddy environment variables" -InformationAction Continue
    } else {
        $validationReport.Issues += "❌ No BusBuddy environment variables found"
    }

    Write-Information "🔍 Checking Azure environment variables..." -InformationAction Continue

    # Check Azure environment variables
    $azureEnvVars = Get-ChildItem env:AZURE_* -ErrorAction SilentlyContinue
    if ($azureEnvVars) {
        $validationReport.EnvironmentVariables.Azure = @{}
        foreach ($env in $azureEnvVars) {
            # Mask sensitive values
            $value = if ($env.Name -match 'PASSWORD|SECRET|KEY') { '***MASKED***' } else { $env.Value }
            $validationReport.EnvironmentVariables.Azure[$env.Name] = $value
        }
        Write-Information "✅ Found $($azureEnvVars.Count) Azure environment variables" -InformationAction Continue
    }

    Write-Information "🔍 Validating PowerShell modules..." -InformationAction Continue

    # Essential modules check
    $essentialModules = @(
        'PSReadLine', 'Az.Accounts', 'Az.Sql', 'PSScriptAnalyzer'
    )

    $validationReport.Modules.Essential = @{}
    foreach ($module in $essentialModules) {
        $moduleInfo = Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($moduleInfo) {
            $validationReport.Modules.Essential[$module] = @{
                Version = $moduleInfo.Version.ToString()
                Path = $moduleInfo.ModuleBase
                Loaded = (Get-Module -Name $module -ErrorAction SilentlyContinue) -ne $null
            }
            Write-Information "✅ Essential module: $module ($($moduleInfo.Version))" -InformationAction Continue
        } else {
            $validationReport.Issues += "❌ Missing essential module: $module"
        }
    }

    Write-Information "🔍 Validating PowerShell profiles..." -InformationAction Continue

    # Profile validation
    $profiles = @{
        'Standard' = $PROFILE.CurrentUserAllHosts
        'BusBuddy' = Join-Path $PSScriptRoot 'Profiles\Microsoft.PowerShell_profile_optimized.ps1'
    }

    $validationReport.Profiles = @{}
    foreach ($profileType in $profiles.Keys) {
        $profilePath = $profiles[$profileType]
        if (Test-Path $profilePath -ErrorAction SilentlyContinue) {
            $profileContent = Get-Content $profilePath -ErrorAction SilentlyContinue
            $validationReport.Profiles[$profileType] = @{
                Path = $profilePath
                Exists = $true
                Lines = $profileContent.Count
                LastModified = (Get-Item $profilePath).LastWriteTime
            }
            Write-Information "✅ Profile found: $profileType" -InformationAction Continue
        } else {
            $validationReport.Profiles[$profileType] = @{
                Path = $profilePath
                Exists = $false
            }
            $validationReport.Issues += "⚠️ Profile not found: $profileType"
        }
    }

    Write-Information "🔍 Checking VS Code integration..." -InformationAction Continue

    # VS Code integration check
    $vscodeSettings = Join-Path $env:BUSBUDDY_ROOT '.vscode\settings.json'
    $vscodeExtensions = Join-Path $env:BUSBUDDY_ROOT '.vscode\extensions.json'

    $validationReport.VSCodeIntegration = @{
        SettingsExists = Test-Path $vscodeSettings -ErrorAction SilentlyContinue
        ExtensionsExists = Test-Path $vscodeExtensions -ErrorAction SilentlyContinue
        Mode = $env:BUSBUDDY_VSCODE_MODE -eq '1'
        PowerShellExtension = $env:POWERSHELL_DISTRIBUTION_CHANNEL -eq 'VSCode'
    }

    if ($validationReport.VSCodeIntegration.SettingsExists) {
        Write-Information "✅ VS Code settings configured" -InformationAction Continue
    } else {
        $validationReport.Issues += "⚠️ VS Code settings not found"
    }

    Write-Information "🔍 Testing Azure integration..." -InformationAction Continue

    # Azure integration test
    $validationReport.AzureIntegration = @{
        EntraIDEnabled = $env:BUSBUDDY_ENTRA_ENABLED -eq '1'
        AuthMethod = $env:BUSBUDDY_AUTH_METHOD
        DatabaseProvider = $env:BUSBUDDY_DB_PROVIDER
        SubscriptionId = $env:AZURE_SUBSCRIPTION_ID
        TenantId = $env:AZURE_TENANT_ID
    }

    Write-Information "🔍 Running PSScriptAnalyzer on profiles..." -InformationAction Continue

    # PowerShell compliance check
    if (Get-Command Invoke-ScriptAnalyzer -ErrorAction SilentlyContinue) {
        $profilePath = Join-Path $PSScriptRoot 'Profiles\Microsoft.PowerShell_profile_optimized.ps1'
        if (Test-Path $profilePath) {
            $analysisResults = Invoke-ScriptAnalyzer -Path $profilePath -Settings "$PSScriptRoot\..\PSScriptAnalyzerSettings.psd1" -ErrorAction SilentlyContinue
            if ($analysisResults) {
                $validationReport.Modules.PSScriptAnalyzer = @{
                    ProfileIssues = $analysisResults.Count
                    Issues = $analysisResults | ForEach-Object {
                        @{
                            RuleName = $_.RuleName
                            Severity = $_.Severity
                            Line = $_.Line
                            Message = $_.Message
                        }
                    }
                }
                Write-Information "⚠️ Found $($analysisResults.Count) PSScriptAnalyzer issues in profile" -InformationAction Continue
            } else {
                Write-Information "✅ Profile passes PSScriptAnalyzer validation" -InformationAction Continue
            }
        }
    }

    Write-Information "🧪 Testing BusBuddy commands..." -InformationAction Continue

    # Test key BusBuddy commands
    $commands = @('bb-build', 'bb-test', 'bb-sql-test', 'bb-entra', 'bb-modules')
    $validationReport.Commands = @{}

    foreach ($cmd in $commands) {
        $commandExists = Get-Command $cmd -ErrorAction SilentlyContinue
        $validationReport.Commands[$cmd] = $commandExists -ne $null
        if ($commandExists) {
            Write-Information "✅ Command available: $cmd" -InformationAction Continue
        } else {
            $validationReport.Issues += "❌ Command not found: $cmd"
        }
    }

    # Calculate overall score
    $totalChecks = 20
    $passedChecks = $totalChecks - $validationReport.Issues.Count
    $validationReport.TotalScore = [math]::Round(($passedChecks / $totalChecks) * 100, 1)

    Write-Information "📊 Validation Score: $($validationReport.TotalScore)%" -InformationAction Continue
}

end {
    # Display summary
    Write-Information "" -InformationAction Continue
    Write-Information "📋 Validation Summary:" -InformationAction Continue
    Write-Information "  Overall Score: $($validationReport.TotalScore)%" -InformationAction Continue
    Write-Information "  Issues Found: $($validationReport.Issues.Count)" -InformationAction Continue
    Write-Information "  PowerShell Processes: $($validationReport.Environment.ProcessCount)" -InformationAction Continue
    Write-Information "  BusBuddy Env Vars: $($validationReport.EnvironmentVariables.BusBuddy.Count)" -InformationAction Continue

    if ($validationReport.Issues.Count -gt 0) {
        Write-Information "" -InformationAction Continue
        Write-Information "⚠️ Issues Found:" -InformationAction Continue
        foreach ($issue in $validationReport.Issues) {
            Write-Information "  $issue" -InformationAction Continue
        }
    }

    # Recommendations
    if ($validationReport.TotalScore -lt 90) {
        $validationReport.Recommendations += "Consider running bb-reload to refresh the PowerShell profile"
        $validationReport.Recommendations += "Run load-azure to ensure Azure modules are available"
        $validationReport.Recommendations += "Verify Syncfusion license key is configured"
    }

    if ($validationReport.Recommendations.Count -gt 0) {
        Write-Information "" -InformationAction Continue
        Write-Information "💡 Recommendations:" -InformationAction Continue
        foreach ($rec in $validationReport.Recommendations) {
            Write-Information "  • $rec" -InformationAction Continue
        }
    }

    # Export report if requested
    if ($ExportReport) {
        $validationReport | ConvertTo-Json -Depth 10 | Set-Content $ReportPath
        Write-Information "📄 Report exported to: $ReportPath" -InformationAction Continue
    }

    # Fix issues if requested
    if ($FixIssues) {
        Write-Information "" -InformationAction Continue
        Write-Information "🔧 Attempting to fix issues..." -InformationAction Continue

        # Reload profile if issues found
        if ($validationReport.Issues -match "Profile|Command") {
            try {
                . (Join-Path $PSScriptRoot 'Profiles\Microsoft.PowerShell_profile_optimized.ps1')
                Write-Information "✅ Profile reloaded" -InformationAction Continue
            } catch {
                Write-Warning "Failed to reload profile: $($_.Exception.Message)"
            }
        }
    }

    return $validationReport
}
