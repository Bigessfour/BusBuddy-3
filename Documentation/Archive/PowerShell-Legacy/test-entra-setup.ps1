#!/usr/bin/env pwsh
# Test Microsoft Entra ID setup for BusBuddy

Write-Information "🔐 Testing Microsoft Entra ID Configuration for BusBuddy" -InformationAction Continue
Write-Information "=" * 60 -InformationAction Continue

# Load the profile if needed
$profilePath = "C:\Users\biges\Desktop\BusBuddy\PowerShell\Profiles\Microsoft.PowerShell_profile_optimized.ps1"
if (Test-Path $profilePath) {
    Write-Information "📁 Loading PowerShell profile..." -InformationAction Continue
    . $profilePath
} else {
    Write-Information "❌ PowerShell profile not found at: $profilePath" -InformationAction Continue
    exit 1
}

Write-Information "" -InformationAction Continue
Write-Information "🌐 Environment Variables Check:" -InformationAction Continue
Write-Information "   AZURE_SUBSCRIPTION_ID: $env:AZURE_SUBSCRIPTION_ID" -InformationAction Continue
Write-Information "   AZURE_TENANT_ID: $env:AZURE_TENANT_ID" -InformationAction Continue
Write-Information "   BUSBUDDY_AUTH_METHOD: $env:BUSBUDDY_AUTH_METHOD" -InformationAction Continue
Write-Information "   BUSBUDDY_DB_PROVIDER: $env:BUSBUDDY_DB_PROVIDER" -InformationAction Continue
Write-Information "   BUSBUDDY_ENTRA_ENABLED: $env:BUSBUDDY_ENTRA_ENABLED" -InformationAction Continue

Write-Information "" -InformationAction Continue
Write-Information "🧪 Testing Functions:" -InformationAction Continue

# Test if function exists
if (Get-Command Initialize-BusBuddyEntraID -ErrorAction SilentlyContinue) {
    Write-Information "✅ Initialize-BusBuddyEntraID function available" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "🚀 Running Entra ID initialization..." -InformationAction Continue
    Write-Information "" -InformationAction Continue

    try {
        Initialize-BusBuddyEntraID
    } catch {
        Write-Information "❌ Error running Initialize-BusBuddyEntraID: $($_.Exception.Message)" -InformationAction Continue
    }
} else {
    Write-Information "❌ Initialize-BusBuddyEntraID function not found" -InformationAction Continue
}

Write-Information "" -InformationAction Continue
Write-Information "🔗 Testing bb-sql-test alias..." -InformationAction Continue

if (Get-Command bb-sql-test -ErrorAction SilentlyContinue) {
    Write-Information "✅ bb-sql-test alias available" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "🧪 Testing Azure SQL connection with Entra ID..." -InformationAction Continue

    try {
        bb-sql-test
    } catch {
        Write-Information "❌ Error testing SQL connection: $($_.Exception.Message)" -InformationAction Continue
    }
} else {
    Write-Information "❌ bb-sql-test alias not found" -InformationAction Continue
}

Write-Information "" -InformationAction Continue
Write-Information "✅ Entra ID configuration test completed!" -InformationAction Continue

$TenantId = $env:AZURE_TENANT_ID
Write-Information "Testing Entra setup for tenant $TenantId" -InformationAction Continue
