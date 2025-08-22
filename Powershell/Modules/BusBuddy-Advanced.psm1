#Requires -Version 7.5

# BusBuddy Advanced PowerShell Module
# Purpose: State-of-the-art PowerShell 7.5.2 functions with hyperthreading support
# Author: BusBuddy Development Team
# Version: 3.0.0

# Import required modules
if (Get-Module -ListAvailable -Name PSScriptAnalyzer) {
    Import-Module PSScriptAnalyzer -ErrorAction SilentlyContinue
}

# Initialize module variables
$script:ModuleVersion = '3.0.0'
$script:LastPerformanceCheck = Get-Date
$script:PerformanceHistory = @()

# Export machine configuration variables
$global:BusBuddyPhysicalCores = $env:BUSBUDDY_PHYSICAL_CORES
$global:BusBuddyLogicalCores = $env:BUSBUDDY_LOGICAL_CORES  
$global:BusBuddyMemoryGB = $env:BUSBUDDY_MEMORY_GB
$global:BusBuddyHyperthreadRatio = $env:BUSBUDDY_HYPERTHREAD_RATIO
$global:BusBuddyOptimalJobs = $env:BUSBUDDY_MAX_PARALLEL_JOBS

# Advanced hyperthreading test function
function Test-BusBuddyHyperthreading {
    [CmdletBinding()]
    param(
        [int]$TestDuration = 10,
        [int]$WorkloadSize = 1000,
        [switch]$DetailedOutput
    )
    
    Write-Information "🧪 Testing hyperthreading performance..." -InformationAction Continue
    
    # Create test workload
    $TestData = 1..$WorkloadSize
    $Results = @{}
    
    # Test with different thread counts
    $ThreadCounts = @(1, 2, $global:BusBuddyPhysicalCores, $global:BusBuddyLogicalCores)
    
    foreach ($ThreadCount in $ThreadCounts) {
        Write-Progress -Activity "Hyperthreading Test" -Status "Testing with $ThreadCount threads" -PercentComplete (($ThreadCounts.IndexOf($ThreadCount) / $ThreadCounts.Count) * 100)
        
        $StartTime = Get-Date
        
        $TestData | ForEach-Object -Parallel {
            # CPU-intensive work simulation
            $result = 0
            for ($i = 0; $i -lt 10000; $i++) {
                $result += [Math]::Sqrt($_)
            }
            return $result
        } -ThrottleLimit $ThreadCount | Out-Null
        
        $EndTime = Get-Date
        $Duration = ($EndTime - $StartTime).TotalSeconds
        
        $Results[$ThreadCount] = [PSCustomObject]@{
            ThreadCount = $ThreadCount
            Duration = $Duration
            Throughput = [math]::Round($WorkloadSize / $Duration, 2)
            Efficiency = if ($ThreadCount -eq 1) { 100 } else { [math]::Round((($WorkloadSize / $Duration) / ($Results[1].Throughput)) * 100 / $ThreadCount, 2) }
        }
    }
    
    Write-Progress -Activity "Hyperthreading Test" -Completed
    
    # Analyze results
    $BestThroughput = ($Results.Values | Sort-Object Throughput -Descending | Select-Object -First 1)
    $HyperthreadingBenefit = [math]::Round((($Results[$global:BusBuddyLogicalCores].Throughput / $Results[$global:BusBuddyPhysicalCores].Throughput) - 1) * 100, 2)
    
    $Summary = [PSCustomObject]@{
        TestDate = Get-Date
        WorkloadSize = $WorkloadSize
        PhysicalCores = $global:BusBuddyPhysicalCores
        LogicalCores = $global:BusBuddyLogicalCores
        HyperthreadingBenefit = "$HyperthreadingBenefit%"
        OptimalThreads = $BestThroughput.ThreadCount
        OptimalThroughput = $BestThroughput.Throughput
        Results = if ($DetailedOutput) { $Results.Values } else { $null }
    }
    
    return $Summary
}

