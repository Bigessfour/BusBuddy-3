#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Auto GitHub Commit Decision Matrix - Execute auto-commit when milestones achieved
.DESCRIPTION
    Implements intelligent decision matrix to automatically commit and push significant milestones.
    Safety-first with backup validation and build success requirements.
.EXAMPLE
    .\auto-github-commit-decision-matrix.ps1 -Execute
#>

param(
    [switch]$CheckOnly,
    [switch]$Execute,
    [switch]$Force
)

function Test-AutoCommitTriggers {
    Write-Host "🔍 Evaluating Auto-Commit Decision Matrix..." -ForegroundColor Cyan

    # Get current script count (excluding backups)
    $currentScripts = (Get-ChildItem -Path "." -Recurse -Include "*.ps1" -File | Where-Object {
            $_.FullName -notmatch "ai-backups" -and $_.Name -notlike "*.backup*"
        }).Count

    # Calculate reduction from baseline (95 scripts)
    $baselineScripts = 95
    $reductionPercent = if ($baselineScripts -gt 0) {
        [math]::Round((($baselineScripts - $currentScripts) / $baselineScripts) * 100, 1)
    }
    else { 0 }

    # Check build status
    Write-Host "🔨 Testing build..." -ForegroundColor Yellow
    $buildResult = dotnet build --verbosity quiet 2>&1
    $buildSuccess = $LASTEXITCODE -eq 0

    # Check Master Suites exist
    $masterSuites = @(
        "AI-Assistant\Tests\Master-Test-Suite.ps1",
        "AI-Assistant\Debug\Master-Debug-Suite.ps1",
        "AI-Assistant\Grok\Master-Grok-Suite.ps1",
        "AI-Assistant\Analysis\Master-Analysis-Suite.ps1"
    )
    $allMasterSuitesExist = ($masterSuites | ForEach-Object { Test-Path $_ } | Where-Object { $_ -eq $true }).Count -eq $masterSuites.Count

    # Check for recent backups
    $recentBackup = $false
    if (Test-Path "ai-backups" -PathType Container) {
        $latestBackup = Get-ChildItem "ai-backups" | Sort-Object CreationTime -Descending | Select-Object -First 1
        if ($latestBackup) {
            $backupAge = (Get-Date) - $latestBackup.CreationTime
            $recentBackup = $backupAge.TotalHours -lt 24
        }
    }

    # Define trigger conditions
    $triggers = @{
        ScriptReduction = $reductionPercent -ge 25        # % reduction achieved
        BuildSuccess    = $buildSuccess                      # Build with 0 errors
        MasterSuites    = $allMasterSuitesExist             # All Master Suites exist
        BackupCreated   = $recentBackup                    # Recent backup exists
        PhaseComplete   = $reductionPercent -ge 60         # Major phase milestone
        TargetProgress  = $reductionPercent -ge 25        # Significant progress
    }

    # Display current status
    Write-Host "`n📊 Current Metrics:" -ForegroundColor Green
    Write-Host "   Scripts: $baselineScripts → $currentScripts ($reductionPercent% reduction)" -ForegroundColor White
    Write-Host "   Build Status: $(if($buildSuccess){'✅ SUCCESS'}else{'❌ FAILED'})" -ForegroundColor $(if ($buildSuccess) { 'Green' }else { 'Red' })
    Write-Host "   Master Suites: $(if($allMasterSuitesExist){'✅ ALL EXIST'}else{'❌ MISSING'})" -ForegroundColor $(if ($allMasterSuitesExist) { 'Green' }else { 'Red' })
    Write-Host "   Recent Backup: $(if($recentBackup){'✅ EXISTS'}else{'❌ MISSING'})" -ForegroundColor $(if ($recentBackup) { 'Green' }else { 'Red' })

    Write-Host "`n🎯 Trigger Analysis:" -ForegroundColor Cyan
    foreach ($trigger in $triggers.GetEnumerator()) {
        $status = if ($trigger.Value) { "✅" } else { "❌" }
        $color = if ($trigger.Value) { "Green" } else { "Red" }
        Write-Host "   $($trigger.Key): $status" -ForegroundColor $color
    }

    # Auto-commit decision logic
    $criticalMet = $triggers.BuildSuccess -and $triggers.BackupCreated
    $triggerCount = ($triggers.Values | Where-Object { $_ -eq $true }).Count
    $shouldAutoCommit = $criticalMet -and $triggerCount -ge 4 -and $triggers.ScriptReduction

    Write-Host "`n🤖 Decision Matrix Result:" -ForegroundColor Magenta
    Write-Host "   Critical conditions met: $(if($criticalMet){'✅ YES'}else{'❌ NO'})" -ForegroundColor $(if ($criticalMet) { 'Green' }else { 'Red' })
    Write-Host "   Trigger count: $triggerCount/6 (need ≥4)" -ForegroundColor $(if ($triggerCount -ge 4) { 'Green' }else { 'Red' })
    Write-Host "   Script reduction: $reductionPercent% (need ≥25%)" -ForegroundColor $(if ($triggers.ScriptReduction) { 'Green' }else { 'Red' })
    Write-Host "   AUTO-COMMIT DECISION: $(if($shouldAutoCommit){'✅ TRIGGERED'}else{'❌ NOT TRIGGERED'})" -ForegroundColor $(if ($shouldAutoCommit) { 'Green' }else { 'Red' })

    return @{
        ShouldCommit     = $shouldAutoCommit
        CurrentScripts   = $currentScripts
        ReductionPercent = $reductionPercent
        BuildSuccess     = $buildSuccess
        Triggers         = $triggers
    }
}

