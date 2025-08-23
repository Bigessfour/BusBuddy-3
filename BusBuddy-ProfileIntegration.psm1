#Requires -Version 7.5

<#
.SYNOPSIS
BusBuddy-ProfileIntegration PowerShell Module - Streamlined Profile Loading and Module Management

.DESCRIPTION
Streamlined profile integration module for BusBuddy-3 that replaces the complex 1969-line
profile with a clean, modular approach. Handles module loading, environment setup, and
VS Code integration following Microsoft PowerShell best practices.

.NOTES
File Name      : BusBuddy-ProfileIntegration.psm1
Version        : 3.0.0
Author         : BusBuddy Development Team
Created        : August 22, 2025
Requires       : PowerShell 7.5+
Module Type    : Script Module
Dependencies   : BusBuddy-HardwareDetection, BusBuddy-Development
Compatibility  : Windows, VS Code PowerShell Extension

.LINK
https://learn.microsoft.com/en-us/powershell/scripting/developer/module/writing-a-powershell-script-module

.EXAMPLE
Import-Module BusBuddy-ProfileIntegration
Initialize-BusBuddyProfile

.EXAMPLE
Get-BusBuddyProfileStatus | Format-Table
#>

#region Module Initialization
# Microsoft recommended module-scoped variables
$script:ModuleName = 'BusBuddy-ProfileIntegration'
$script:ModuleVersion = '3.0.0'
$script:ProfileStartTime = Get-Date

# Initialize global profile state with thread-safe collections
if (-not $global:BusBuddyProfileState) {
    $global:BusBuddyProfileState = [PSCustomObject]@{
        ProfilePath = $PSCommandPath ?? $MyInvocation.MyCommand.Path
        LoadTime = $null
        LoadDuration = $null
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        SessionId = [System.Guid]::NewGuid().ToString()
        VSCodeMode = $env:VSCODE_PID -or $env:TERM_PROGRAM -eq 'vscode'
        ModulesLoaded = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
        ErrorCount = 0
        LastError = $null
        TrunkAvailable = $false
        PSReadLineAvailable = $false
        EnhancementsLoaded = @()
        ShowWelcome = $true
        Loaded = $false
        ParallelCapabilities = @{
            OptimalThrottleLimit = 8
            MaxConcurrency = [System.Environment]::ProcessorCount
            UseParallelProcessing = [System.Environment]::ProcessorCount -ge 4
        }
        VSCodeInfo = @{
            IsVSCode = $false
            HasPowerShellExtension = $false
            TerminalType = 'Unknown'
            IntegratedConsole = $false
            ExtensionVersion = $null
            DebugMode = $false
        }
        Terminal = @{
            Width = 120
            Height = 30
            Type = 'Unknown'
            SupportsColors = $true
        }
    }
}
#endregion Module Initialization

#region Core Profile Functions

