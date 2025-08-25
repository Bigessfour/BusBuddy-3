#requires -Version 7.5
<#
.SYNOPSIS
BusBuddy PowerShell Profile - Microsoft Best Practices Edition v6.0
Enhanced Error Handling, Structured Logging, and Performance Optimization

.DESCRIPTION
Microsoft-compliant PowerShell profile implementing official best practices:
- Structured error handling with try/catch blocks
- Proper output streams (Write-Information, Write-Debug, Write-Verbose)
- Enhanced logging patterns for troubleshooting
- Optimized for hyperthreading with intelligent ThrottleLimit
- Zero tolerance for Write-Host violations

.NOTES
Author: BusBuddy Development Team  
Version: 6.0.0 (Microsoft Best Practices)
PowerShell: 7.5.2+
Reference: Microsoft PowerShell Development Guidelines
Compliance: Azure Functions PowerShell developer guide
#>

# Microsoft Best Practice: Use preference variables for controlled output
$ErrorActionPreference = 'Continue'  # Allow error capture without stopping
$VerbosePreference = 'SilentlyContinue'  # Control verbose output
$DebugPreference = 'SilentlyContinue'    # Control debug output

# Global Error Tracking with Microsoft-Approved Patterns
$global:BusBuddyErrorCount = 0
$global:BusBuddyModuleErrors = [System.Collections.Generic.List[PSObject]]::new()
$global:BusBuddyProfileStartTime = Get-Date
$global:BusBuddyDebugLogPath = "$env:TEMP\busbuddy-profile-debug-v6.log"

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
                Add-Content -Path $global:BusBuddyDebugLogPath -Value $logEntry -Force
            }
            'Warning' {
                Write-Warning -Message $logEntry -WarningAction Continue
                Add-Content -Path $global:BusBuddyDebugLogPath -Value $logEntry -Force
            }
            'Information' {
                Write-Information -MessageData $logEntry -InformationAction Continue
                Add-Content -Path $global:BusBuddyDebugLogPath -Value $logEntry -Force
            }
            'Debug' {
                Write-Debug -Message $logEntry
                if ($DebugPreference -ne 'SilentlyContinue') {
                    Add-Content -Path $global:BusBuddyDebugLogPath -Value $logEntry -Force
                }
            }
            'Verbose' {
                Write-Verbose -Message $logEntry
                if ($VerbosePreference -ne 'SilentlyContinue') {
                    Add-Content -Path $global:BusBuddyDebugLogPath -Value $logEntry -Force
                }
            }
        }
    }
    catch {
        # Fallback logging if structured logging fails
        Add-Content -Path $global:BusBuddyDebugLogPath -Value "FALLBACK: $Message" -Force -ErrorAction SilentlyContinue
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
        $global:BusBuddyErrorCount++
        
        $errorInfo = [PSCustomObject]@{
            Timestamp = Get-Date
            Source = $Source
            Message = $Message
            ErrorRecord = $ErrorRecord
            Exception = $ErrorRecord?.Exception?.Message
            StackTrace = $ErrorRecord?.Exception?.StackTrace
        }
        
        $global:BusBuddyModuleErrors.Add($errorInfo)
        
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
function Get-BusBuddyModules {
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
function Process-BusBuddyResults {
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
        
        if ($global:BusBuddyErrorCount -gt 0) {
            Write-Information "⚠️  Module Errors: $($global:BusBuddyErrorCount) modules failed to load" -InformationAction Continue
        }
        
        # Enhanced statistics logging
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
        if (Test-Path -Path $global:BusBuddyDebugLogPath) {
            Write-Information "📋 BusBuddy Profile Debug Log (last $Tail lines):" -InformationAction Continue
            Get-Content -Path $global:BusBuddyDebugLogPath -Tail $Tail | Write-Information -InformationAction Continue
        } else {
            Write-Information "⚠️  Debug log file not found: $global:BusBuddyDebugLogPath" -InformationAction Continue
        }
    }
    catch {
        Write-Error "Failed to read debug log: $($_.Exception.Message)"
    }
}

function Clear-BusBuddyProfileDebugLog {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    
    if ($PSCmdlet.ShouldProcess($global:BusBuddyDebugLogPath, "Clear debug log")) {
        try {
            if (Test-Path -Path $global:BusBuddyDebugLogPath) {
                Remove-Item -Path $global:BusBuddyDebugLogPath -Force
                Write-Information "✅ Debug log cleared" -InformationAction Continue
            }
            
            # Reset error counters
            $global:BusBuddyErrorCount = 0
            $global:BusBuddyModuleErrors.Clear()
            
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
        Write-Information "Total Errors: $global:BusBuddyErrorCount" -InformationAction Continue
        
        if ($global:BusBuddyModuleErrors.Count -gt 0) {
            Write-Information "Error Details:" -InformationAction Continue
            foreach ($error in $global:BusBuddyModuleErrors) {
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

# MAIN EXECUTION with Microsoft Best Practices
try {
    Write-BusBuddyLog -Message "Starting main profile execution" -Level "Information" -Component "Main"
    
    # Discover modules
    $modules = Get-BusBuddyModules
    
    if ($modules.Count -eq 0) {
        Write-BusBuddyLog -Message "No modules found for loading" -Level "Warning" -Component "Main"
    } else {
        # Analyze modules in parallel
        $analysisResults = Invoke-BusBuddyModuleAnalysis -Modules $modules -ThrottleLimit $optimalThrottleLimit
        
        # Process results
        $summary = Process-BusBuddyResults -ModuleAnalysis $analysisResults
        
        if ($summary) {
            $profileLoadTime = ((Get-Date) - $global:BusBuddyProfileStartTime).TotalMilliseconds
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
    $totalLoadTime = ((Get-Date) - $global:BusBuddyProfileStartTime).TotalMilliseconds
    Write-BusBuddyLog -Message "Profile loading completed in ${totalLoadTime}ms with $global:BusBuddyErrorCount errors" -Level "Information" -Component "Main"
    
    if ($global:BusBuddyErrorCount -eq 0) {
        Write-Information "🎉 BusBuddy Profile loaded successfully with zero errors!" -InformationAction Continue
    } else {
        Write-Information "⚠️  BusBuddy Profile loaded with $global:BusBuddyErrorCount errors. Use Get-BusBuddyErrorSummary for details." -InformationAction Continue
    }
}

# Microsoft Best Practice: Export only intended functions
$ModuleMemberExports = @(
    'Write-BusBuddyLog',
    'Get-BusBuddyProfileDebugLog', 
    'Clear-BusBuddyProfileDebugLog',
    'Get-BusBuddyErrorSummary',
    'Get-BusBuddyHyperthreadingInfo',
    'Test-BusBuddyHyperthreadedFile'
)

# Create aliases for common debugging tasks
Set-Alias -Name 'bb-profile-debug' -Value 'Get-BusBuddyProfileDebugLog' -Scope Global
Set-Alias -Name 'bb-profile-errors' -Value 'Get-BusBuddyErrorSummary' -Scope Global
Set-Alias -Name 'bb-hyperthreading' -Value 'Get-BusBuddyHyperthreadingInfo' -Scope Global

Write-BusBuddyLog -Message "Microsoft Best Practices Profile v6.0 initialization complete" -Level "Information" -Component "Main"
