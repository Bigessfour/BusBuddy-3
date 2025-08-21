# BusBuddy Azure Environment - Comprehensive Reference Guide

## 🔵 **Azure Infrastructure Overview**

### **Current Azure Environment Status**

- **Primary Database**: Azure SQL Database (`busbuddy-server-sm2.database.windows.net`)
- **Authentication**: Azure AD passwordless authentication
- **Region**: Central US (primary deployment region)
- **Resource Group**: `BusBuddy-Production` (inferred)
- **Subscription**: Active Azure subscription with development/production tiers

---

## 🗄️ **Azure SQL Database Configuration**

### **Database Server Details**

```yaml
Server Name: busbuddy-server-sm2.database.windows.net
Database Name: BusBuddy (production), BusBuddyTest (development)
Authentication: Azure Active Directory Integrated
Port: 1433 (default)
Region: Central US
Tier: Standard/Premium (production workloads)
```

### **Connection Strings**

#### **Production Connection**

```csharp
// appsettings.json - Production
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddy;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "DatabaseProvider": "Azure"
}
```

#### **Development Connection**

```csharp
// appsettings.Staging.json - Development
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyTest;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "DatabaseProvider": "Azure"
}
```

#### **LocalDB Fallback**

```csharp
// appsettings.Development.json - Local Development
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True"
  },
  "DatabaseProvider": "LocalDB"
}
```

### **Entity Framework Configuration**

```csharp
// BusBuddy.Core/Data/BusBuddyDbContext.cs
public class BusBuddyDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var provider = configuration["DatabaseProvider"];
        var connectionString = provider switch
        {
            "Azure" => configuration.GetConnectionString("AzureConnection"),
            "LocalDB" => configuration.GetConnectionString("DefaultConnection"),
            _ => configuration.GetConnectionString("DefaultConnection")
        };

        optionsBuilder.UseSqlServer(connectionString, options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }
}
```

### **Database Schema**

#### **Core Tables**

```sql
-- Students Table
CREATE TABLE Students (
    Id int IDENTITY(1,1) PRIMARY KEY,
    StudentNumber nvarchar(50) NOT NULL UNIQUE,
    StudentName nvarchar(255) NOT NULL,
    Address nvarchar(500),
    PhoneNumber nvarchar(20),
    EmergencyContact nvarchar(255),
    Grade nvarchar(10),
    RouteId int FOREIGN KEY REFERENCES Routes(Id),
    IsActive bit DEFAULT 1,
    CreatedDate datetime2 DEFAULT GETUTCDATE(),
    ModifiedDate datetime2 DEFAULT GETUTCDATE()
);

-- Routes Table
CREATE TABLE Routes (
    Id int IDENTITY(1,1) PRIMARY KEY,
    RouteName nvarchar(255) NOT NULL,
    RouteNumber nvarchar(50) NOT NULL UNIQUE,
    Description nvarchar(1000),
    VehicleId int FOREIGN KEY REFERENCES Vehicles(Id),
    DriverId int FOREIGN KEY REFERENCES Drivers(Id),
    StartTime time,
    EndTime time,
    IsActive bit DEFAULT 1,
    CreatedDate datetime2 DEFAULT GETUTCDATE(),
    ModifiedDate datetime2 DEFAULT GETUTCDATE()
);

-- Vehicles Table
CREATE TABLE Vehicles (
    Id int IDENTITY(1,1) PRIMARY KEY,
    BusNumber nvarchar(50) NOT NULL UNIQUE,
    Make nvarchar(100),
    Model nvarchar(100),
    Year int,
    Capacity int,
    LicensePlate nvarchar(20),
    VIN nvarchar(50),
    IsActive bit DEFAULT 1,
    CreatedDate datetime2 DEFAULT GETUTCDATE(),
    ModifiedDate datetime2 DEFAULT GETUTCDATE()
);

-- Drivers Table
CREATE TABLE Drivers (
    Id int IDENTITY(1,1) PRIMARY KEY,
    EmployeeId nvarchar(50) NOT NULL UNIQUE,
    FirstName nvarchar(100) NOT NULL,
    LastName nvarchar(100) NOT NULL,
    LicenseNumber nvarchar(50),
    LicenseExpiration date,
    PhoneNumber nvarchar(20),
    Email nvarchar(255),
    IsActive bit DEFAULT 1,
    CreatedDate datetime2 DEFAULT GETUTCDATE(),
    ModifiedDate datetime2 DEFAULT GETUTCDATE()
);
```

