#requires -Version 7.5 -PSEdition Core
<#
    BusBuddy Unified PowerShell Profile — Initializes modules, stubs, and aliases.
    PERFORMANCE: Lazy-loads Az/SqlServer modules only when functions are called (avoids 15+ second startup delay)

    References:
    - PowerShell profiles: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Profiles
    - PowerShell output streams: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
    - Azure SQL: https://learn.microsoft.com/azure/azure-sql/?view=azuresql
    - Syncfusion WPF: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
#>

# Require PowerShell Core 7.5+ — warn if mismatch
$requiredVersion = [Version]'7.5.0'
$currentVersion = $PSVersionTable.PSVersion
if ($PSVersionTable.PSEdition -ne 'Core' -or $currentVersion -lt $requiredVersion) {
    Write-Warning "BusBuddy expects PowerShell Core 7.5+. Current: $($PSVersionTable.PSEdition) $currentVersion. Some commands may be unavailable."
}

# Idempotent guard to prevent multiple executions in the same session
if ($env:BUSBUDDY_PROFILE_LOADED -eq '1') {
    Write-Output "BusBuddy profile already loaded; skipping reload."
    return
}

# Set strict mode and error handling for better script quality
try { Set-StrictMode -Version 3.0 } catch { Write-Verbose "StrictMode not set: $_" }
$ErrorActionPreference = 'Stop'

# === REPO ROOT DETECTION ===
# Start from profile script directory; fall back to current location if unavailable
$probe = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# Walk up the directory tree to locate the repo root (contains BusBuddy.sln)
while ($probe -and -not (Test-Path (Join-Path $probe 'BusBuddy.sln'))) {
    $next = Split-Path $probe -Parent
    if (-not $next -or $next -eq $probe) { $probe = $null; break }
    $probe = $next
}

if (-not $probe) {
    Write-Warning "BusBuddy repo root not found. Ensure BusBuddy.sln exists in repo root."
    Write-Warning "For setup guidance see: Azure SQL https://learn.microsoft.com/azure/azure-sql/?view=azuresql and Syncfusion WPF https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf"
    return
}

# Expose discovered repo root to environment and current session
$env:BUSBUDDY_REPO_ROOT = $probe
$BusBuddyRepoPath = $probe

# === ENVIRONMENT SETUP ===
# Set default environment variables from BusBuddy CI defaults
$env:DOTNET_VERSION = "9.0.108"  # Latest as of August 2025
$env:BUILD_CONFIGURATION = "Release"
$env:SOLUTION_FILE = "BusBuddy.sln"

# Add BusBuddy PowerShell scripts folder to PSModulePath for easy import
if (Test-Path "$BusBuddyRepoPath\PowerShell") {
    $env:PSModulePath = $env:PSModulePath + ";$BusBuddyRepoPath\PowerShell"
}

# === FUNCTION DEFINITIONS ===
# Lazy-load CLI integration module
function Import-BusBuddyCli {
    [CmdletBinding()]
    param()

    $cliModulePath = Join-Path $BusBuddyRepoPath "PowerShell\Modules\BusBuddy.CLI"
    if (Test-Path $cliModulePath) {
        try {
            Import-Module $cliModulePath -Force -DisableNameChecking -ErrorAction Stop
            Write-Output "BusBuddy CLI module loaded"
            return $true
        }
        catch {
            Write-Warning "Failed to load BusBuddy CLI module: $($_.Exception.Message)"
            return $false
        }
    }

    # Keep function lightweight: only load Az/SqlServer modules when called and then execute requested CLI actions.
    if (-not (Get-Module Az -ListAvailable)) {
        Write-Warning 'Az module not installed. Run: Install-Module Az -Scope CurrentUser'
        return $false
    }
    if (-not (Get-Module Az)) {
        Write-Output 'Loading Az module (this may take 10-15 seconds)...'
        Import-Module Az -ErrorAction Stop
    }

    # Lazy-load SqlServer module only when needed
    if (-not (Get-Module SqlServer -ListAvailable)) {
        Write-Warning 'SqlServer module not installed. Run: Install-Module SqlServer -Scope CurrentUser'
        return $false
    }
    if (-not (Get-Module SqlServer)) {
        Write-Output 'Loading SqlServer module...'
        Import-Module SqlServer -ErrorAction Stop
    }

    return $true
}

# Function: Enable Azure SQL Firewall Rule for current IP (inspired by CI YAML)
# Usage: Enable-BusBuddyFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'
# Requires Az module and Azure login
function Enable-BusBuddyFirewall {
    param (
        [string]$ResourceGroup = 'BusBuddy-RG',
        [string]$SqlServer = 'busbuddy-server-sm2',
        [string]$RuleName = "dev-rule-$(Get-Date -Format 'yyyyMMdd')"
    )

    # Lazy-load Az module only when needed
    if (-not (Get-Module Az -ListAvailable)) {
        Write-Warning 'Az module not installed. Run: Install-Module Az -Scope CurrentUser'
        return
    }
    if (-not (Get-Module Az)) {
        Write-Output 'Loading Az module (this may take 10-15 seconds)...'
        Import-Module Az -ErrorAction Stop
    }

    # Get current public IP (requires internet)
    $ip = (Invoke-RestMethod -Uri 'http://ipinfo.io/json').ip

    New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -FirewallRuleName $RuleName -StartIpAddress $ip -EndIpAddress $ip
}

