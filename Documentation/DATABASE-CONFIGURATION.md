# BusBuddy Database Configuration Update

## Overview

This document summarizes the implementation of a multi-environment database strategy for BusBuddy:

- SQL Server LocalDB for development
- Azure SQL Database for production
- SQLite for legacy support (Phase 1 compatibility)

## Key Components

### 1. Connection Strings

Updated connection strings in:

- `BusBuddy.Core/appsettings.json`
- `BusBuddy.WPF/appsettings.json`
- `BusBuddy.Core/appsettings.Development.json`
- `BusBuddy.Core/appsettings.Production.json`

### 2. Environment Helper

Enhanced the `EnvironmentHelper` class with methods to:

- Detect current environment (Development/Production)
- Identify database provider (LocalDB/Azure/SQLite)
- Select appropriate connection string

<<<<<<< HEAD

### 3. Database Context

=======

### 3. Database Context & Seeding

> > > > > > > df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))
> > > > > > > Modified `BusBuddyDbContext.cs` to:

- Support SQL Server LocalDB for development
- Optimize Azure SQL for production
- Maintain SQLite for legacy support (Phase 1 compatibility)
- Configure provider-specific optimizations
- Eliminate duplicate context factories that cause EF conflicts

# <<<<<<< HEAD

#### Seeding Strategy (2025+)

- **Unified seeding service**: All development/test seeding logic is now in `SeedDataService` (see `BusBuddy.Core/Services/SeedDataService.cs`).
- **No JSON or legacy seeding**: All JSON import and legacy/phase seeding services have been removed.
- **Manual entry for production**: Production and real data entry is always manual; seeding is for development/testing only.
- **Opt-in seeding**: Seeding must be explicitly invoked in development/test environments and is never run in production.

> > > > > > > df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))

### 4. Utility Scripts

Created PowerShell scripts for database management:

- `Scripts\Setup\setup-localdb.ps1`: Sets up LocalDB for development
- `deploy-azure-sql.ps1`: Deploys schema to Azure SQL
- `switch-database-provider.ps1`: Switches between providers

<<<<<<< HEAD

## 🚀 Azure SQL Production-Readiness Improvements (2025)

### High Priority

```json
"User ID=@Microsoft.KeyVault(VaultName=busbuddy-vault;SecretName=sql-user)"
```

- Add a PowerShell function to deployment scripts to fetch secrets from Key Vault.
- Reference: [Microsoft Docs: Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/general/)

#### Automate Firewall Rule Management

- Extend `deploy-azure-sql.ps1` to add/remove firewall rules automatically using Azure CLI:
  ```powershell
  $ip = (Invoke-WebRequest -Uri "https://api.ipify.org").Content
  az sql server firewall-rule create --resource-group BusBuddy-RG --server busbuddy-server-sm2 --name DevIP --start-ip-address $ip --end-ip-address $ip
  ```
- Add a cleanup option for old rules.

#### Implement Integration Testing for Azure SQL

- Add xUnit integration tests in `BusBuddy.Tests` using a test Azure SQL DB or EF InMemory.
- Run tests via CI/CD or local scripts to validate schema and connection.

### Medium Priority

#### Document Backup and Disaster Recovery

- Add a section on enabling long-term retention and restore commands in Azure SQL.
- Update NuGet.config if needed for the sink package.

#### CI/CD Integration with GitHub Actions

- Add `.github/workflows/deploy.yml` to automate deployments using `deploy-azure-sql.ps1` on push to main.
- Use GitHub Secrets for credentials.

### Low Priority

#### Cost Monitoring and Tier Evaluation

- Add a script (e.g., `bb-azure-cost.ps1`) using `az costmanagement query` to monitor Azure SQL costs.
- Document upgrade paths from Free to Standard tiers.

- Add a link to `DATABASE-CONFIGURATION.md` in the main README under Architecture/Database.
- Ensure all script paths exist and are documented for onboarding.

---

> > > > > > > df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))

## Usage Instructions

### Development Environment (LocalDB)

```powershell
# Set up LocalDB
.\Scripts\Setup\setup-localdb.ps1

# Switch to LocalDB provider
<<<<<<< HEAD
.\switch-database-provider.ps1 -Provider LocalDB
=======
.\Scripts\switch-database-provider.ps1 -Provider LocalDB

# (Optional) Seed development/test data
dotnet run --project BusBuddy.WPF -- seed
>>>>>>> df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))
```

### Production Environment (Azure SQL)

```powershell
# Deploy to Azure SQL
<<<<<<< HEAD
.\deploy-azure-sql.ps1 -ServerName busbuddy-server-sm2 -DatabaseName BusBuddyDB -AdminUsername your_admin_username -ResourceGroup BusBuddy -CreateIfNotExists

# Switch to Azure provider
.\switch-database-provider.ps1 -Provider Azure

# Set environment variables for Azure SQL authentication
[Environment]::SetEnvironmentVariable("AZURE_SQL_USER", "your_username", "User")
[Environment]::SetEnvironmentVariable("AZURE_SQL_PASSWORD", "your_password", "User")
```

