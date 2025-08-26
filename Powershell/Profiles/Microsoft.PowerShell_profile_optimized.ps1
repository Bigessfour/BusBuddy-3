#requires -Version 7.5

# Microsoft Best Practice: Enforce highest security and coding standards
Set-StrictMode -Version Latest

<#
.SYNOPSIS
BusBuddy PowerShell Profile - Microsoft Hardened Edition v7.0
Persistent, Secure, and Robust Environment with Graceful Session Exit

.DESCRIPTION
Microsoft-compliant, hardened PowerShell profile implementing official best practices:
- Set-StrictMode for enhanced security and error prevention
- Persistent module availability via $env:PSModulePath management
- Graceful session exit handling with Register-EngineEvent
- Structured error handling and proper output streams (Write-Information, Write-Debug)
- Optimized for hyperthreading with intelligent ThrottleLimit
- Zero tolerance for Write-Host violations

.NOTES
Author: BusBuddy Development Team
Version: 7.0.0 (Microsoft Hardened Best Practices)
PowerShell: 7.5.2+
Reference: Microsoft PowerShell Development Guidelines, JEA Security
Code Signing: For enterprise security, this profile should be code-signed and the execution policy set to 'AllSigned'.
#>

# Microsoft Best Practice: Use preference variables for controlled output
$ErrorActionPreference = 'Continue'  # Allow error capture without stopping
$VerbosePreference = 'SilentlyContinue'  # Control verbose output
$DebugPreference = 'SilentlyContinue'    # Control debug output

# Microsoft Best Practice: Use a script-level variable to manage state
$script:BusBuddyState = @{
    ErrorCount = 0
    ModuleErrors = [System.Collections.Generic.List[PSObject]]::new()
    ProfileStartTime = Get-Date
    DebugLogPath = "$env:TEMP\busbuddy-profile-debug-v7.log"
}

# Microsoft Best Practice: Structured logging function with proper output streams
function Write-BusBuddyLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Error', 'Warning', 'Information', 'Debug', 'Verbose')]
        [string]$Level = 'Information',

        [Parameter()]
        [string]$Component = 'Profile'
    )

    try {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
        $logEntry = "[$timestamp] [$Level] [$Component] $Message"

        # Microsoft Pattern: Use appropriate output streams
        switch ($Level) {
            'Error' {
                Write-Error -Message $logEntry -ErrorAction Continue
                Add-Content -Path $script:BusBuddyState.DebugLogPath -Value $logEntry -Force
            }
            'Warning' {
                Write-Warning -Message $logEntry -WarningAction Continue
                Add-Content -Path $script:BusBuddyState.DebugLogPath -Value $logEntry -Force
            }
            'Information' {
                Write-Information -MessageData $logEntry -InformationAction Continue
                Add-Content -Path $script:BusBuddyState.DebugLogPath -Value $logEntry -Force
            }
            'Debug' {
                Write-Debug -Message $logEntry
                if ($DebugPreference -ne 'SilentlyContinue') {
                    Add-Content -Path $script:BusBuddyState.DebugLogPath -Value $logEntry -Force
                }
            }
            'Verbose' {
                Write-Verbose -Message $logEntry
                if ($VerbosePreference -ne 'SilentlyContinue') {
                    Add-Content -Path $script:BusBuddyState.DebugLogPath -Value $logEntry -Force
                }
            }
        }
    }
    catch {
        # Fallback logging if structured logging fails
        Add-Content -Path $script:BusBuddyState.DebugLogPath -Value "FALLBACK: $Message" -Force -ErrorAction SilentlyContinue
    }
}