function Initialize-BusBuddyProfile {
    <#
    .SYNOPSIS
    Initializes the complete BusBuddy PowerShell profile with modular architecture

    .DESCRIPTION
    Microsoft recommended profile initialization pattern that loads all BusBuddy modules
    and configures the development environment in the correct order.

    .PARAMETER SkipWelcome
    Skip the welcome banner display

    .PARAMETER SkipModuleLoad
    Skip automatic module loading

    .PARAMETER LoadAsync
    Load non-essential modules asynchronously

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Profile initialization result

    .EXAMPLE
    Initialize-BusBuddyProfile

    .EXAMPLE
    Initialize-BusBuddyProfile -SkipWelcome -LoadAsync

    .NOTES
    This replaces the 1969-line embedded profile with clean modular loading
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$SkipWelcome,

        [Parameter()]
        [switch]$SkipModuleLoad,

        [Parameter()]
        [switch]$LoadAsync
    )

    $initResult = [PSCustomObject]@{
        StartTime = $script:ProfileStartTime
        EndTime = $null
        Duration = $null
        ModulesLoaded = @()
        ModulesSkipped = @()
        Success = $false
        Environment = @{}
        VSCodeInfo = @{}
        HardwareInfo = @{}
        Errors = @()
    }

    try {
        Write-Information "🚀 Initializing BusBuddy-3 modular development environment..." -InformationAction Continue

        # Step 1: VS Code and Terminal Detection
        Write-Verbose "Detecting VS Code environment and terminal capabilities..."
        $vsCodeInfo = Test-VSCodePowerShellExtension
        $global:BusBuddyProfileState.VSCodeInfo = $vsCodeInfo
        $initResult.VSCodeInfo = $vsCodeInfo

        # Step 2: Hardware Detection and Environment Variables (Essential)
        if (-not $SkipModuleLoad) {
            Write-Information "🔧 Loading hardware detection module..." -InformationAction Continue
            try {
                Import-Module BusBuddy-HardwareDetection -Force -ErrorAction Stop
                $global:BusBuddyProfileState.ModulesLoaded.Add('BusBuddy-HardwareDetection')
                $initResult.ModulesLoaded += 'BusBuddy-HardwareDetection'

                # Update environment variables with fresh hardware detection
                $hardwareInfo = Update-BusBuddyEnvironmentVariables
                $initResult.HardwareInfo = $hardwareInfo

                Write-Information "✅ Hardware detection: $($env:BUSBUDDY_LOGICAL_CORES) cores, $($env:BUSBUDDY_MEMORY_GB)GB RAM" -InformationAction Continue
            }
            catch {
                $initResult.Errors += "Hardware detection module failed: $($_.Exception.Message)"
                $initResult.ModulesSkipped += 'BusBuddy-HardwareDetection'
                Write-Warning "⚠️ Hardware detection module not available - using defaults"
            }

            # Step 3: Development Workflow Module (Essential)
            Write-Information "⚙️ Loading development workflow module..." -InformationAction Continue
            try {
                Import-Module BusBuddy-Development -Force -ErrorAction Stop
                $global:BusBuddyProfileState.ModulesLoaded.Add('BusBuddy-Development')
                $initResult.ModulesLoaded += 'BusBuddy-Development'

                Write-Information "✅ Development commands: bb-run, bb-health, bb-build, bb-test available" -InformationAction Continue
            }
            catch {
                $initResult.Errors += "Development module failed: $($_.Exception.Message)"
                $initResult.ModulesSkipped += 'BusBuddy-Development'
                Write-Warning "⚠️ Development workflow module not available"
            }

            # Step 4: Core Environment Setup
            Write-Verbose "Configuring BusBuddy environment variables..."
            try {
                Set-BusBuddyEnvironment
                $initResult.Environment = @{
                    BusBuddyRoot = $env:BUSBUDDY_ROOT
                    DotNetVersion = $env:DOTNET_VERSION
                    BuildConfiguration = $env:BUILD_CONFIGURATION
                    LogicalCores = $env:BUSBUDDY_LOGICAL_CORES
                    MaxParallelJobs = $env:BUSBUDDY_MAX_PARALLEL_JOBS
                }
            }
            catch {
                $initResult.Errors += "Environment setup failed: $($_.Exception.Message)"
                Write-Warning "⚠️ Environment setup partially failed"
            }

            # Step 5: Enhanced PSReadLine Configuration (Microsoft Pattern)
            Write-Verbose "Configuring enhanced PSReadLine following Microsoft best practices..."
            try {
                $psReadLineResult = Set-BusBuddyPSReadLineEnhanced
                if ($psReadLineResult.Success) {
                    $global:BusBuddyProfileState.EnhancementsLoaded += 'PSReadLine'
                    Write-Information "✅ Enhanced PSReadLine configured with predictions and custom key bindings" -InformationAction Continue
                } else {
                    $initResult.Errors += $psReadLineResult.Errors
                }
            }
            catch {
                $initResult.Errors += "Enhanced PSReadLine configuration failed: $($_.Exception.Message)"
                Write-Verbose "Enhanced PSReadLine configuration failed - trying basic configuration"
                try {
                    Set-BusBuddyPSReadLineConfig
                }
                catch {
                    Write-Verbose "Basic PSReadLine configuration also failed"
                }
            }

            # Step 6: Trunk.io Integration for Code Quality
            Write-Verbose "Initializing Trunk.io integration for code formatting and linting..."
            try {
                $trunkResult = Initialize-BusBuddyTrunkIntegration
                if ($trunkResult.Success) {
                    $global:BusBuddyProfileState.EnhancementsLoaded += 'Trunk'
                    Write-Information "✅ Trunk.io integration: bb-format, bb-lint, bb-fix commands available" -InformationAction Continue
                }
            }
            catch {
                $initResult.Errors += "Trunk integration failed: $($_.Exception.Message)"
                Write-Verbose "Trunk.io integration failed - continuing without code quality tools"
            }

            # Step 7: Enhanced Tab Completion (Microsoft Pattern)
            Write-Verbose "Setting up enhanced argument completers..."
            try {
                $completerResult = Add-BusBuddyArgumentCompleters
                if ($completerResult.Success) {
                    $global:BusBuddyProfileState.EnhancementsLoaded += 'Completers'
                    Write-Verbose "Argument completers registered for: $($completerResult.Completers -join ', ')"
                }
            }
            catch {
                $initResult.Errors += "Argument completer setup failed: $($_.Exception.Message)"
                Write-Verbose "Argument completer setup failed"
            }

            # Step 8: VS Code Extension Integration
            Write-Verbose "Initializing VS Code extension integration..."
            try {
                $extensionResult = Initialize-BusBuddyVSCodeExtensionIntegration
                if ($extensionResult.Success) {
                    $global:BusBuddyProfileState.EnhancementsLoaded += 'VSCode Extensions'
                    if ($extensionResult.IntegrationsEnabled.Count -gt 0) {
                        Write-Information "✅ VS Code integrations: $($extensionResult.IntegrationsEnabled -join ', ')" -InformationAction Continue
                    }
                }
            }
            catch {
                $initResult.Errors += "VS Code extension integration failed: $($_.Exception.Message)"
                Write-Verbose "VS Code extension integration failed"
            }

            # Step 9: xAI Grok-4 AI Integration
            Write-Verbose "Initializing xAI Grok-4 AI integration..."
            try {
                $grokResult = Initialize-BusBuddyGrokIntegration
                if ($grokResult.Success) {
                    $global:BusBuddyProfileState.EnhancementsLoaded += 'xAI Grok-4'
                    Write-Information "✅ xAI Grok-4 AI: bb-ai-chat, bb-ai-route, bb-ai-review commands available" -InformationAction Continue
                    if ($grokResult.BusBuddyServiceAvailable) {
                        Write-Information "🔗 BusBuddy GrokGlobalAPI service integration detected" -InformationAction Continue
                    }
                } else {
                    Write-Verbose "xAI Grok-4 integration not configured (missing API key)"
                }
            }
            catch {
                $initResult.Errors += "xAI Grok integration failed: $($_.Exception.Message)"
                Write-Verbose "xAI Grok integration failed"
            }

            # Step 10: Optional Module Loading (can be async)
            if ($LoadAsync) {
                Write-Information "📦 Starting asynchronous optional module loading..." -InformationAction Continue
                Start-Job -Name "BusBuddy-OptionalModules" -ScriptBlock {
                    # Load extended development modules in background
                    try {
                        $optionalModules = @(
                            'Microsoft.PowerShell.ConsoleGuiTools',
                            'PSScriptAnalyzer'
                        )

                        foreach ($module in $optionalModules) {
                            try {
                                Import-Module $module -Force -ErrorAction SilentlyContinue
                                Write-Information "✅ Optional module loaded: $module" -InformationAction Continue
                            }
                            catch {
                                Write-Verbose "Optional module $module not available"
                            }
                        }
                    }
                    catch {
                        Write-Verbose "Async module loading encountered issues"
                    }
                } | Out-Null
            }
        }

        # Step 11: Welcome Display
        if (-not $SkipWelcome -and $global:BusBuddyProfileState.ShowWelcome) {
            Show-BusBuddyWelcome
        }

        # Finalize initialization
        $initResult.EndTime = Get-Date
        $initResult.Duration = $initResult.EndTime - $initResult.StartTime
        $initResult.Success = $initResult.Errors.Count -eq 0

        $global:BusBuddyProfileState.LoadTime = $initResult.EndTime
        $global:BusBuddyProfileState.LoadDuration = $initResult.Duration
        $global:BusBuddyProfileState.Loaded = $true
        $global:BusBuddyProfileState.ErrorCount = $initResult.Errors.Count

        Write-Information "🎉 BusBuddy profile initialized in $([math]::Round($initResult.Duration.TotalMilliseconds))ms" -InformationAction Continue
        Write-Information "💡 Type 'Get-Command bb-*' to see all available development commands" -InformationAction Continue

        return $initResult
    }
    catch {
        $initResult.EndTime = Get-Date
        $initResult.Duration = $initResult.EndTime - $initResult.StartTime
        $initResult.Errors += $_.Exception.Message
        $initResult.Success = $false

        Write-Error "Profile initialization failed: $($_.Exception.Message)"
        return $initResult
    }
}

