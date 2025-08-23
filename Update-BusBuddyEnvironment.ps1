#Requires -Version 7.5

<#
.SYNOPSIS
Updates BusBuddy environment variables with correct hardware detection

.DESCRIPTION
Standalone script to fix environment variables when hardware detection is incorrect.
Uses direct CIM queries to detect hardware and update environment variables.
Based on Microsoft PowerShell documentation for environment variable management.

.PARAMETER Force
Forces immediate hardware re-detection without cache

.EXAMPLE
.\Update-BusBuddyEnvironment.ps1 -Force

.NOTES
Created: August 23, 2025
Purpose: Fix environment variable propagation for BusBuddy-3 development
Hardware: Optimized for 13th Gen Intel i5-1334U (12 logical processors)
#>

[CmdletBinding()]
param(
    [switch]$Force
)

# Clear hardware cache if forced
if ($Force -and $global:BusBuddyHardwareCache) {
    Write-Information "🔄 Clearing hardware cache..." -InformationAction Continue
    $global:BusBuddyHardwareCache = $null
}

# Direct hardware detection using CIM queries (Microsoft recommended approach)
function Get-HardwareInfo {
    try {
        Write-Information "🔍 Detecting hardware via CIM queries..." -InformationAction Continue

        $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
        $processor = Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop | Select-Object -First 1

        $hardwareInfo = @{
            LogicalProcessors = $computerSystem.NumberOfLogicalProcessors
            PhysicalProcessors = $computerSystem.NumberOfProcessors
            TotalMemoryGB = [math]::Round($computerSystem.TotalPhysicalMemory / 1GB, 2)
            ProcessorName = $processor.Name
            ProcessorCores = $processor.NumberOfCores
            ProcessorThreads = $processor.NumberOfLogicalProcessors
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

        $hardwareInfo.OptimalBatchSize = [math]::Max([math]::Floor($hardwareInfo.LogicalProcessors / 2), 2)

        Write-Information "✅ Hardware detected: $($hardwareInfo.LogicalProcessors) logical processors, $($hardwareInfo.TotalMemoryGB)GB RAM" -InformationAction Continue
        return $hardwareInfo
    }
    catch {
        Write-Error "Hardware detection failed: $($_.Exception.Message)"
        return $null
    }
}

# Update environment variables
function Update-EnvironmentVariables {
    param($HardwareInfo)

    if (-not $HardwareInfo) {
        Write-Error "Cannot update environment variables: no hardware information provided"
        return
    }

    try {
        Write-Information "🔧 Updating BusBuddy environment variables..." -InformationAction Continue

        # Core hardware environment variables
        $env:BUSBUDDY_LOGICAL_CORES = $HardwareInfo.LogicalProcessors.ToString()
        $env:BUSBUDDY_PHYSICAL_CORES = $HardwareInfo.PhysicalProcessors.ToString()
        $env:BUSBUDDY_MEMORY_GB = $HardwareInfo.TotalMemoryGB.ToString()
        $env:BUSBUDDY_MAX_PARALLEL_JOBS = $HardwareInfo.OptimalThreadLimit.ToString()
        $env:BUSBUDDY_OPTIMAL_BATCH_SIZE = $HardwareInfo.OptimalBatchSize.ToString()

        # Update hyperthread ratio
        $env:BUSBUDDY_HYPERTHREAD_RATIO = if ($HardwareInfo.HasHyperthreading) {
            [math]::Round($HardwareInfo.LogicalProcessors / $HardwareInfo.PhysicalProcessors, 2).ToString()
        } else { "1.0" }

        # Cache the results for future use
        $global:BusBuddyHardwareCache = @{
            LogicalProcessors = $HardwareInfo.LogicalProcessors
            TotalMemoryGB = $HardwareInfo.TotalMemoryGB
            PhysicalProcessors = $HardwareInfo.PhysicalProcessors
            ProcessorName = $HardwareInfo.ProcessorName
            Timestamp = Get-Date
        }

        Write-Information "✅ Environment variables updated successfully!" -InformationAction Continue
        Write-Information "   Logical Cores: $($env:BUSBUDDY_LOGICAL_CORES)" -InformationAction Continue
        Write-Information "   Physical Cores: $($env:BUSBUDDY_PHYSICAL_CORES)" -InformationAction Continue
        Write-Information "   Memory: $($env:BUSBUDDY_MEMORY_GB)GB" -InformationAction Continue
        Write-Information "   Max Parallel Jobs: $($env:BUSBUDDY_MAX_PARALLEL_JOBS)" -InformationAction Continue
        Write-Information "   Optimal Batch Size: $($env:BUSBUDDY_OPTIMAL_BATCH_SIZE)" -InformationAction Continue

        return $true
    }
    catch {
        Write-Error "Failed to update environment variables: $($_.Exception.Message)"
        return $false
    }
}

# Main execution
Write-Information "🚌 BusBuddy -InformationAction Continue-3 Environment Variable Update Script" -ForegroundColor Green
Write-Information "================================================="  -InformationAction Continue-ForegroundColor Green

# Show current values
Write-Information "📊 Current environment variables:" -InformationAction Continue
Write-Information "   BUSBUDDY_LOGICAL_CORES = $($env:BUSBUDDY_LOGICAL_CORES)" -InformationAction Continue
Write-Information "   BUSBUDDY_PHYSICAL_CORES = $($env:BUSBUDDY_PHYSICAL_CORES)" -InformationAction Continue
Write-Information "   BUSBUDDY_MEMORY_GB = $($env:BUSBUDDY_MEMORY_GB)" -InformationAction Continue
Write-Information "   BUSBUDDY_MAX_PARALLEL_JOBS = $($env:BUSBUDDY_MAX_PARALLEL_JOBS)" -InformationAction Continue

# Detect hardware
$hardwareInfo = Get-HardwareInfo

if ($hardwareInfo) {
    # Update environment variables
    $success = Update-EnvironmentVariables -HardwareInfo $hardwareInfo

    if ($success) {
        Write-Information "🎉 Environment variables successfully updated!"  -InformationAction Continue-ForegroundColor Green
        Write-Information "   Use 'Get -InformationAction Continue-ChildItem Env:BUSBUDDY_*' to view all variables" -ForegroundColor Cyan
    } else {
        Write-Information "❌ Failed to update environment variables"  -InformationAction Continue-ForegroundColor Red
        exit 1
    }
} else {
    Write-Information "❌ Hardware detection failed"  -InformationAction Continue-ForegroundColor Red
    exit 1
}





