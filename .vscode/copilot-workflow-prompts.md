# BusBuddy Production-Focused Workflow Prompts

⚡ **Focus:** Get features built## 🛠️ Prod## 🔍 Async Workflow Check (Periodic)
- **Prompt:**
  > Quickly check GitHub Actions results when convenient - during coffee breaks, between features, or when notifications arrive.

- **Command:**
  > ```powershell
  # Quick status glance (30 seconds max) - Windows PowerShell compatible
  Write-Host "📊 Quick CI Status Check..." -ForegroundColor Cyan
  gh run list --limit 3

  # Check latest run details (PowerShell compatible)
  $latestRun = gh run list --limit 1 --json id,conclusion,status,workflowName | ConvertFrom-Json
  if ($latestRun.conclusion -eq "failure") {
      Write-Host "❌ Latest run failed - getting summary..." -ForegroundColor Red
      gh run view $latestRun.id --json jobs | ConvertFrom-Json | ForEach-Object { $_.jobs | Where-Object { $_.conclusion -eq "failure" } | Select-Object name,conclusion }
      Write-Host "🔧 Fix during next development cycle" -ForegroundColor Yellow
  } else {
      Write-Host "✅ All good - keep coding!" -ForegroundColor Green
  }

  # Alternative: Get specific workflow results (no 'head' command needed)
  Write-Host "🔍 Latest workflow summary:" -ForegroundColor Cyan
  gh run view --json conclusion,jobs,workflowName | ConvertFrom-Json | ForEach-Object {
      Write-Host "Workflow: $($_.workflowName)" -ForegroundColor Yellow
      Write-Host "Status: $($_.conclusion)" -ForegroundColor $(if($_.conclusion -eq "success"){"Green"}else{"Red"})
  }
  ```

---

## 🛠️ Industry Fix Cycle (When CI Fails)
- **Prompt:**
  > Handle CI failures the professional way: quick assessment, targeted fix, immediate re-push.

- **Command:**
  > ```powershell
  # Professional failure handling workflow
  Write-Host "🔧 Industry-Standard CI Fix Cycle..." -ForegroundColor Yellow

  # Get failure details (PowerShell compatible)
  $failedRun = gh run list --status failure --limit 1 --json id,conclusion,workflowName | ConvertFrom-Json
  if ($failedRun) {
      Write-Host "❌ Analyzing failure in: $($failedRun.workflowName)" -ForegroundColor Red

      # Get failed job details
      $jobs = gh run view $failedRun.id --json jobs | ConvertFrom-Json
      $failedJobs = $jobs.jobs | Where-Object { $_.conclusion -eq "failure" }

      foreach ($job in $failedJobs) {
          Write-Host "Failed Job: $($job.name)" -ForegroundColor Red
          Write-Host "Next: Check logs and make targeted fix" -ForegroundColor Yellow
      }

      Write-Host "🚀 Professional approach:" -ForegroundColor Cyan
      Write-Host "  1. Make minimal fix" -ForegroundColor White
      Write-Host "  2. git add . && git commit -m 'fix: address CI failure' && git push" -ForegroundColor White
      Write-Host "  3. Continue coding immediately" -ForegroundColor White
      Write-Host "  4. Check results via notifications" -ForegroundColor White
  } else {
      Write-Host "✅ No recent failures - all systems green!" -ForegroundColor Green
  }
  ```ocus

**Industry Standard Workflow:**
1. **🚀 Ship Fast** - Push and immediately continue coding
2. **📱 Async Monitoring** - Use notifications, not active waiting
3. **🔄 Parallel Development** - Work on next feature while CI validates current
4. **⚡ Periodic Checks** - Quick status glances during natural breaks
5. **🎯 Notification-Driven** - Act on CI results when convenient, not immediately

**Professional Flow:** `bb-daily` → code → `git push` → continue coding → check notifications → fix if needed

---

## 🔔 Setup Industry Notifications (One-Time)
- **Prompt:**
  > Configure professional-grade notifications for GitHub Actions so you never wait around for CI.

- **Command:**
  > ```powershell
  Write-Host "🔔 Setting up industry-standard CI notifications..." -ForegroundColor Cyan
  Write-Host ""
  Write-Host "✅ Already configured:" -ForegroundColor Green
  Write-Host "   📧 Email notifications (you heard 4 dings earlier!)" -ForegroundColor Cyan
  Write-Host ""
  Write-Host "🔧 Recommended additions:" -ForegroundColor Yellow
  Write-Host "   📱 Install GitHub Mobile app for push notifications"
  Write-Host "   🔌 VS Code GitHub extension (already installed)"
  Write-Host "   💬 Slack/Teams integration (if team development)"
  Write-Host ""
  Write-Host "💡 Professional tip: Never wait for CI - notifications will tell you when to act!"
  ```

---d fast. Less PowerShell, more production code.

Quick access prompts to keep development moving toward production goals with minimal workflow overhead.

---


## 🚀 Quick Dev Start
- **Prompt:**
  > Start coding immediately. Build, test, run—no PowerShell tweaking.

- **Command:**
  > `bb-daily` (builds, tests, runs app in one command)

---

## 🏗️ Build & Ship
- **Prompt:**
  > Build the solution, run tests, fix any warnings. Focus on production readiness.

- **Command:**
  > `bb-build && bb-test && bb-warning-analysis`

---

## 🔧 Quick Fix Cycle
- **Prompt:**
  > Fast iteration: build → test → fix → commit. Stay in the code, not the tooling.

- **Command:**
  > Build: `bb-build -Quick`
  > Test: `bb-test -Fast`
  > Commit: `git add . && git commit -m "fix: quick iteration" && git push`

---

## 📦 Ship & Continue (Industry Standard)
- **Prompt:**
  > Push to GitHub Actions testing platform and immediately continue development. Check results asynchronously via notifications.

- **Command:**
  > ```powershell
  # Ship current work
  git add .
  git commit -m "feat: implement feature X"
  git push

  # Immediately continue development (industry standard)
  Write-Host "🚀 Shipped to GitHub Actions Testing Platform" -ForegroundColor Green
  Write-Host "📱 Monitor via email notifications or GitHub extension" -ForegroundColor Cyan
  Write-Host "⚡ Continue coding - don't wait for CI!" -ForegroundColor Yellow

  # Optional: Start next feature branch
  # git checkout -b feature/next-feature
  ```

---

## 🔍 Async Workflow Check (Periodic)
- **Prompt:**
  > Quickly check GitHub Actions results when convenient - during coffee breaks, between features, or when notifications arrive.

- **Command:**
  > ```powershell
  # Quick status glance (30 seconds max)
  Write-Host "📊 Quick CI Status Check..." -ForegroundColor Cyan
  gh run list --limit 3

  # If any failures, get summary only
  $failedRuns = gh run list --status failure --limit 1 --json conclusion,id,headSha
  if ($failedRuns) {
      Write-Host "❌ Found failed run - quick summary:" -ForegroundColor Red
      gh run view --json jobs,conclusion,workflowName
      Write-Host "� Fix during next development cycle" -ForegroundColor Yellow
  } else {
      Write-Host "✅ All good - keep coding!" -ForegroundColor Green
  }
  ```

## �️ Production Development Focus

**Use these prompts to:**
1. **🎯 Stay in production code** - Less tooling, more features
2. **⚡ Move fast** - Quick build-test-ship cycles
3. **🚀 Ship often** - Let GitHub Actions handle validation
4. **� Fix and iterate** - Stay in the development flow

**Simple workflow:** `bb-daily` → code → `bb-build && bb-test` → `git push` → repeat

---
