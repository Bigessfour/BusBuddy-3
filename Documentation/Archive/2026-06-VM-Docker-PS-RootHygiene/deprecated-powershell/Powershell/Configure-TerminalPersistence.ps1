#requires -Version 7.5
<#
.SYNOPSIS
    Configure Terminal Persistence Attributes for BusBuddy Development Environment

.DESCRIPTION
    This script configures advanced terminal persistence features including:
    - PSReadLine advanced settings
    - Windows Terminal configuration
    - VS Code terminal integration
    - PowerShell history persistence
    - Custom key bindings and shortcuts

.PARAMETER EnableAdvancedFeatures
    Enable advanced terminal features like predictive IntelliSense and enhanced key bindings

.PARAMETER ConfigureWindowsTerminal
    Configure Windows Terminal settings for optimal BusBuddy development

.PARAMETER SetupVSCodeIntegration
    Configure VS Code terminal integration settings

.EXAMPLE
    .\Configure-TerminalPersistence.ps1 -EnableAdvancedFeatures -ConfigureWindowsTerminal

.NOTES
    Author: BusBuddy Development Team
    Date: August 21, 2025
    Requires: PowerShell 7.5+, PSReadLine 2.3+
#>

[CmdletBinding()]
param(
    [switch]$EnableAdvancedFeatures,
    [switch]$ConfigureWindowsTerminal,
    [switch]$SetupVSCodeIntegration
)

Write-Information "🚌 Configuring Terminal Persistence Attributes for BusBuddy" -InformationAction Continue

# Advanced PSReadLine Configuration
if ($EnableAdvancedFeatures) {
    Write-Information "⚙️ Enabling advanced PSReadLine features..." -InformationAction Continue
    
    # Predictive IntelliSense (PowerShell 7.2+)
    if ($PSVersionTable.PSVersion -ge [Version]'7.2') {
        Set-PSReadLineOption -PredictionSource HistoryAndPlugin
        Set-PSReadLineOption -PredictionViewStyle ListView
        Write-Information "✅ Predictive IntelliSense enabled" -InformationAction Continue
    }
    
    # Enhanced completion settings
    Set-PSReadLineOption -CompletionQueryItems 50
    Set-PSReadLineOption -MaximumKillRingCount 10
    
    # Advanced key handlers
    Set-PSReadLineKeyHandler -Key Ctrl+Shift+j -Function MenuComplete
    Set-PSReadLineKeyHandler -Key Ctrl+Shift+k -Function TabCompletePrevious
    Set-PSReadLineKeyHandler -Key F1 -Function ShowCommandHelp
    Set-PSReadLineKeyHandler -Key Ctrl+r -Function ReverseSearchHistory
    Set-PSReadLineKeyHandler -Key Ctrl+s -Function ForwardSearchHistory
    
    # Custom function for BusBuddy-specific completions
    Set-PSReadLineKeyHandler -Key Ctrl+Shift+b -ScriptBlock {
        param($key, $arg)
        
        $common_commands = @(
            'bb-build', 'bb-run', 'bb-test', 'bb-deps-check',
            'dbuild', 'dtest', 'sqltest', 'azcheck',
            'dotnet build BusBuddy.sln', 'dotnet run --project BusBuddy.WPF'
        )
        
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert(($common_commands | Out-GridView -PassThru -Title "BusBuddy Commands"))
    }
    
    Write-Information "✅ Advanced PSReadLine features configured" -InformationAction Continue
}

