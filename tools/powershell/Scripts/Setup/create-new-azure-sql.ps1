# Create New Azure SQL Database for BusBuddy
Write-Information "🚀 Creating New Azure SQL Database" -InformationAction Continue
Write-Information "=================================" -InformationAction Continue

$resourceGroup = "BusBuddy-RG"
$serverName = "busbuddy-server-new-$(Get-Random -Minimum 1000 -Maximum 9999)"
$databaseName = "BusBuddyDB"
$locations = @("West US 2", "Central US", "West US", "South Central US", "East US 2", "North Central US")
$adminUser = "busbuddy_admin"

Write-Information "`n📋 Configuration:" -InformationAction Continue
Write-Information "Resource Group: $resourceGroup" -InformationAction Continue
Write-Information "Server Name: $serverName" -InformationAction Continue
Write-Information "Database: $databaseName" -InformationAction Continue
Write-Information "Regions to try: $($locations -join ', ')" -InformationAction Continue
Write-Information "Admin User: $adminUser" -InformationAction Continue

# Get secure password
Write-Information "`n🔐 Enter password for SQL admin (minimum 8 chars, mix of letters/numbers/symbols):" -InformationAction Continue
$adminPassword = Read-Host "Password" -AsSecureString
$adminPasswordText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPassword))

try {
    Write-Information "`n🔌 Step 1: Registering Microsoft.Sql Provider..." -InformationAction Continue
    az provider register --namespace Microsoft.Sql

    Write-Information "⏳ Waiting for provider registration..." -InformationAction Continue
    do {
        Start-Sleep -Seconds 10
        $status = az provider show --namespace Microsoft.Sql --query registrationState -o tsv
        Write-Information "Registration status: $status" -InformationAction Continue
    } while ($status -eq "Registering")

    if ($status -eq "Registered") {
        Write-Information "✅ Microsoft.Sql provider registered successfully" -InformationAction Continue
    } else {
        throw "Provider registration failed with status: $status"
    }

    Write-Information "`n🏗️ Step 2: Creating Resource Group..." -InformationAction Continue
    # Try the first location for resource group
    $selectedLocation = $locations[0]
    az group create --name $resourceGroup --location $selectedLocation

    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Resource group created in $selectedLocation" -InformationAction Continue
    } else {
        Write-Information "ℹ️ Resource group already exists or created" -InformationAction Continue
    }

    Write-Information "`n🖥️ Step 3: Creating SQL Server (trying multiple regions)..." -InformationAction Continue
    $serverCreated = $false
    foreach ($location in $locations) {
        Write-Information "🌍 Trying region: $location" -InformationAction Continue
        az sql server create `
            --name $serverName `
            --resource-group $resourceGroup `
            --location $location `
            --admin-user $adminUser `
            --admin-password $adminPasswordText

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ SQL Server created successfully in $location" -InformationAction Continue
            $selectedLocation = $location
            $serverCreated = $true
            break
        } else {
            Write-Information "❌ Failed in $location, trying next region..." -InformationAction Continue
        }
    }

    if (-not $serverCreated) {
        throw "Failed to create SQL server in any available region"
    }

    Write-Information "`n🔒 Step 4: Configuring Firewall..." -InformationAction Continue
    # Allow your IP
    $myIP = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
    az sql server firewall-rule create `
        --name "AllowMyIP" `
        --resource-group $resourceGroup `
        --server $serverName `
        --start-ip-address $myIP `
        --end-ip-address $myIP

    Write-Information "✅ Firewall configured for IP: $myIP" -InformationAction Continue

    Write-Information "`n📊 Step 5: Creating Database..." -InformationAction Continue
    az sql db create `
        --name $databaseName `
        --resource-group $resourceGroup `
        --server $serverName `
        --service-objective Basic

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create database"
    }
    Write-Information "✅ Database created: $databaseName" -InformationAction Continue

    Write-Information "`n🎉 SUCCESS! Your new Azure SQL Database is ready!" -InformationAction Continue
    Write-Information "================================================" -InformationAction Continue
    Write-Information "Server: $serverName.database.windows.net" -InformationAction Continue
    Write-Information "Database: $databaseName" -InformationAction Continue
    Write-Information "Username: $adminUser" -InformationAction Continue
    Write-Information "Location: $selectedLocation" -InformationAction Continue
    Write-Information "Resource Group: $resourceGroup" -InformationAction Continue

    # Update connection string
    $newConnectionString = "Server=tcp:$serverName.database.windows.net,1433;Initial Catalog=$databaseName;User ID=$adminUser;Password=$adminPasswordText;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    Write-Information "`n🔧 Updating appsettings.json..." -InformationAction Continue
    $appsettingsPath = "appsettings.json"
    if (Test-Path $appsettingsPath) {
        $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
        $appsettings.ConnectionStrings.AzureConnection = $newConnectionString
        $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
        Write-Information "✅ appsettings.json updated with new connection string" -InformationAction Continue
    }

    # Set environment variables
    Write-Information "`n🌐 Setting Environment Variables..." -InformationAction Continue
    [Environment]::SetEnvironmentVariable("AZURE_SQL_USER", $adminUser, "User")
    [Environment]::SetEnvironmentVariable("AZURE_SQL_PASSWORD", $adminPasswordText, "User")
    $env:AZURE_SQL_USER = $adminUser
    $env:AZURE_SQL_PASSWORD = $adminPasswordText
    Write-Information "✅ Environment variables set" -InformationAction Continue

    Write-Information "`n🧪 Testing Connection..." -InformationAction Continue
    $connection = New-Object System.Data.SqlClient.SqlConnection($newConnectionString)
    $connection.Open()
    Write-Information "✅ Connection test successful!" -InformationAction Continue
    $connection.Close()

    Write-Information "`n🚀 Next Steps:" -InformationAction Continue
    Write-Information "1. Run your BusBuddy application - it should connect automatically" -InformationAction Continue
    Write-Information "2. Entity Framework will create tables on first run" -InformationAction Continue
    Write-Information "3. Test with: bb-sql-test" -InformationAction Continue

} catch {
    Write-Error "❌ Setup failed: $($_.Exception.Message)"
    Write-Information "`n🔧 Cleanup commands if needed:" -InformationAction Continue
    Write-Information "az group delete --name $resourceGroup --yes --no-wait" -InformationAction Continue
}