#### **Relationship Mapping**

```csharp
// Entity Framework Model Configuration
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(50);
        builder.Property(s => s.StudentName).IsRequired().HasMaxLength(255);
        builder.HasIndex(s => s.StudentNumber).IsUnique();

        // Route relationship
        builder.HasOne(s => s.Route)
               .WithMany(r => r.Students)
               .HasForeignKey(s => s.RouteId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
```

---

## 🔐 **Azure Authentication & Security**

### **Azure Active Directory Integration**

#### **Service Principal Configuration**

```json
// Azure AD App Registration
{
    "ApplicationId": "00000000-0000-0000-0000-000000000000",
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "ClientSecret": "stored-in-key-vault",
    "Audience": "https://database.windows.net/"
}
```

#### **Managed Identity Setup**

```csharp
// Program.cs - Azure identity configuration
services.AddAuthentication(AzureADDefaults.BearerAuthenticationScheme)
        .AddAzureADBearer(options => configuration.Bind("AzureAd", options));

// For Azure SQL authentication
var credential = new DefaultAzureCredential();
services.AddSingleton<TokenCredential>(credential);
```

#### **Key Vault Integration**

```csharp
// appsettings.Azure.json
{
  "KeyVault": {
    "VaultName": "busbuddy-keyvault",
    "VaultUrl": "https://busbuddy-keyvault.vault.azure.net/"
  }
}

// Configuration
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());
```

### **Security Policies**

#### **Database Security**

```sql
-- Row Level Security (if implemented)
CREATE SECURITY POLICY StudentFilter
ADD FILTER PREDICATE dbo.fn_securitypredicate(UserId) ON dbo.Students
WITH (STATE = ON);

-- Always Encrypted (for sensitive data)
CREATE COLUMN MASTER KEY StudentData_CMK
WITH (
    KEY_STORE_PROVIDER_NAME = 'AZURE_KEY_VAULT',
    KEY_PATH = 'https://busbuddy-keyvault.vault.azure.net/keys/StudentDataKey'
);
```

#### **Network Security**

```yaml
# Azure SQL Firewall Rules
Firewall Rules:
    - Rule Name: "Azure Services"
      Start IP: 0.0.0.0
      End IP: 0.0.0.0
    - Rule Name: "Development IPs"
      Start IP: [Your IP Range]
      End IP: [Your IP Range]

# Virtual Network Integration (if configured)
VNet: BusBuddy-VNet
Subnet: Database-Subnet
Service Endpoints: Microsoft.Sql
```

---

## 🚀 **Azure Deployment Architecture**

### **Resource Group Structure**

```yaml
Resource Group: BusBuddy-Production
├── SQL Server: busbuddy-server-sm2
│   ├── Database: BusBuddy (Production)
│   ├── Database: BusBuddyTest (Staging)
│   └── Firewall Rules
├── Key Vault: busbuddy-keyvault
├── App Service Plan: busbuddy-plan (if web deployment)
├── Application Insights: busbuddy-insights
└── Storage Account: busbstorage (for backups/logs)
```

### **App Service Configuration** (Future Web Deployment)

```json
// App Service settings
{
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE": false,
    "WEBSITE_TIME_ZONE": "Central Standard Time",
    "ASPNETCORE_ENVIRONMENT": "Production",
    "ConnectionStrings__DefaultConnection": "@Microsoft.KeyVault(SecretUri=https://busbuddy-keyvault.vault.azure.net/secrets/DatabaseConnection/)",
    "Syncfusion__LicenseKey": "@Microsoft.KeyVault(SecretUri=https://busbuddy-keyvault.vault.azure.net/secrets/SyncfusionLicense/)"
}
```

### **CI/CD Pipeline** (GitHub Actions)

```yaml
# .github/workflows/azure-deploy.yml
name: Deploy to Azure
on:
    push:
        branches: [master]

jobs:
    deploy:
        runs-on: windows-latest
        steps:
            - uses: actions/checkout@v4

            - name: Setup .NET
              uses: actions/setup-dotnet@v4
              with:
                  dotnet-version: "8.0.x"

            - name: Build
              run: dotnet build --configuration Release

            - name: Test
              run: dotnet test --no-build --configuration Release

            - name: Publish
              run: dotnet publish -c Release -o ${{env.DOTNET_ROOT}}/myapp

            - name: Deploy to Azure
              uses: azure/webapps-deploy@v2
              with:
                  app-name: "busbuddy-app"
                  publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
                  package: ${{env.DOTNET_ROOT}}/myapp
```