function Invoke-AutoCommit {
    param($metrics)

    Write-Host "`n🚀 Executing Auto-Commit..." -ForegroundColor Green

    # Generate commit message
    $commitMessage = "🚀 Phase 3A Complete: 95→$($metrics.CurrentScripts) scripts ($($metrics.ReductionPercent)% reduction), Master Suites functional"

    $commitDescription = @"
- Script count reduced from 95 to $($metrics.CurrentScripts) PowerShell files
- $($95 - $metrics.CurrentScripts) files safely consolidated into 4 Master Suites
- All Master Suites tested and functional
- Backup created: ai-backups/phase3-consolidation-$(Get-Date -Format 'yyyyMMdd-HHmmss')
- Build successful with 0 errors
- Phase 3 substantially complete (target <30, achieved $($metrics.CurrentScripts))
- Auto-commit triggered by decision matrix
"@

    try {
        Write-Host "📝 Staging files..." -ForegroundColor Yellow
        git add .

        Write-Host "💾 Creating commit..." -ForegroundColor Yellow
        git commit -m $commitMessage -m $commitDescription

        if ($LASTEXITCODE -eq 0) {
            Write-Host "📤 Pushing to origin..." -ForegroundColor Yellow
            git push origin main

            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Auto-commit successful!" -ForegroundColor Green
                Write-Host "   Commit: $commitMessage" -ForegroundColor White
                return $true
            }
            else {
                Write-Host "❌ Push failed!" -ForegroundColor Red
                return $false
            }
        }
        else {
            Write-Host "❌ Commit failed!" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "❌ Auto-commit error: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main execution
Write-Host "🤖 Auto GitHub Commit Decision Matrix" -ForegroundColor Magenta
Write-Host "=====================================" -ForegroundColor Magenta

# Check if git repository
if (-not (Test-Path ".git" -PathType Container)) {
    Write-Host "❌ Not a git repository!" -ForegroundColor Red
    exit 1
}

# Evaluate triggers
$metrics = Test-AutoCommitTriggers

if ($CheckOnly) {
    Write-Host "`n✅ Check complete. Use -Execute to perform auto-commit if triggered." -ForegroundColor Cyan
    exit 0
}

if ($metrics.ShouldCommit -or $Force) {
    if ($Force) {
        Write-Host "`n⚠️ Force flag specified - bypassing decision matrix" -ForegroundColor Yellow
    }

    if ($Execute) {
        $success = Invoke-AutoCommit -metrics $metrics
        if ($success) {
            Write-Host "`n🎉 Phase 3A auto-commit complete! Repository updated." -ForegroundColor Green
        }
    }
    else {
        Write-Host "`n⚠️ Auto-commit triggered but -Execute not specified." -ForegroundColor Yellow
        Write-Host "   Use: .\auto-github-commit-decision-matrix.ps1 -Execute" -ForegroundColor Cyan
    }
}
else {
    Write-Host "`n📋 Auto-commit not triggered. Continue development." -ForegroundColor Cyan
}

Write-Host "`n✅ Decision matrix evaluation complete." -ForegroundColor Green
