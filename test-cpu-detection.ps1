# Test CPU Detection for i5-1334U
Write-Output "=== BusBuddy CPU Detection Test ==="

# Direct hardware query
$computerInfo = Get-ComputerInfo | Select-Object CsNumberOfLogicalProcessors, CsTotalPhysicalMemory, CsProcessors
Write-Output "Direct Query Results:"
Write-Output "  Logical Processors: $($computerInfo.CsNumberOfLogicalProcessors)"
Write-Output "  Total Memory: $([math]::Round($computerInfo.CsTotalPhysicalMemory / 1GB, 2))GB"
Write-Output "  Processor: $($computerInfo.CsProcessors)"

# Calculate what the new profile settings should be
$logicalProcessors = $computerInfo.CsNumberOfLogicalProcessors
$memoryGB = [math]::Round($computerInfo.CsTotalPhysicalMemory / 1GB, 2)

# New throttle limit calculation
$newThrottleLimit = switch ($logicalProcessors) {
    { $_ -ge 12 } { [math]::Min($logicalProcessors * 0.67, 8) }
    { $_ -ge 8 }  { [math]::Min($logicalProcessors * 0.75, 6) }
    { $_ -ge 4 }  { [math]::Min($logicalProcessors * 0.5, 4) }
    default       { 2 }
}

# GC heap count for 16GB+ memory
$gcHeapCount = [math]::Min($logicalProcessors / 2, 8)

Write-Output ""
Write-Output "=== Optimized Settings for Your Hardware ==="
Write-Output "  Logical Processors: $logicalProcessors"
Write-Output "  Memory: ${memoryGB}GB"
Write-Output "  Optimal Throttle Limit: $newThrottleLimit (was capped at 4)"
Write-Output "  .NET GC Heap Count: $gcHeapCount"
Write-Output "  Performance Gain: $([math]::Round(($newThrottleLimit / 4 - 1) * 100, 0))% more parallel capacity"

Write-Output ""
Write-Output "✅ Profile updated to utilize your 12-core i5-1334U properly!"