function Test-VSCodePowerShellExtension {
    <#
    .SYNOPSIS
    Detects VS Code PowerShell extension environment and capabilities

    .DESCRIPTION
    Microsoft recommended VS Code environment detection following official documentation
    patterns for PowerShell extension integration.

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] VS Code environment information

    .EXAMPLE
    Test-VSCodePowerShellExtension

    .NOTES
    Based on VS Code PowerShell extension documentation
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    # Initialize with default values
    $extensionInfo = [PSCustomObject]@{
        IsVSCode = $false
        HasPowerShellExtension = $false
        TerminalType = 'External'
        IntegratedConsole = $false
        ExtensionVersion = $null
        DebugMode = $false
    }

    # VS Code detection based on Microsoft documentation
    $extensionInfo.IsVSCode = $null -ne $env:VSCODE_PID -or
                             $env:TERM_PROGRAM -eq 'vscode' -or
                             $Host.Name -eq 'Visual Studio Code Host'

    if ($extensionInfo.IsVSCode) {
        # PowerShell Extension detection
        $extensionInfo.HasPowerShellExtension = $Host.Name -eq 'Visual Studio Code Host' -or
                                              $null -ne $env:VSCODE_POWERSHELL_EXTENSION_VERSION

        # Terminal type detection based on VS Code documentation
        $extensionInfo.TerminalType = if ($env:TERM_PROGRAM -eq 'vscode') {
            'Integrated Terminal'
        } elseif ($Host.Name -eq 'Visual Studio Code Host') {
            'Extension Host'
        } else {
            'External Terminal'
        }

        # Integrated console detection (PowerShell extension specific)
        $extensionInfo.IntegratedConsole = $Host.Name -eq 'Visual Studio Code Host'

        # Debug mode detection
        $extensionInfo.DebugMode = $env:VSCODE_DEBUG_MODE -eq '1' -or $env:VSCODE_DEBUGGING -eq '1'

        # Extension version detection (if available)
        try {
            $psExtension = Get-Module -ListAvailable | Where-Object { $_.Name -like '*PowerShellEditorServices*' } | Select-Object -First 1
            if ($psExtension) {
                $extensionInfo.ExtensionVersion = $psExtension.Version.ToString()
            }
        } catch {
            # Ignore errors in extension version detection
        }
    }

    return $extensionInfo
}

function Set-BusBuddyEnvironment {
    <#
    .SYNOPSIS
    Configures BusBuddy-3 development environment variables

    .DESCRIPTION
    Microsoft recommended environment configuration pattern with VS Code integration
    and hardware-optimized settings.

    .INPUTS
    None

    .OUTPUTS
    None

    .EXAMPLE
    Set-BusBuddyEnvironment

    .NOTES
    Based on Microsoft .NET development environment best practices
    #>
    [CmdletBinding()]
    param()

    try {
        # Microsoft recommended workspace detection
        $workspaceRoot = if ($env:BUSBUDDY_ROOT) {
            $env:BUSBUDDY_ROOT
        } elseif (Test-Path 'BusBuddy.sln') {
            $PWD.Path
        } else {
            (Get-Location).Path
        }

        # Core application environment
        $env:BUSBUDDY_ROOT = $workspaceRoot
        $env:BUSBUDDY_VERSION = '3.0.0'
        $env:BUSBUDDY_ENVIRONMENT = if ($env:COMPUTERNAME -match 'PROD|PRODUCTION') { 'Production' } else { 'Development' }

        # Microsoft recommended .NET configuration
        if (-not $env:DOTNET_VERSION -or $env:BUSBUDDY_INHERIT_ENV) {
            $env:DOTNET_VERSION = '9.0.303'
            $env:DOTNET_TARGET_FRAMEWORK = 'net9.0-windows'
            $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
            $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
            $env:DOTNET_NOLOGO = '1'
            $env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
        }

        # Microsoft recommended PowerShell configuration
        if (-not $env:POWERSHELL_TELEMETRY_OPTOUT) {
            $env:POWERSHELL_TELEMETRY_OPTOUT = '1'
            $env:POWERSHELL_UPDATECHECK = 'Off'
            $env:POWERSHELL_UPDATECHECK_OPTOUT = '1'
        }

        # Application-specific paths following Microsoft conventions
        $env:BUSBUDDY_LOGS_PATH = Join-Path $workspaceRoot 'logs'
        $env:BUSBUDDY_CONFIG_PATH = Join-Path $workspaceRoot 'appsettings.json'
        $env:BUSBUDDY_MODULES_PATH = Join-Path $workspaceRoot 'PowerShell\Modules'

        # Development and debugging
        $env:BUSBUDDY_DEV_MODE = '1'
        $env:BUSBUDDY_DEBUG_ENABLED = '1'
        $env:BUSBUDDY_VERBOSE_LOGGING = '1'
        $env:BUSBUDDY_PERFORMANCE_PROFILE = 'Optimized'

        # Database and authentication configuration
        $env:BUSBUDDY_DB_PROVIDER = 'Azure'
        $env:BUSBUDDY_AUTH_METHOD = 'EntraID'

        # Build configuration
        $env:SOLUTION_FILE = 'BusBuddy.sln'
        $env:BUILD_CONFIGURATION = 'Debug'
        $env:TEST_CONFIGURATION = 'Debug'

        # Git integration with error handling
        try {
            $env:BUSBUDDY_GIT_BRANCH = git branch --show-current 2>$null
            if ($LASTEXITCODE -ne 0) { $env:BUSBUDDY_GIT_BRANCH = 'unknown' }
        } catch {
            $env:BUSBUDDY_GIT_BRANCH = 'unknown'
        }

        $env:BUSBUDDY_PROFILE_LOADED = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')

        Write-Verbose "✅ BusBuddy-3 environment configured successfully"
    }
    catch {
        Write-Error "Environment setup failed: $($_.Exception.Message)"
        throw
    }
}

