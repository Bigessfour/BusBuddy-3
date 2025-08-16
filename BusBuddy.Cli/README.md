# BusBuddy.Cli

A .NET console app for heavier project tasks that complement the PowerShell bb* workflow. It does not replace PowerShell; use it for scenarios that benefit from compiled .NET and libraries like EF Core and SMO.

- Commands (via System.CommandLine):
  - `migrate` — run Azure SQL migration scripts with SMO (dry-run supported)
  - `analyze` — simple code analysis output (json/xml/console)
  - `health` — environment health probe
  - `cleanup` — simulated cleanup of artifacts

Examples

```powershell
# health check (verbose)
dotnet run --project BusBuddy.Cli/BusBuddy.Cli.csproj health --verbose

# migration (dry-run)
dotnet run --project BusBuddy.Cli/BusBuddy.Cli.csproj migrate --connection-string "<conn>" --script-path "migration-script.sql" --dry-run
```

Notes
- This CLI is optional. Primary dev workflow remains PowerShell aliases: `bbBuild`, `bbRun`, `bbTest`, `bbHealth`, `bbAntiRegression`, `bbXamlValidate`.
- Docs
  - System.CommandLine: https://learn.microsoft.com/dotnet/standard/commandline/
  - EF Core CLI: https://learn.microsoft.com/ef/core/cli/dotnet
  - Azure SQL: https://learn.microsoft.com/azure/azure-sql/