# Windows Terminal Configuration
if ($ConfigureWindowsTerminal) {
    Write-Information "⚙️ Configuring Windows Terminal settings..." -InformationAction Continue
    
    $wtSettingsPath = "$env:LOCALAPPDATA\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json"
    
    if (Test-Path $wtSettingsPath) {
        try {
            $wtSettings = Get-Content $wtSettingsPath | ConvertFrom-Json
            
            # BusBuddy-specific profile
            $busBuddyProfile = @{
                name = "BusBuddy PowerShell"
                commandline = "pwsh.exe -NoProfile -NoExit -Command `"& '$PWD\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1'`""
                startingDirectory = "C:\Users\biges\Desktop\BusBuddy"
                icon = "🚌"
                colorScheme = "Campbell Powershell"
                fontSize = 11
                fontFace = "Cascadia Code"
                cursorShape = "bar"
                antialiasingMode = "cleartype"
            }
            
            # Add to profiles if not exists
            $existingProfile = $wtSettings.profiles.list | Where-Object { $_.name -eq "BusBuddy PowerShell" }
            if (-not $existingProfile) {
                $wtSettings.profiles.list += $busBuddyProfile
                $wtSettings | ConvertTo-Json -Depth 10 | Set-Content $wtSettingsPath -Encoding UTF8
                Write-Information "✅ BusBuddy profile added to Windows Terminal" -InformationAction Continue
            } else {
                Write-Information "✅ BusBuddy profile already exists in Windows Terminal" -InformationAction Continue
            }
        } catch {
            Write-Warning "❌ Failed to configure Windows Terminal: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "❌ Windows Terminal settings file not found"
    }
}

# VS Code Terminal Integration
if ($SetupVSCodeIntegration) {
    Write-Information "⚙️ Configuring VS Code terminal integration..." -InformationAction Continue
    
    $vscodeSettingsPath = "$PWD\.vscode\settings.json"
    
    if (Test-Path $vscodeSettingsPath) {
        try {
            $vscodeSettings = Get-Content $vscodeSettingsPath | ConvertFrom-Json
            
            # Enhanced terminal settings
            $terminalSettings = @{
                "terminal.integrated.defaultProfile.windows" = "BusBuddy PowerShell 7.5.2"
                "terminal.integrated.profiles.windows" = @{
                    "BusBuddy PowerShell 7.5.2" = @{
                        "path" = "pwsh.exe"
                        "args" = @(
                            "-NoProfile",
                            "-NoExit", 
                            "-Command",
                            "& '$PWD\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1'"
                        )
                        "icon" = "terminal-powershell"
                    }
                }
                "terminal.integrated.enablePersistentSessions" = $true
                "terminal.integrated.persistentSessionReviveProcess" = "onExitAndWindowClose"
                "terminal.integrated.showExitAlert" = $false
                "terminal.integrated.confirmOnExit" = "hasChildProcesses"
                "terminal.integrated.enableImages" = $true
                "terminal.integrated.gpuAcceleration" = "on"
            }
            
            # Merge settings
            $terminalSettings.GetEnumerator() | ForEach-Object {
                $vscodeSettings | Add-Member -Type NoteProperty -Name $_.Key -Value $_.Value -Force
            }
            
            $vscodeSettings | ConvertTo-Json -Depth 10 | Set-Content $vscodeSettingsPath -Encoding UTF8
            Write-Information "✅ VS Code terminal settings updated" -InformationAction Continue
        } catch {
            Write-Warning "❌ Failed to update VS Code settings: $($_.Exception.Message)"
        }
    } else {
        Write-Information "⚠️ VS Code settings.json not found, creating with terminal persistence settings..." -InformationAction Continue
        
        $newVSCodeSettings = @{
            "terminal.integrated.defaultProfile.windows" = "BusBuddy PowerShell 7.5.2"
            "terminal.integrated.profiles.windows" = @{
                "BusBuddy PowerShell 7.5.2" = @{
                    "path" = "pwsh.exe"
                    "args" = @(
                        "-NoProfile",
                        "-NoExit", 
                        "-Command",
                        "& '$PWD\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1'"
                    )
                    "icon" = "terminal-powershell"
                }
            }
            "terminal.integrated.enablePersistentSessions" = $true
            "terminal.integrated.persistentSessionReviveProcess" = "onExitAndWindowClose"
        }
        
        New-Item -Path "$PWD\.vscode" -ItemType Directory -Force | Out-Null
        $newVSCodeSettings | ConvertTo-Json -Depth 10 | Set-Content $vscodeSettingsPath -Encoding UTF8
        Write-Information "✅ Created VS Code settings with terminal persistence" -InformationAction Continue
    }
}

# Create terminal persistence test script
$testScript = @'
# Terminal Persistence Test Script
Write-Output "🚌 Testing BusBuddy Terminal Persistence Features"
Write-Output "Current Session: $(Get-Date)"
Write-Output "Working Directory: $(Get-Location)"
Write-Output "Available Commands: session-state, save-session, restore-session"

# Test key bindings
Write-Output "`nTesting Key Bindings:"
Write-Output "  Ctrl+R: Reverse search history"
Write-Output "  Ctrl+Shift+B: BusBuddy command picker"
Write-Output "  Tab: Enhanced completion"
Write-Output "  Up/Down: History search"

# Test persistence
session-state
'@

$testScript | Out-File -FilePath "$PWD\PowerShell\Test-TerminalPersistence.ps1" -Encoding UTF8
Write-Information "✅ Created terminal persistence test script" -InformationAction Continue

Write-Information "📊 Terminal Persistence Configuration Summary:" -InformationAction Continue
Write-Information "  Advanced Features: $($EnableAdvancedFeatures ? 'Enabled' : 'Disabled')" -InformationAction Continue
Write-Information "  Windows Terminal: $($ConfigureWindowsTerminal ? 'Configured' : 'Skipped')" -InformationAction Continue
Write-Information "  VS Code Integration: $($SetupVSCodeIntegration ? 'Configured' : 'Skipped')" -InformationAction Continue
Write-Information "  Test Script: PowerShell\Test-TerminalPersistence.ps1" -InformationAction Continue

Write-Information "🎯 To test terminal persistence features, run: .\PowerShell\Test-TerminalPersistence.ps1" -InformationAction Continue
Write-Information "🔄 Restart terminal or reload profile to activate all features: . `$PROFILE" -InformationAction Continue
