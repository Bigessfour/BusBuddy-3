# Azure SQL Database Setup for BusBuddy

## Step-by-Step Setup

### 1. Create Resource Group

1. In Azure Portal, click "Resource groups"
2. Click "Create"
3. Name: `BusBuddy-RG`
4. Region: Choose closest to you (e.g., "East US")
5. Click "Review + create"

### 2. Create SQL Server

1. Search "SQL databases" in Azure Portal
2. Click "Create"
3. **Basics tab:**
    - Resource group: `BusBuddy-RG`
    - Database name: `BusBuddyDB`
    - Server: Click "Create new"
        - Server name: `busbuddy-server-[your-initials]` (must be globally unique)
        - Location: Same as resource group
        - Authentication: SQL authentication
        - Server admin login: `busbuddy_admin`
        - Password: Create strong password (save it!)

### 3. Configure Database Tier (FREE TIER)

1. **Compute + storage:** Click "Configure database"
2. **Service tier:** Look for one of these FREE options:
    - **Basic (DTU):** 5 DTU, 2 GB storage - $4.90/month (often has free credits)
    - **Serverless (vCore):** 0.5-1 vCore, 32 GB storage - Auto-pause enabled (only pay when active)
    - **Try Azure for Free:** If you have a new account, you get $200 credit for 30 days
3. **For completely free option:**
    - Choose **Serverless** compute tier
    - Set **Min vCores** to 0.5
    - Set **Max vCores** to 1
    - **Auto-pause delay:** 1 hour (database pauses when not in use)
    - This costs almost nothing when paused
4. Click "Apply"

### Step-by-Step to Find Free/Low-Cost Options:

1. **In Database Creation:**
    - When you see "Compute + storage", click "Configure database"
    - Look for "Service tier" dropdown
    - Select "General Purpose" or "Basic"
    - If you see "Serverless", select that
    - Look for "Auto-pause" toggle and turn it ON

2. **Pricing Calculator:**
    - Before clicking "Create", look for "Pricing calculator" link
    - This shows estimated monthly cost
    - Aim for $0-5/month for development

3. **Alternative: Development Database:**
    - Consider keeping your local SQL Server Express for development
    - Only migrate to Azure when ready for production
    - This avoids any costs during development phase

### Alternative: Use SQL Database Free Trial

- If available, look for "Try for free" options during database creation
- Some regions offer SQL Database free tier with 32 GB storage

### 4. Networking Configuration

1. **Networking tab:**
    - Connectivity method: "Public endpoint"
    - Allow Azure services: Yes
    - Add current client IP: Yes
2. Click "Review + create"
3. Click "Create" (takes 2-5 minutes)

### 5. Get Connection String

1. Go to your database in Azure Portal
2. Click "Connection strings" in left menu
3. Copy the "ADO.NET" connection string
4. Replace `{your_password}` with your actual password

## Important Notes About Azure Free Tier

### Current Azure SQL Database Free Options:

1. **Serverless with Auto-Pause:** Best option for development
    - Database automatically pauses after 1 hour of inactivity
    - You only pay for compute when the database is active
    - Storage costs ~$0.10/GB per month (very low for small databases)

2. **Azure Free Account Benefits:**
    - New accounts get $200 credit for 30 days
    - Some services remain free after trial period
    - Check your subscription type in Azure Portal

3. **Cost Management Tips:**
    - Always set up **Cost Alerts** in Azure Portal
    - Use **Auto-pause** feature for development databases
    - Monitor usage in "Cost Management + Billing"

### If You Can't Find Free Tier:

- Your subscription type might not support free tier
- Try creating with a different Azure subscription
- Consider using **Azure SQL Edge** locally for development
- Use **SQL Server Express LocalDB** for local development

## Alternative: Azure CLI Setup (Recommended for Free Tier)

This method guarantees you get the free serverless tier and is faster than the portal:

### Prerequisites

1. Install Azure CLI: `winget install Microsoft.AzureCLI`
2. Login: `az login`
3. Set subscription (if you have multiple): `az account set --subscription "Your Subscription Name"`

### CLI Commands (Run in PowerShell)

