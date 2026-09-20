# 🚌 BusBuddy – VS Code Quick Guide (Conci## ✅ Current snapshot (Aug 22Don't

- Don't introduce WPF DataGrid (keep SfDataGrid)
- Don't use Microsoft.Extensions.Logging
- Don't write DB queries via PowerShell
- Don't invent Syncfusion APIs; use documented patterns only
- Don't hardcode secrets in PowerShell scripts; always use environment variables)

- Clean build; EF Core aligned
- UI buttons/forms validated across Students, Drivers, Vehicles, Activities
- Syncfusion-only UI policy enforced (no standard WPF DataGrid)
- Geo stack wired: SfMap + overlays + offline geocoding; eligibility (in district AND not in town)
- Students "Add" flow works end-to-end; save + list refresh verified## 🧭 Source of truth

- Canonical status and priorities: see `GROK-README.md` (project root).
- This page is a short, skimmable guide for day-to-day dev in VS Code.
- If anything conflicts, prefer `GROK-README.md` and the docs it links.

## 🚀 Build & run

```powershell
# Build
dotnet build BusBuddy.sln

# Run (WPF)
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
```

## ⚙️ Config overlays (what to edit)

- The app merges configuration at runtime (App.xaml.cs):
    - appsettings.json (base, required) — `DatabaseProvider` is Postgres
    - Environment variables (for secrets and `BUSBUDDY_CONNECTION`)

## 🔐 Postgres (default)

- Mac Docker Postgres (`docker compose --profile db up -d`). Guest uses `BUSBUDDY_CONNECTION` with the Mac host IP.
- See `Documentation/DATABASE-CONFIGURATION.md`.

## ✅ Current snapshot (Aug 10, 2025)

- Clean build; EF Core aligned
- UI buttons/forms validated across Students, Drivers, Vehicles, Activities
- Syncfusion-only UI policy enforced (no standard WPF DataGrid)
- Geo stack wired: SfMap + overlays + offline geocoding; eligibility (in district AND not in town)
- Students “Add” flow works end-to-end; save + list refresh verified

Active work

- Migration history sync vs existing schema
- Persist Student.Latitude/Longitude on plot
- Route assignment flows leveraging plotted students
- Optimize CI pipeline for faster runs by caching NuGet packages

## 🤖 LLM guardrails (do / don’t)

Do

- Use Syncfusion controls (SfDataGrid, SfMap) with FluentDark/FluentLight themes
- Log with Serilog only (structured logging)
- Query Postgres via `BUSBUDDY_CONNECTION` / Docker, not ad-hoc PowerShell SQL
- Follow official docs (Syncfusion WPF, .NET, EF Core)
- Use Trunk for all formatting and linting to enforce consistency

Don’t

- Don’t introduce WPF DataGrid (keep SfDataGrid)
- Don’t use Microsoft.Extensions.Logging
- Don’t write DB queries via PowerShell
- Don’t invent Syncfusion APIs; use documented patterns only

## 🔗 Quick links

- GROK-README.md (status & priorities)
- SETUP-GUIDE.md (environment setup)
- .github/copilot-instructions.md (AI assistant guidance)
- Syncfusion WPF docs: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- Syncfusion API (WPF): https://help.syncfusion.com/cr/wpf/Syncfusion.html
- EF Core docs: https://learn.microsoft.com/ef/core/
- Postgres / EF Core: https://learn.microsoft.com/ef/core/providers/npgsql/