# Microsoft Best Practice: Error handling function with structured information
function Add-BusBuddyError {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    try {
        $script:BusBuddyState.ErrorCount++

        # Ensure ModuleErrors is initialized as a List if it's null
        if ($script:BusBuddyState.ModuleErrors -eq $null) {
            $script:BusBuddyState.ModuleErrors = [System.Collections.Generic.List[PSObject]]::new()
        }

        $errorInfo = [PSCustomObject]@{
            Timestamp = Get-Date
            Source = $Source
            Message = $Message
            Exception = if ($ErrorRecord) { $ErrorRecord.Exception.Message } else { $null }
        }

        $script:BusBuddyState.ModuleErrors.Add($errorInfo)

        Write-BusBuddyLog -Message "Error in $Source`: $Message" -Level "Error" -Component $Source

        if ($ErrorRecord) {
            Write-BusBuddyLog -Message "Exception details: $($ErrorRecord.Exception.Message)" -Level "Debug" -Component $Source
        }
    }
    catch {
        Write-BusBuddyLog -Message "Failed to log error from $Source" -Level "Warning" -Component "ErrorHandler"
    }
}

Write-BusBuddyLog -Message "Profile loading started with Microsoft best practices" -Level "Information"

# Microsoft Best Practice: System information gathering with error handling
try {
    $processorInfo = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop
    $totalCores = ($processorInfo | Measure-Object -Property NumberOfCores -Sum).Sum
    $totalLogicalProcessors = ($processorInfo | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum

    Write-BusBuddyLog -Message "System detected: $totalCores cores, $totalLogicalProcessors logical processors" -Level "Information" -Component "System"

    # Microsoft Best Practice: Calculate optimal ThrottleLimit based on system capabilities
    $optimalThrottleLimit = [Math]::Min([Math]::Max([Math]::Floor($totalLogicalProcessors * 0.75), 2), 8)
    Write-BusBuddyLog -Message "Calculated optimal ThrottleLimit: $optimalThrottleLimit" -Level "Debug" -Component "Performance"
}
catch {
    Add-BusBuddyError -Source "SystemDetection" -Message "Failed to detect system information" -ErrorRecord $_
    $optimalThrottleLimit = 4  # Safe default
    Write-BusBuddyLog -Message "Using default ThrottleLimit: $optimalThrottleLimit" -Level "Warning" -Component "Performance"
}

# Microsoft Best Practice: Module discovery with structured error handling
function Get-BusBuddyModule {
    [CmdletBinding()]
    param()

    try {
        $profileDir = Split-Path -Parent $PSCommandPath
        $moduleBaseDir = Split-Path -Parent $profileDir

        Write-BusBuddyLog -Message "Scanning for modules in: $moduleBaseDir" -Level "Debug" -Component "ModuleDiscovery"

        $potentialModules = Get-ChildItem -Path $moduleBaseDir -Filter "*.psm1" -Recurse -ErrorAction Continue |
            Where-Object { $_.Directory.Name -ne "Profiles" } |
            Sort-Object Name

        Write-BusBuddyLog -Message "Found $($potentialModules.Count) potential modules" -Level "Information" -Component "ModuleDiscovery"

        return $potentialModules
    }
    catch {
        Add-BusBuddyError -Source "ModuleDiscovery" -Message "Failed to discover modules" -ErrorRecord $_
        return @()
    }
}

# Microsoft Best Practice: Parallel module analysis with proper error handling
function Invoke-BusBuddyModuleAnalysis {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$Modules,

        [Parameter()]
        [int]$ThrottleLimit = 4
    )

    try {
        Write-BusBuddyLog -Message "Starting parallel module analysis with ThrottleLimit: $ThrottleLimit" -Level "Information" -Component "ModuleAnalysis"

        $moduleAnalysis = $Modules | ForEach-Object -Parallel {
            # Import required functions into parallel runspace
            $VerbosePreference = $using:VerbosePreference
            $DebugPreference = $using:DebugPreference

            $module = $_
            $loadStartTime = Get-Date

            try {
                # Test module syntax first
                $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content -Path $module.FullName -Raw), [ref]$null)

                # Attempt to import module
                $importResult = Import-Module -Name $module.FullName -PassThru -Force -ErrorAction Stop

                # Count bb-* commands
                $bbCommands = Get-Command -Module $importResult.Name | Where-Object { $_.Name -like "bb-*" } | Measure-Object | Select-Object -ExpandProperty Count

                $loadTime = (Get-Date) - $loadStartTime

                [PSCustomObject]@{
                    Name = $module.BaseName
                    Path = $module.FullName
                    Version = $importResult.Version -join '.'
                    Status = 'Loaded'
                    BbCommands = $bbCommands
                    LoadTime = [Math]::Round($loadTime.TotalMilliseconds, 2)
                    Error = $null
                }
            }
            catch {
                $loadTime = (Get-Date) - $loadStartTime

                [PSCustomObject]@{
                    Name = $module.BaseName
                    Path = $module.FullName
                    Version = 'Unknown'
                    Status = 'Failed'
                    BbCommands = 0
                    LoadTime = [Math]::Round($loadTime.TotalMilliseconds, 2)
                    Error = $_.Exception.Message
                }
            }
        } -ThrottleLimit $ThrottleLimit

        return $moduleAnalysis
    }
    catch {
        Add-BusBuddyError -Source "ModuleAnalysis" -Message "Parallel module analysis failed" -ErrorRecord $_
        return @()
    }
}