function Set-BusBuddyPSReadLineConfig {
    <#
    .SYNOPSIS
    Configures PSReadLine for optimal BusBuddy development experience

    .DESCRIPTION
    Microsoft recommended PSReadLine configuration with VS Code PowerShell extension
    integration and development-optimized settings.

    .INPUTS
    None

    .OUTPUTS
    None

    .EXAMPLE
    Set-BusBuddyPSReadLineConfig

    .NOTES
    Based on Microsoft PSReadLine documentation and VS Code integration best practices
    #>
    [CmdletBinding()]
    param()

    if (Get-Module -Name PSReadLine -ErrorAction SilentlyContinue) {
        try {
            # Microsoft and VS Code recommended PSReadLine configuration
            Set-PSReadLineOption -PredictionSource HistoryAndPlugin
            Set-PSReadLineOption -PredictionViewStyle ListView
            Set-PSReadLineOption -EditMode Windows
            Set-PSReadLineOption -BellStyle None
            Set-PSReadLineOption -HistorySearchCursorMovesToEnd
            Set-PSReadLineOption -MaximumHistoryCount 32767
            Set-PSReadLineOption -ShowToolTips
            Set-PSReadLineOption -HistoryNoDuplicates

            # VS Code specific optimizations
            if ($global:BusBuddyProfileState.VSCodeInfo.HasPowerShellExtension) {
                Set-PSReadLineOption -PredictionViewStyle ListView
                Set-PSReadLineOption -MaximumKillRingCount 10

                # VS Code optimized key bindings
                Set-PSReadLineKeyHandler -Key Ctrl+Shift+j -Function AcceptNextSuggestionWord
                Set-PSReadLineKeyHandler -Key Ctrl+f -Function AcceptSuggestion
                Set-PSReadLineKeyHandler -Key F1 -Function ShowCommandHelp
            }

            # Enhanced history filtering for development
            Set-PSReadLineOption -AddToHistoryHandler {
                param([string]$line)
                return $line.Length -gt 3 -and
                       $line[0] -ne ' ' -and
                       $line -notmatch '^(exit|quit|history|cls|clear|code)$' -and
                       $line -notmatch '^\s*#'  # Exclude comments
            }

            # Microsoft recommended key bindings for PowerShell 7.5
            Set-PSReadLineKeyHandler -Key Ctrl+d -Function DeleteChar
            Set-PSReadLineKeyHandler -Key Ctrl+w -Function BackwardDeleteWord
            Set-PSReadLineKeyHandler -Key Alt+d -Function DeleteWord
            Set-PSReadLineKeyHandler -Key Ctrl+LeftArrow -Function BackwardWord
            Set-PSReadLineKeyHandler -Key Ctrl+RightArrow -Function NextWord
            Set-PSReadLineKeyHandler -Key UpArrow -Function HistorySearchBackward
            Set-PSReadLineKeyHandler -Key DownArrow -Function HistorySearchForward
            Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete
            Set-PSReadLineKeyHandler -Key Shift+Tab -Function TabCompletePrevious

            # Microsoft recommended color scheme for accessibility
            Set-PSReadLineOption -Colors @{
                Command = [ConsoleColor]::Green
                Parameter = [ConsoleColor]::Gray
                Operator = [ConsoleColor]::Magenta
                Variable = [ConsoleColor]::Yellow
                String = [ConsoleColor]::Blue
                Number = [ConsoleColor]::Red
                Type = [ConsoleColor]::Cyan
                Comment = [ConsoleColor]::DarkGreen
                Keyword = [ConsoleColor]::DarkBlue
                Error = [ConsoleColor]::DarkRed
                Selection = [ConsoleColor]::DarkGray
                Emphasis = [ConsoleColor]::Cyan
                InlinePrediction = [ConsoleColor]::DarkGray
                ListPrediction = [ConsoleColor]::Yellow
                ListPredictionSelected = [ConsoleColor]::DarkYellow
            }

            Write-Verbose "✅ PSReadLine configured for enhanced command line editing"
        }
        catch {
            Write-Warning "PSReadLine configuration failed: $($_.Exception.Message)"
        }
    }
}

function Show-BusBuddyWelcome {
    <#
    .SYNOPSIS
    Displays the BusBuddy-3 welcome banner with environment information

    .DESCRIPTION
    Microsoft recommended welcome display pattern with comprehensive system information
    and available commands overview.

    .INPUTS
    None

    .OUTPUTS
    None

    .EXAMPLE
    Show-BusBuddyWelcome

    .NOTES
    Based on Microsoft PowerShell profile best practices
    #>
    [CmdletBinding()]
    param()

    if ($global:BusBuddyProfileState.ShowWelcome) {
        try {
            $hardwareInfo = if (Get-Command Get-BusBuddyHardwareInfo -ErrorAction SilentlyContinue) {
                Get-BusBuddyHardwareInfo
            } else {
                @{
                    TotalMemoryGB = $env:BUSBUDDY_MEMORY_GB ?? 'Unknown'
                    LogicalProcessors = $env:BUSBUDDY_LOGICAL_CORES ?? 'Unknown'
                    ProcessorName = 'Unknown'
                }
            }

            $loadDuration = $global:BusBuddyProfileState.LoadDuration?.TotalMilliseconds ?? 0
            $terminalInfo = $global:BusBuddyProfileState.Terminal
            $vsInfo = $global:BusBuddyProfileState.VSCodeInfo

            $banner = @"
        ┌─────────────────────────────────────────────────────────────────┐
        │  🚌  BusBuddy-3 Modular PowerShell Development Environment      │
        ├─────────────────────────────────────────────────────────────────┤
        │  .NET 9.0 │ WPF │ Syncfusion │ Azure SQL │ Microsoft Entra ID   │
        ├─────────────────────────────────────────────────────────────────┤
        │  Project: $($env:BUSBUDDY_ROOT)
        │  Version: $($env:BUSBUDDY_VERSION) │ Branch: $($env:BUSBUDDY_GIT_BRANCH)
        │  PowerShell: $($PSVersionTable.PSVersion) │ Host: $($Host.Name)
        ├─────────────────────────────────────────────────────────────────┤
        │  Terminal: $($vsInfo.TerminalType) │ $($terminalInfo.Width)x$($terminalInfo.Height) │ Colors: $($terminalInfo.SupportsColors)
        │  VS Code: $($vsInfo.IsVSCode) │ Extension: $($vsInfo.HasPowerShellExtension) │ Version: $($vsInfo.ExtensionVersion ?? 'N/A')
        ├─────────────────────────────────────────────────────────────────┤
        │  Hardware: $($hardwareInfo.TotalMemoryGB)GB RAM │ $($hardwareInfo.LogicalProcessors) cores │ $($hardwareInfo.ProcessorName)
        │  Profile Load: $([math]::Round($loadDuration, 1))ms │ Modules: $($global:BusBuddyProfileState.ModulesLoaded.Count)
        ├─────────────────────────────────────────────────────────────────┤
        │  Development: bb-run │ bb-build │ bb-test │ bb-health │ bb-clean   │
        │  Information: bb-info │ bb-deps-check │ bb-profile-health          │
        │  VS Code:     bb-code │ code-busbuddy │ bb-vscode                  │
        │  Hardware:    Get-BusBuddyHardwareInfo │ Update-BusBuddyEnvironment │
        └─────────────────────────────────────────────────────────────────┘
"@
            Write-Information $banner -InformationAction Continue
            $global:BusBuddyProfileState.ShowWelcome = $false
        }
        catch {
            Write-Warning "Welcome banner display failed: $($_.Exception.Message)"
        }
    }
}

function Get-BusBuddyProfileStatus {
    <#
    .SYNOPSIS
    Gets comprehensive status of the BusBuddy profile and loaded modules

    .DESCRIPTION
    Microsoft recommended profile diagnostics pattern that provides detailed information
    about profile loading, module status, and environment configuration.

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Comprehensive profile status information

    .EXAMPLE
    Get-BusBuddyProfileStatus

    .EXAMPLE
    Get-BusBuddyProfileStatus | Format-Table

    .NOTES
    Replacement for the complex profile health function in the original 1969-line profile
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $profileStatus = [PSCustomObject]@{
        ProfilePath = $global:BusBuddyProfileState.ProfilePath
        LoadTime = $global:BusBuddyProfileState.LoadTime
        LoadDuration = $global:BusBuddyProfileState.LoadDuration
        PowerShellVersion = $global:BusBuddyProfileState.PowerShellVersion
        SessionId = $global:BusBuddyProfileState.SessionId
        VSCodeMode = $global:BusBuddyProfileState.VSCodeMode
        ModulesLoaded = @($global:BusBuddyProfileState.ModulesLoaded)
        ModuleCount = $global:BusBuddyProfileState.ModulesLoaded.Count
        ErrorCount = $global:BusBuddyProfileState.ErrorCount
        LastError = $global:BusBuddyProfileState.LastError
        Loaded = $global:BusBuddyProfileState.Loaded
        VSCodeInfo = $global:BusBuddyProfileState.VSCodeInfo
        ParallelCapabilities = $global:BusBuddyProfileState.ParallelCapabilities
        Environment = @{
            BusBuddyRoot = $env:BUSBUDDY_ROOT
            DotNetVersion = $env:DOTNET_VERSION
            LogicalCores = $env:BUSBUDDY_LOGICAL_CORES
            MemoryGB = $env:BUSBUDDY_MEMORY_GB
            MaxParallelJobs = $env:BUSBUDDY_MAX_PARALLEL_JOBS
            ThreadLimit = $env:BUSBUDDY_THREAD_LIMIT
            BuildConfiguration = $env:BUILD_CONFIGURATION
        }
        AvailableCommands = @()
    }

    # Get available BusBuddy commands
    try {
        $profileStatus.AvailableCommands = Get-Command bb-* -ErrorAction SilentlyContinue |
                                         Select-Object -ExpandProperty Name |
                                         Sort-Object
    }
    catch {
        Write-Verbose "Could not enumerate available BusBuddy commands"
    }

    return $profileStatus
}

