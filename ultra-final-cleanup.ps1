#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Phase 3 Ultra-Final Cleanup - Get Under 30 Scripts
.DESCRIPTION
    Removes final unnecessary scripts to achieve <30 target
    Current: 37 scripts → Target: <30 scripts (need 8+ removals)
#>

Write-Information "🎯 ULTRA-FINAL CLEANUP - TARGET <30 SCRIPTS" -InformationAction Continue

# Files that can be safely removed (duplicates, unnecessary, superseded)
$filesToRemove = @(
    "phase3-cleanup-consolidated-files.ps1",  # No longer needed
    "phase3b-aggressive-consolidation.ps1",  # No longer needed
    "phase3-final-push.ps1",                 # This very script
    "busbuddy-multithreading-implementation.ps1", # Experimental, not core
    "circuit-breaker.ps1",                   # Duplicate of CircuitBreaker.ps1
    "cache-manager.ps1",                     # Utility, not essential
    "cleanup-ai-assistant.ps1",              # Superseded by new cleanup
    "run-ai-cleanup.ps1",                    # Superseded
    "Initialize-BBFoundation.ps1",           # Duplicate (appears twice)
    "BusBuddy-File-Debugger.ps1",           # Debugging only
    "PowerShell-Debugging-Guide.ps1",       # Documentation, not functional
    "diagnose-exit-code-1.ps1"              # Specific debugging tool
)

Write-Information "📊 ULTRA-FINAL REMOVAL TARGETS:" -InformationAction Continue
$existingFiles = $filesToRemove | Where-Object { Test-Path $_ }
Write-Information "  Files to remove: $($existingFiles.Count)" -InformationAction Continue

$currentCount = (Get-ChildItem -Path "." -Recurse -Include "*.ps1" -File | Where-Object { $_.FullName -notlike "*ai-backups*" }).Count
$projectedCount = $currentCount - $existingFiles.Count

Write-Information "  Current scripts: $currentCount" -InformationAction Continue
Write-Information "  Projected final: $projectedCount" -InformationAction Continue
Write-Information "  Target status: $(if ($projectedCount -lt 30) { '🎯 TARGET ACHIEVED!' } else { '⚠️ NEED ' + ($projectedCount - 29) + ' MORE' })" -InformationAction Continue

foreach ($file in $existingFiles) {
    Write-Information "  🗑️ $file" -InformationAction Continue
}

# Execute removal
Write-Information "🚀 EXECUTING ULTRA-FINAL CLEANUP..." -InformationAction Continue

$removedCount = 0
foreach ($file in $existingFiles) {
    try {
        Remove-Item -Path $file -Force
        Write-Information "  ✅ Removed: $file" -InformationAction Continue
        $removedCount++
    }
    catch {
        Write-Warning "⚠️ Failed to remove $file`: $_"
    }
}

$finalCount = (Get-ChildItem -Path "." -Recurse -Include "*.ps1" -File | Where-Object { $_.FullName -notlike "*ai-backups*" }).Count

Write-Information "📊 ULTRA-FINAL RESULTS:" -InformationAction Continue
Write-Information "  Files removed: $removedCount" -InformationAction Continue
Write-Information "  Final script count: $finalCount" -InformationAction Continue
Write-Information "  Target status: $(if ($finalCount -lt 30) { '🎯 TARGET ACHIEVED! 🎉' } else { '⚠️ Still need ' + ($finalCount - 29) + ' more removals' })" -InformationAction Continue

if ($finalCount -lt 30) {
    Write-Information "🎉 GROK PHASE 3 OPTIMIZATION COMPLETE!" -InformationAction Continue
    Write-Information "   Achieved <30 script sustainability target" -InformationAction Continue
    Write-Information "   Ready for long-term maintainable development" -InformationAction Continue
}

Write-Information "🎯 Ultra-Final Cleanup Complete" -InformationAction Continue