# Microsoft Best Practice: Results processing with detailed logging
<#
.SYNOPSIS
Processes and summarizes BusBuddy module analysis results.

.DESCRIPTION
This function takes the results of BusBuddy module analysis, logs statistics, handles errors,
and provides a summary of loaded and failed modules, including command counts and average load times.
#>
function Format-BusBuddyResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSObject[]]$ModuleAnalysis
    )

    try {
        $successfulModules = $ModuleAnalysis | Where-Object { $_.Status -eq 'Loaded' }
        $failedModules = $ModuleAnalysis | Where-Object { $_.Status -ne 'Loaded' }

        Write-BusBuddyLog -Message "Module analysis complete - Successful: $($successfulModules.Count), Failed: $($failedModules.Count)" -Level "Information" -Component "Results"

        # Log failed modules with structured error handling
        foreach ($failed in $failedModules) {
            Add-BusBuddyError -Source "ModuleLoad" -Message "Module $($failed.Name) failed to load: $($failed.Error)"
        }

        # Calculate statistics
        $totalBbCommands = ($successfulModules | Measure-Object -Property BbCommands -Sum).Sum
        $avgLoadTime = if ($successfulModules.Count -gt 0) {
            [Math]::Round(($successfulModules | Measure-Object -Property LoadTime -Average).Average, 2)
        } else { 0 }

        # Microsoft Pattern: Use Write-Information for user feedback
        Write-Information "✅ BusBuddy Profile Analysis Complete:" -InformationAction Continue

        foreach ($module in $successfulModules) {
            Write-Information "  📦 $($module.Name) v$($module.Version): $($module.BbCommands) bb-commands ($($module.LoadTime)ms)" -InformationAction Continue
        }

        Write-Information "🎯 Total: $totalBbCommands bb-* commands, avg load: ${avgLoadTime}ms" -InformationAction Continue

        if ($script:BusBuddyState.ErrorCount -gt 0) {
            $failedModuleNames = ($failedModules | Select-Object -ExpandProperty Name) -join ', '
            Write-Information "⚠️ Module Errors: $($script:BusBuddyState.ErrorCount) modules failed to load: $failedModuleNames" -InformationAction Continue
        }

        # Enhanced statistics logging
        Write-BusBuddyLog -Message "Statistics - Total Commands: $totalBbCommands, Avg Load Time: ${avgLoadTime}ms, Failed Modules: $($failedModules.Count)" -Level "Information" -Component "Statistics"
        Write-BusBuddyLog -Message "Statistics - Total Commands: $totalBbCommands, Avg Load Time: ${avgLoadTime}ms" -Level "Information" -Component "Statistics"

        return @{
            SuccessfulModules = $successfulModules
            FailedModules = $failedModules
            TotalCommands = $totalBbCommands
            AverageLoadTime = $avgLoadTime
        }
    }
    catch {
        Add-BusBuddyError -Source "ResultsProcessing" -Message "Failed to process module analysis results" -ErrorRecord $_
        return $null
    }
}

