# BusBuddy Database Configuration Update

# Deploy to Azure SQL
.\Scripts\deploy-azure-sql.ps1 -ServerName busbuddy-server-sm2 -DatabaseName BusBuddyDB -AdminUsername your_admin_username -ResourceGroup BusBuddy -CreateIfNotExists

# Switch to Azure provider
.\Scripts\switch-database-provider.ps1 -Provider Azure

# Set environment variables for Azure SQL authentication
[Environment]::SetEnvironmentVariable("AZURE_SQL_USER", "your_username", "User")
[Environment]::SetEnvironmentVariable("AZURE_SQL_PASSWORD", "your_password", "User")

# Test Azure SQL connection (recommended after switching):
.\Scripts\Test-AzureConnection.ps1

#### One-Command Azure Setup (Single-User Convenience)
Add this function to your PowerShell profile or module for a streamlined Azure setup:
```powershell
function bb-azure-setup {
    .\Scripts\deploy-azure-sql.ps1 -ServerName busbuddy-server-sm2 -DatabaseName BusBuddyDB -ResourceGroup BusBuddy -CreateIfNotExists
    .\Scripts\switch-database-provider.ps1 -Provider Azure
}
```
Run `bb-azure-setup` to deploy, configure firewall, set credentials, and switch provider in one step.

### Legacy Support (SQLite)
```powershell
# Switch to SQLite provider
.\Scripts\switch-database-provider.ps1 -Provider SQLite
```

### Seeding Data (Development/Test Only)
```powershell
# Run unified seeding service (development/test only)
dotnet run --project BusBuddy.WPF -- seed
```


### Implementation Details

#### Connection String Format
- **LocalDB**: `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True`
- **Azure SQL**: `Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
- **SQLite**: `Data Source=BusBuddy.db`

#### Seeding Service Details
- All seeding logic is in `SeedDataService` (C#).
- Seeding is only available in development/test environments.
- No JSON import, no legacy/phase-specific seeding code remains.
- To clear seeded data: use the `ClearSeedDataAsync` method in `SeedDataService`.

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
   .\Scripts\Test-AzureConnection.ps1
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

---

## Improvement Status Table

| Improvement                | Description                                 | Status      |
|----------------------------|---------------------------------------------|-------------------------------|
| Azure Key Vault            | Use Key Vault for secrets                   | Planned (Optional for Single-User) |
| Firewall Automation        | Automate IP rules with Azure CLI            | In Progress |
| Integration Testing        | xUnit tests for Azure SQL                   | Planned (Optional for Single-User) |
| Backup/Disaster Recovery   | Document retention/restore                  | Planned (Optional for Single-User) |
| Azure Monitoring           | Serilog + Application Insights              | Planned (Optional for Single-User) |
| CI/CD with GitHub Actions  | Automated deploy workflow                   | Planned (Optional for Single-User) |
| Cost Monitoring            | Script for Azure SQL cost                   | Planned (Optional for Single-User) |
| Cross-Reference Docs       | Absolute GitHub URLs for code/scripts       | In Progress |

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
