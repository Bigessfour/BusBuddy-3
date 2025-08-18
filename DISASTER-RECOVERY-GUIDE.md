# BusBuddy Disaster Recovery & Reinstall Guide

Purpose: Single, authoritative checklist to rebuild a functioning BusBuddy dev/runtime environment after catastrophic machine loss or repo corruption.

Snapshot Reference:

- Latest backup commit (pre-tech visit): 67f6c21 (master)
- Optional archives (if created locally): BusBuddy-git-archive-<timestamp>.zip, BusBuddy-working-backup-<timestamp>.zip

---

## 1. Prerequisites (Install in This Order)

1. Windows 10/11 with latest updates
2. PowerShell 7.5.2 (https://github.com/PowerShell/PowerShell)
3. Git (https://git-scm.com)
4. .NET SDK 9.x per `global.json` (9.0.303) — verify: `dotnet --version`
5. SQL Server LocalDB (installed with Visual Studio Build Tools or SQL Express) OR access to Azure SQL BusBuddyDB
6. (Optional) Visual Studio Code + required extensions:
   - ms-dotnettools.csdevkit
   - ms-vscode.powershell
   - spmeesseman.vscode-taskexplorer
   - ms-dotnettools.xaml
7. Syncfusion WPF license key (environment variable `SYNCFUSION_LICENSE_KEY`)

Environment Variables (User or System scope):

```
SYNCFUSION_LICENSE_KEY=<your_key>
AZURE_SQL_USER=<azure_sql_user>           (only if using Azure SQL)
AZURE_SQL_PASSWORD=<azure_sql_password>   (only if using Azure SQL)
```

---

## 2. Source Code Retrieval

Option A (Git):

```
git clone https://github.com/Bigessfour/BusBuddy-3.git BusBuddy
cd BusBuddy
git checkout 67f6c21   # Or latest master
```

Option B (Zip Archive Backup):

1. Extract `BusBuddy-git-archive-*.zip` to target directory.
2. (If using working backup zip) Extract and then run `git init` + `git add -A` + `git commit -m "Recovered working backup"` (optional version control restart).

Integrity Check:

```
dir Directory.Build.props
dir global.json
```

---

## 3. .NET & Toolchain Verification

```
dotnet --info | findstr /I "Version: 9"
pwsh -Version
```

If version mismatch, install required SDK first.

---

## 4. Package Restore & Initial Build

Preferred (Task Explorer / bb-\* wrappers if still present):

```
dotnet restore BusBuddy.sln
dotnet build BusBuddy.sln -c Debug
```

Expected: 0 errors. Warnings allowed (tech debt tracked in GROK-README.md).

---

## 5. Database Setup

### LocalDB (Dev)

Connection string (default) is in `appsettings.json` / `BusBuddy.Core/appsettings.json`.
Validate instance:

```
sqllocaldb info
```

If DB missing:

```
dotnet tool install --global dotnet-ef --version 9.*
dotnet ef database update --project BusBuddy.Core/BusBuddy.Core.csproj --startup-project BusBuddy.WPF/BusBuddy.WPF.csproj
```

### Azure SQL (Optional)

Ensure firewall / AAD access. Set env vars (`AZURE_SQL_USER`, `AZURE_SQL_PASSWORD`) or use AAD interactive.
Update `DatabaseProvider` in WPF/Core `appsettings.*` if required.

---

## 6. Data Seeding (MVP Baseline)

Seeds: Students (~54), Routes (5), Vehicles (10), Drivers (8).
Scripts (PowerShell 7):

```
pwsh -File .\PowerShell\Scripts\Seed-Mock-Routes.ps1 -WhatIf   # Preview
pwsh -File .\PowerShell\Scripts\Seed-Mock-Routes.ps1          # Apply
pwsh -File .\PowerShell\Scripts\Verify-MVP-Data.ps1            # Counts
```

Expected Counts (post-run): Students=54, Routes=5, Vehicles=10, Drivers=8.

---

## 7. Syncfusion Licensing

Ensure `SYNCFUSION_LICENSE_KEY` is set before running.
Verification: App startup should NOT show license warning dialog.
License registration occurs early in `BusBuddy.WPF/App.xaml.cs`.

---

## 8. Run Application

Primary run (explicit project):

```
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
```

Smoke Checklist:

- Students grid loads (shows ~54 entries or seeded count)
- Route Management view loads; create/edit/delete operational
- Route Assignment view opens; routes, buses, drivers populate
- No runtime exception dialogs

---

## 9. Post-Reinstall Validation

1. Build succeeds (0 errors)
2. UI opens; theming (FluentDark) applied
3. Student add → persists and logs via Serilog (check `BusBuddy.WPF/logs/`)
4. Route assignment: assign a student to a route; in-memory StudentCount increments
5. Commands (Add Stop, Move Up/Down, Assign Vehicle/Driver) enabled appropriately

Optional Tests (if test project stable on environment):

```
dotnet test BusBuddy.sln --no-build
```

---

## 10. Disaster Recovery From Archive Only (No Git)

1. Extract working backup zip
2. Manually recreate solution-level git (optional):

```
git init
git add -A
git commit -m "Recovered from archive"
```

3. Proceed with steps 3–9.

---

## 11. Periodic Backup Recommendations

Automate (Windows Scheduled Task / cron alternative):

```
cd <repo_root>
git pull --rebase
git add -A
git commit -m "auto: daily snapshot" || echo "No changes"
git push origin master
git archive --format=zip -o backups/BusBuddy-git-archive-$(Get-Date -Format yyyyMMdd).zip HEAD
```

Keep last 14 daily archives; prune older.

---

## 12. Common Recovery Pitfalls

| Symptom                             | Cause                                                   | Fix                                            |
| ----------------------------------- | ------------------------------------------------------- | ---------------------------------------------- |
| Syncfusion license dialog           | Missing env var                                         | Set `SYNCFUSION_LICENSE_KEY` then rebuild/run  |
| CS1729 on RouteAssignmentViewModel  | Missing new constructors (added in commit 67f6c21)      | Pull latest master                             |
| Map / geo features disabled         | Non-MVP services disabled in config                     | Re-enable flags in `appsettings.*` post-MVP    |
| Duplicate seed rows                 | Reran seeding scripts against already saturated targets | Scripts are idempotent; review logs; no action |
| WPF build asks which project to run | Using `dotnet run` at solution root                     | Use explicit project argument                  |

---

## 13. Minimal Recovery (Ultra-Fast Path)

If time-constrained:

```
git clone https://github.com/Bigessfour/BusBuddy-3.git
cd BusBuddy-3
dotnet build BusBuddy.sln
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
```

Then (optional) run seeding scripts if local DB is empty.

---

## 14. Verification Script (Optional Inline)

Create `verify.ps1` for quick health check:

```
$errors = 0
dotnet build BusBuddy.sln | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj -- --quick-exit 2>$null
Write-Host "Build & launch smoke completed" -ForegroundColor Green
```

---

## 15. References

- global.json / Directory.Build.props: central version + analyzer policy
- GROK-README.md: rolling change log & tech debt tables
- SETUP-GUIDE.md: extended environment setup
- FILE-FETCHABILITY-GUIDE.md: raw link retrieval patterns
- Microsoft Docs (.NET / EF / PowerShell) & Syncfusion WPF docs (version 30.1.42)

---

## 16. Next Hardening Steps (Optional Post-Recovery)

| Priority | Item                                     | Benefit                   |
| -------- | ---------------------------------------- | ------------------------- |
| High     | Tag snapshot `v0.1.0-mvp-backup`         | Immutable reference point |
| High     | GitHub Release w/ attached zip           | One-click restore asset   |
| Med      | Automated nightly CI artifact (zip)      | Continuous restore points |
| Med      | Add DB migration verification step in CI | Early drift detection     |
| Low      | Scripted local backup rotation           | Storage hygiene           |

Tag Example:

```
git tag -a v0.1.0-mvp-backup -m "MVP backup (post RouteAssignmentViewModel repair)"
git push origin v0.1.0-mvp-backup
```

---

End of Guide