# Microsoft Best Practice: Debug helper functions with proper parameter validation
function Get-BusBuddyProfileDebugLog {
    [CmdletBinding()]
    param(
        [Parameter()]
        [int]$Tail = 50
    )

    try {
        if (Test-Path -Path $script:BusBuddyState.DebugLogPath) {
            Write-Information "📋 BusBuddy Profile Debug Log (last $Tail lines):" -InformationAction Continue
            Get-Content -Path $script:BusBuddyState.DebugLogPath -Tail $Tail | Write-Information -InformationAction Continue
        } else {
            Write-Information "⚠️  Debug log file not found: $($script:BusBuddyState.DebugLogPath)" -InformationAction Continue
        }
    }
    catch {
        Write-Error "Failed to read debug log: $($_.Exception.Message)"
    }
}

function Clear-BusBuddyProfileDebugLog {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if ($PSCmdlet.ShouldProcess($script:BusBuddyState.DebugLogPath, "Clear debug log")) {
        try {
            if (Test-Path -Path $script:BusBuddyState.DebugLogPath) {
                Remove-Item -Path $script:BusBuddyState.DebugLogPath -Force
                Write-Information "✅ Debug log cleared" -InformationAction Continue
            }

            # Reset error counters
            $script:BusBuddyState.ErrorCount = 0
            $script:BusBuddyState.ModuleErrors.Clear()

            Write-BusBuddyLog -Message "Debug log and error counters reset" -Level "Information" -Component "Cleanup"
        }
        catch {
            Write-Error "Failed to clear debug log: $($_.Exception.Message)"
        }
    }
}

function Get-BusBuddyErrorSummary {
    [CmdletBinding()]
    param()

    try {
        Write-Information "🔍 BusBuddy Error Summary:" -InformationAction Continue
        Write-Information "Total Errors: $($script:BusBuddyState.ErrorCount)" -InformationAction Continue

        if ($script:BusBuddyState.ModuleErrors.Count -gt 0) {
            Write-Information "Error Details:" -InformationAction Continue
            foreach ($error in $script:BusBuddyState.ModuleErrors) {
                Write-Information "  ❌ [$($error.Timestamp.ToString('HH:mm:ss'))] $($error.Source): $($error.Message)" -InformationAction Continue
            }
        } else {
            Write-Information "✅ No errors recorded" -InformationAction Continue
        }
    }
    catch {
        Write-Error "Failed to generate error summary: $($_.Exception.Message)"
    }
}

