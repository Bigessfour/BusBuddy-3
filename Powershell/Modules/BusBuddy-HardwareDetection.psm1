#Requires -Version 7.5

# BusBuddy Hardware Detection Module
# Purpose: Hardware detection and environment variable persistence for BusBuddy development
# Author: BusBuddy Development Team
# Version: 3.0.0
# Based on proven success pattern from Update-BusBuddyEnvironment.ps1

# Module initialization
$script:ModuleVersion = '3.0.0'
$script:HardwareCache = $null
$script:LastCacheUpdate = $null

function Get-BusBuddyHardwareInfo {
    <#
    .SYNOPSIS
    Detects comprehensive hardware information for BusBuddy development optimization.

    .DESCRIPTION
    Uses CIM queries to detect CPU cores, memory, and hyperthreading capabilities.
    Calculates optimal threading parameters for PowerShell parallel processing.
    Proven pattern with Intel i5-1334U 12-core detection success.

    .EXAMPLE
    $hardware = Get-BusBuddyHardwareInfo
    Write-Output "Detected $($hardware.LogicalProcessors) logical processors"

    .NOTES
    Requires PowerShell 7.5+ and CIM access for hardware queries.
    #>
    [CmdletBinding()]
    param(
        [switch]$UseCache,
        [switch]$Force
    )

    try {
        # Use cached results if available and not forced
        if ($UseCache -and $script:HardwareCache -and -not $Force) {
            $cacheAge = (Get-Date) - $script:LastCacheUpdate
            if ($cacheAge.TotalMinutes -lt 30) {
                Write-Verbose "Using cached hardware information (age: $($cacheAge.TotalMinutes.ToString('F1')) minutes)"
                return $script:HardwareCache
            }
        }

        Write-Information "🔍 Detecting hardware configuration..." -InformationAction Continue

        # Initialize hardware information object
        $hardwareInfo = [PSCustomObject]@{
            ProcessorName = $null
            ProcessorCores = 0
            ProcessorThreads = 0
            LogicalProcessors = 0
            PhysicalProcessors = 0
            TotalMemoryGB = 0
            HasHyperthreading = $false
            OptimalThreadLimit = 2
            OptimalBatchSize = 2
            DetectionTimestamp = Get-Date
            DetectionMethod = "CIM"
        }

        # Processor information using CIM (proven method)
        $processor = Get-CimInstance -ClassName Win32_Processor | Select-Object -First 1
        if ($processor) {
            $hardwareInfo.ProcessorName = $processor.Name.Trim()
            $hardwareInfo.ProcessorCores = $processor.NumberOfCores
            $hardwareInfo.ProcessorThreads = $processor.ThreadCount
            $hardwareInfo.LogicalProcessors = $processor.NumberOfLogicalProcessors
            $hardwareInfo.PhysicalProcessors = (Get-CimInstance -ClassName Win32_Processor).Count

            Write-Verbose "Processor: $($hardwareInfo.ProcessorName)"
            Write-Verbose "Cores: $($hardwareInfo.ProcessorCores), Threads: $($hardwareInfo.ProcessorThreads)"
        }

        # Memory information using CIM
        $memory = Get-CimInstance -ClassName Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum
        if ($memory.Sum -gt 0) {
            $hardwareInfo.TotalMemoryGB = [math]::Round($memory.Sum / 1GB, 2)
            Write-Verbose "Memory: $($hardwareInfo.TotalMemoryGB)GB"
        }

        # Calculate derived properties
        $hardwareInfo.HasHyperthreading = $hardwareInfo.ProcessorThreads -gt $hardwareInfo.ProcessorCores

        # Intelligent throttle limit calculation optimized for modern CPUs
        $hardwareInfo.OptimalThreadLimit = switch ($hardwareInfo.LogicalProcessors) {
            { $_ -ge 12 } { [math]::Ceiling($_ * 0.67) }  # 67% utilization for 12+ cores
            { $_ -ge 8 }  { [math]::Ceiling($_ * 0.75) }  # 75% utilization for 8+ cores
            { $_ -ge 4 }  { [math]::Ceiling($_ * 0.5) }   # 50% utilization for 4+ cores
            default       { 2 }                           # Conservative fallback
        }
        # Ensure single value for OptimalThreadLimit
        if ($hardwareInfo.OptimalThreadLimit -is [array]) {
            $hardwareInfo.OptimalThreadLimit = $hardwareInfo.OptimalThreadLimit[0]
        }

        $hardwareInfo.OptimalBatchSize = [math]::Max([math]::Floor($hardwareInfo.LogicalProcessors / 2), 2)

        # Cache the results
        $script:HardwareCache = $hardwareInfo
        $script:LastCacheUpdate = Get-Date

        Write-Information "✅ Hardware detected: $($hardwareInfo.LogicalProcessors) logical processors, $($hardwareInfo.TotalMemoryGB)GB RAM" -InformationAction Continue
        return $hardwareInfo
    }
    catch {
        Write-Error "Hardware detection failed: $($_.Exception.Message)"
        return $null
    }
}

