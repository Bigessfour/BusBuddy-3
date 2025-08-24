#requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy PowerShell Profile - Module-Based Architecture
    Following Microsoft PowerShell Best Practices

.DESCRIPTION
    Modern PowerShell profile using proper module loading instead of inline functions.
    Implements Microsoft-recommended patterns for performance and maintainability.

.NOTES
    Author: BusBuddy Development Team
    Version: 2.0.0 (Module-Based Architecture)
    PowerShell: 7.5.2+
    Architecture: Module loading over alias definitions
#>

# Microsoft PowerShell Global Variable Best Practice:
# Initialize global variables with proper existence checks to prevent "variable not set" errors
# Reference: https://learn.microsoft.com/en-us/powershell/scripting/lang-spec/chapter-03#35-scopes

# Initialize profile loading state using Microsoft-documented safe pattern
# Check if profile is already loaded in THIS PowerShell session (using PID to ensure per-session tracking)
$sessionKey = "BusBuddyProfileLoaded_$PID"
if (-not (Get-Variable -Name $sessionKey -Scope Global -ErrorAction SilentlyContinue)) {
    # Variable doesn't exist for this session, create it
    New-Variable -Name $sessionKey -Value $false -Scope Global
}

# Prevent multiple loads within the same PowerShell session (Microsoft best practice)
if ((Get-Variable -Name $sessionKey -Scope Global).Value) {
    Write-Information "BusBuddy profile already loaded in this session (PID: $PID), skipping reload" -InformationAction Continue
    return
}

# Initialize global variables and defaults (Microsoft pattern)
$global:LASTEXITCODE = 0
$global:ErrorActionPreference = 'Continue'
$global:PSDefaultParameterValues = @{}
$global:Error.Clear()
$global:ErrorActionPreference = 'Continue'
$global:PSDefaultParameterValues = @{}
$global:Error.Clear()

# Profile state tracking (Microsoft recommended pattern)
$stateKey = "BusBuddyProfileState_$PID"
if (-not (Get-Variable -Name $stateKey -Scope Global -ErrorAction SilentlyContinue)) {
    New-Variable -Name $stateKey -Value ([ordered]@{
        StartTime = Get-Date
        Version = '2.0.0'
        Architecture = 'Module-Based'
        LoadedModules = @()
        VSCodeDetected = $false
        ModulesPath = ''
        LoadingErrors = @()
        SessionPID = $PID
    }) -Scope Global
} else {
    # Reset for reload scenario
    $profileState = Get-Variable -Name $stateKey -Scope Global
    $profileState.Value.StartTime = Get-Date
    $profileState.Value.LoadedModules = @()
    $profileState.Value.LoadingErrors = @()
}

# Mark profile as loaded for this session using Microsoft documented pattern
Set-Variable -Name $sessionKey -Value $true -Scope Global

Write-Information "Loading BusBuddy Profile v2.0.0 (Module-Based)" -InformationAction Continue

