# 🚌 BusBuddy – VS Code Quick Guide (Concise)

## 🧭 Source of truth
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
  - appsettings.json (base, required)
  - appsettings.azure.json (optional Azure/cloud overlay)
  - Environment variables (for secrets)
- Keep Azure-specific settings in `appsettings.azure.json`. Don’t merge into base.

## 🔐 Azure SQL (default): AZ CLI + sqlcmd
- Policy: Use Azure CLI authentication + sqlcmd. PowerShell DB querying is deprecated.
```powershell
# Verify Azure login context
az account show --output table

# Query students (uses your Azure CLI login)
sqlcmd -S tcp:busbuddy-server-sm2.database.windows.net,1433 `
       -d BusBuddyDB `
       --authentication-method ActiveDirectoryAzCli `
       -Q "SELECT TOP 10 * FROM dbo.Students ORDER BY 1;" `
       -W -s ","
```

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

## 🤖 LLM guardrails (do / don’t)
Do
- Use Syncfusion controls (SfDataGrid, SfMap) with FluentDark/FluentLight themes
- Log with Serilog only (structured logging)
- Query Azure SQL via AZ CLI + `sqlcmd --authentication-method ActiveDirectoryAzCli`
- Follow official docs (Syncfusion WPF, .NET, EF Core)

Don’t
- Don’t introduce WPF DataGrid (keep SfDataGrid)
- Don’t use Microsoft.Extensions.Logging
- Don’t write DB queries via PowerShell
- Don’t invent Syncfusion APIs; use documented patterns only

## 🔗 Quick links
- GROK-README.md (status & priorities)
- SETUP-GUIDE.md (environment setup)
- Syncfusion WPF docs: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
- Syncfusion API (WPF): https://help.syncfusion.com/cr/wpf/Syncfusion.html
- EF Core docs: https://learn.microsoft.com/ef/core/