### Legacy Support (SQLite)

```powershell
# Switch to SQLite provider
.\switch-database-provider.ps1 -Provider SQLite
```

## Implementation Details

### Connection String Format

=======
.\Scripts\deploy-azure-sql.ps1 -ServerName busbuddy-server-sm2 -DatabaseName BusBuddyDB -AdminUsername your_admin_username -ResourceGroup BusBuddy -CreateIfNotExists

# Switch to Azure provider

.\Scripts\switch-database-provider.ps1 -Provider Azure

# Validate Azure SQL configuration and test connectivity (recommended after switching):

```powershell
. ./PowerShell/Azure-SQL-Diagnostic.ps1
Test-BusBuddyAzureSql
```

This method checks environment variables, prints the current configuration, and runs the connectivity test using `Test-AzureConnection.ps1` if available. It is PowerShell 7.5.2 compliant and can be loaded in your profile for convenience.

````

#### One-Command Azure Setup (Single-User Convenience)
Add this function to your PowerShell profile or module for a streamlined Azure setup:
```powershell
function bb-azure-setup {
    .\Scripts\switch-database-provider.ps1 -Provider Azure
}
````

Run `bb-azure-setup` to deploy, configure firewall, set credentials, and switch provider in one step.

### Legacy Support (SQLite)

```powershell
# Switch to SQLite provider
.\Scripts\switch-database-provider.ps1 -Provider SQLite
```

### Seeding Data (Development/Test Only)

```powershell
# Run unified seeding service (development/test only)
```

### Implementation Details

#### Connection String Format

> > > > > > > df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))

- **LocalDB**: `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True`
- **Azure SQL**: `Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
- **SQLite**: `Data Source=BusBuddy.db`

# <<<<<<< HEAD

#### Seeding Service Details

- All seeding logic is in `SeedDataService` (C#).
- Seeding is only available in development/test environments.
- No JSON import, no legacy/phase-specific seeding code remains.
- To clear seeded data: use the `ClearSeedDataAsync` method in `SeedDataService`.

> > > > > > > df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))

### Microsoft Azure SQL Connection Standards