function Update-BusBuddyEnvironmentVariables {
    <#
    .SYNOPSIS
    Updates BusBuddy environment variables with hardware detection results.

    .DESCRIPTION
    Sets environment variables for both current session and persistent User registry.
    Uses proven [System.Environment]::SetEnvironmentVariable with 'User' target for cross-session persistence.

    .PARAMETER HardwareInfo
    Hardware information object from Get-BusBuddyHardwareInfo.

    .PARAMETER PersistentOnly
    Only update persistent environment variables, skip current session.

    .EXAMPLE
    $hardware = Get-BusBuddyHardwareInfo
    Update-BusBuddyEnvironmentVariables -HardwareInfo $hardware

    .NOTES
    Requires administrative privileges for persistent environment variable updates.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$HardwareInfo,

        [switch]$PersistentOnly
    )

    if (-not $HardwareInfo) {
        Write-Error "Cannot update environment variables: no hardware information provided"
        return
    }

    try {
        Write-Information "🔧 Updating BusBuddy environment variables..." -InformationAction Continue

        # Define environment variables to set
        $envVars = @{
            'BUSBUDDY_LOGICAL_CORES' = $HardwareInfo.LogicalProcessors.ToString()
            'BUSBUDDY_PHYSICAL_CORES' = $HardwareInfo.PhysicalProcessors.ToString()
            'BUSBUDDY_MEMORY_GB' = $HardwareInfo.TotalMemoryGB.ToString()
            'BUSBUDDY_MAX_PARALLEL_JOBS' = $HardwareInfo.OptimalThreadLimit.ToString()
            'BUSBUDDY_OPTIMAL_BATCH_SIZE' = $HardwareInfo.OptimalBatchSize.ToString()
            'BUSBUDDY_PROCESSOR_NAME' = $HardwareInfo.ProcessorName
            'BUSBUDDY_HAS_HYPERTHREADING' = $HardwareInfo.HasHyperthreading.ToString()
            'BUSBUDDY_HARDWARE_LAST_UPDATE' = $HardwareInfo.DetectionTimestamp.ToString('yyyy-MM-dd HH:mm:ss')
        }

        # Calculate hyperthread ratio
        $hyperthreadRatio = if ($HardwareInfo.HasHyperthreading) {
            [math]::Round($HardwareInfo.LogicalProcessors / $HardwareInfo.PhysicalProcessors, 2).ToString()
        } else { "1.0" }
        $envVars['BUSBUDDY_HYPERTHREAD_RATIO'] = $hyperthreadRatio

        # Update current session variables (unless PersistentOnly)
        if (-not $PersistentOnly) {
            foreach ($var in $envVars.GetEnumerator()) {
                Set-Item -Path "env:$($var.Key)" -Value $var.Value
                Write-Verbose "Session: $($var.Key) = $($var.Value)"
            }
        }

        # Update persistent User environment variables (proven method)
        foreach ($var in $envVars.GetEnumerator()) {
            [System.Environment]::SetEnvironmentVariable($var.Key, $var.Value, 'User')
            Write-Verbose "Persistent: $($var.Key) = $($var.Value)"
        }

        Write-Information "✅ Environment variables updated successfully" -InformationAction Continue

        # Update global cache variables for immediate availability
        $global:BusBuddyPhysicalCores = $HardwareInfo.PhysicalProcessors
        $global:BusBuddyLogicalCores = $HardwareInfo.LogicalProcessors
        $global:BusBuddyMemoryGB = $HardwareInfo.TotalMemoryGB
        $global:BusBuddyHyperthreadRatio = $hyperthreadRatio
        $global:BusBuddyOptimalJobs = $HardwareInfo.OptimalThreadLimit

    }
    catch {
        Write-Error "Failed to update environment variables: $($_.Exception.Message)"
    }
}

