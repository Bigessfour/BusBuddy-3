# Database Connectivity Scripts

This folder contains PowerShell scripts to resolve common database connectivity issues with BusBuddy.

## Scripts Overview

### 1. Add-AzureSqlFirewallRule.ps1
**Purpose**: Automatically adds your current public IP to Azure SQL Server firewall rules.

**Usage**:
```powershell
# Basic usage
.\Add-AzureSqlFirewallRule.ps1 -ResourceGroup "BusBuddy-RG" -ServerName "busbuddy-server-sm2"

# Custom rule name
.\Add-AzureSqlFirewallRule.ps1 -ResourceGroup "BusBuddy-RG" -ServerName "busbuddy-server-sm2" -RuleName "DevMachine-IP"

# Test run (shows what would be done)
.\Add-AzureSqlFirewallRule.ps1 -ResourceGroup "BusBuddy-RG" -ServerName "busbuddy-server-sm2" -WhatIf
```

**Requirements**:
- Azure CLI installed and authenticated (`az login`)
- Contributor or SQL Server Contributor role on the resource group
- Public internet access to detect your IP

### 2. Install-LocalDB.ps1
**Purpose**: Installs SQL Server LocalDB and creates the default instance for local development.

**Usage**:
```powershell
# Install and setup LocalDB
.\Install-LocalDB.ps1

# Install and test connection
.\Install-LocalDB.ps1 -TestConnection

# Skip auto-install, just show manual instructions
.\Install-LocalDB.ps1 -SkipInstall
```

**What it does**:
- Checks if LocalDB is already installed
- Attempts installation via Chocolatey (if available)
- Creates and starts the default `MSSQLLocalDB` instance
- Optionally tests the connection

### 3. Update-AppSettingsForLocalDB.ps1
**Purpose**: Updates BusBuddy.WPF appsettings.json to use LocalDB instead of Azure SQL.

**Usage**:
```powershell
# Update appsettings in the WPF project
.\Update-AppSettingsForLocalDB.ps1

# Create backup before updating
.\Update-AppSettingsForLocalDB.ps1 -BackupOriginal

# Update appsettings in a different project path
.\Update-AppSettingsForLocalDB.ps1 -WpfProjectPath "C:\path\to\BusBuddy.WPF"
```

**Changes made**:
- Sets `DatabaseProvider` to `"LocalDB"`
- Updates `ConnectionStrings.DefaultConnection` to use LocalDB
- Preserves other settings

## Common Scenarios

### Scenario 1: Azure SQL Firewall Block
**Error**: `Microsoft.Data.SqlClient.SqlException (40615): Cannot open server... Client with IP address 'X.X.X.X' is not allowed`

**Solution**:
```powershell
# Quick fix - add your current IP
.\Add-AzureSqlFirewallRule.ps1 -ResourceGroup "YourRG" -ServerName "YourServer"
```

### Scenario 2: LocalDB Not Installed
**Error**: `A network-related or instance-specific error... Unable to locate a Local Database Runtime installation`

**Solution**:
```powershell
# Install and configure LocalDB
.\Install-LocalDB.ps1 -TestConnection

# Update app to use LocalDB
.\Update-AppSettingsForLocalDB.ps1 -BackupOriginal
```

### Scenario 3: Switch from Azure to Local Development
**Need**: Use LocalDB for development instead of Azure SQL

**Solution**:
```powershell
# 1. Install LocalDB
.\Install-LocalDB.ps1

# 2. Update app configuration
.\Update-AppSettingsForLocalDB.ps1 -BackupOriginal

# 3. Run EF migrations (from project root)
cd ..\..\
dotnet ef database update --project BusBuddy.WPF
```

## Troubleshooting

### Azure SQL Issues
- **Private Endpoint**: If server uses private endpoint, firewall rules won't help - you need VPN/VNet access
- **Conditional Access**: Corporate policies may block connections even with correct firewall rules
- **TLS/Encryption**: Ensure connection string includes `Encrypt=True`

### LocalDB Issues
- **Service not starting**: Try `sqllocaldb stop MSSQLLocalDB` then `sqllocaldb start MSSQLLocalDB`
- **Version conflicts**: Multiple SQL Server versions can conflict - check `sqllocaldb versions`
- **Permissions**: LocalDB uses Windows authentication - ensure your user has access

### General Database Issues
- **Connection timeouts**: Increase `Connect Timeout` in connection string
- **Entity Framework**: Run `dotnet ef database update` after changing providers
- **Configuration**: Use environment-specific appsettings (appsettings.Development.json)

## Best Practices

1. **Environment Separation**: Use LocalDB for development, Azure SQL for staging/production
2. **Backup Settings**: Always backup appsettings before modifications
3. **IP Management**: Consider static IP ranges for Azure SQL if you switch networks frequently
4. **Security**: Never commit connection strings with passwords to source control

## Dependencies

- **PowerShell 7.0+** (all scripts)
- **Azure CLI** (for Azure SQL scripts)
- **Chocolatey** (optional, for LocalDB auto-install)
- **.NET SDK** (for EF migrations)

## Documentation References

- [Azure SQL Firewall Rules](https://learn.microsoft.com/azure/azure-sql/database/firewall-configure)
- [SQL Server Express LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [PowerShell ShouldProcess](https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process)