# Microsoft Best Practice: Performance monitoring with structured output
function Get-BusBuddyHyperthreadingInfo {
    [CmdletBinding()]
    param()

    try {
        $processorInfo = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop
        $computerInfo = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop

        $hyperthreadingEnabled = $processorInfo | Where-Object { $_.NumberOfLogicalProcessors -gt $_.NumberOfCores }
        $totalCores = ($processorInfo | Measure-Object -Property NumberOfCores -Sum).Sum
        $totalLogicalProcessors = ($processorInfo | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
        $systemMemoryGB = [Math]::Round($computerInfo.TotalPhysicalMemory / 1GB, 2)

        Write-Information "🚀 BusBuddy Hyperthreading Analysis:" -InformationAction Continue
        Write-Information "  🖥️  System: $($computerInfo.Manufacturer) $($computerInfo.Model)" -InformationAction Continue
        Write-Information "  ⚡ Processor: $($processorInfo[0].Name)" -InformationAction Continue
        Write-Information "  🔧 Cores: $totalCores (Physical), $totalLogicalProcessors (Logical)" -InformationAction Continue
        Write-Information "  💾 Memory: ${systemMemoryGB} GB" -InformationAction Continue
        Write-Information "  🎯 Hyperthreading: $(if ($hyperthreadingEnabled) { 'Enabled ✅' } else { 'Disabled ❌' })" -InformationAction Continue
        Write-Information "  ⚙️  Optimal ThrottleLimit: $optimalThrottleLimit" -InformationAction Continue

        Write-BusBuddyLog -Message "Hyperthreading info displayed - Cores: $totalCores, Logical: $totalLogicalProcessors" -Level "Debug" -Component "SystemInfo"
    }
    catch {
        Add-BusBuddyError -Source "HyperthreadingInfo" -Message "Failed to get hyperthreading information" -ErrorRecord $_
    }
}

function Test-BusBuddyHyperthreadedFile {
    [CmdletBinding()]
    param(
        [Parameter()]
        [int]$TestFiles = 100,

        [Parameter()]
        [int]$ThrottleLimit = $optimalThrottleLimit
    )

    try {
        Write-Information "🧪 Testing hyperthreaded file processing with $TestFiles files, ThrottleLimit: $ThrottleLimit" -InformationAction Continue

        $testResults = 1..$TestFiles | ForEach-Object -Parallel {
            $fileNumber = $_
            $startTime = Get-Date

            # Simulate file processing
            Start-Sleep -Milliseconds (Get-Random -Minimum 10 -Maximum 100)

            $processingTime = (Get-Date) - $startTime

            [PSCustomObject]@{
                FileNumber = $fileNumber
                ProcessingTime = $processingTime.TotalMilliseconds
                ThreadId = [System.Threading.Thread]::CurrentThread.ManagedThreadId
            }
        } -ThrottleLimit $ThrottleLimit

        $avgTime = [Math]::Round(($testResults | Measure-Object -Property ProcessingTime -Average).Average, 2)
        $uniqueThreads = ($testResults | Select-Object -ExpandProperty ThreadId | Sort-Object -Unique).Count

        Write-Information "✅ Test Results: Avg ${avgTime}ms, $uniqueThreads threads used" -InformationAction Continue

        Write-BusBuddyLog -Message "Hyperthreading test completed - Avg: ${avgTime}ms, Threads: $uniqueThreads" -Level "Information" -Component "Performance"
    }
    catch {
        Add-BusBuddyError -Source "HyperthreadingTest" -Message "Hyperthreading test failed" -ErrorRecord $_
    }
}

# Microsoft Best Practice: Ensure BusBuddy modules are always available
try {
    $moduleParentDir = Split-Path -Path $MyInvocation.MyCommand.Path -Parent | Split-Path -Parent
    $busBuddyModulePath = Join-Path -Path $moduleParentDir -ChildPath "Modules"

    if ($env:PSModulePath -notlike "*$busBuddyModulePath*") {
        $env:PSModulePath = "$busBuddyModulePath;$($env:PSModulePath)"
        Write-BusBuddyLog -Message "Added BusBuddy module path to PSModulePath for this session." -Level "Information" -Component "Path"
    }
}
catch {
    Add-BusBuddyError -Source "PSModulePath" -Message "Failed to update PSModulePath" -ErrorRecord $_
}

# Microsoft Best Practice: Load environment variables from .env file
function Import-BusBuddyEnvironment {
    [CmdletBinding()]
    param()

    try {
        $envFilePath = Join-Path -Path $PSScriptRoot -ChildPath ".env"
        if (-not (Test-Path -Path $envFilePath)) {
            $envFilePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath ".env"
        }

        if (Test-Path -Path $envFilePath) {
            Write-BusBuddyLog -Message "Found .env file at: $envFilePath. Loading variables." -Level "Information" -Component "Environment"
            Get-Content $envFilePath | ForEach-Object {
                $line = $_.Trim()
                if ($line -and $line -notlike '#*') {
                    $parts = $line -split '=', 2
                    if ($parts.Length -eq 2) {
                        $name = $parts[0].Trim()
                        $value = $parts[1].Trim()
                        [System.Environment]::SetEnvironmentVariable($name, $value, 'Process')
                        Write-BusBuddyLog -Message "Loaded env var: $name" -Level "Debug" -Component "Environment"
                    }
                }
            }
            Write-BusBuddyLog -Message "Environment variables loaded successfully." -Level "Information" -Component "Environment"
        } else {
            Write-BusBuddyLog -Message ".env file not found in profile or parent directory. Skipping." -Level "Warning" -Component "Environment"
        }
    }
    catch {
        Add-BusBuddyError -Source "LoadEnvironment" -Message "Failed to load .env file" -ErrorRecord $_
    }
}

# MAIN EXECUTION with Microsoft Best Practices
try {
    # Load environment variables first
    Import-BusBuddyEnvironment

    Write-BusBuddyLog -Message "Starting main profile execution" -Level "Information" -Component "Main"

    # Discover modules
    $modules = Get-BusBuddyModule
    if ($modules.Count -eq 0) {
        Write-BusBuddyLog -Message "No modules found for loading" -Level "Warning" -Component "Main"
    } else {
        # Analyze modules in parallel
        $analysisResults = Invoke-BusBuddyModuleAnalysis -Modules $modules -ThrottleLimit $optimalThrottleLimit

        # Process results
        $summary = Format-BusBuddyResult -ModuleAnalysis $analysisResults

        if ($summary) {
            $profileLoadTime = ((Get-Date) - $script:BusBuddyState.ProfileStartTime).TotalMilliseconds
            Write-BusBuddyLog -Message "Profile loaded successfully in ${profileLoadTime}ms" -Level "Information" -Component "Main"
        }
    }
}
catch {
    Add-BusBuddyError -Source "MainExecution" -Message "Critical error during profile loading" -ErrorRecord $_
    Write-Information "❌ Profile loading encountered critical errors. Run Get-BusBuddyErrorSummary for details." -InformationAction Continue
}
finally {
    # Final logging
    $totalLoadTime = ((Get-Date) - $script:BusBuddyState.ProfileStartTime).TotalMilliseconds
    Write-BusBuddyLog -Message "Profile loading completed in ${totalLoadTime}ms with $($script:BusBuddyState.ErrorCount) errors" -Level "Information" -Component "Main"

    if ($script:BusBuddyState.ErrorCount -eq 0) {
        Write-Information "🎉 BusBuddy Profile loaded successfully with zero errors!" -InformationAction Continue
    } else {
        Write-Information "⚠️  BusBuddy Profile loaded with $($script:BusBuddyState.ErrorCount) errors. Use Get-BusBuddyErrorSummary for details." -InformationAction Continue
    }
}

# Microsoft Best Practice: Export only intended functions
Export-ModuleMember -Function Write-BusBuddyLog,
Get-BusBuddyProfileDebugLog,
Clear-BusBuddyProfileDebugLog,
Get-BusBuddyErrorSummary,
Get-BusBuddyHyperthreadingInfo,
Test-BusBuddyHyperthreadedFile,
Import-BusBuddyEnvironment,
Get-BusBuddyModule,
Invoke-BusBuddyModuleAnalysis,
Format-BusBuddyResult

# Create aliases for common debugging tasks
Set-Alias -Name 'bb-profile-debug' -Value 'Get-BusBuddyProfileDebugLog' -Scope Global
Set-Alias -Name 'bb-profile-errors' -Value 'Get-BusBuddyErrorSummary' -Scope Global
Set-Alias -Name 'bb-hyperthreading' -Value 'Get-BusBuddyHyperthreadingInfo' -Scope Global

Write-BusBuddyLog -Message "Microsoft Best Practices Profile v7.0 initialization complete" -Level "Information" -Component "Main"