---

## 📊 **Azure Monitoring & Logging**

### **Application Insights Configuration**

```csharp
// Program.cs - Telemetry setup
services.AddApplicationInsightsTelemetry(configuration);

// Custom telemetry
services.AddSingleton<ITelemetryInitializer, BusBuddyTelemetryInitializer>();

public class BusBuddyTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Component.Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        telemetry.Context.GlobalProperties["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }
}
```

### **Log Analytics Workspace**

```yaml
Workspace Name: BusBuddy-Logs
Retention: 90 days
Location: Central US

# Custom Queries
StudentOperations:
    query: |
        traces
        | where customDimensions.Operation == "StudentManagement"
        | summarize count() by bin(timestamp, 1h), tostring(customDimensions.Action)

DatabasePerformance:
    query: |
        dependencies
        | where type == "SQL"
        | summarize avg(duration) by bin(timestamp, 5m)
        | render timechart
```

### **Alerting Rules**

```yaml
Database Performance Alert:
    condition: "Average database response time > 5 seconds"
    frequency: "5 minutes"
    action: "Email admin team"

High Error Rate Alert:
    condition: "Error rate > 5% over 10 minutes"
    frequency: "1 minute"
    action: "SMS notification"

Student Data Access Alert:
    condition: "Unusual student data access patterns"
    frequency: "Real-time"
    action: "Security team notification"
```

---

## 💾 **Backup & Disaster Recovery**

### **Azure SQL Backup Strategy**

```yaml
Automated Backups:
    - Point-in-time restore: 35 days
    - Long-term retention: 7 years (yearly)
    - Geo-redundant backup: Enabled
    - Backup frequency: Every 12 hours

Manual Backup Commands:
```

```powershell
# Manual database export
$exportRequest = New-AzSqlDatabaseExport `
    -ResourceGroupName "BusBuddy-Production" `
    -ServerName "busbuddy-server-sm2" `
    -DatabaseName "BusBuddy" `
    -StorageKeyType "StorageAccessKey" `
    -StorageKey $storageKey `
    -StorageUri "https://busbstorage.blob.core.windows.net/backups/BusBuddy-$(Get-Date -Format 'yyyyMMdd-HHmm').bacpac" `
    -AdministratorLogin $adminLogin `
    -AdministratorLoginPassword $adminPassword
```

### **Disaster Recovery Plan**

```yaml
RTO (Recovery Time Objective): 4 hours
RPO (Recovery Point Objective): 1 hour

Failover Strategy:
    Primary Region: Central US
    Secondary Region: East US 2

Auto-Failover Group:
    - Name: busbuddy-failover-group
    - Read-write endpoint: busbuddy-server-sm2.database.windows.net
    - Read-only endpoint: busbuddy-server-sm2-secondary.database.windows.net
```

---

## 🔧 **Development & Testing Environments**

### **Environment Configuration**

```yaml
Development:
    Database: BusBuddyDev (local/Azure)
    Azure subscription: Development subscription
    Resource group: BusBuddy-Dev

Staging:
    Database: BusBuddyTest on busbuddy-server-sm2
    Resource group: BusBuddy-Staging
    Deployment: Automated from develop branch

Production:
    Database: BusBuddy on busbuddy-server-sm2
    Resource group: BusBuddy-Production
    Deployment: Manual approval required
```

### **Azure CLI Commands for Management**

#### **Database Operations**

```powershell
# Connect to Azure
az login

# Set subscription
az account set --subscription "BusBuddy Subscription"

# List databases
az sql db list --resource-group BusBuddy-Production --server busbuddy-server-sm2

# Create database backup
az sql db export \
    --resource-group BusBuddy-Production \
    --server busbuddy-server-sm2 \
    --name BusBuddy \
    --storage-key-type StorageAccessKey \
    --storage-key $storageKey \
    --storage-uri "https://busbstorage.blob.core.windows.net/backups/backup.bacpac" \
    --admin-user $adminUser \
    --admin-password $adminPassword

# Scale database
az sql db update \
    --resource-group BusBuddy-Production \
    --server busbuddy-server-sm2 \
    --name BusBuddy \
    --service-objective S2
```

#### **Monitoring Commands**

