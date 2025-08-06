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

### 3. Database Context
Modified `BusBuddyDbContext.cs` to:
- Support SQL Server LocalDB for development
- Optimize Azure SQL for production
- Maintain SQLite for legacy support (Phase 1 compatibility)
- Configure provider-specific optimizations
- Eliminate duplicate context factories that cause EF conflicts

### 4. Utility Scripts
Created PowerShell scripts for database management:
- `Scripts\Setup\setup-localdb.ps1`: Sets up LocalDB for development
- `deploy-azure-sql.ps1`: Deploys schema to Azure SQL
- `switch-database-provider.ps1`: Switches between providers

## Usage Instructions

### Development Environment (LocalDB)
```powershell
# Set up LocalDB
.\Scripts\Setup\setup-localdb.ps1

# Switch to LocalDB provider
.\switch-database-provider.ps1 -Provider LocalDB
```

### Production Environment (Azure SQL)
```powershell
# Deploy to Azure SQL
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
- **LocalDB**: `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True`
- **Azure SQL**: `Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
- **SQLite**: `Data Source=BusBuddy.db`

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
   .\Test-AzureConnection.ps1
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
