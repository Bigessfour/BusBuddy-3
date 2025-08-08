#requires -Version 7.5
<#
.SYNOPSIS
    Set Syncfusion License Key for BusBuddy WPF Application

.DESCRIPTION
    Sets the SYNCFUSION_LICENSE_KEY environment variable for Syncfusion v30.1.42.
    The license key should be obtained from your Syncfusion account downloads page.

.PARAMETER LicenseKey
    Your Syncfusion license key (long alphanumeric string)

.PARAMETER Scope
    Environment variable scope: User (default) or Machine

.EXAMPLE
    .\Set-SyncfusionLicense.ps1 -LicenseKey "MjAxNDEzQDMxMzkyZTMzMmUzMG..."

.NOTES
    Version: 1.0.0
    Compatible with: Syncfusion WPF v30.1.42
    Requires restart of VS Code/Terminal after setting
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$LicenseKey,

    [Parameter(Mandatory = $false)]
    [ValidateSet("User", "Machine")]
    [string]$Scope = "User"
)

Write-Information "🔑 Setting Syncfusion License Key for BusBuddy" -InformationAction Continue

# Validate license key format
if ($LicenseKey.Length -lt 20) {
    Write-Error "❌ License key appears too short. Syncfusion keys are typically 100+ characters."
    Write-Information "💡 Get your license key from: https://www.syncfusion.com/account/downloads" -InformationAction Continue
    exit 1
}

# Check for placeholder values
$invalidPlaceholders = @("YOUR_LICENSE_KEY", "YOUR LICENSE KEY", "PLACEHOLDER", "TRIAL", "DEMO")
if ($invalidPlaceholders -contains $LicenseKey.ToUpper()) {
    Write-Error "❌ Please replace placeholder with your actual Syncfusion license key"
    Write-Information "💡 Get your license key from: https://www.syncfusion.com/account/downloads" -InformationAction Continue
    exit 1
}

try {
    # Set environment variable
    $target = if ($Scope -eq "Machine") {
        [System.EnvironmentVariableTarget]::Machine
    } else {
        [System.EnvironmentVariableTarget]::User
    }

    [System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", $LicenseKey, $target)

    Write-Information "✅ Syncfusion license key set successfully!" -InformationAction Continue
    Write-Information "📋 Scope: $Scope" -InformationAction Continue
    Write-Information "📏 Key Length: $($LicenseKey.Length) characters" -InformationAction Continue
    Write-Information "🔍 Key Preview: $($LicenseKey.Substring(0, [Math]::Min(20, $LicenseKey.Length)))..." -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "⚠️  IMPORTANT: You must restart VS Code/Terminal for the change to take effect!" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "🧪 To test after restart:" -InformationAction Continue
    Write-Information "   dotnet run --project BusBuddy.WPF" -InformationAction Continue
    Write-Information "   Check logs for: '✅ Syncfusion license registered successfully'" -InformationAction Continue

} catch {
    Write-Error "❌ Failed to set environment variable: $($_.Exception.Message)"

    if ($Scope -eq "Machine") {
        Write-Information "💡 Machine scope requires administrator privileges. Try:" -InformationAction Continue
        Write-Information "   1. Run PowerShell as Administrator, or" -InformationAction Continue
        Write-Information "   2. Use -Scope User instead" -InformationAction Continue
    }

    exit 1
}