```powershell
# Check database metrics
az monitor metrics list \
    --resource "/subscriptions/{subscription-id}/resourceGroups/BusBuddy-Production/providers/Microsoft.Sql/servers/busbuddy-server-sm2/databases/BusBuddy" \
    --metric-names cpu_percent,dtu_consumption_percent

# View activity logs
az monitor activity-log list \
    --resource-group BusBuddy-Production \
    --start-time 2025-08-20T00:00:00Z

# Application Insights queries
az monitor app-insights query \
    --app busbuddy-insights \
    --analytics-query "requests | summarize count() by bin(timestamp, 1h)"
```

---

## 🔍 **Troubleshooting & Diagnostics**

### **Common Connection Issues**

#### **Authentication Problems**

```powershell
# Test Azure CLI authentication
az account show

# Test database connectivity
sqlcmd -S busbuddy-server-sm2.database.windows.net -d BusBuddy -G -l 30

# PowerShell test
$connectionString = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddy;Authentication=Active Directory Default;Encrypt=True;"
try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "✅ Connection successful" -ForegroundColor Green
    $connection.Close()
} catch {
    Write-Error "❌ Connection failed: $_"
}
```

#### **Performance Diagnostics**

```sql
-- Check database performance
SELECT
    query_hash,
    query_plan_hash,
    total_worker_time / execution_count AS avg_cpu_time,
    total_elapsed_time / execution_count AS avg_duration,
    execution_count,
    SUBSTRING(qt.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(qt.text)
        ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1) AS statement_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
ORDER BY avg_cpu_time DESC;

-- Check blocking queries
SELECT
    r.session_id,
    r.blocking_session_id,
    r.wait_type,
    r.wait_time,
    r.last_wait_type,
    t.text AS sql_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.blocking_session_id > 0;
```

### **Health Check Procedures**

```csharp
// Health check implementation
public class AzureSqlHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Azure SQL Database is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Azure SQL Database is unhealthy", ex);
        }
    }
}
```

---

## 📈 **Cost Management & Optimization**

### **Current Cost Structure**

```yaml
Azure SQL Database:
    Tier: Standard S2 (estimated)
    Monthly Cost: ~$30-75
    Storage: Pay-as-you-go

Key Vault:
    Monthly Cost: ~$0.03 per 10,000 operations

Application Insights:
    Monthly Cost: ~$2.88 per GB ingested

Storage Account:
    Monthly Cost: ~$0.18 per GB (LRS)
```

### **Cost Optimization Strategies**

```powershell
# Monitor costs with Azure CLI
az consumption usage list \
    --billing-period-name "202508" \
    --top 10

# Set up cost alerts
az consumption budget create \
    --resource-group BusBuddy-Production \
    --budget-name "BusBuddy-Monthly-Budget" \
    --amount 100 \
    --time-grain Monthly \
    --time-period start-date="2025-08-01" end-date="2026-08-01"

# Database scaling automation
az sql db update \
    --resource-group BusBuddy-Production \
    --server busbuddy-server-sm2 \
    --name BusBuddy \
    --service-objective S1  # Scale down during off-hours
```

---

## 🛠️ **PowerShell Integration for BusBuddy**

### **Azure Management Functions**