function Set-BusBuddyOptimalThreading {
    <#
    .SYNOPSIS
    Configures optimal threading parameters for BusBuddy development operations.

    .DESCRIPTION
    Sets PowerShell execution policy and parallel processing defaults based on detected hardware.

    .EXAMPLE
    Set-BusBuddyOptimalThreading

    .NOTES
    Modifies global PowerShell threading behavior for current session.
    #>
    [CmdletBinding()]
    param()

    try {
        $hardware = Get-BusBuddyHardwareInfo -UseCache
        if (-not $hardware) {
            Write-Warning "Could not detect hardware for threading optimization"
            return
        }

        # Set optimal defaults for parallel operations
        $global:PSDefaultParameterValues = @{
            'ForEach-Object:ThrottleLimit' = $hardware.OptimalThreadLimit
            'Start-ThreadJob:ThrottleLimit' = $hardware.OptimalThreadLimit
        }

        Write-Information "🚀 Threading optimized for $($hardware.LogicalProcessors) logical processors (limit: $($hardware.OptimalThreadLimit))" -InformationAction Continue
    }
    catch {
        Write-Error "Failed to set optimal threading: $($_.Exception.Message)"
    }
}

function Test-BusBuddyHardwareCache {
    <#
    .SYNOPSIS
    Tests and validates the hardware detection cache.

    .DESCRIPTION
    Compares cached hardware information with fresh detection to ensure accuracy.

    .EXAMPLE
    Test-BusBuddyHardwareCache

    .NOTES
    Useful for debugging hardware detection issues.
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Information "🧪 Testing hardware detection cache..." -InformationAction Continue

        $cached = Get-BusBuddyHardwareInfo -UseCache
        $fresh = Get-BusBuddyHardwareInfo -Force

        if (-not $cached -or -not $fresh) {
            Write-Error "Hardware detection failed"
            return $false
        }

        $differences = @()
        if ($cached.LogicalProcessors -ne $fresh.LogicalProcessors) {
            $differences += "LogicalProcessors: $($cached.LogicalProcessors) vs $($fresh.LogicalProcessors)"
        }
        if ($cached.TotalMemoryGB -ne $fresh.TotalMemoryGB) {
            $differences += "TotalMemoryGB: $($cached.TotalMemoryGB) vs $($fresh.TotalMemoryGB)"
        }

        if ($differences.Count -eq 0) {
            Write-Information "✅ Cache validation successful - no differences detected" -InformationAction Continue
            return $true
        } else {
            Write-Warning "❌ Cache validation failed - differences: $($differences -join ', ')"
            return $false
        }
    }
    catch {
        Write-Error "Cache test failed: $($_.Exception.Message)"
        return $false
    }
}

function Get-BusBuddyEnvironmentSummary {
    <#
    .SYNOPSIS
    Displays a comprehensive summary of BusBuddy environment configuration.

    .DESCRIPTION
    Shows current hardware detection, environment variables, and module status.

    .EXAMPLE
    Get-BusBuddyEnvironmentSummary

    .NOTES
    Useful for troubleshooting and validation.
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Output ""
        Write-Output "🚌 BusBuddy Environment Summary"
        Write-Output "================================"

        $hardware = Get-BusBuddyHardwareInfo -UseCache
        if ($hardware) {
            Write-Output "Hardware Configuration:"
            Write-Output "  Processor: $($hardware.ProcessorName)"
            Write-Output "  Logical Cores: $($hardware.LogicalProcessors)"
            Write-Output "  Physical Cores: $($hardware.PhysicalProcessors)"
            Write-Output "  Memory: $($hardware.TotalMemoryGB)GB"
            Write-Output "  Hyperthreading: $($hardware.HasHyperthreading)"
            Write-Output "  Optimal Thread Limit: $($hardware.OptimalThreadLimit)"
            Write-Output ""
        }

        Write-Output "Environment Variables:"
        $envVars = @(
            'BUSBUDDY_LOGICAL_CORES',
            'BUSBUDDY_PHYSICAL_CORES',
            'BUSBUDDY_MEMORY_GB',
            'BUSBUDDY_MAX_PARALLEL_JOBS',
            'BUSBUDDY_HYPERTHREAD_RATIO'
        )

        foreach ($var in $envVars) {
            $value = [System.Environment]::GetEnvironmentVariable($var, 'User')
            Write-Output "  $var = $value"
        }

        Write-Output ""
        Write-Output "Module Information:"
        Write-Output "  Version: $script:ModuleVersion"
        Write-Output "  Cache Status: $($script:HardwareCache -ne $null)"
        Write-Output "  Last Update: $($script:LastCacheUpdate)"
    }
    catch {
        Write-Error "Failed to generate environment summary: $($_.Exception.Message)"
    }
}

# Export public functions following Microsoft standards
Export-ModuleMember -Function @(
    'Get-BusBuddyHardwareInfo',
    'Update-BusBuddyEnvironmentVariables',
    'Set-BusBuddyOptimalThreading',
    'Test-BusBuddyHardwareCache',
    'Get-BusBuddyEnvironmentSummary'
)

# Module initialization
Write-Verbose "BusBuddy-HardwareDetection module loaded (v$script:ModuleVersion)"