function Set-BusBuddyPSReadLineEnhanced {
    <#
    .SYNOPSIS
    Enhanced PSReadLine configuration following Microsoft best practices

    .DESCRIPTION
    Configures PSReadLine with Microsoft recommended settings for enhanced command line
    editing, including custom key bindings, colors, and prediction features.
    Based on: https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/creating-profiles

    .PARAMETER EnablePredictiveText
    Enable predictive text features (default: true)

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Configuration result

    .EXAMPLE
    Set-BusBuddyPSReadLineEnhanced

    .EXAMPLE
    Set-BusBuddyPSReadLineEnhanced -EnablePredictiveText:$false

    .NOTES
    Follows Microsoft PowerShell profile documentation patterns
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [bool]$EnablePredictiveText = $true
    )

    $configResult = [PSCustomObject]@{
        Success = $false
        PSReadLineAvailable = $false
        ConfigApplied = @()
        Errors = @()
    }

    try {
        # Import PSReadLine if available
        if (-not (Get-Module PSReadLine)) {
            Import-Module PSReadLine -ErrorAction Stop
        }
        $configResult.PSReadLineAvailable = $true
        $global:BusBuddyProfileState.PSReadLineAvailable = $true

        # Create $PSStyle if running on a version older than 7.2 (Microsoft pattern)
        if ($PSVersionTable.PSVersion -lt '7.2.0') {
            $esc = [char]0x1b
            if (-not $global:PSStyle) {
                $global:PSStyle = [pscustomobject]@{
                    Foreground = @{
                        Magenta = "${esc}[35m"
                        BrightYellow = "${esc}[93m"
                        Green = "${esc}[32m"
                        Blue = "${esc}[34m"
                    }
                    Background = @{
                        BrightBlack = "${esc}[100m"
                    }
                }
            }
        }

        # Microsoft recommended PSReadLine options
        $PSROptions = @{
            ContinuationPrompt = '  '
            Colors = @{
                Operator         = $PSStyle.Foreground.Magenta
                Parameter        = $PSStyle.Foreground.Magenta
                Selection        = $PSStyle.Background.BrightBlack
                InLinePrediction = $PSStyle.Foreground.BrightYellow + $PSStyle.Background.BrightBlack
                Command          = $PSStyle.Foreground.Green
                String           = $PSStyle.Foreground.Blue
            }
        }

        if ($EnablePredictiveText) {
            $PSROptions.PredictionSource = 'HistoryAndPlugin'
            $PSROptions.PredictionViewStyle = 'InlineView'
        }

        Set-PSReadLineOption @PSROptions
        $configResult.ConfigApplied += 'PSReadLine Options'

        # Microsoft recommended key bindings
        Set-PSReadLineKeyHandler -Chord 'Ctrl+f' -Function ForwardWord
        Set-PSReadLineKeyHandler -Chord 'Enter' -Function ValidateAndAcceptLine
        Set-PSReadLineKeyHandler -Chord 'Ctrl+d' -Function DeleteChar
        Set-PSReadLineKeyHandler -Chord 'Ctrl+w' -Function BackwardDeleteWord
        $configResult.ConfigApplied += 'Key Bindings'

        # BusBuddy-specific key bindings
        Set-PSReadLineKeyHandler -Chord 'Alt+b' -ScriptBlock {
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert('bb-')
        }
        $configResult.ConfigApplied += 'BusBuddy Key Bindings'

        $configResult.Success = $true
        Write-Verbose "PSReadLine enhanced configuration applied successfully"
    }
    catch {
        $configResult.Errors += $_.Exception.Message
        Write-Warning "PSReadLine configuration failed: $($_.Exception.Message)"
    }

    return $configResult
}

function Initialize-BusBuddyTrunkIntegration {
    <#
    .SYNOPSIS
    Initialize Trunk.io integration for code formatting and linting

    .DESCRIPTION
    Sets up Trunk.io integration for PowerShell, C#, XAML, and other file types
    used in BusBuddy development. Provides commands for formatting and linting.

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Integration result

    .EXAMPLE
    Initialize-BusBuddyTrunkIntegration

    .NOTES
    Trunk.io integration for enhanced code quality
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $trunkResult = [PSCustomObject]@{
        Success = $false
        TrunkAvailable = $false
        Commands = @()
        Errors = @()
    }

    try {
        # Check if trunk is available
        $trunkPath = Get-Command trunk -ErrorAction SilentlyContinue
        if ($trunkPath) {
            $trunkResult.TrunkAvailable = $true
            $global:BusBuddyProfileState.TrunkAvailable = $true

            # Add Trunk functions to the session
            function global:Invoke-TrunkFormat {
                <#
                .SYNOPSIS
                Format code using Trunk.io

                .PARAMETER Path
                Path to format (default: all files)

                .EXAMPLE
                Invoke-TrunkFormat

                .EXAMPLE
                Invoke-TrunkFormat -Path "*.ps1"
                #>
                [CmdletBinding()]
                param(
                    [Parameter()]
                    [string]$Path = "--all"
                )

                Write-Information "🎨 Formatting code with Trunk..." -InformationAction Continue
                if ($Path -eq "--all") {
                    & trunk fmt --all
                } else {
                    & trunk fmt $Path
                }
            }

            function global:Invoke-TrunkCheck {
                <#
                .SYNOPSIS
                Run linting and checks using Trunk.io

                .PARAMETER Path
                Path to check (default: all files)

                .PARAMETER Fix
                Automatically fix issues where possible

                .EXAMPLE
                Invoke-TrunkCheck

                .EXAMPLE
                Invoke-TrunkCheck -Fix
                #>
                [CmdletBinding()]
                param(
                    [Parameter()]
                    [string]$Path = "--all",

                    [Parameter()]
                    [switch]$Fix
                )

                Write-Information "🔍 Running Trunk checks..." -InformationAction Continue
                $args = @('check')
                if ($Path -eq "--all") { $args += '--all' } else { $args += $Path }
                if ($Fix) { $args += '--fix' }

                & trunk @args
            }

            # Create convenient aliases
            Set-Alias -Name 'bb-format' -Value 'Invoke-TrunkFormat' -Scope Global
            Set-Alias -Name 'bb-lint' -Value 'Invoke-TrunkCheck' -Scope Global
            Set-Alias -Name 'bb-fix' -Value 'Invoke-TrunkCheck' -Scope Global

            $trunkResult.Commands += @('Invoke-TrunkFormat', 'Invoke-TrunkCheck', 'bb-format', 'bb-lint', 'bb-fix')
            $trunkResult.Success = $true

            Write-Information "✅ Trunk.io integration initialized" -InformationAction Continue
        } else {
            Write-Verbose "Trunk.io not available - skipping integration"
        }
    }
    catch {
        $trunkResult.Errors += $_.Exception.Message
        Write-Warning "Trunk integration failed: $($_.Exception.Message)"
    }

    return $trunkResult
}