try {
    # Get the session-specific profile state
    $profileState = (Get-Variable -Name $stateKey -Scope Global).Value

    # Create compatibility alias for modules that might reference the old global variable name
    # This ensures backward compatibility with existing modules
    if (-not (Get-Variable -Name 'BusBuddyProfileState' -Scope Global -ErrorAction SilentlyContinue)) {
        Set-Variable -Name 'BusBuddyProfileState' -Value $profileState -Scope Global
    } else {
        Set-Variable -Name 'BusBuddyProfileState' -Value $profileState -Scope Global -Force
    }

    # Determine workspace path (Microsoft pattern)
    $workspaceRoot = if ($PWD.Path -like "*BusBuddy*") {
        $PWD.Path.Split('BusBuddy')[0] + 'BusBuddy'
    } else {
        'C:\Users\biges\Desktop\BusBuddy'
    }    # Set modules path
    $profileState.ModulesPath = Join-Path $workspaceRoot 'PowerShell\Modules'

    # Verify modules directory exists
    if (-not (Test-Path $profileState.ModulesPath)) {
        Write-Warning "BusBuddy modules directory not found: $($profileState.ModulesPath)"
        return
    }

    # Configure PSDefaultParameterValues for consistent module loading (Microsoft best practice)
    $global:PSDefaultParameterValues = @{
        'Import-Module:ErrorAction' = 'Stop'
        'Import-Module:WarningAction' = 'SilentlyContinue'
        'Import-Module:Force' = $true
        'Import-Module:PassThru' = $true
    }

    # Detect VS Code (Microsoft pattern)
    $profileState.VSCodeDetected = $null -ne $env:VSCODE_PID -or
                                   $null -ne (Get-Process 'Code' -ErrorAction SilentlyContinue) -or
                                   $null -ne (Get-Process 'Code - Insiders' -ErrorAction SilentlyContinue)

    Write-Information "VS Code Detected: $($profileState.VSCodeDetected)" -InformationAction Continue

    # Create compatibility alias for modules that expect $global:BusBuddyProfileState
    # This allows existing modules to work without modification
    if (-not (Get-Variable -Name 'BusBuddyProfileState' -Scope Global -ErrorAction SilentlyContinue)) {
        New-Variable -Name 'BusBuddyProfileState' -Value $profileState -Scope Global
    } else {
        Set-Variable -Name 'BusBuddyProfileState' -Value $profileState -Scope Global
    }

    # Core BusBuddy modules (Microsoft recommended loading order)
    $coreModules = @(
        'BusBuddy-Core',
        'BusBuddy-DependencyManagement',
        'BusBuddy-GitWorkflow',
        'BusBuddy-HardwareDetection',
        'BusBuddy-LazyLoader',
        'BusBuddy-Advanced',
        'BusBuddy-CIAnalysis',
        'BusBuddy-GrokAssistant'
    )    # Load core modules with error handling (Microsoft pattern)
    foreach ($moduleName in $coreModules) {
        try {
            $modulePath = Join-Path $profileState.ModulesPath "$moduleName.psm1"

            if (Test-Path $modulePath) {
                Write-Verbose "Loading module: $moduleName" -Verbose

                $module = Import-Module $modulePath

                if ($module) {
                    $profileState.LoadedModules += $moduleName
                    Write-Information "✅ Loaded: $moduleName" -InformationAction Continue
                } else {
                    Write-Warning "⚠️  Module loaded but no return object: $moduleName"
                }
            } else {
                Write-Warning "⚠️  Module file not found: $modulePath"
            }
        }
        catch {
                $err = $Error[0]
                $errorInfo = @{
                    Module = $moduleName
                    Error = $err.Exception.Message
                    Line = $err.InvocationInfo.ScriptLineNumber
                }
                $profileState.LoadingErrors += $errorInfo
                Write-Error "❌ Failed to load $moduleName`: $($err.Exception.Message)"
        }
    }

    # Load root-level modules if they exist (backward compatibility)
    $rootModules = @(
        'BusBuddy-Development',
        'BusBuddy-ProfileIntegration'
    )

    foreach ($rootModule in $rootModules) {
        try {
            $rootModulePath = Join-Path $workspaceRoot "$rootModule.psm1"

            if (Test-Path $rootModulePath) {
                Write-Verbose "Loading root module: $rootModule" -Verbose

                $module = Import-Module $rootModulePath

                if ($module) {
                    $profileState.LoadedModules += $rootModule
                    Write-Information "✅ Loaded root module: $rootModule" -InformationAction Continue
                }
            }
        }
        catch {
                $err = $Error[0]
                Write-Warning "⚠️  Failed to load root module $rootModule`: $($err.Exception.Message)"
        }
    }

    # Final profile state
    $loadTime = (Get-Date) - $profileState.StartTime
    $loadedCount = $profileState.LoadedModules.Count
    $errorCount = $profileState.LoadingErrors.Count

    Write-Information "Profile Load Complete:" -InformationAction Continue
    Write-Information "  📦 Modules Loaded: $loadedCount" -InformationAction Continue
    Write-Information "  ⏱️  Load Time: $($loadTime.TotalMilliseconds)ms" -InformationAction Continue
    Write-Information "  ❌ Errors: $errorCount" -InformationAction Continue

    if ($errorCount -gt 0) {
        Write-Warning "Module loading errors occurred. Use `$(Get-Variable -Name $stateKey -Scope Global).Value.LoadingErrors` to review."
    }

    # Display available commands (Microsoft best practice)
    Write-Information "Available commands start with 'bb-' prefix. Use Get-Command bb-* to list all." -InformationAction Continue

    # Performance tracking for optimization
    $profileState.LoadComplete = Get-Date
    $profileState.TotalLoadTime = $loadTime

} catch {
    $err = $Error[0]
    Write-Error "Critical error in BusBuddy profile loading: $($err.Exception.Message)"
    Write-Error "Line: $($err.InvocationInfo.ScriptLineNumber)"
    Write-Error "Profile loading halted."
}

# Ensure a persistent report variable exists and persist it to the user environment
# This avoids "variable not set" errors and makes the report available across sessions.
try {
    $reportKey = "report_$PID"
    if (-not (Get-Variable -Name $reportKey -Scope Global -ErrorAction SilentlyContinue)) {
        $reportObj = [ordered]@{
            ModulesLoaded = $profileState.LoadedModules
            LoadTimeMs    = [math]::Round($profileState.TotalLoadTime.TotalMilliseconds, 2)
            ErrorCount    = $profileState.LoadingErrors.Count
            Errors        = $profileState.LoadingErrors
            Timestamp     = $profileState.LoadComplete
            Version       = $profileState.Version
            SessionPID    = $PID
        }

        New-Variable -Name $reportKey -Value ($reportObj | ConvertTo-Json -Depth 5) -Scope Global
    }

    # Persist to user environment variables (persistent across sessions)
    $reportContent = (Get-Variable -Name $reportKey -Scope Global).Value
    [Environment]::SetEnvironmentVariable('BusBuddyProfileReport', $reportContent, [System.EnvironmentVariableTarget]::User)
    Write-Information "Persisted BusBuddyProfileReport to user environment variables." -InformationAction Continue
}
catch {
    $err = $Error[0]
    Write-Warning "Failed to initialize/persist BusBuddyProfileReport: $($err.Exception.Message)"
}

# Clean up temporary variables (Microsoft best practice)
Remove-Variable -Name workspaceRoot, coreModules, rootModules, loadTime, loadedCount, errorCount, sessionKey, stateKey, profileState, reportKey -ErrorAction SilentlyContinue
