# Test-Azure-CLI-Connection.ps1
# Quick test script to validate Azure CLI database connection

Write-Host "🚌 Testing Azure CLI Database Connection" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# Check Azure CLI installation
Write-Host "1. Checking Azure CLI installation..." -ForegroundColor Yellow
try {
    $cliVersion = az version --output json | ConvertFrom-Json
    Write-Host "   ✅ Azure CLI Version: $($cliVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Host "   ❌ Azure CLI not installed" -ForegroundColor Red
    Write-Host "   💡 Run: .\Setup-Azure-CLI-Database.ps1 -InstallCLI" -ForegroundColor Yellow
    exit 1
}

# Check authentication
Write-Host "2. Checking Azure authentication..." -ForegroundColor Yellow
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "   ✅ Authenticated as: $($account.user.name)" -ForegroundColor Green
    Write-Host "   📋 Subscription: $($account.name)" -ForegroundColor White
}
catch {
    Write-Host "   ❌ Not authenticated with Azure" -ForegroundColor Red
    Write-Host "   💡 Run: az login" -ForegroundColor Yellow
    exit 1
}

# Check server access
Write-Host "3. Checking Azure SQL Server access..." -ForegroundColor Yellow
try {
    $server = az sql server show --name "busbuddy-server-sm2" --resource-group "busbuddy-rg" --output json | ConvertFrom-Json
    Write-Host "   ✅ Server accessible: $($server.fullyQualifiedDomainName)" -ForegroundColor Green
    Write-Host "   📍 Location: $($server.location)" -ForegroundColor White
    Write-Host "   🔧 Version: $($server.version)" -ForegroundColor White
}
catch {
    Write-Host "   ❌ Cannot access server: busbuddy-server-sm2" -ForegroundColor Red
    Write-Host "   💡 Check resource group and server name" -ForegroundColor Yellow
}

# List databases
Write-Host "4. Listing databases on server..." -ForegroundColor Yellow
try {
    Write-Host "   Available databases:" -ForegroundColor White
    az sql db list --server "busbuddy-server-sm2" --resource-group "busbuddy-rg" --output table
}
catch {
    Write-Host "   ❌ Cannot list databases" -ForegroundColor Red
}

# Check firewall rules
Write-Host "5. Checking firewall rules..." -ForegroundColor Yellow
try {
    Write-Host "   Current firewall rules:" -ForegroundColor White
    az sql server firewall-rule list --server "busbuddy-server-sm2" --resource-group "busbuddy-rg" --output table
}
catch {
    Write-Host "   ❌ Cannot list firewall rules" -ForegroundColor Red
}

Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "   1. If authentication failed: az login" -ForegroundColor White
Write-Host "   2. To create database: .\Setup-Azure-CLI-Database.ps1 -CreateDatabase" -ForegroundColor White
Write-Host "   3. To setup tables: .\Setup-Azure-CLI-Database.ps1 -SetupTables" -ForegroundColor White
Write-Host ""