function Add-BusBuddyArgumentCompleters {
    <#
    .SYNOPSIS
    Add argument completers for enhanced tab completion

    .DESCRIPTION
    Microsoft recommended argument completer patterns for dotnet CLI and other tools
    used in BusBuddy development.

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Completer setup result

    .EXAMPLE
    Add-BusBuddyArgumentCompleters

    .NOTES
    Based on Microsoft PowerShell profile documentation examples
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $completerResult = [PSCustomObject]@{
        Success = $false
        Completers = @()
        Errors = @()
    }

    try {
        # Microsoft recommended dotnet CLI completer
        $dotnetCompleter = {
            param($wordToComplete, $commandAst, $cursorPosition)
            dotnet complete --position $cursorPosition $commandAst.ToString() |
                ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                }
        }
        Register-ArgumentCompleter -Native -CommandName dotnet -ScriptBlock $dotnetCompleter
        $completerResult.Completers += 'dotnet'

        # BusBuddy bb-* command completer
        $bbCompleter = {
            param($wordToComplete, $commandAst, $cursorPosition)
            $commands = Get-Command bb-* -ErrorAction SilentlyContinue |
                       Where-Object { $_.Name -like "$wordToComplete*" } |
                       ForEach-Object {
                           [System.Management.Automation.CompletionResult]::new(
                               $_.Name,
                               $_.Name,
                               'Function',
                               $_.Name
                           )
                       }
            return $commands
        }
        Register-ArgumentCompleter -CommandName bb-* -ScriptBlock $bbCompleter
        $completerResult.Completers += 'bb-commands'

        # Trunk command completer (if available)
        if ($global:BusBuddyProfileState.TrunkAvailable) {
            $trunkCompleter = {
                param($wordToComplete, $commandAst, $cursorPosition)
                $commonCommands = @('check', 'fmt', 'init', 'upgrade', 'actions', 'plugins')
                $commonCommands | Where-Object { $_ -like "$wordToComplete*" } |
                    ForEach-Object {
                        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                    }
            }
            Register-ArgumentCompleter -Native -CommandName trunk -ScriptBlock $trunkCompleter
            $completerResult.Completers += 'trunk'
        }

        $completerResult.Success = $true
        Write-Verbose "Argument completers registered successfully"
    }
    catch {
        $completerResult.Errors += $_.Exception.Message
        Write-Warning "Argument completer setup failed: $($_.Exception.Message)"
    }

function Initialize-BusBuddyVSCodeExtensionIntegration {
    <#
    .SYNOPSIS
    Initialize integration with VS Code extensions for enhanced development experience

    .DESCRIPTION
    Detects and configures integration with installed VS Code extensions including:
    - Trunk.io for code quality
    - GitLens for Git integration
    - GitHub Copilot for AI assistance
    - Azure tools for cloud development
    - SQL Server tools for database work

    .OUTPUTS
    [PSCustomObject] Integration status for each extension

    .EXAMPLE
    Initialize-BusBuddyVSCodeExtensionIntegration

    .NOTES
    Enhances development workflow by leveraging VS Code extension ecosystem
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $extensionResult = [PSCustomObject]@{
        Success = $false
        VSCodeDetected = $false
        ExtensionsFound = @()
        IntegrationsEnabled = @()
        Errors = @()
    }

    try {
        # Detect VS Code environment
        $vsCodePaths = @(
            $env:VSCODE_CWD,
            $env:VSCODE_WORKSPACE,
            (Get-Command code -ErrorAction SilentlyContinue)?.Source
        ) | Where-Object { $_ }

        if ($vsCodePaths -or $env:VSCODE_PID) {
            $extensionResult.VSCodeDetected = $true

            # Check for VS Code extensions directory
            $extensionsPath = "$env:USERPROFILE\.vscode\extensions"
            if (Test-Path $extensionsPath) {
                $installedExtensions = Get-ChildItem $extensionsPath -Directory | Select-Object -ExpandProperty Name

                # Key extensions we integrate with
                $keyExtensions = @{
                    'trunk.io' = 'Trunk Code Quality'
                    'eamodio.gitlens' = 'GitLens Git Integration'
                    'github.copilot' = 'GitHub Copilot AI'
                    'ms-azuretools.vscode-azureresourcegroups' = 'Azure Resource Groups'
                    'ms-mssql.mssql' = 'SQL Server Tools'
                    'ms-dotnettools.csdevkit' = 'C# Dev Kit'
                    'ms-vscode.powershell' = 'PowerShell Extension'
                    'spmeesseman.vscode-taskexplorer' = 'Task Explorer'
                }

                foreach ($extId in $keyExtensions.Keys) {
                    $found = $installedExtensions | Where-Object { $_ -like "$extId*" }
                    if ($found) {
                        $extensionResult.ExtensionsFound += [PSCustomObject]@{
                            Id = $extId
                            Name = $keyExtensions[$extId]
                            Version = ($found | Select-Object -First 1).Split('-')[-1]
                        }
                    }
                }

                # Enable specific integrations based on found extensions
                if ($extensionResult.ExtensionsFound | Where-Object Id -eq 'trunk.io') {
                    # Trunk integration already handled in Initialize-BusBuddyTrunkIntegration
                    $extensionResult.IntegrationsEnabled += 'Trunk Quality Tools'
                }

                if ($extensionResult.ExtensionsFound | Where-Object Id -eq 'eamodio.gitlens') {
                    # Add GitLens helper functions
                    function global:Show-GitHistory { code --command gitlens.showFileHistory }
                    function global:Show-GitBlame { code --command gitlens.toggleFileBlame }
                    Set-Alias -Name 'bb-git-history' -Value 'Show-GitHistory' -Scope Global
                    Set-Alias -Name 'bb-git-blame' -Value 'Show-GitBlame' -Scope Global
                    $extensionResult.IntegrationsEnabled += 'GitLens Integration'
                }

                if ($extensionResult.ExtensionsFound | Where-Object Id -eq 'github.copilot') {
                    # Add Copilot helper functions
                    function global:Start-CopilotChat { code --command github.copilot.interactiveEditor.explain }
                    Set-Alias -Name 'bb-ai-explain' -Value 'Start-CopilotChat' -Scope Global
                    $extensionResult.IntegrationsEnabled += 'GitHub Copilot AI'
                }

                if ($extensionResult.ExtensionsFound | Where-Object Id -eq 'ms-mssql.mssql') {
                    # Add SQL Server integration functions
                    function global:Open-SQLServerConnection { code --command mssql.connect }
                    function global:Show-SQLServerExplorer { code --command mssql.showCommands }
                    Set-Alias -Name 'bb-sql-connect' -Value 'Open-SQLServerConnection' -Scope Global
                    Set-Alias -Name 'bb-sql-explorer' -Value 'Show-SQLServerExplorer' -Scope Global
                    $extensionResult.IntegrationsEnabled += 'SQL Server Tools'
                }

                if ($extensionResult.ExtensionsFound | Where-Object Id -eq 'spmeesseman.vscode-taskexplorer') {
                    # Add Task Explorer integration
                    function global:Show-TaskExplorer { code --command taskExplorer.focus }
                    Set-Alias -Name 'bb-tasks' -Value 'Show-TaskExplorer' -Scope Global
                    $extensionResult.IntegrationsEnabled += 'Task Explorer'
                }
            }

            $extensionResult.Success = $true
        } else {
            Write-Verbose "VS Code environment not detected"
        }
    }
    catch {
        $extensionResult.Errors += $_.Exception.Message
        Write-Warning "VS Code extension integration failed: $($_.Exception.Message)"
    }

    return $extensionResult
}
#endregion Core Profile Functions