# Function: Connect to Azure SQL Database
# Reference: https://learn.microsoft.com/azure/azure-sql/database/connect-query-powershell?view=azuresql
function Connect-BusBuddySql {
    param(
        [string]$Database = 'BusBuddy',
        [string]$Query = 'SELECT GETDATE() AS CurrentTime'
    )

    if (-not (Get-Module SqlServer -ListAvailable)) {
        Write-Warning 'SqlServer module not installed. Run: Install-Module SqlServer -Scope CurrentUser'
        return
    }
    if (-not (Get-Module SqlServer)) {
        Write-Output 'Loading SqlServer module...'
        Import-Module SqlServer -ErrorAction Stop
    }

    # Use environment variables from CI YAML (set these externally)
    $server = if ($env:AZURE_SQL_SERVER) { "$($env:AZURE_SQL_SERVER).database.windows.net" } else { Write-Error "Set AZURE_SQL_SERVER environment variable"; return }

    # Authenticate to Azure and get access token
    Connect-AzAccount -ErrorAction Stop | Out-Null
    $token = (Get-AzAccessToken -ResourceUrl "https://database.windows.net/").Token

    # Execute query
    Invoke-Sqlcmd -ServerInstance $server -Database $Database -AccessToken $token -Query $Query
}

# Function: Check and Set Syncfusion WPF License (supports v30.x)
# Registers via code in WPF app (App.xaml.cs), but checks env var for CI/builds
# Reference: https://help.syncfusion.com/wpf/licensing/license-key-registration
function Set-SyncfusionLicense {
    if ($env:SYNCFUSION_LICENSE_KEY) {
        # Mask key for display
        $len = $env:SYNCFUSION_LICENSE_KEY.Length
        $masked = '*' * [Math]::Max(0, $len - 8) + $env:SYNCFUSION_LICENSE_KEY.Substring([Math]::Max(0, $len - 8))
        Write-Output "Syncfusion license key detected (set for builds/runtimes). Key (masked): $masked"
        return
    }

    # Try to hydrate the process environment from persistent user/machine environment variables (Windows registry-backed)
    try {
        $userKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
        $machineKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Machine')

        if (-not [string]::IsNullOrWhiteSpace($userKey)) {
            $env:SYNCFUSION_LICENSE_KEY = $userKey
            $len = $userKey.Length
            $masked = '*' * [Math]::Max(0, $len - 8) + $userKey.Substring([Math]::Max(0, $len - 8))
            Write-Output "Syncfusion license imported into session from User environment. Key (masked): $masked"
            return
        }
        elseif (-not [string]::IsNullOrWhiteSpace($machineKey)) {
            $env:SYNCFUSION_LICENSE_KEY = $machineKey
            $len = $machineKey.Length
            $masked = '*' * [Math]::Max(0, $len - 8) + $machineKey.Substring([Math]::Max(0, $len - 8))
            Write-Output "Syncfusion license imported into session from Machine environment. Key (masked): $masked"
            return
        }
        else {
            Write-Warning 'SYNCFUSION_LICENSE_KEY not set. Generate from Syncfusion dashboard and set as env var.'
            Write-Output "To set for this session: `$env:SYNCFUSION_LICENSE_KEY = 'your-key-here'"
            Write-Output 'To persist for current user: setx SYNCFUSION_LICENSE_KEY "your-key-here"'
        }
    }
    catch {
        Write-Warning "Failed to probe user/machine environment for SYNCFUSION_LICENSE_KEY: $($_.Exception.Message)"
        Write-Output "Set SYNCFUSION_LICENSE_KEY manually: `$env:SYNCFUSION_LICENSE_KEY = 'your-key'` or use setx to persist."
    }

    Write-Output 'Syncfusion WPF Latest Version: 30.2.5 (update NuGet packages if needed)'
}

# Functions for common BusBuddy dev tasks (inspired by CI YAML)
function Build-BusBuddy { dotnet build $env:SOLUTION_FILE --configuration $env:BUILD_CONFIGURATION --no-restore }
function Test-BusBuddy { dotnet test $env:SOLUTION_FILE --configuration $env:BUILD_CONFIGURATION --no-build --verbosity normal }
function Restore-BusBuddy { dotnet restore $env:SOLUTION_FILE }

# === MODULE LOADING ===
# Import core BusBuddy module (contains bb* aliases)
$busBuddyModulePath = Join-Path $BusBuddyRepoPath 'PowerShell\Modules\BusBuddy'
if (Test-Path $busBuddyModulePath) {
    try {
        Import-Module $busBuddyModulePath -Force -DisableNameChecking -ErrorAction Stop
        Write-Output 'BusBuddy core module loaded (bb* aliases available)'
    }
    catch {
        Write-Warning "Failed to load BusBuddy core module: $($_.Exception.Message)"
    }
} else {
    Write-Warning "BusBuddy core module not found at: $busBuddyModulePath"
}

# === PROFILE INITIALIZATION ===
# Set environment guard
$env:BUSBUDDY_PROFILE_LOADED = '1'

# Run initialization functions
Set-SyncfusionLicense
Import-BusBuddyCli  # Prepare lazy-loads for Az/SqlServer when used

# Display helpful information
Write-Output 'BusBuddy Dev Profile Loaded: Use bb* aliases (bbHealth, bbBuild, bbTest, bbRun, etc.) or Connect-BusBuddySql for Azure SQL queries.'
Write-Output 'Core Commands: bbHealth, bbBuild, bbTest, bbRun, bbMvpCheck, bbAntiRegression, bbXamlValidate'
Write-Output 'CLI Commands: bbFullScan (comprehensive), bbWorkflows (GitHub), bbAzResources (Azure), bbRepos (GitKraken)'

# Example: Uncomment to test connection on load
# Connect-BusBuddySql -Query "SELECT DB_NAME() AS DatabaseName"

Write-Output 'BusBuddy unified profile loaded successfully.'