Based on [Microsoft ADO.NET SQL Server Documentation](https://learn.microsoft.com/en-us/sql/connect/ado-net/microsoft-ado-net-sql-server):

- **MultipleActiveResultSets=False** (recommended for new applications)
- **Encrypt=True** (required for Azure SQL)
- **TrustServerCertificate=False** (security best practice)
- **Connection Timeout=30** (sufficient for most operations)

### Azure SQL Production Configuration

- **Subscription ID**: `57b297a5-44cf-4abc-9ac4-91a5ed147de1`
- **Resource Group**: `BusBuddy-RG`
- **Server**: `busbuddy-server-sm2.database.windows.net`
- **Server Resource Path**: `/subscriptions/57b297a5-44cf-4abc-9ac4-91a5ed147de1/resourceGroups/BusBuddy-RG/providers/Microsoft.Sql/servers/busbuddy-server-sm2`
- **Database**: `BusBuddyDB`
- **Admin User**: `busbuddy_admin` (use environment variable AZURE_SQL_USER)
- **Admin Password**: Use environment variable AZURE_SQL_PASSWORD
- **Tier**: Free General Purpose Serverless (Gen5, 2 vCores)
- **Location**: Central US

### Azure SQL Firewall Configuration

**⚠️ IMPORTANT**: Azure SQL requires firewall rules to allow connections.

To connect from your development machine:

1. **Get your public IP address**:

   ```powershell
   (Invoke-WebRequest -Uri "https://api.ipify.org").Content
   ```

2. **Add firewall rule in Azure Portal**:
   - Go to Azure Portal → SQL databases → BusBuddyDB
   - Select "Set server firewall"
   - Add rule: Name="Development", Start IP=your_ip, End IP=your_ip
   - **OR** temporarily enable "Allow Azure services and resources to access this server"

3. **Test connection**:
   ```powershell
   <<<<<<< HEAD
   .\Test-AzureConnection.ps1
   =======
   .\Scripts\Test-AzureConnection.ps1
   >>>>>>> df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))
   ```

### Common Azure SQL Issues

- **Connection Timeout**: Usually firewall blocking connection
- **Login Failed (18456)**: Wrong username/password
- **Database Not Found (4060)**: Database doesn't exist or no permissions
- **Server Paused**: Serverless tier may auto-pause (will auto-resume on first connection)

### Environment Detection

The system detects the environment using:

1. `IConfiguration["Environment"]` setting
2. `ASPNETCORE_ENVIRONMENT` environment variable
3. Falls back to "Production" if neither is specified

### Provider Selection

The provider is selected from:

1. `DatabaseProvider` configuration setting
2. Falls back to "LocalDB" if not specified
   <<<<<<< HEAD
   =======

---

## Improvement Status Table

| Improvement               | Description                           | Status                             |
| ------------------------- | ------------------------------------- | ---------------------------------- |
| Azure Key Vault           | Use Key Vault for secrets             | Planned (Optional for Single-User) |
| Firewall Automation       | Automate IP rules with Azure CLI      | In Progress                        |
| Integration Testing       | xUnit tests for Azure SQL             | Planned (Optional for Single-User) |
| Backup/Disaster Recovery  | Document retention/restore            | Planned (Optional for Single-User) |
| Azure Monitoring          | Serilog + Application Insights        | Planned (Optional for Single-User) |
| CI/CD with GitHub Actions | Automated deploy workflow             | Planned (Optional for Single-User) |
| Cost Monitoring           | Script for Azure SQL cost             | Planned (Optional for Single-User) |
| Cross-Reference Docs      | Absolute GitHub URLs for code/scripts | In Progress                        |

## Azure Key Vault Setup

1. Create vault:
   ```powershell
   az keyvault create --name busbuddy-vault --resource-group BusBuddy-RG --location centralus
   ```
2. Add secrets:
   ```powershell
   az keyvault secret set --vault-name busbuddy-vault --name sql-user --value busbuddy_admin
   az keyvault secret set --vault-name busbuddy-vault --name sql-password --value <your_password>
   ```
3. Grant access: Update `appsettings.Production.json` with Key Vault bindings.

## Application Insights Setup

1. Create Application Insights resource:
   ```powershell
   az monitor app-insights component create --app busbuddy-insights --resource-group BusBuddy-RG --location centralus
   ```
2. Example configuration for `appsettings.Production.json`:
   ```json
   "Serilog": {
     "WriteTo": [
       { "Name": "AzureAnalytics", "Args": { "workspaceId": "your_workspace_id" } }
     ]
   }
   ```

## Sample GitHub Actions Workflow

```yaml
name: Deploy Azure SQL
on: push
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - run: pwsh ./Scripts/deploy-azure-sql.ps1 -ServerName busbuddy-server-sm2 -DatabaseName BusBuddyDB
        env:
          AZURE_SQL_USER: ${{ secrets.AZURE_SQL_USER }}
          AZURE_SQL_PASSWORD: ${{ secrets.AZURE_SQL_PASSWORD }}
```

## Sample Cost Monitoring Script

```powershell
try {
    $cost = az costmanagement query --type Usage --timeframe MonthToDate --scope /subscriptions/57b297a5-44cf-4abc-9ac4-91a5ed147de1
    Write-Output "Current Azure SQL cost: $cost"
} catch {
    Write-Error "Failed to fetch Azure SQL cost: $_"
    exit 1
}
```

## Supporting Code References (with Absolute URLs)

- **DbContext Implementation:** [BusBuddyDbContext.cs](https://github.com/Bigessfour/BusBuddy-2/blob/main/BusBuddy.Core/BusBuddyDbContext.cs)
- **Seeding Service:** [SeedDataService.cs](https://github.com/Bigessfour/BusBuddy-2/blob/main/BusBuddy.Core/Services/SeedDataService.cs)
- **Environment Helper:** [EnvironmentHelper.cs](https://github.com/Bigessfour/BusBuddy-2/blob/main/BusBuddy.Core/Utilities/EnvironmentHelper.cs)
- **Sample App Configuration:** [appsettings.json](https://github.com/Bigessfour/BusBuddy-2/blob/main/BusBuddy.Core/appsettings.json)
- **Test Project:** [BusBuddy.Tests](https://github.com/Bigessfour/BusBuddy-2/tree/main/BusBuddy.Tests)
- **PowerShell Scripts:**
  - [setup-localdb.ps1](https://github.com/Bigessfour/BusBuddy-2/blob/main/Scripts/Setup/setup-localdb.ps1)
  - [deploy-azure-sql.ps1](https://github.com/Bigessfour/BusBuddy-2/blob/main/Scripts/deploy-azure-sql.ps1)
  - [switch-database-provider.ps1](https://github.com/Bigessfour/BusBuddy-2/blob/main/Scripts/switch-database-provider.ps1)

---

## Testing (Integration)

Once integration tests are implemented, after switching to Azure, run:

```powershell
bb-test -TestSuite Integration
```

to validate Azure SQL connectivity and schema. For now, manual testing with `.\Scripts\Test-AzureConnection.ps1` is sufficient for single-user setups.

---

This setup positions BusBuddy as a robust, Azure-native app. For deeper dives, see the above code and script links. If you provide more files (e.g., actual scripts or DbContext code), further review and guidance can be provided.

> > > > > > > df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))