#region xAI Grok-4 Integration Functions

function Initialize-BusBuddyGrokIntegration {
    <#
    .SYNOPSIS
    Initializes xAI Grok-4 API integration for BusBuddy PowerShell environment

    .DESCRIPTION
    Sets up xAI Grok-4 API integration following official xAI documentation patterns.
    Configures API client, environment variables, and PowerShell helper functions.

    .PARAMETER ApiKey
    xAI API key for Grok-4 access

    .PARAMETER BaseUrl
    Base URL for xAI API (defaults to https://api.x.ai/v1)

    .PARAMETER DefaultModel
    Default Grok model to use (defaults to grok-4-latest)

    .EXAMPLE
    Initialize-BusBuddyGrokIntegration -ApiKey "xai-1234567890abcdef"

    .OUTPUTS
    PSCustomObject with integration status and configuration details
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$ApiKey,

        [Parameter()]
        [string]$BaseUrl = "https://api.x.ai/v1",

        [Parameter()]
        [string]$DefaultModel = "grok-4-latest"
    )

    $integrationResult = [PSCustomObject]@{
        Success = $false
        ApiKeyConfigured = $false
        BaseUrl = $BaseUrl
        DefaultModel = $DefaultModel
        EnvironmentVariables = @{}
        Functions = @()
        Aliases = @()
        Errors = @()
        ConfigurationSource = 'None'
        BusBuddyServiceAvailable = $false
    }

    try {
        Write-Verbose "Initializing xAI Grok-4 integration..."

        # Check for API key in parameters, environment, or configuration
        if (-not $ApiKey) {
            $ApiKey = $env:XAI_API_KEY
            if ($ApiKey) {
                $integrationResult.ConfigurationSource = 'Environment Variable'
            }
        } else {
            $integrationResult.ConfigurationSource = 'Parameter'
        }

        # Check if BusBuddy Core GrokGlobalAPI service is available
        $busBuddyPath = Split-Path -Parent $PSScriptRoot
        $grokServicePath = Join-Path $busBuddyPath "BusBuddy.Core\Services\GrokGlobalAPI.cs"
        if (Test-Path $grokServicePath) {
            $integrationResult.BusBuddyServiceAvailable = $true
            Write-Verbose "BusBuddy GrokGlobalAPI service detected"
        }

        if ($ApiKey -and $ApiKey -ne '${XAI_API_KEY}') {
            $integrationResult.ApiKeyConfigured = $true

            # Set environment variables for xAI configuration
            $env:XAI_API_KEY = $ApiKey
            $env:XAI_BASE_URL = $BaseUrl
            $env:XAI_DEFAULT_MODEL = $DefaultModel
            $env:XAI_USE_LIVE_API = "true"

            $integrationResult.EnvironmentVariables = @{
                XAI_API_KEY = "[CONFIGURED]"
                XAI_BASE_URL = $BaseUrl
                XAI_DEFAULT_MODEL = $DefaultModel
                XAI_USE_LIVE_API = "true"
            }

            # Create Grok helper functions
            function global:Invoke-GrokChat {
                <#
                .SYNOPSIS
                Sends a chat message to xAI Grok-4 and returns the response

                .PARAMETER Message
                The message to send to Grok

                .PARAMETER SystemPrompt
                Optional system prompt to set context

                .PARAMETER Temperature
                Response creativity (0.0-1.0, default 0.3)

                .EXAMPLE
                Invoke-GrokChat "Explain the benefits of PowerShell 7.5"

                .EXAMPLE
                Invoke-GrokChat "Optimize this route" -SystemPrompt "You are a transportation expert"
                #>
                [CmdletBinding()]
                param(
                    [Parameter(Mandatory=$true, Position=0)]
                    [string]$Message,

                    [Parameter()]
                    [string]$SystemPrompt = "You are a helpful AI assistant.",

                    [Parameter()]
                    [double]$Temperature = 0.3
                )

                try {
                    $headers = @{
                        'Authorization' = "Bearer $env:XAI_API_KEY"
                        'Content-Type' = 'application/json'
                        'User-Agent' = 'BusBuddy-PowerShell/1.0'
                    }

                    $body = @{
                        model = $env:XAI_DEFAULT_MODEL
                        messages = @(
                            @{
                                role = 'system'
                                content = $SystemPrompt
                            },
                            @{
                                role = 'user'
                                content = $Message
                            }
                        )
                        temperature = $Temperature
                        max_tokens = 4000
                    } | ConvertTo-Json -Depth 10

                    Write-Verbose "Sending request to xAI Grok-4..."
                    $response = Invoke-RestMethod -Uri "$env:XAI_BASE_URL/chat/completions" -Method Post -Headers $headers -Body $body

                    if ($response.choices -and $response.choices[0].message.content) {
                        return $response.choices[0].message.content
                    } else {
                        Write-Warning "No response content received from Grok"
                        return $null
                    }
                }
                catch {
                    Write-Error "Grok API call failed: $($_.Exception.Message)"
                    return $null
                }
            }

            function global:Invoke-GrokRouteOptimization {
                <#
                .SYNOPSIS
                Uses Grok-4 to analyze and optimize bus routes

                .PARAMETER RouteData
                Route information as hashtable or JSON string

                .PARAMETER OptimizationGoals
                Specific optimization goals (efficiency, safety, time, etc.)

                .EXAMPLE
                $routeInfo = @{ RouteId = "R001"; Stops = 15; Distance = "12.5 miles" }
                Invoke-GrokRouteOptimization -RouteData $routeInfo -OptimizationGoals "efficiency,safety"
                #>
                [CmdletBinding()]
                param(
                    [Parameter(Mandatory=$true)]
                    [object]$RouteData,

                    [Parameter()]
                    [string]$OptimizationGoals = "efficiency,safety,time"
                )

                $systemPrompt = @"
You are an expert transportation analyst specializing in school bus route optimization.
Analyze the provided route data and suggest improvements focusing on: $OptimizationGoals.
Provide specific, actionable recommendations with reasoning.
Format your response as structured analysis with clear recommendations.
"@

                $routeJson = if ($RouteData -is [string]) { $RouteData } else { $RouteData | ConvertTo-Json -Depth 5 }
                $message = "Analyze and optimize this bus route data: $routeJson"

                return Invoke-GrokChat -Message $message -SystemPrompt $systemPrompt -Temperature 0.2
            }

            function global:Invoke-GrokCodeReview {
                <#
                .SYNOPSIS
                Uses Grok-4 to review PowerShell or C# code

                .PARAMETER Code
                Code to review

                .PARAMETER Language
                Programming language (PowerShell, CSharp, XAML)

                .PARAMETER Focus
                Review focus areas (performance, security, best-practices)

                .EXAMPLE
                Invoke-GrokCodeReview -Code $codeContent -Language "PowerShell" -Focus "performance,best-practices"
                #>
                [CmdletBinding()]
                param(
                    [Parameter(Mandatory=$true)]
                    [string]$Code,

                    [Parameter()]
                    [ValidateSet('PowerShell', 'CSharp', 'XAML', 'JSON', 'YAML')]
                    [string]$Language = 'PowerShell',

                    [Parameter()]
                    [string]$Focus = "best-practices,performance,security"
                )

                $systemPrompt = @"
You are an expert $Language developer and code reviewer.
Review the provided code focusing on: $Focus.
Follow Microsoft coding standards and best practices.
Provide specific improvements with code examples where applicable.
Be concise but thorough in your analysis.
"@

                $message = "Review this $Language code:`n`n```$($Language.ToLower())`n$Code`n```"

                return Invoke-GrokChat -Message $message -SystemPrompt $systemPrompt -Temperature 0.1
            }

            # Create convenient aliases following bb- convention
            Set-Alias -Name 'bb-ai-chat' -Value 'Invoke-GrokChat' -Scope Global
            Set-Alias -Name 'bb-ai-route' -Value 'Invoke-GrokRouteOptimization' -Scope Global
            Set-Alias -Name 'bb-ai-review' -Value 'Invoke-GrokCodeReview' -Scope Global

            $integrationResult.Functions = @(
                'Invoke-GrokChat',
                'Invoke-GrokRouteOptimization',
                'Invoke-GrokCodeReview'
            )

            $integrationResult.Aliases = @(
                'bb-ai-chat',
                'bb-ai-route',
                'bb-ai-review'
            )

            Write-Information "✅ xAI Grok-4 integration configured successfully" -InformationAction Continue
            Write-Information "Available commands: bb-ai-chat, bb-ai-route, bb-ai-review" -InformationAction Continue

            $integrationResult.Success = $true
        }
        else {
            Write-Warning "⚠️  xAI API key not configured. Set `$env:XAI_API_KEY or provide -ApiKey parameter"
            Write-Information "To configure: `$env:XAI_API_KEY = 'your-api-key-here'" -InformationAction Continue
        }
    }
    catch {
        $integrationResult.Errors += $_.Exception.Message
        Write-Error "xAI Grok integration failed: $($_.Exception.Message)"
    }

    return $integrationResult
}