```powershell
# Add to BusBuddy PowerShell profile

function Test-AzureBusBuddyConnection {
    [CmdletBinding()]
    param()

    try {
        Write-Information "Testing Azure SQL connection..." -InformationAction Continue

        $connectionString = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddy;Authentication=Active Directory Default;Encrypt=True;"
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT COUNT(*) FROM Students"
        $studentCount = $command.ExecuteScalar()

        Write-Host "✅ Azure SQL Connected - $studentCount students in database" -ForegroundColor Green
        $connection.Close()
        return $true
    }
    catch {
        Write-Error "❌ Azure SQL Connection Failed: $_"
        return $false
    }
}

function Backup-BusBuddyDatabase {
    [CmdletBinding()]
    param(
        [string]$BackupName = "BusBuddy-$(Get-Date -Format 'yyyyMMdd-HHmm')"
    )

    Write-Information "Creating database backup: $BackupName" -InformationAction Continue

    try {
        $exportRequest = New-AzSqlDatabaseExport `
            -ResourceGroupName "BusBuddy-Production" `
            -ServerName "busbuddy-server-sm2" `
            -DatabaseName "BusBuddy" `
            -StorageKeyType "StorageAccessKey" `
            -StorageKey $env:AZURE_STORAGE_KEY `
            -StorageUri "https://busbstorage.blob.core.windows.net/backups/$BackupName.bacpac" `
            -AdministratorLogin $env:AZURE_SQL_ADMIN `
            -AdministratorLoginPassword (ConvertTo-SecureString $env:AZURE_SQL_PASSWORD -AsPlainText -Force)

        Write-Host "✅ Backup initiated: $BackupName" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Backup failed: $_"
    }
}

function Get-BusBuddyAzureStatus {
    [CmdletBinding()]
    param()

    Write-Host "🔵 BusBuddy Azure Environment Status" -ForegroundColor Blue
    Write-Host "=====================================" -ForegroundColor Blue

    # Test authentication
    $authStatus = az account show --query "name" -o tsv 2>$null
    if ($authStatus) {
        Write-Host "✅ Azure CLI: Authenticated ($authStatus)" -ForegroundColor Green
    } else {
        Write-Host "❌ Azure CLI: Not authenticated" -ForegroundColor Red
    }

    # Test database connection
    if (Test-AzureBusBuddyConnection) {
        Write-Host "✅ Database: Connected to busbuddy-server-sm2" -ForegroundColor Green
    } else {
        Write-Host "❌ Database: Connection failed" -ForegroundColor Red
    }

    # Check resource group
    $rgExists = az group exists --name "BusBuddy-Production" 2>$null
    if ($rgExists -eq "true") {
        Write-Host "✅ Resource Group: BusBuddy-Production exists" -ForegroundColor Green
    } else {
        Write-Host "❌ Resource Group: BusBuddy-Production not found" -ForegroundColor Red
    }

    Write-Host "`n🔗 Quick Access:" -ForegroundColor Cyan
    Write-Host "Database: https://portal.azure.com/#resource/subscriptions/{subscription-id}/resourceGroups/BusBuddy-Production/providers/Microsoft.Sql/servers/busbuddy-server-sm2/databases/BusBuddy" -ForegroundColor Gray
}

# Aliases for convenience
Set-Alias bb-azure-status Get-BusBuddyAzureStatus
Set-Alias bb-azure-test Test-AzureBusBuddyConnection
Set-Alias bb-azure-backup Backup-BusBuddyDatabase
```

### **Environment Setup Commands**

```powershell
function Initialize-BusBuddyAzureEnvironment {
    [CmdletBinding()]
    param()

    Write-Host "🚀 Initializing BusBuddy Azure Environment" -ForegroundColor Blue

    # Check prerequisites
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Error "Azure CLI not installed. Install from: https://aka.ms/installazurecliwindows"
        return
    }

    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Warning "SQL Server Command Line Utilities not found. Some features may be limited."
    }

    # Login check
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "Please login to Azure..." -ForegroundColor Yellow
        az login
    }

    # Set subscription if multiple available
    $subscriptions = az account list --query "[].{name:name, id:id}" | ConvertFrom-Json
    if ($subscriptions.Count -gt 1) {
        Write-Host "Multiple subscriptions available:" -ForegroundColor Yellow
        $subscriptions | ForEach-Object { Write-Host "  - $($_.name) ($($_.id))" }

        $subChoice = Read-Host "Enter subscription name or ID for BusBuddy"
        az account set --subscription $subChoice
    }

    Write-Host "✅ Azure environment initialized" -ForegroundColor Green
    Get-BusBuddyAzureStatus
}

Set-Alias bb-azure-init Initialize-BusBuddyAzureEnvironment
```

---

## 📚 **Additional Resources**

### **Documentation Links**

- **Azure SQL Database**: https://docs.microsoft.com/en-us/azure/azure-sql/database/
- **Azure Active Directory**: https://docs.microsoft.com/en-us/azure/active-directory/
- **Entity Framework Core with Azure SQL**: https://docs.microsoft.com/en-us/ef/core/providers/sql-server/
- **Azure Key Vault**: https://docs.microsoft.com/en-us/azure/key-vault/
- **Application Insights**: https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview

### **Best Practices References**

- **Azure Security**: https://docs.microsoft.com/en-us/azure/security/
- **Database Performance**: https://docs.microsoft.com/en-us/azure/azure-sql/database/performance-guidance
- **Cost Optimization**: https://docs.microsoft.com/en-us/azure/cost-management-billing/

### **Support Contacts**

- **Azure Support**: https://azure.microsoft.com/en-us/support/
- **SQL Database Support**: Premium support subscription
- **Emergency Contacts**: [Your organization's contacts]

---

**Last Updated**: August 21, 2025  
**Document Version**: 1.0  
**Environment**: Production/Staging  
**Maintained By**: BusBuddy Development Team

---

_This document serves as the comprehensive reference for all Azure-related configurations, procedures, and best practices for the BusBuddy application. Keep this document updated as the Azure environment evolves._