# Optimize parallelism based on workload characteristics
function Optimize-BusBuddyParallelism {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('CPU-Intensive', 'IO-Intensive', 'Memory-Intensive', 'Mixed')]
        [string]$WorkloadType,
        
        [int]$DataSize = 1000,
        
        [switch]$SetGlobalDefaults
    )
    
    # Workload-specific optimization
    $OptimalSettings = switch ($WorkloadType) {
        'CPU-Intensive' {
            @{
                ThreadCount = $global:BusBuddyPhysicalCores  # No benefit from hyperthreading for pure CPU work
                BatchSize = [math]::Max([math]::Floor($DataSize / $global:BusBuddyPhysicalCores), 1)
                MemoryLimit = [math]::Floor($global:BusBuddyMemoryGB * 0.4)  # Conservative memory usage
            }
        }
        'IO-Intensive' {
            @{
                ThreadCount = [math]::Min($global:BusBuddyLogicalCores * 2, 16)  # Higher thread count for I/O waiting
                BatchSize = [math]::Max([math]::Floor($DataSize / ($global:BusBuddyLogicalCores * 2)), 1)
                MemoryLimit = [math]::Floor($global:BusBuddyMemoryGB * 0.6)  # More memory for I/O buffers
            }
        }
        'Memory-Intensive' {
            @{
                ThreadCount = [math]::Max([math]::Floor($global:BusBuddyLogicalCores / 2), 2)  # Reduce threads to save memory
                BatchSize = [math]::Max([math]::Floor($DataSize / [math]::Floor($global:BusBuddyLogicalCores / 2)), 1)
                MemoryLimit = [math]::Floor($global:BusBuddyMemoryGB * 0.8)  # High memory allocation
            }
        }
        'Mixed' {
            @{
                ThreadCount = $global:BusBuddyLogicalCores  # Balanced approach
                BatchSize = [math]::Max([math]::Floor($DataSize / $global:BusBuddyLogicalCores), 1)
                MemoryLimit = [math]::Floor($global:BusBuddyMemoryGB * 0.5)  # Balanced memory usage
            }
        }
    }
    
    if ($SetGlobalDefaults) {
        $env:BUSBUDDY_OPTIMAL_THREADS = $OptimalSettings.ThreadCount
        $env:BUSBUDDY_OPTIMAL_BATCH_SIZE = $OptimalSettings.BatchSize
        $env:BUSBUDDY_MEMORY_LIMIT_GB = $OptimalSettings.MemoryLimit
        Write-Information "✅ Global defaults updated for $WorkloadType workload" -InformationAction Continue
    }
    
    return [PSCustomObject]@{
        WorkloadType = $WorkloadType
        DataSize = $DataSize
        OptimalThreads = $OptimalSettings.ThreadCount
        OptimalBatchSize = $OptimalSettings.BatchSize
        MemoryLimitGB = $OptimalSettings.MemoryLimit
        Recommendation = "Use $($OptimalSettings.ThreadCount) threads with batch size $($OptimalSettings.BatchSize) for optimal $WorkloadType performance"
    }
}

# Performance profile management
function Set-BusBuddyPerformanceProfile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Development', 'Testing', 'Production', 'HighThroughput', 'LowLatency', 'Balanced')]
        [string]$Profile,
        
        [switch]$Force
    )
    
    $ProfileSettings = switch ($Profile) {
        'Development' {
            @{
                GCServer = '0'
                GCConcurrent = '1'
                GCRetainVM = '0'
                MaxParallelJobs = [math]::Min($global:BusBuddyLogicalCores, 4)
                BatchSize = 2
                MemoryThreshold = 30
                VerboseLogging = '1'
            }
        }
        'Testing' {
            @{
                GCServer = '1'
                GCConcurrent = '1'
                GCRetainVM = '0'
                MaxParallelJobs = [math]::Min($global:BusBuddyLogicalCores, 6)
                BatchSize = 4
                MemoryThreshold = 50
                VerboseLogging = '1'
            }
        }
        'Production' {
            @{
                GCServer = '1'
                GCConcurrent = '1'
                GCRetainVM = '1'
                MaxParallelJobs = $global:BusBuddyLogicalCores
                BatchSize = $global:BusBuddyOptimalJobs
                MemoryThreshold = 70
                VerboseLogging = '0'
            }
        }
        'HighThroughput' {
            @{
                GCServer = '1'
                GCConcurrent = '0'
                GCRetainVM = '1'
                MaxParallelJobs = [math]::Min($global:BusBuddyLogicalCores * 2, 16)
                BatchSize = [math]::Max($global:BusBuddyOptimalJobs * 2, 8)
                MemoryThreshold = 80
                VerboseLogging = '0'
            }
        }
        'LowLatency' {
            @{
                GCServer = '0'
                GCConcurrent = '1'
                GCRetainVM = '0'
                MaxParallelJobs = [math]::Max([math]::Floor($global:BusBuddyLogicalCores / 2), 2)
                BatchSize = 1
                MemoryThreshold = 40
                VerboseLogging = '0'
            }
        }
        'Balanced' {
            @{
                GCServer = '1'
                GCConcurrent = '1'
                GCRetainVM = '1'
                MaxParallelJobs = $global:BusBuddyLogicalCores
                BatchSize = $global:BusBuddyOptimalJobs
                MemoryThreshold = 60
                VerboseLogging = '0'
            }
        }
    }
    
    # Apply settings
    $env:DOTNET_GCServer = $ProfileSettings.GCServer
    $env:DOTNET_GCConcurrent = $ProfileSettings.GCConcurrent
    $env:DOTNET_GCRetainVM = $ProfileSettings.GCRetainVM
    $env:BUSBUDDY_MAX_PARALLEL_JOBS = $ProfileSettings.MaxParallelJobs
    $env:BUSBUDDY_OPTIMAL_BATCH_SIZE = $ProfileSettings.BatchSize
    $env:BUSBUDDY_MEMORY_THRESHOLD_PCT = $ProfileSettings.MemoryThreshold
    $env:BUSBUDDY_VERBOSE_LOGGING = $ProfileSettings.VerboseLogging
    $env:BUSBUDDY_PERFORMANCE_PROFILE = $Profile
    
    Write-Information "✅ Performance profile set to: $Profile" -InformationAction Continue
    
    return [PSCustomObject]@{
        Profile = $Profile
        AppliedAt = Get-Date
        Settings = $ProfileSettings
        Recommendation = "Profile optimized for $Profile scenarios with $($ProfileSettings.MaxParallelJobs) max parallel jobs"
    }
}