function global:bb-ai-help {
    <#
    .SYNOPSIS
    Shows available xAI Grok-4 commands and usage examples
    #>
    Write-Information "`n🤖 BusBuddy xAI Grok -InformationAction Continue-4 Integration Commands" -ForegroundColor Cyan
    Write-Information "=" * 50  -InformationAction Continue-ForegroundColor Cyan

    Write-Information "`nCore Commands:"  -InformationAction Continue-ForegroundColor Yellow
    Write-Information "  bb -InformationAction Continue-ai-chat     - Chat with Grok-4" -ForegroundColor Green
    Write-Information "  bb -InformationAction Continue-ai-route    - Route optimization analysis" -ForegroundColor Green
    Write-Information "  bb -InformationAction Continue-ai-review   - Code review assistance" -ForegroundColor Green
    Write-Information "  bb -InformationAction Continue-ai-help     - Show this help" -ForegroundColor Green

    Write-Information "`nExamples:"  -InformationAction Continue-ForegroundColor Yellow
    Write-Information '  bb -InformationAction Continue-ai-chat "Explain PowerShell 7.5 features"' -ForegroundColor Gray
    Write-Information '  bb -InformationAction Continue-ai-route @{RouteId="R001"; Stops=15} "efficiency"' -ForegroundColor Gray
    Write-Information '  bb -InformationAction Continue-ai-review $code "PowerShell" "performance"' -ForegroundColor Gray

    Write-Information "`nConfiguration:"  -InformationAction Continue-ForegroundColor Yellow
    if ($env:XAI_API_KEY) {
        Write-Information "  ✅ API Key: Configured"  -InformationAction Continue-ForegroundColor Green
        Write-Information "  📡 Base URL: $($env:XAI_BASE_URL ?? 'https://api.x.ai/v1')"  -InformationAction Continue-ForegroundColor Green
        Write-Information "  🤖 Model: $($env:XAI_DEFAULT_MODEL ?? 'grok -InformationAction Continue-4-latest')" -ForegroundColor Green
    } else {
        Write-Information "  ❌ API Key: Not configured"  -InformationAction Continue-ForegroundColor Red
        Write-Information "  💡 Set with: `$env:XAI_API_KEY = 'your -InformationAction Continue-key'" -ForegroundColor Yellow
    }
    Write-Information ""
}

#endregion xAI Grok -InformationAction Continue-4 Integration Functions

#region Module Export
# Microsoft recommended explicit export pattern
Export-ModuleMember -Function @(
    'Initialize-BusBuddyProfile',
    'Test-VSCodePowerShellExtension',
    'Set-BusBuddyEnvironment',
    'Set-BusBuddyPSReadLineConfig',
    'Set-BusBuddyPSReadLineEnhanced',
    'Initialize-BusBuddyTrunkIntegration',
    'Add-BusBuddyArgumentCompleters',
    'Initialize-BusBuddyVSCodeExtensionIntegration',
    'Initialize-BusBuddyGrokIntegration',
    'Show-BusBuddyWelcome',
    'Get-BusBuddyProfileStatus'
) -Alias @()
#endregion Module Export