```powershell
# 1. Create resource group
az group create --name BusBuddy-RG --location eastus

# 2. Create SQL Server (replace [your-initials] with your actual initials)
az sql server create --name busbuddy-server-[your-initials] --resource-group BusBuddy-RG --location eastus --admin-user busbuddy_admin --admin-password [your-password]

# 3. Create database with FREE serverless tier
az sql db create --name BusBuddyDB --resource-group BusBuddy-RG --server busbuddy-server-[your-initials] --edition GeneralPurpose --family Gen5 --capacity 2 --compute-model Serverless --use-free-limit --free-limit-exhaustion-behavior AutoPause

# 4. Add firewall rule for your IP (more secure than AllowAll)
az sql server firewall-rule create --resource-group BusBuddy-RG --server busbuddy-server-[your-initials] --name MyIP --start-ip-address $(Invoke-WebRequest -Uri "https://ipinfo.io/ip" -UseBasicParsing).Content.Trim() --end-ip-address $(Invoke-WebRequest -Uri "https://ipinfo.io/ip" -UseBasicParsing).Content.Trim()

# 5. Get connection string
az sql db show-connection-string --name BusBuddyDB --server busbuddy-server-[your-initials] --client ado.net --auth-type SqlPassword
```

### What This Does:

- **`--use-free-limit`**: Ensures you get the free tier
- **`--compute-model Serverless`**: Auto-pause when not in use
- **`--free-limit-exhaustion-behavior AutoPause`**: Pauses when free limit reached
- **Firewall rule**: Adds your current IP for secure access

## Quick Setup Script

I've created a PowerShell script that automates the entire process:

```powershell
# Run the setup script (replace 'sm' with your initials)
.\setup-azure-free.ps1 -YourInitials "sm" -AdminPassword (Read-Host "Enter admin password" -AsSecureString)
```

This script will:

- ✅ Create all Azure resources with FREE tier
- ✅ Set up firewall rules for your IP
- ✅ Generate the connection string
- ✅ Save connection string to file
- ✅ Show you next steps

## Next Steps

1. Update appsettings.json with new connection string
2. Run database migration to cloud
3. Test connection

## ✅ SUCCESSFUL DEPLOYMENT COMPLETED

### 🎉 Your Azure SQL Database is Live!

**Deployment Details:**

- **Resource Group**: BusBuddy-RG (Central US)
- **SQL Server**: busbuddy-server-sm2.database.windows.net
- **Database**: BusBuddyDB (FREE Serverless tier)
- **Admin User**: busbuddy_admin
- **Status**: ✅ Active and migrated
- **Cost**: FREE with auto-pause after 1 hour

### 📋 Connection Details

```
Server: busbuddy-server-sm2.database.windows.net
Database: BusBuddyDB
Username: busbuddy_admin
Authentication: SQL Server Authentication
Tier: General Purpose Serverless (FREE)
Auto-pause: 1 hour of inactivity
```

### 🔗 Active Connection String

Your application is now configured with:

```
Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID=busbuddy_admin;Password=COspr1ng$;MultipleActiveResultSets=False;Encrypt=true;TrustServerCertificate=False;Connection Timeout=30;
```

### ✅ Migration Status

- Database schema: ✅ Migrated successfully
- Tables created: ✅ All BusBuddy tables deployed
- Application config: ✅ Updated to use Azure
- Firewall rules: ✅ Your IP (216.147.126.177) allowed

### 🚀 What's Working Now

1. **Cloud Database**: Your app now uses Azure SQL Database
2. **Auto-scaling**: Database scales from 0.5 to 2 vCores as needed
3. **Cost Optimization**: Automatically pauses when inactive
4. **High Availability**: Built-in Azure reliability and backups
5. **Secure Access**: Encrypted connections with firewall protection

### 💡 Development Tips

- **Local to Cloud**: You can switch between local and Azure by changing `DatabaseProvider` in appsettings.json
- **Cost Monitoring**: Check Azure Portal > Cost Management for usage
- **Database Pausing**: Don't worry if first connection is slow - database may be waking up
- **Backups**: Azure automatically handles backups and point-in-time restore

### 🔧 Useful Azure CLI Commands

```powershell
# Check database status
az sql db show --name BusBuddyDB --server busbuddy-server-sm2 --resource-group BusBuddy-RG

# Monitor costs
az consumption usage list --top 5

# Add more firewall rules if needed
az sql server firewall-rule create --resource-group BusBuddy-RG --server busbuddy-server-sm2 --name "NewIP" --start-ip-address [IP] --end-ip-address [IP]
```
