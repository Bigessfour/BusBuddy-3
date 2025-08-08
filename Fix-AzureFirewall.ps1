# Azure SQL Firewall Fix Script
# This script helps diagnose and fix Azure SQL firewall issues

Write-Host "🚌 BusBuddy Azure SQL Firewall Diagnostics" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Gray

# Get current IP addresses
Write-Host "`n🔍 Checking IP Addresses:" -ForegroundColor Yellow
try {
    $publicIP1 = (Invoke-RestMethod -Uri "http://checkip.amazonaws.com").Trim()
    Write-Host "   AWS CheckIP: $publicIP1" -ForegroundColor Green
} catch {
    Write-Host "   AWS CheckIP: Failed" -ForegroundColor Red
}

try {
    $publicIP2 = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
    Write-Host "   Ipify.org:   $publicIP2" -ForegroundColor Green
} catch {
    Write-Host "   Ipify.org:   Failed" -ForegroundColor Red
}

try {
    $publicIP3 = (Invoke-RestMethod -Uri "https://icanhazip.com").Trim()
    Write-Host "   CanHazIP:    $publicIP3" -ForegroundColor Green
} catch {
    Write-Host "   CanHazIP:    Failed" -ForegroundColor Red
}

# Show the error IP from logs
Write-Host "   Error Log IP: 216.147.124.207" -ForegroundColor Red

Write-Host "`n📋 Azure SQL Server Details:" -ForegroundColor Yellow
Write-Host "   Server: busbuddy-server-sm2.database.windows.net" -ForegroundColor White
Write-Host "   Database: BusBuddyDB" -ForegroundColor White
Write-Host "   User: $env:AZURE_SQL_USER" -ForegroundColor White

Write-Host "`n🛠️ Solutions:" -ForegroundColor Yellow

Write-Host "`n1️⃣ Quick Fix - Use Local Database:" -ForegroundColor Cyan
Write-Host "   Temporarily switch to LocalDB to continue development:" -ForegroundColor Gray
Write-Host "   Set-Content -Path '.env' -Value 'DatabaseProvider=Local'" -ForegroundColor White

Write-Host "`n2️⃣ Azure Portal Fix:" -ForegroundColor Cyan
Write-Host "   1. Go to Azure Portal: https://portal.azure.com" -ForegroundColor Gray
Write-Host "   2. Navigate to SQL servers > busbuddy-server-sm2" -ForegroundColor Gray
Write-Host "   3. Go to 'Firewalls and virtual networks'" -ForegroundColor Gray
Write-Host "   4. Add your IP addresses:" -ForegroundColor Gray
if ($publicIP1) { Write-Host "      - $publicIP1" -ForegroundColor White }
if ($publicIP2) { Write-Host "      - $publicIP2" -ForegroundColor White }
if ($publicIP3) { Write-Host "      - $publicIP3" -ForegroundColor White }
Write-Host "      - 216.147.124.207 (from error log)" -ForegroundColor White
Write-Host "   5. Click 'Save' and wait 5 minutes" -ForegroundColor Gray

Write-Host "`n3️⃣ Azure CLI Fix:" -ForegroundColor Cyan
Write-Host "   If you have Azure CLI installed:" -ForegroundColor Gray
Write-Host "   az sql server firewall-rule create --resource-group <your-rg> --server busbuddy-server-sm2 --name 'DevMachine' --start-ip-address $publicIP1 --end-ip-address $publicIP1" -ForegroundColor White

Write-Host "`n4️⃣ PowerShell Azure Fix:" -ForegroundColor Cyan
Write-Host "   If you have Az PowerShell module:" -ForegroundColor Gray
Write-Host "   New-AzSqlServerFirewallRule -ResourceGroupName '<your-rg>' -ServerName 'busbuddy-server-sm2' -FirewallRuleName 'DevMachine' -StartIpAddress '$publicIP1' -EndIpAddress '$publicIP1'" -ForegroundColor White

Write-Host "`n🔧 Testing Azure Connection:" -ForegroundColor Yellow
Write-Host "   After updating firewall rules, run:" -ForegroundColor Gray
Write-Host "   .\Test-AzureConnection.ps1" -ForegroundColor White
Write-Host "   dotnet run --project BusBuddy.WPF" -ForegroundColor White

Write-Host "`n💡 Why This Happened:" -ForegroundColor Yellow
Write-Host "   - Your IP address changed since yesterday" -ForegroundColor Gray
Write-Host "   - Network routing might show different IPs" -ForegroundColor Gray
Write-Host "   - Azure SQL firewall rules are strict and specific" -ForegroundColor Gray

Write-Host "`n" -ForegroundColor Gray