# Advanced memory monitoring
function Get-BusBuddyMemoryMetrics {
    [CmdletBinding()]
    param(
        [switch]$IncludeGCStats,
        [switch]$IncludeProcessStats,
        [switch]$ContinuousMonitoring,
        [int]$MonitoringIntervalSeconds = 5
    )
    
    if ($ContinuousMonitoring) {
        Write-Information "🔍 Starting continuous memory monitoring (Ctrl+C to stop)..." -InformationAction Continue
        
        try {
            while ($true) {
                $metrics = Get-BusBuddyMemoryMetrics -IncludeGCStats:$IncludeGCStats -IncludeProcessStats:$IncludeProcessStats
                Clear-Host
                Write-Output "=== BusBuddy Memory Metrics ($(Get-Date)) ==="
                $metrics | Format-Table -AutoSize
                Start-Sleep -Seconds $MonitoringIntervalSeconds
            }
        } catch {
            Write-Information "Monitoring stopped." -InformationAction Continue
        }
        return
    }
    
    $SystemMemory = Get-CimInstance -ClassName Win32_OperatingSystem
    $Process = Get-Process -Id $PID
    
    $BaseMetrics = [PSCustomObject]@{
        Timestamp = Get-Date
        SystemMemoryGB = [math]::Round($SystemMemory.TotalVisibleMemorySize / 1MB, 2)
        SystemFreeGB = [math]::Round($SystemMemory.FreePhysicalMemory / 1MB, 2)
        SystemUsagePercent = [math]::Round((($SystemMemory.TotalVisibleMemorySize - $SystemMemory.FreePhysicalMemory) / $SystemMemory.TotalVisibleMemorySize) * 100, 2)
        ProcessWorkingSetMB = [math]::Round($Process.WorkingSet64 / 1MB, 2)
        ProcessPrivateMemoryMB = [math]::Round($Process.PrivateMemorySize64 / 1MB, 2)
        ProcessVirtualMemoryMB = [math]::Round($Process.VirtualMemorySize64 / 1MB, 2)
    }
    
    if ($IncludeGCStats) {
        $GCStats = [PSCustomObject]@{
            Gen0Collections = [GC]::CollectionCount(0)
            Gen1Collections = [GC]::CollectionCount(1)
            Gen2Collections = [GC]::CollectionCount(2)
            TotalMemoryMB = [math]::Round([GC]::GetTotalMemory($false) / 1MB, 2)
            MaxGeneration = [GC]::MaxGeneration
        }
        $BaseMetrics | Add-Member -NotePropertyName GCStatistics -NotePropertyValue $GCStats
    }
    
    if ($IncludeProcessStats) {
        $ProcessStats = [PSCustomObject]@{
            ThreadCount = $Process.Threads.Count
            HandleCount = $Process.HandleCount
            PagedMemoryMB = [math]::Round($Process.PagedMemorySize64 / 1MB, 2)
            PagedSystemMemoryMB = [math]::Round($Process.PagedSystemMemorySize64 / 1MB, 2)
            NonPagedSystemMemoryMB = [math]::Round($Process.NonpagedSystemMemorySize64 / 1MB, 2)
        }
        $BaseMetrics | Add-Member -NotePropertyName ProcessStatistics -NotePropertyValue $ProcessStats
    }
    
    return $BaseMetrics
}

# Export module functions and aliases
Export-ModuleMember -Function @(
    'Test-BusBuddyHyperthreading',
    'Optimize-BusBuddyParallelism', 
    'Set-BusBuddyPerformanceProfile',
    'Get-BusBuddyMemoryMetrics'
) -Alias @(
    'httest',
    'htopt',
    'perfprof',
    'memmetrics'
) -Variable @(
    'BusBuddyPhysicalCores',
    'BusBuddyLogicalCores',
    'BusBuddyMemoryGB', 
    'BusBuddyHyperthreadRatio',
    'BusBuddyOptimalJobs'
)

# Module initialization
Write-Information "✅ BusBuddy Advanced Module v$script:ModuleVersion loaded" -InformationAction Continue
Write-Information "🔧 Machine: $global:BusBuddyPhysicalCores cores ($global:BusBuddyLogicalCores logical), ${global:BusBuddyMemoryGB}GB RAM" -InformationAction Continue
Write-Information "⚡ Available functions: httest, htopt, perfprof, memmetrics" -InformationAction Continue
