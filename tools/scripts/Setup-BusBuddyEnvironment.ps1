# BusBuddy Environment Setup Helper
# Sets up required environment variables for testing

param(
    [string]$SyncfusionLicenseKey,
    [switch]$Persist
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "🔧 $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Yellow
}

# Set required environment variables
Write-Step "Setting up BusBuddy testing environment..."

# Always set these
$env:BUSBUDDY_USE_INMEMORY = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Write-Success "BUSBUDDY_USE_INMEMORY=1 (forces in-memory database for tests)"
Write-Success "DOTNET_CLI_TELEMETRY_OPTOUT=1 (disables telemetry)"

# Handle Syncfusion license
if ($SyncfusionLicenseKey) {
    $env:SYNCFUSION_LICENSE_KEY = $SyncfusionLicenseKey
    Write-Success "SYNCFUSION_LICENSE_KEY set from parameter"
}
elseif ($env:SYNCFUSION_LICENSE_KEY) {
    Write-Success "SYNCFUSION_LICENSE_KEY already set"
}
else {
    Write-Info "SYNCFUSION_LICENSE_KEY not set - some Syncfusion features may be unavailable without a valid license key"
    Write-Info "You can set it later with: `$env:SYNCFUSION_LICENSE_KEY = 'your_key_here'` (use double quotes if your key contains special characters)"
}

# Persist to environment if requested
if ($Persist) {
    Write-Step "Persisting environment variables to user profile..."

    # PowerShell profile approach
    $profilePath = $PROFILE
    if (Test-Path $profilePath) {
        $profileContent = Get-Content $profilePath -Raw

        # Add environment variables if not already present
        if ($profileContent -notmatch 'BUSBUDDY_USE_INMEMORY') {
            Add-Content $profilePath "`n# BusBuddy Testing Environment Variables`n`$env:BUSBUDDY_USE_INMEMORY = '1'`n`$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'"
        }

        if ($SyncfusionLicenseKey -and $profileContent -notmatch 'SYNCFUSION_LICENSE_KEY') {
            Add-Content $profilePath "`n`$env:SYNCFUSION_LICENSE_KEY = '$SyncfusionLicenseKey'"
        }

        Write-Success "Environment variables added to PowerShell profile: $profilePath"
    }
    else {
        Write-Info "PowerShell profile not found. Create it first with: New-Item -Path $profilePath -ItemType File -Force"
    }
}

# Display current environment
Write-Step "Current BusBuddy testing environment:"
Write-Host "  BUSBUDDY_USE_INMEMORY: $env:BUSBUDDY_USE_INMEMORY" -ForegroundColor $(if ($env:BUSBUDDY_USE_INMEMORY) { 'Green' } else { 'Red' })
Write-Host "  SYNCFUSION_LICENSE_KEY: $([bool]$env:SYNCFUSION_LICENSE_KEY)" -ForegroundColor $(if ($env:SYNCFUSION_LICENSE_KEY) { 'Green' } else { 'Yellow' })
Write-Host "  DOTNET_CLI_TELEMETRY_OPTOUT: $env:DOTNET_CLI_TELEMETRY_OPTOUT" -ForegroundColor $(if ($env:DOTNET_CLI_TELEMETRY_OPTOUT) { 'Green' } else { 'Red' })

Write-Success "BusBuddy testing environment setup complete!"
Write-Info "You can now run: bb-test -Quick"
Write-Info "Or run health check: bb-th"
