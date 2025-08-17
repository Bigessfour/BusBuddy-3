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

# Initialize script-level variables
$script:ProfileLogger = $null

# === LOGGING FUNCTIONS (MUST BE DEFINED EARLY) ===
function Initialize-BusBuddyProfileLogger {
    [CmdletBinding()]
    param()

    # Skip if already initialized
    if ($script:ProfileLogger) { return $script:ProfileLogger }

    try {
        # Check for Serilog module availability (lazy-loading)
        if (-not (Get-Module Serilog -ListAvailable -ErrorAction SilentlyContinue)) {
            Write-Verbose "Serilog module not available - profile logging disabled"
            return $null
        }

        # Import Serilog module only when needed
        Import-Module Serilog -Force -ErrorAction Stop

        # Configure logger for profile events with structured output
        $logPath = Join-Path $BusBuddyRepoPath "logs\profile.log"
        $loggerConfig = [Serilog.LoggerConfiguration]::new()
        $loggerConfig = $loggerConfig.MinimumLevel.Information()
        $loggerConfig = $loggerConfig.WriteTo.File($logPath,
            [Serilog.Events.LogEventLevel]::Information,
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            $null, 1048576, 31, $false, $false, $null, [System.Text.Encoding]::UTF8)
        $loggerConfig = $loggerConfig.WriteTo.Console([Serilog.Events.LogEventLevel]::Warning)
        $script:ProfileLogger = $loggerConfig.CreateLogger()

        Write-Verbose "✅ Profile logger initialized: $logPath"
        return $script:ProfileLogger
    }
    catch {
        Write-Verbose "⚠️ Failed to initialize profile logger: $($_.Exception.Message)"
        return $null
    }
}

function Write-ProfileLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [ValidateSet('Information', 'Warning', 'Error')]
        [string]$Level = 'Information'
    )

    # Initialize logger on first use (lazy loading)
    if (-not $script:ProfileLogger) {
        $script:ProfileLogger = Initialize-BusBuddyProfileLogger
    }

    # Fall back to Write-Information/Write-Warning if Serilog unavailable
    if (-not $script:ProfileLogger) {
        switch ($Level) {
            'Information' { Write-Information $Message -InformationAction Continue }
            'Warning' { Write-Warning $Message }
            'Error' { Write-Error $Message }
        }
        return
    }

    # Log via Serilog with structured format
    switch ($Level) {
        'Information' { $script:ProfileLogger.Information($Message) }
        'Warning' { $script:ProfileLogger.Warning($Message) }
        'Error' { $script:ProfileLogger.Error($Message) }
    }
}

# === ENVIRONMENT SETUP ===
# Set default environment variables from BusBuddy CI defaults
$env:DOTNET_VERSION = "9.0.108"  # Latest as of August 2025
$env:BUILD_CONFIGURATION = "Release"
$env:SOLUTION_FILE = "BusBuddy.sln"

# === SQL LocalDB PATH DETECTION ===
# Search for and add SqlLocalDB.exe to PATH for database management
# Reference: https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb
$localDbPaths = @(
    "${env:ProgramFiles}\Microsoft SQL Server\*\Tools\Binn",
    "${env:ProgramFiles(x86)}\Microsoft SQL Server\*\Tools\Binn",
    "${env:ProgramFiles}\Microsoft Visual Studio\*\*\*\Common7\IDE\CommonExtensions\Microsoft\SQLDB\DAC\*",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\*\Common7\IDE\CommonExtensions\Microsoft\SQLDB\DAC\*"
)

$localDbExe = $null
foreach ($pathPattern in $localDbPaths) {
    $foundPaths = Get-ChildItem -Path $pathPattern -Filter "SqlLocalDB.exe" -ErrorAction SilentlyContinue
    if ($foundPaths) {
        $localDbExe = $foundPaths[0].DirectoryName
        break
    }
}

if ($localDbExe -and $localDbExe -notin ($env:PATH -split ';')) {
    $env:PATH = "$env:PATH;$localDbExe"
    Write-Verbose "✅ Added SqlLocalDB to PATH: $localDbExe"
    Write-ProfileLog "Added SqlLocalDB to PATH: $localDbExe" -Level Information
} elseif (-not $localDbExe) {
    Write-Verbose "⚠️  SqlLocalDB.exe not found. Install SQL Server Express LocalDB: https://www.microsoft.com/en-us/sql-server/sql-server-downloads"
    Write-ProfileLog "SqlLocalDB.exe not found - install SQL Server Express LocalDB" -Level Warning
}

# === WINGET PATH DETECTION ===
# Ensure winget.exe is available in PATH for package management
# Reference: https://learn.microsoft.com/windows/package-manager/winget/
$wingetPath = "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps"
if ((Test-Path "$wingetPath\winget.exe") -and ($wingetPath -notin ($env:PATH -split ';'))) {
    $env:PATH = "$env:PATH;$wingetPath"
    Write-Verbose "✅ Added winget to PATH: $wingetPath"
    Write-ProfileLog "Added winget to PATH: $wingetPath" -Level Information
} elseif (-not (Test-Path "$wingetPath\winget.exe")) {
    Write-Verbose "⚠️  winget.exe not found. Install from Microsoft Store or GitHub: https://github.com/microsoft/winget-cli"
    Write-ProfileLog "winget.exe not found - install from Microsoft Store or GitHub" -Level Warning
}

# === GROK CLI PATH DETECTION ===
# Ensure grok CLI is available in PATH for XAI integration
# Reference: https://docs.x.ai/docs/grok-cli
$grokPaths = @(
    "$env:USERPROFILE\.grok",
    "$env:USERPROFILE\AppData\Local\grok",
    "$env:USERPROFILE\AppData\Roaming\grok",
    "$env:ProgramFiles\grok",
    "$env:ProgramFiles(x86)\grok"
)

$grokExe = $null
foreach ($grokPath in $grokPaths) {
    if (Test-Path "$grokPath\grok.exe") {
        $grokExe = $grokPath
        break
    }
}

if ($grokExe -and ($grokExe -notin ($env:PATH -split ';'))) {
    $env:PATH = "$env:PATH;$grokExe"
    Write-Verbose "✅ Added Grok CLI to PATH: $grokExe"
    Write-ProfileLog "Added Grok CLI to PATH: $grokExe" -Level Information
} elseif (-not $grokExe) {
    Write-Verbose "⚠️  grok.exe not found. Install Grok CLI: Install-BusBuddyGrokCli"
    Write-ProfileLog "grok.exe not found - install Grok CLI: Install-BusBuddyGrokCli" -Level Warning
}

# === GOOGLE CLOUD CLI PATH DETECTION ===
# Search for and add Google Cloud SDK (gcloud) to PATH for Google Earth Engine
# Reference: https://cloud.google.com/sdk/docs/install-sdk#windows
$gcloudPaths = @(
    "$env:USERPROFILE\AppData\Local\Google\Cloud SDK\google-cloud-sdk\bin",
    "$env:ProgramFiles\Google\Cloud SDK\google-cloud-sdk\bin",
    "$env:ProgramFiles(x86)\Google\Cloud SDK\google-cloud-sdk\bin",
    "$env:LOCALAPPDATA\Google\Cloud SDK\google-cloud-sdk\bin"
)

$gcloudExe = $null
foreach ($gcloudPath in $gcloudPaths) {
    if (Test-Path "$gcloudPath\gcloud.cmd") {
        $gcloudExe = $gcloudPath
        break
    }
}

if ($gcloudExe -and ($gcloudExe -notin ($env:PATH -split ';'))) {
    $env:PATH = "$env:PATH;$gcloudExe"
    Write-Verbose "✅ Added Google Cloud CLI to PATH: $gcloudExe"
    Write-ProfileLog "Added Google Cloud CLI to PATH: $gcloudExe" -Level Information
} elseif (-not $gcloudExe) {
    Write-Verbose "⚠️  gcloud.cmd not found. Install Google Cloud CLI: Install-BusBuddyGoogleCli"
    Write-ProfileLog "gcloud.cmd not found - install Google Cloud CLI: Install-BusBuddyGoogleCli" -Level Warning
}

# === EARTH ENGINE CLI PATH DETECTION ===
# Earth Engine CLI is typically installed via pip and may be in Python Scripts directory
# Reference: https://developers.google.com/earth-engine/guides/command_line
$earthenginePaths = @(
    "$env:USERPROFILE\AppData\Local\Programs\Python\*\Scripts",
    "$env:ProgramFiles\Python*\Scripts",
    "$env:LOCALAPPDATA\Programs\Python\*\Scripts"
)

$earthengineExe = $null
foreach ($pathPattern in $earthenginePaths) {
    $foundPaths = Get-ChildItem -Path $pathPattern -Filter "earthengine.exe" -ErrorAction SilentlyContinue
    if ($foundPaths) {
        $earthengineExe = $foundPaths[0].DirectoryName
        break
    }
}

if ($earthengineExe -and ($earthengineExe -notin ($env:PATH -split ';'))) {
    $env:PATH = "$env:PATH;$earthengineExe"
    Write-Verbose "✅ Added Earth Engine CLI to PATH: $earthengineExe"
    Write-ProfileLog "Added Earth Engine CLI to PATH: $earthengineExe" -Level Information
} elseif (-not $earthengineExe) {
    Write-Verbose "⚠️  earthengine.exe not found. Install Earth Engine CLI: pip install earthengine-api"
    Write-ProfileLog "earthengine.exe not found - install Earth Engine CLI: pip install earthengine-api" -Level Warning
}

# Add BusBuddy PowerShell scripts folder to PSModulePath for easy import
if (Test-Path "$BusBuddyRepoPath\PowerShell") {
    $env:PSModulePath = $env:PSModulePath + ";$BusBuddyRepoPath\PowerShell"
}

# === SERILOG INTEGRATION ===
# Lazy-load Serilog for profile event logging with PowerShell 7.5 compliance
# Reference: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
$script:ProfileLogger = $null

# === HARDENED MODULE LOADING SYSTEM ===
# Load the hardened module manager for robust command availability
# Reference: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module
$moduleManagerPath = Join-Path $BusBuddyRepoPath "PowerShell\Profiles\BusBuddy.ModuleManager.ps1"
if (Test-Path $moduleManagerPath) {
    try {
        Write-Information "🔧 Loading hardened module manager..." -InformationAction Continue
        Write-ProfileLog "Loading hardened module manager: $moduleManagerPath" -Level Information
        . $moduleManagerPath -Quiet

        # Verify critical commands are available
        $criticalCommands = @('bbHealth', 'bbBuild', 'bbTest', 'bbRefresh', 'bbStatus')
        $missingCommands = @()

        foreach ($cmd in $criticalCommands) {
            if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
                $missingCommands += $cmd
            }
        }

        if ($missingCommands.Count -eq 0) {
            Write-Information "✅ All critical commands available via hardened loader" -InformationAction Continue
            Write-ProfileLog "All critical commands loaded successfully: $($criticalCommands -join ', ')" -Level Information
        } else {
            Write-Warning "Some commands unavailable: $($missingCommands -join ', '). Use bbRefresh to reload."
            Write-ProfileLog "Missing commands detected: $($missingCommands -join ', ')" -Level Warning
        }
    }
    catch {
        Write-Warning "Failed to load hardened module manager: $($_.Exception.Message)"
        Write-ProfileLog "Failed to load hardened module manager: $($_.Exception.Message)" -Level Warning
        Write-Information "Falling back to basic module loading..." -InformationAction Continue

        # Fallback to basic module loading
        $importScript = Join-Path $BusBuddyRepoPath "PowerShell\Profiles\Import-BusBuddyModule.ps1"
        if (Test-Path $importScript) {
            Write-ProfileLog "Using fallback module loading: $importScript" -Level Information
            . $importScript -Quiet
        }
    }
} else {
    Write-Information "⚠️ Hardened module manager not found, using basic loading..." -InformationAction Continue
    Write-ProfileLog "Hardened module manager not found at: $moduleManagerPath" -Level Warning

    # Fallback to basic module loading
    $importScript = Join-Path $BusBuddyRepoPath "PowerShell\Profiles\Import-BusBuddyModule.ps1"
    if (Test-Path $importScript) {
        Write-ProfileLog "Using basic module loading: $importScript" -Level Information
        . $importScript -Quiet
    }
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

# Function: Enable Broad Azure SQL Firewall Access (All IPs) — DEV/TESTING ONLY
# WARNING: This exposes your database to the public internet. Use strong authentication and passwords.
# Reference: https://learn.microsoft.com/azure/azure-sql/database/firewall-configure?view=azuresql
function Enable-BusBuddyBroadFirewall {
    <#
    .SYNOPSIS
    Creates an Azure SQL firewall rule allowing all IP addresses (0.0.0.0 to 255.255.255.255)

    .DESCRIPTION
    For development/testing scenarios where specific IP restrictions are impractical.
    This effectively bypasses IP restrictions while relying on strong authentication.

    SECURITY WARNING: This exposes your database to the public internet.
    - Use very strong passwords (16+ characters, mixed case, symbols, numbers)
    - Consider Azure AD authentication instead of SQL authentication
    - Prefer LocalDB for development when possible
    - Remove this rule when not needed

    .PARAMETER ResourceGroup
    Azure resource group containing the SQL server

    .PARAMETER SqlServer
    Azure SQL server name (without .database.windows.net suffix)

    .PARAMETER RuleName
    Name for the firewall rule (default: AllowAllIPs-{date})

    .EXAMPLE
    Enable-BusBuddyBroadFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'

    .NOTES
    Requires Az.Sql module and Azure login (Connect-AzAccount)
    Rule takes ~5 minutes to propagate globally
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$SqlServer,

        [string]$RuleName = "AllowAllIPs-$(Get-Date -Format 'yyyyMMdd-HHmm')"
    )

    # Lazy-load Az.Sql module only when needed
    if (-not (Get-Module Az.Sql -ListAvailable)) {
        Write-Warning 'Az.Sql module not installed. Run: Install-Module Az -Scope CurrentUser'
        return
    }
    if (-not (Get-Module Az.Sql)) {
        Write-Information 'Loading Az.Sql module...' -InformationAction Continue
        Import-Module Az.Sql -ErrorAction Stop
    }

    # Check Azure authentication
    try {
        $context = Get-AzContext
        if (-not $context) {
            Write-Warning 'Not authenticated to Azure. Run: Connect-AzAccount'
            return
        }
        Write-Information "Connected to Azure as: $($context.Account.Id)" -InformationAction Continue
    }
    catch {
        Write-Warning 'Failed to get Azure context. Run: Connect-AzAccount'
        return
    }

    # Security confirmation
    $confirmMessage = @"
WARNING: This will allow ALL IP addresses (0.0.0.0 to 255.255.255.255) to connect to your Azure SQL server '$SqlServer'.

This exposes your database to the public internet. Ensure you have:
- Very strong SQL authentication passwords (16+ characters)
- Azure AD authentication enabled if possible
- Database-level security properly configured
- Plan to remove this rule when testing is complete

Do you want to proceed?
"@

    if ($PSCmdlet.ShouldProcess("Azure SQL Server '$SqlServer'", $confirmMessage)) {
        try {
            Write-Information "Creating broad firewall rule '$RuleName' on server '$SqlServer'..." -InformationAction Continue

            $result = New-AzSqlServerFirewallRule `
                -ResourceGroupName $ResourceGroup `
                -ServerName $SqlServer `
                -FirewallRuleName $RuleName `
                -StartIpAddress "0.0.0.0" `
                -EndIpAddress "255.255.255.255" `
                -ErrorAction Stop

            Write-Information "✅ Firewall rule created successfully!" -InformationAction Continue
            Write-Information "📡 Rule will propagate globally within ~5 minutes" -InformationAction Continue
            Write-Information "🔒 Remember to use strong authentication and remove this rule when not needed" -InformationAction Continue

            # Show the created rule
            Write-Information "Rule details:" -InformationAction Continue
            $result | Format-Table -Property FirewallRuleName, StartIpAddress, EndIpAddress -AutoSize

            return $result
        }
        catch {
            Write-Error "Failed to create firewall rule: $($_.Exception.Message)"
            Write-Information "💡 Troubleshooting tips:" -InformationAction Continue
            Write-Information "  - Verify resource group and server names are correct" -InformationAction Continue
            Write-Information "  - Check Azure permissions (Contributor role on SQL server)" -InformationAction Continue
            Write-Information "  - Ensure server exists: Get-AzSqlServer -ResourceGroupName '$ResourceGroup'" -InformationAction Continue
        }
    }
    else {
        Write-Information "Operation cancelled by user." -InformationAction Continue
    }
}

# Function: Remove Broad Azure SQL Firewall Rule
# Reference: https://learn.microsoft.com/azure/azure-sql/database/firewall-configure?view=azuresql
function Remove-BusBuddyBroadFirewall {
    <#
    .SYNOPSIS
    Removes broad Azure SQL firewall rules (cleanup after testing)

    .DESCRIPTION
    Removes firewall rules that allow broad IP access, identified by name pattern or explicit rule name.
    Use this to clean up after development/testing with broad access rules.

    .PARAMETER ResourceGroup
    Azure resource group containing the SQL server

    .PARAMETER SqlServer
    Azure SQL server name (without .database.windows.net suffix)

    .PARAMETER RuleName
    Specific rule name to remove. If not specified, removes rules matching "AllowAllIPs*" pattern

    .EXAMPLE
    Remove-BusBuddyBroadFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'

    .EXAMPLE
    Remove-BusBuddyBroadFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2' -RuleName 'AllowAllIPs-20250816-1430'
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$SqlServer,

        [string]$RuleName
    )

    # Lazy-load Az.Sql module
    if (-not (Get-Module Az.Sql)) {
        Import-Module Az.Sql -ErrorAction Stop
    }

    try {
        # Get existing firewall rules
        $existingRules = Get-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -ErrorAction Stop

        if ($RuleName) {
            # Remove specific rule
            $ruleToRemove = $existingRules | Where-Object { $_.FirewallRuleName -eq $RuleName }
            if (-not $ruleToRemove) {
                Write-Warning "Firewall rule '$RuleName' not found on server '$SqlServer'"
                return
            }
            $rulesToRemove = @($ruleToRemove)
        }
        else {
            # Find broad access rules by pattern and IP range
            $rulesToRemove = $existingRules | Where-Object {
                $_.FirewallRuleName -like "AllowAllIPs*" -or
                ($_.StartIpAddress -eq "0.0.0.0" -and $_.EndIpAddress -eq "255.255.255.255")
            }
        }

        if (-not $rulesToRemove -or $rulesToRemove.Count -eq 0) {
            Write-Information "No broad firewall rules found to remove." -InformationAction Continue
            return
        }

        Write-Information "Found $($rulesToRemove.Count) broad firewall rule(s) to remove:" -InformationAction Continue
        $rulesToRemove | Format-Table -Property FirewallRuleName, StartIpAddress, EndIpAddress -AutoSize

        foreach ($rule in $rulesToRemove) {
            if ($PSCmdlet.ShouldProcess("Firewall rule '$($rule.FirewallRuleName)'", "Remove from Azure SQL server '$SqlServer'")) {
                try {
                    Remove-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -FirewallRuleName $rule.FirewallRuleName -ErrorAction Stop
                    Write-Information "✅ Removed firewall rule: $($rule.FirewallRuleName)" -InformationAction Continue
                }
                catch {
                    Write-Error "Failed to remove rule '$($rule.FirewallRuleName)': $($_.Exception.Message)"
                }
            }
        }
    }
    catch {
        Write-Error "Failed to retrieve firewall rules: $($_.Exception.Message)"
    }
}

# Function: Update Azure SQL Firewall Rule for Dynamic IP (Auto-updates when IP changes)
# Usage: Update-BusBuddyDynamicFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'
# Reference: https://learn.microsoft.com/azure/azure-sql/database/firewall-configure?view=azuresql
function Update-BusBuddyDynamicFirewall {
    <#
    .SYNOPSIS
    Automatically updates Azure SQL firewall rule when your IP address changes

    .DESCRIPTION
    Checks your current public IP and updates/creates a firewall rule if needed.
    This solves the dynamic IP problem by automatically maintaining a current rule.

    .PARAMETER ResourceGroup
    Azure resource group containing the SQL server

    .PARAMETER SqlServer
    Azure SQL server name (without .database.windows.net suffix)

    .PARAMETER RuleName
    Name for the firewall rule (default: DynamicIP-{hostname})

    .PARAMETER Force
    Force update even if IP hasn't changed

    .EXAMPLE
    Update-BusBuddyDynamicFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'

    .NOTES
    Requires Az.Sql module and Azure login (Connect-AzAccount)
    Stores last IP in environment variable to avoid unnecessary updates
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$SqlServer,

        [string]$RuleName = "DynamicIP-$($env:COMPUTERNAME)",

        [switch]$Force
    )

    # Lazy-load Az.Sql module
    if (-not (Get-Module Az.Sql -ListAvailable)) {
        Write-Warning 'Az.Sql module not installed. Run: Install-Module Az -Scope CurrentUser'
        return
    }
    if (-not (Get-Module Az.Sql)) {
        Write-Information 'Loading Az.Sql module...' -InformationAction Continue
        Import-Module Az.Sql -ErrorAction Stop
    }

    try {
        # Get current public IP
        Write-Information "🔍 Checking current public IP..." -InformationAction Continue
        $currentIp = (Invoke-RestMethod -Uri 'http://ipinfo.io/json' -TimeoutSec 10).ip
        Write-Information "📍 Current IP: $currentIp" -InformationAction Continue

        # Check if IP has changed (stored in environment variable)
        $lastIpVar = "BUSBUDDY_LAST_IP_$($SqlServer.Replace('-', '_').ToUpper())"
        $lastIp = [Environment]::GetEnvironmentVariable($lastIpVar, 'User')

        if (-not $Force -and $lastIp -eq $currentIp) {
            Write-Information "✅ IP hasn't changed ($currentIp), firewall rule should still be valid" -InformationAction Continue
            return
        }

        # Check if rule exists
        $existingRule = Get-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -FirewallRuleName $RuleName -ErrorAction SilentlyContinue

        if ($existingRule) {
            if ($existingRule.StartIpAddress -eq $currentIp -and $existingRule.EndIpAddress -eq $currentIp) {
                Write-Information "✅ Firewall rule '$RuleName' already matches current IP: $currentIp" -InformationAction Continue
                # Store current IP
                [Environment]::SetEnvironmentVariable($lastIpVar, $currentIp, 'User')
                return
            }

            Write-Information "🔄 Updating existing firewall rule '$RuleName' from $($existingRule.StartIpAddress) to $currentIp" -InformationAction Continue
            Set-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -FirewallRuleName $RuleName -StartIpAddress $currentIp -EndIpAddress $currentIp -ErrorAction Stop
        }
        else {
            Write-Information "🆕 Creating new firewall rule '$RuleName' for IP: $currentIp" -InformationAction Continue
            New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer -FirewallRuleName $RuleName -StartIpAddress $currentIp -EndIpAddress $currentIp -ErrorAction Stop
        }

        # Store current IP for next time
        [Environment]::SetEnvironmentVariable($lastIpVar, $currentIp, 'User')

        Write-Information "✅ Firewall rule updated successfully!" -InformationAction Continue
        Write-Information "📝 IP stored for future comparisons: $currentIp" -InformationAction Continue

        # Show current rules
        Write-Information "Current firewall rules:" -InformationAction Continue
        Get-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroup -ServerName $SqlServer |
            Where-Object { $_.FirewallRuleName -like "*Dynamic*" -or $_.FirewallRuleName -eq $RuleName } |
            Format-Table -Property FirewallRuleName, StartIpAddress, EndIpAddress -AutoSize
    }
    catch {
        Write-Error "Failed to update dynamic firewall rule: $($_.Exception.Message)"
        Write-Information "💡 Troubleshooting:" -InformationAction Continue
        Write-Information "  - Check internet connection for IP detection" -InformationAction Continue
        Write-Information "  - Verify Azure authentication: Get-AzContext" -InformationAction Continue
        Write-Information "  - Confirm resource group and server names are correct" -InformationAction Continue
    }
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

# Function: Configure Azure SQL Database for Entra ID (Managed Identity) Authentication
# Reference: https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-configure
function Set-BusBuddyEntraIDAuth {
    <#
    .SYNOPSIS
    Configures Azure SQL Database for Microsoft Entra ID (Managed Identity) authentication

    .DESCRIPTION
    Sets up passwordless authentication for BusBuddy using Microsoft Entra ID.
    This eliminates the need for passwords in connection strings and environment variables.

    .PARAMETER ResourceGroup
    Azure resource group containing the SQL server

    .PARAMETER SqlServer
    Azure SQL server name (without .database.windows.net suffix)

    .PARAMETER Database
    Database name (default: BusBuddyDB)

    .PARAMETER SetEntraAdmin
    Whether to set the current user as Entra ID admin (default: true)

    .EXAMPLE
    Set-BusBuddyEntraIDAuth -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'

    .NOTES
    Requires Az.Sql module and Azure login with sufficient permissions
    Reference: https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-configure
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$SqlServer,

        [string]$Database = 'BusBuddyDB',

        [bool]$SetEntraAdmin = $true
    )

    # Load required modules
    if (-not (Get-Module Az.Sql -ListAvailable)) {
        Write-Warning 'Az.Sql module not installed. Run: Install-Module Az -Scope CurrentUser'
        return
    }
    if (-not (Get-Module Az.Sql)) {
        Write-Information 'Loading Az.Sql module...' -InformationAction Continue
        Import-Module Az.Sql -ErrorAction Stop
    }

    try {
        # Get current Azure context
        $context = Get-AzContext
        if (-not $context) {
            Write-Warning 'Not logged in to Azure. Run: Connect-AzAccount'
            return
        }

        Write-Information "✅ Connected to Azure as: $($context.Account.Id)" -InformationAction Continue

        if ($SetEntraAdmin -and $PSCmdlet.ShouldProcess("Azure SQL Server '$SqlServer'", "Set Entra ID Admin")) {
            # Set the current user as Entra ID admin for the SQL server
            Write-Information "🔧 Setting Entra ID admin for SQL server '$SqlServer'..." -InformationAction Continue

            $currentUser = $context.Account.Id
            Set-AzSqlServerActiveDirectoryAdministrator -ResourceGroupName $ResourceGroup -ServerName $SqlServer -DisplayName $currentUser

            Write-Information "✅ Entra ID admin set to: $currentUser" -InformationAction Continue
        }

        # Instructions for database user setup
        Write-Information "" -InformationAction Continue
        Write-Information "📋 Next Steps for Database Configuration:" -InformationAction Continue
        Write-Information "1. Connect to your database using Azure Data Studio or SSMS with Entra ID authentication" -InformationAction Continue
        Write-Information "2. Run the following SQL commands to create database users:" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "   -- For your user account:" -InformationAction Continue
        Write-Information "   CREATE USER [$($context.Account.Id)] FROM EXTERNAL PROVIDER;" -InformationAction Continue
        Write-Information "   ALTER ROLE db_owner ADD MEMBER [$($context.Account.Id)];" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "   -- For system-assigned managed identity (when deploying to Azure):" -InformationAction Continue
        Write-Information "   CREATE USER [your-app-name] FROM EXTERNAL PROVIDER;" -InformationAction Continue
        Write-Information "   ALTER ROLE db_datareader ADD MEMBER [your-app-name];" -InformationAction Continue
        Write-Information "   ALTER ROLE db_datawriter ADD MEMBER [your-app-name];" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "💡 Test connection with: Test-BusBuddyEntraIDConnection" -InformationAction Continue

    }
    catch {
        Write-Error "Failed to configure Entra ID authentication: $($_.Exception.Message)"
    }
}

# Function: Test Entra ID Connection to Azure SQL
function Test-BusBuddyEntraIDConnection {
    <#
    .SYNOPSIS
    Tests Microsoft Entra ID authentication to Azure SQL Database

    .DESCRIPTION
    Verifies that passwordless authentication is working correctly for BusBuddy

    .PARAMETER ConnectionType
    Type of Entra ID connection to test: Default, Interactive, or ManagedIdentity

    .PARAMETER Database
    Database name to test (default: BusBuddyDB)

    .EXAMPLE
    Test-BusBuddyEntraIDConnection -ConnectionType Default

    .EXAMPLE
    Test-BusBuddyEntraIDConnection -ConnectionType Interactive -Database BusBuddyDB
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [ValidateSet('Default', 'Interactive', 'ManagedIdentity')]
        [string]$ConnectionType = 'Default',

        [string]$Database = 'BusBuddyDB'
    )

    $connectionStrings = @{
        'Default' = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=$Database;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
        'Interactive' = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=$Database;Authentication=Active Directory Interactive;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
        'ManagedIdentity' = "Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=$Database;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    }

    $connectionString = $connectionStrings[$ConnectionType]
    Write-Information "🧪 Testing $ConnectionType Entra ID connection..." -InformationAction Continue
    Write-Information "Connection string: $connectionString" -InformationAction Continue

    try {
        # Load SqlClient assembly
        Add-Type -AssemblyName "Microsoft.Data.SqlClient"

        $connection = New-Object Microsoft.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        # Test basic query
        $command = New-Object Microsoft.Data.SqlClient.SqlCommand("SELECT GETDATE() AS CurrentTime, USER_NAME() AS CurrentUser, DB_NAME() AS DatabaseName", $connection)
        $result = $command.ExecuteReader()

        if ($result.Read()) {
            Write-Information "✅ Connection successful!" -InformationAction Continue
            Write-Information "📅 Server Time: $($result['CurrentTime'])" -InformationAction Continue
            Write-Information "👤 Connected User: $($result['CurrentUser'])" -InformationAction Continue
            Write-Information "🗄️  Database: $($result['DatabaseName'])" -InformationAction Continue
        }

        $result.Close()
        $connection.Close()

        Write-Information "🎉 Entra ID authentication is working correctly!" -InformationAction Continue
        return $true
    }
    catch {
        Write-Warning "❌ Connection failed: $($_.Exception.Message)"

        if ($_.Exception.Message -like "*firewall*" -or $_.Exception.Message -like "*40615*") {
            Write-Information "💡 Firewall issue detected. Run: Enable-BusBuddyBroadFirewall -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'" -InformationAction Continue
        }
        elseif ($_.Exception.Message -like "*authentication*" -or $_.Exception.Message -like "*login*") {
            Write-Information "💡 Authentication issue. Ensure:" -InformationAction Continue
            Write-Information "  - You're logged in to Azure: az login" -InformationAction Continue
            Write-Information "  - Entra ID admin is set: Set-BusBuddyEntraIDAuth" -InformationAction Continue
            Write-Information "  - Database user exists (see Set-BusBuddyEntraIDAuth output)" -InformationAction Continue
        }

        return $false
    }
}

# Function: Switch BusBuddy to use Entra ID Authentication
function Switch-BusBuddyToEntraID {
    <#
    .SYNOPSIS
    Switches BusBuddy configuration to use Entra ID passwordless authentication

    .DESCRIPTION
    Updates BusBuddy configuration files to prioritize Entra ID connections over traditional SQL authentication

    .PARAMETER ConnectionType
    Type of Entra ID connection to use: Default (recommended for local dev), Interactive, or ManagedIdentity (for Azure hosting)

    .EXAMPLE
    Switch-BusBuddyToEntraID -ConnectionType Default

    .NOTES
    This updates appsettings.json to set DatabaseProvider=Azure and prioritize Entra ID connections
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $false)]
        [ValidateSet('Default', 'Interactive', 'ManagedIdentity')]
        [string]$ConnectionType = 'Default'
    )

    if ($PSCmdlet.ShouldProcess("BusBuddy configuration", "Switch to Entra ID authentication")) {
        try {
            # Update the main appsettings.json
            $appsettingsPath = "$BusBuddyRepoPath\appsettings.json"
            if (Test-Path $appsettingsPath) {
                $config = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
                $config.DatabaseProvider = "Azure"

                # Set environment hint for which Entra ID connection to prefer
                $env:BUSBUDDY_ENTRAID_TYPE = $ConnectionType

                $config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath -Encoding UTF8
                Write-Information "✅ Updated $appsettingsPath to use Azure provider" -InformationAction Continue
            }

            # Update WPF appsettings.json
            $wpfAppsettingsPath = "$BusBuddyRepoPath\BusBuddy.WPF\appsettings.json"
            if (Test-Path $wpfAppsettingsPath) {
                $wpfConfig = Get-Content $wpfAppsettingsPath -Raw | ConvertFrom-Json
                if ($wpfConfig.PSObject.Properties['DatabaseProvider']) {
                    $wpfConfig.DatabaseProvider = "Azure"
                } else {
                    $wpfConfig | Add-Member -NotePropertyName 'DatabaseProvider' -NotePropertyValue 'Azure'
                }
                $wpfConfig | ConvertTo-Json -Depth 10 | Set-Content $wpfAppsettingsPath -Encoding UTF8
                Write-Information "✅ Updated $wpfAppsettingsPath to use Azure provider" -InformationAction Continue
            }

            Write-Information "" -InformationAction Continue
            Write-Information "🎉 BusBuddy is now configured for Entra ID authentication!" -InformationAction Continue
            Write-Information "📋 Configuration Details:" -InformationAction Continue
            Write-Information "  - Database Provider: Azure" -InformationAction Continue
            Write-Information "  - Auth Type: $ConnectionType" -InformationAction Continue
            Write-Information "  - Connection: Passwordless via Entra ID" -InformationAction Continue
            Write-Information "" -InformationAction Continue
            Write-Information "🧪 Test the connection: Test-BusBuddyEntraIDConnection -ConnectionType $ConnectionType" -InformationAction Continue
            Write-Information "🚀 Run the app: bbRun" -InformationAction Continue

        }
        catch {
            Write-Error "Failed to switch to Entra ID configuration: $($_.Exception.Message)"
        }
    }
}

# Function: Set up Windows Scheduled Task for Automatic Firewall Updates
# Usage: Set-BusBuddyFirewallSchedule -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'
# Reference: https://learn.microsoft.com/powershell/module/scheduledtasks/
function Set-BusBuddyFirewallSchedule {
    <#
    .SYNOPSIS
    Creates a Windows Scheduled Task to automatically update Azure SQL firewall rules

    .DESCRIPTION
    Sets up a scheduled task that runs every 30 minutes to check and update firewall rules.
    This provides hands-off dynamic IP handling for Azure SQL connectivity.

    .PARAMETER ResourceGroup
    Azure resource group containing the SQL server

    .PARAMETER SqlServer
    Azure SQL server name (without .database.windows.net suffix)

    .PARAMETER IntervalMinutes
    How often to check for IP changes (default: 30 minutes)

    .EXAMPLE
    Set-BusBuddyFirewallSchedule -ResourceGroup 'BusBuddy-RG' -SqlServer 'busbuddy-server-sm2'

    .NOTES
    Requires administrator privileges to create scheduled tasks
    Creates task under current user context to maintain Azure authentication
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param (
        [Parameter(Mandatory = $true)]
        [string]$ResourceGroup,

        [Parameter(Mandatory = $true)]
        [string]$SqlServer,

        [int]$IntervalMinutes = 30
    )

    $taskName = "BusBuddy-DynamicFirewall-$SqlServer"

    if ($PSCmdlet.ShouldProcess("Windows Scheduled Task '$taskName'", "Create automatic firewall update task")) {
        try {
            # Create the PowerShell command to run
            $command = "pwsh.exe"
            $arguments = @(
                "-NoProfile"
                "-WindowStyle", "Hidden"
                "-Command"
                "& {Import-Module '$BusBuddyRepoPath\PowerShell\Profiles\Microsoft.PowerShell_profile.ps1' -Force; Update-BusBuddyDynamicFirewall -ResourceGroup '$ResourceGroup' -SqlServer '$SqlServer'}"
            )

            # Create scheduled task action
            $action = New-ScheduledTaskAction -Execute $command -Argument ($arguments -join ' ')

            # Create trigger (every X minutes, starting now)
            $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes)

            # Create settings
            $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RunOnlyIfNetworkAvailable

            # Create principal (run as current user to maintain Azure auth)
            $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive

            # Register the task
            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Automatically updates Azure SQL firewall rules for BusBuddy when IP address changes" -Force

            Write-Information "✅ Scheduled task '$taskName' created successfully!" -InformationAction Continue
            Write-Information "⏰ Will check for IP changes every $IntervalMinutes minutes" -InformationAction Continue
            Write-Information "💡 To remove: Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false" -InformationAction Continue
        }
        catch {
            Write-Error "Failed to create scheduled task: $($_.Exception.Message)"
            Write-Information "💡 Try running PowerShell as Administrator for scheduled task creation" -InformationAction Continue
        }
    }
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

# Function: Check and Install .NET SDK 9.0.108 if missing
# Uses dotnet --list-sdks to check for specific version and offers winget installation
# Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet-sdk-check
function confirmNetSdk908Install {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    try {
        Write-Information "Checking for .NET SDK 9.0.108..." -InformationAction Continue

        # Get list of installed SDKs
        $installedSdks = & dotnet --list-sdks 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet command not found or failed. Ensure .NET SDK is installed."
            return $false
        }

        # Parse SDK versions and check for 9.0.108
        $targetVersion = "9.0.108"
        $hasTargetSdk = $false

        foreach ($sdkLine in $installedSdks) {
            if ($sdkLine -match '^(\d+\.\d+\.\d+)') {
                $version = $matches[1]
                if ($version -eq $targetVersion) {
                    $hasTargetSdk = $true
                    break
                }
            }
        }

        if ($hasTargetSdk) {
            Write-Information "✅ .NET SDK $targetVersion is already installed." -InformationAction Continue
            return $true
        }

        Write-Information "⚠️  .NET SDK $targetVersion not found." -InformationAction Continue
        Write-Information "Currently installed SDKs:" -InformationAction Continue
        foreach ($sdk in $installedSdks) {
            Write-Information "  $sdk" -InformationAction Continue
        }

        # Offer installation via winget with ShouldProcess confirmation
        if ($PSCmdlet.ShouldProcess("Microsoft.DotNet.SDK.9", "Install .NET SDK 9.0.108 via winget")) {
            Write-Information "Installing .NET SDK 9.0.108 via winget..." -InformationAction Continue

            # Check if winget is available
            if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
                Write-Warning "winget not found. Install from Microsoft Store or GitHub: https://github.com/microsoft/winget-cli"
                return $false
            }

            # Install the SDK
            $installResult = & winget install Microsoft.DotNet.SDK.9 --accept-package-agreements --accept-source-agreements 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Information "✅ .NET SDK 9.0.108 installation completed successfully." -InformationAction Continue
                Write-Information "💡 You may need to restart your terminal for PATH changes to take effect." -InformationAction Continue
                return $true
            } else {
                Write-Warning "Failed to install .NET SDK 9.0.108. Exit code: $LASTEXITCODE"
                Write-Warning "Output: $($installResult -join "`n")"
                return $false
            }
        } else {
            Write-Information "Installation cancelled by user." -InformationAction Continue
            Write-Information "To install manually: winget install Microsoft.DotNet.SDK.9" -InformationAction Continue
            return $false
        }
    }
    catch {
        Write-Warning "Error checking/installing .NET SDK: $($_.Exception.Message)"
        Write-Information "Manual installation: winget install Microsoft.DotNet.SDK.9" -InformationAction Continue
        return $false
    }
}

# Functions for common BusBuddy dev tasks (inspired by CI YAML)
function Build-BusBuddy { dotnet build $env:SOLUTION_FILE --configuration $env:BUILD_CONFIGURATION --no-restore }
function Test-BusBuddy { dotnet test $env:SOLUTION_FILE --configuration $env:BUILD_CONFIGURATION --no-build --verbosity normal }
function Restore-BusBuddy { dotnet restore $env:SOLUTION_FILE }

# LocalDB management function
function Install-BusBuddyLocalDB {
    <#
    .SYNOPSIS
    Install SQL Server Express LocalDB for BusBuddy development
    .DESCRIPTION
    Downloads and installs SQL Server Express LocalDB using winget or provides manual download link.
    Reference: https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Information "🔍 Checking for existing LocalDB installation..." -InformationAction Continue

    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True"
        $conn.Open()
        $conn.Close()
        Write-Information "✅ LocalDB is already installed and accessible" -InformationAction Continue
        return
    }
    catch {
        Write-Information "❌ LocalDB not found or not accessible" -InformationAction Continue
    }

    if ($PSCmdlet.ShouldProcess("SQL Server Express LocalDB", "Install")) {
        Write-Information "📦 Installing SQL Server Express LocalDB..." -InformationAction Continue

        try {
            # Try winget first (fastest method)
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Write-Information "Using winget to install SQL Server 2022 Express..." -InformationAction Continue
                winget install Microsoft.SQLServer.2022.Express --accept-package-agreements --accept-source-agreements
            }
            else {
                Write-Information "winget not available. Manual installation required:" -InformationAction Continue
                Write-Information "1. Download SQL Server Express from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -InformationAction Continue
                Write-Information "2. Select 'Download Media' > 'LocalDB'" -InformationAction Continue
                Write-Information "3. Run the installer with default settings" -InformationAction Continue
                return
            }

            # Verify installation
            Start-Sleep -Seconds 5
            try {
                $conn = New-Object System.Data.SqlClient.SqlConnection "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True"
                $conn.Open()
                $conn.Close()
                Write-Information "✅ LocalDB installation successful!" -InformationAction Continue
                Write-Information "💡 You can now run EF migrations: dotnet ef database update --project BusBuddy.Core" -InformationAction Continue
            }
            catch {
                Write-Warning "LocalDB installation may not be complete. Try restarting PowerShell or run: sqllocaldb start mssqllocaldb"
            }
        }
        catch {
            Write-Warning "Failed to install LocalDB: $($_.Exception.Message)"
            Write-Information "Manual installation: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -InformationAction Continue
        }
    }
}

# Grok CLI management function
function Install-BusBuddyGrokCli {
    <#
    .SYNOPSIS
    Install Grok CLI for XAI integration with BusBuddy
    .DESCRIPTION
    Downloads and installs Grok CLI using winget, npm, or provides manual download options.
    Reference: https://docs.x.ai/docs/grok-cli
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Information "🔍 Checking for existing Grok CLI installation..." -InformationAction Continue

    # Check if grok is already available
    try {
        $grokVersion = & grok --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Grok CLI is already installed: $grokVersion" -InformationAction Continue
            return
        }
    }
    catch {
        # Continue with installation
    }

    if ($PSCmdlet.ShouldProcess("Grok CLI", "Install")) {
        Write-Information "📦 Installing Grok CLI..." -InformationAction Continue

        # Try multiple installation methods
        $installSuccess = $false

        # Method 1: Try winget installation
        try {
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Write-Information "🔧 Attempting installation via winget..." -InformationAction Continue
                & winget install --id x.ai.grok-cli --accept-source-agreements --accept-package-agreements 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $installSuccess = $true
                    Write-Information "✅ Grok CLI installed via winget" -InformationAction Continue
                }
            }
        }
        catch {
            Write-Verbose "Winget installation failed: $($_.Exception.Message)"
        }

        # Method 2: Try npm installation (if Node.js is available)
        if (-not $installSuccess) {
            try {
                if (Get-Command npm -ErrorAction SilentlyContinue) {
                    Write-Information "🔧 Attempting installation via npm..." -InformationAction Continue
                    & npm install -g @x-ai/grok-cli 2>$null
                    if ($LASTEXITCODE -eq 0) {
                        $installSuccess = $true
                        Write-Information "✅ Grok CLI installed via npm" -InformationAction Continue
                    }
                }
            }
            catch {
                Write-Verbose "npm installation failed: $($_.Exception.Message)"
            }
        }

        # Method 3: Try direct download (fallback)
        if (-not $installSuccess) {
            try {
                Write-Information "🔧 Attempting direct download..." -InformationAction Continue
                $grokDir = "$env:USERPROFILE\.grok"
                if (-not (Test-Path $grokDir)) {
                    New-Item -ItemType Directory -Path $grokDir -Force | Out-Null
                }

                # Download latest release (platform-specific)
                $platform = if ($env:PROCESSOR_ARCHITECTURE -eq "AMD64") { "windows-x64" } else { "windows-x86" }
                $downloadUrl = "https://github.com/x-ai/grok-cli/releases/latest/download/grok-$platform.exe"
                $grokExePath = "$grokDir\grok.exe"

                Invoke-WebRequest -Uri $downloadUrl -OutFile $grokExePath -ErrorAction Stop

                # Add to PATH for current session
                if ($grokDir -notin ($env:PATH -split ';')) {
                    $env:PATH = "$env:PATH;$grokDir"
                }

                $installSuccess = $true
                Write-Information "✅ Grok CLI installed via direct download to $grokDir" -InformationAction Continue
            }
            catch {
                Write-Verbose "Direct download failed: $($_.Exception.Message)"
            }
        }

        # Verify installation
        if ($installSuccess) {
            Start-Sleep -Seconds 2
            try {
                $grokVersion = & grok --version 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Information "✅ Grok CLI installation successful: $grokVersion" -InformationAction Continue
                    Write-Information "💡 Configure API key: grok auth login" -InformationAction Continue
                    Write-Information "💡 Test integration: grok chat 'Hello from BusBuddy!'" -InformationAction Continue
                }
                else {
                    Write-Warning "Grok CLI installation may not be complete. Try restarting PowerShell."
                }
            }
            catch {
                Write-Warning "Grok CLI installation verification failed. Try restarting PowerShell."
            }
        }
        else {
            Write-Warning "Failed to install Grok CLI automatically."
            Write-Information "Manual installation options:" -InformationAction Continue
            Write-Information "1. Download from: https://github.com/x-ai/grok-cli/releases" -InformationAction Continue
            Write-Information "2. Install via npm: npm install -g @x-ai/grok-cli" -InformationAction Continue
            Write-Information "3. Visit: https://docs.x.ai/docs/grok-cli" -InformationAction Continue
        }
    }
}

# Google Cloud CLI management function
function Install-BusBuddyGoogleCli {
    <#
    .SYNOPSIS
    Install Google Cloud CLI (gcloud) for Google Earth Engine integration
    .DESCRIPTION
    Downloads and installs Google Cloud CLI for Earth Engine operations in BusBuddy.
    Reference: https://cloud.google.com/sdk/docs/install-sdk#windows
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Information "🔍 Checking for existing Google Cloud CLI installation..." -InformationAction Continue

    # Check if gcloud is already available
    try {
        & gcloud version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Google Cloud CLI is already installed" -InformationAction Continue
            Write-Information "💡 Enable Earth Engine API: gcloud services enable earthengine.googleapis.com" -InformationAction Continue
            return
        }
    }
    catch {
        # Not installed, proceed with installation
    }

    if ($PSCmdlet.ShouldProcess("Google Cloud CLI", "Install")) {
        Write-Information "📦 Installing Google Cloud CLI..." -InformationAction Continue
        $installSuccess = $false

        # Method 1: Try winget installation
        try {
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Write-Information "🔧 Attempting installation via winget..." -InformationAction Continue
                & winget install Google.CloudSDK --accept-package-agreements --accept-source-agreements 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $installSuccess = $true
                    Write-Information "✅ Google Cloud CLI installed via winget" -InformationAction Continue
                }
            }
        }
        catch {
            Write-Verbose "Winget installation failed: $($_.Exception.Message)"
        }

        # Method 2: Try direct download installer
        if (-not $installSuccess) {
            try {
                Write-Information "🔧 Attempting direct download..." -InformationAction Continue
                $installerUrl = "https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe"
                $installerPath = "$env:TEMP\GoogleCloudSDKInstaller.exe"

                Write-Information "📥 Downloading Google Cloud SDK installer..." -InformationAction Continue
                Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -ErrorAction Stop

                Write-Information "🚀 Running installer..." -InformationAction Continue
                Start-Process -FilePath $installerPath -ArgumentList "/S" -Wait -ErrorAction Stop

                $installSuccess = $true
                Write-Information "✅ Google Cloud CLI installed via direct download" -InformationAction Continue
            }
            catch {
                Write-Verbose "Direct download failed: $($_.Exception.Message)"
            }
        }

        # Verify installation
        if ($installSuccess) {
            Write-Information "⏳ Waiting for installation to complete..." -InformationAction Continue
            Start-Sleep -Seconds 10
            try {
                # Refresh PATH to pick up new installation
                $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

                & gcloud version 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Information "✅ Google Cloud CLI installation successful!" -InformationAction Continue
                    Write-Information "💡 Next steps:" -InformationAction Continue
                    Write-Information "   1. Authenticate: gcloud auth login" -InformationAction Continue
                    Write-Information "   2. Set project: gcloud config set project YOUR_PROJECT_ID" -InformationAction Continue
                    Write-Information "   3. Enable Earth Engine: gcloud services enable earthengine.googleapis.com" -InformationAction Continue
                    Write-Information "   4. Install Earth Engine CLI: pip install earthengine-api" -InformationAction Continue
                }
                else {
                    Write-Warning "Google Cloud CLI installation may not be complete. Try restarting PowerShell."
                }
            }
            catch {
                Write-Warning "Google Cloud CLI installation verification failed. Try restarting PowerShell."
            }
        }
        else {
            Write-Warning "Failed to install Google Cloud CLI automatically."
            Write-Information "Manual installation:" -InformationAction Continue
            Write-Information "1. Download from: https://cloud.google.com/sdk/docs/install-sdk#windows" -InformationAction Continue
            Write-Information "2. Run the installer and follow setup instructions" -InformationAction Continue
        }
    }
}

# Earth Engine CLI management function
function Install-BusBuddyEarthEngineCli {
    <#
    .SYNOPSIS
    Install Earth Engine CLI for Google Earth Engine operations
    .DESCRIPTION
    Installs the Earth Engine Python API and CLI tools for BusBuddy GIS operations.
    Reference: https://developers.google.com/earth-engine/guides/command_line
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Information "🔍 Checking for existing Earth Engine CLI installation..." -InformationAction Continue

    # Check if earthengine CLI is already available
    try {
        $eeVersion = & earthengine --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Earth Engine CLI is already installed: $eeVersion" -InformationAction Continue
            Write-Information "💡 Authenticate: earthengine authenticate" -InformationAction Continue
            return
        }
    }
    catch {
        # Not installed, proceed with installation
    }

    if ($PSCmdlet.ShouldProcess("Earth Engine CLI", "Install")) {
        Write-Information "📦 Installing Earth Engine CLI..." -InformationAction Continue
        $installSuccess = $false

        # Check if Python/pip is available
        $pythonCmd = $null
        $pipCmd = $null

        # Try different Python commands
        foreach ($cmd in @("python", "python3", "py")) {
            try {
                $null = & $cmd --version 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $pythonCmd = $cmd
                    break
                }
            }
            catch { }
        }

        # Try different pip commands
        foreach ($cmd in @("pip", "pip3")) {
            try {
                $null = & $cmd --version 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $pipCmd = $cmd
                    break
                }
            }
            catch { }
        }

        if (-not $pythonCmd) {
            Write-Warning "Python is required for Earth Engine CLI. Install Python first:"
            Write-Information "1. Download from: https://www.python.org/downloads/" -InformationAction Continue
            Write-Information "2. Or install via winget: winget install Python.Python.3" -InformationAction Continue
            return
        }

        if (-not $pipCmd) {
            Write-Warning "pip is required for Earth Engine CLI installation."
            return
        }

        # Install Earth Engine API via pip
        try {
            Write-Information "🔧 Installing Earth Engine API via pip..." -InformationAction Continue
            & $pipCmd install earthengine-api --upgrade 2>$null
            if ($LASTEXITCODE -eq 0) {
                $installSuccess = $true
                Write-Information "✅ Earth Engine API installed via pip" -InformationAction Continue
            }
        }
        catch {
            Write-Warning "Failed to install Earth Engine API: $($_.Exception.Message)"
        }

        # Verify installation
        if ($installSuccess) {
            Start-Sleep -Seconds 3
            try {
                $eeVersion = & earthengine --version 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Information "✅ Earth Engine CLI installation successful: $eeVersion" -InformationAction Continue
                    Write-Information "💡 Next steps:" -InformationAction Continue
                    Write-Information "   1. Authenticate: earthengine authenticate" -InformationAction Continue
                    Write-Information "   2. Test: earthengine --help" -InformationAction Continue
                    Write-Information "   3. Configure in BusBuddy via GEE_ACCESS_TOKEN environment variable" -InformationAction Continue
                }
                else {
                    Write-Warning "Earth Engine CLI installation may not be complete."
                }
            }
            catch {
                Write-Warning "Earth Engine CLI verification failed."
            }
        }
        else {
            Write-Warning "Failed to install Earth Engine CLI."
            Write-Information "Manual installation:" -InformationAction Continue
            Write-Information "1. Install Python: https://www.python.org/downloads/" -InformationAction Continue
            Write-Information "2. Run: pip install earthengine-api" -InformationAction Continue
            Write-Information "3. Authenticate: earthengine authenticate" -InformationAction Continue
        }
    }
}

# Function to persist PATH changes to user environment
function Set-BusBuddyPermanentPath {
    <#
    .SYNOPSIS
    Make winget and LocalDB PATH changes permanent in user environment
    .DESCRIPTION
    Adds winget and LocalDB paths to the user's permanent PATH environment variable
    Reference: https://learn.microsoft.com/dotnet/api/system.environment.setenvironmentvariable
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if ($PSCmdlet.ShouldProcess("User PATH environment variable", "Add winget and LocalDB paths")) {
        try {
            $currentUserPath = [Environment]::GetEnvironmentVariable("PATH", "User")
            $pathsToAdd = @()

            # Add winget path if not already present
            $wingetPath = "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps"
            if ((Test-Path "$wingetPath\winget.exe") -and ($currentUserPath -notlike "*$wingetPath*")) {
                $pathsToAdd += $wingetPath
            }

            # Add LocalDB path if found and not already present
            $localDbPaths = @(
                "${env:ProgramFiles}\Microsoft SQL Server\*\Tools\Binn",
                "${env:ProgramFiles(x86)}\Microsoft SQL Server\*\Tools\Binn"
            )

            foreach ($pathPattern in $localDbPaths) {
                $foundPaths = Get-ChildItem -Path $pathPattern -Filter "SqlLocalDB.exe" -ErrorAction SilentlyContinue
                if ($foundPaths) {
                    $localDbPath = $foundPaths[0].DirectoryName
                    if ($currentUserPath -notlike "*$localDbPath*") {
                        $pathsToAdd += $localDbPath
                    }
                    break
                }
            }

            if ($pathsToAdd.Count -gt 0) {
                $newUserPath = $currentUserPath + ";" + ($pathsToAdd -join ";")
                [Environment]::SetEnvironmentVariable("PATH", $newUserPath, "User")
                Write-Information "✅ Added to permanent user PATH: $($pathsToAdd -join ', ')" -InformationAction Continue
                Write-Information "💡 Restart PowerShell sessions to see the changes" -InformationAction Continue
            } else {
                Write-Information "✅ All required paths already in permanent user PATH" -InformationAction Continue
            }
        }
        catch {
            Write-Warning "Failed to update permanent PATH: $($_.Exception.Message)"
        }
    }
}

# CLI Tools Status Check Function
function Get-BusBuddyCliStatus {
    <#
    .SYNOPSIS
    Check status of all CLI tools used by BusBuddy
    .DESCRIPTION
    Displays availability status of winget, grok, git, az, gh, sqlcmd, and LocalDB tools
    #>
    [CmdletBinding()]
    param()

    Write-Information "🔍 BusBuddy CLI Tools Status Check" -InformationAction Continue
    Write-Information "=================================" -InformationAction Continue

    $cliTools = @(
        @{ Name = "winget"; Command = "winget"; Description = "Package manager" },
        @{ Name = "grok"; Command = "grok"; Description = "XAI/Grok CLI" },
        @{ Name = "gcloud"; Command = "gcloud"; Description = "Google Cloud CLI" },
        @{ Name = "earthengine"; Command = "earthengine"; Description = "Earth Engine CLI" },
        @{ Name = "git"; Command = "git"; Description = "Version control" },
        @{ Name = "dotnet"; Command = "dotnet"; Description = ".NET CLI" },
        @{ Name = "az"; Command = "az"; Description = "Azure CLI" },
        @{ Name = "gh"; Command = "gh"; Description = "GitHub CLI" },
        @{ Name = "sqlcmd"; Command = "sqlcmd"; Description = "SQL Server CLI" },
        @{ Name = "sqllocaldb"; Command = "sqllocaldb"; Description = "LocalDB management" },
        @{ Name = "npm"; Command = "npm"; Description = "Node package manager" },
        @{ Name = "node"; Command = "node"; Description = "Node.js runtime" }
    )

    foreach ($tool in $cliTools) {
        try {
            & $tool.Command --version 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Information "✅ $($tool.Name.PadRight(12)) - $($tool.Description) - Available" -InformationAction Continue
            }
            else {
                Write-Information "❌ $($tool.Name.PadRight(12)) - $($tool.Description) - Not available" -InformationAction Continue
            }
        }
        catch {
            Write-Information "❌ $($tool.Name.PadRight(12)) - $($tool.Description) - Not available" -InformationAction Continue
        }
    }

    Write-Information "" -InformationAction Continue
    Write-Information "💡 Install missing tools:" -InformationAction Continue
    Write-Information "   • Grok CLI: Install-BusBuddyGrokCli" -InformationAction Continue
    Write-Information "   • Google Cloud CLI: Install-BusBuddyGoogleCli" -InformationAction Continue
    Write-Information "   • Earth Engine CLI: Install-BusBuddyEarthEngineCli" -InformationAction Continue
    Write-Information "   • LocalDB: Install-BusBuddyLocalDB" -InformationAction Continue
    Write-Information "   • Azure CLI: winget install Microsoft.AzureCLI" -InformationAction Continue
    Write-Information "   • GitHub CLI: winget install GitHub.GitHubCLI" -InformationAction Continue
}

function Get-BusBuddyDatabaseModuleStatus {
    <#
    .SYNOPSIS
    Check status of all database and WPF PowerShell modules
    .DESCRIPTION
    Displays availability and loading status of SqlServer, dbatools, Logging, WPFBot3000, and PoshWPF modules
    #>
    [CmdletBinding()]
    param()

    Write-Information "📦 BusBuddy PowerShell Modules Status" -InformationAction Continue
    Write-Information "====================================" -InformationAction Continue

    $requiredModules = @(
        @{ Name = "SqlServer"; Description = "SQL Server PowerShell tools"; Required = $true },
        @{ Name = "dbatools"; Description = "Advanced SQL Server administration"; Required = $false },
        @{ Name = "Logging"; Description = "Enhanced PowerShell logging"; Required = $false },
        @{ Name = "WPFBot3000"; Description = "WPF UI automation framework"; Required = $false },
        @{ Name = "PoshWPF"; Description = "WPF XAML integration"; Required = $false }
    )

    foreach ($module in $requiredModules) {
        try {
            $available = Get-Module -ListAvailable -Name $module.Name -ErrorAction SilentlyContinue
            $loaded = Get-Module -Name $module.Name -ErrorAction SilentlyContinue

            if ($available) {
                $status = if ($loaded) { "✅ Loaded" } else { "📦 Available" }
                $version = if ($available.Version) { "v$($available.Version)" } else { "unknown" }
                Write-Information "$($status) $($module.Name.PadRight(12)) $version - $($module.Description)" -InformationAction Continue
            } else {
                $indicator = if ($module.Required) { "❌ REQUIRED" } else { "⚠️  Optional" }
                Write-Information "$indicator $($module.Name.PadRight(12)) - $($module.Description) - Not installed" -InformationAction Continue
            }
        }
        catch {
            Write-Information "❌ $($module.Name.PadRight(12)) - $($module.Description) - Error checking" -InformationAction Continue
        }
    }

    Write-Information "" -InformationAction Continue
    Write-Information "💡 Install missing modules with:" -InformationAction Continue
    Write-Information "   Install-Module SqlServer,dbatools,Logging,WPFBot3000,PoshWPF -Scope CurrentUser -Force" -InformationAction Continue
}

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

# === ADDITIONAL MODULES ===
# Import database administration and WPF modules for enhanced PowerShell capabilities
$additionalModules = @(
    @{Name = 'dbatools'; Description = 'SQL Server administration tools'},
    @{Name = 'Logging'; Description = 'Enhanced PowerShell logging framework'},
    @{Name = 'WPFBot3000'; Description = 'WPF DSL framework for UI automation'},
    @{Name = 'PoshWPF'; Description = 'WPF XAML UI integration for PowerShell'}
)

foreach ($module in $additionalModules) {
    try {
        if (Get-Module -ListAvailable -Name $module.Name -ErrorAction SilentlyContinue) {
            Import-Module $module.Name -Force -ErrorAction Stop
            Write-Verbose "✅ Loaded $($module.Name): $($module.Description)"
        } else {
            Write-Verbose "⚠️  Module $($module.Name) not installed. Run: Install-Module $($module.Name) -Scope CurrentUser"
        }
    }
    catch {
        Write-Verbose "⚠️  Failed to load $($module.Name): $($_.Exception.Message)"
    }
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
Write-Output 'CLI Tools: Get-BusBuddyCliStatus, Get-BusBuddyDatabaseModuleStatus, Install-BusBuddyGrokCli, Install-BusBuddyGoogleCli, Install-BusBuddyEarthEngineCli, Install-BusBuddyLocalDB'

# Show loaded module status
$loadedDbModules = @('dbatools', 'Logging', 'WPFBot3000', 'PoshWPF', 'SqlServer') | Where-Object { Get-Module $_ -ErrorAction SilentlyContinue }
if ($loadedDbModules) {
    Write-Output "Database & WPF Modules: $($loadedDbModules -join ', ')"
}

# === CLI TOOL ALIASES ===
Set-Alias -Name bbCliStatus -Value Get-BusBuddyCliStatus
Set-Alias -Name bbModuleStatus -Value Get-BusBuddyDatabaseModuleStatus
Set-Alias -Name bbInstallGrok -Value Install-BusBuddyGrokCli
Set-Alias -Name bbInstallLocalDB -Value Install-BusBuddyLocalDB

# Example: Uncomment to test connection on load
# Connect-BusBuddySql -Query "SELECT DB_NAME() AS DatabaseName"

Write-Output 'BusBuddy unified profile loaded successfully.'

function Invoke-BusBuddyBuild {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [string]$Configuration = "Debug",
        [string]$Verbosity = "detailed",
        [switch]$NoBuild,
        [string[]]$AdditionalArgs = @()
    )

    # Reference: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process
    if ($PSCmdlet.ShouldProcess("BusBuddy solution", "Build")) {
        try {
            $solutionPath = "BusBuddy.sln"

            if (-not (Test-Path $solutionPath)) {
                Write-Warning "Solution file not found: $solutionPath"
                return $false
            }

            $buildArgs = @(
                "build"
                $solutionPath
                "--configuration", $Configuration
                "--verbosity", $Verbosity
            )

            if ($AdditionalArgs) {
                $buildArgs += $AdditionalArgs
            }

            Write-Information "Building solution with configuration: $Configuration" -InformationAction Continue
            Write-Information "Command: dotnet $($buildArgs -join ' ')" -InformationAction Continue

            # Use proper .NET build command without problematic redirection
            # Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet-build
            $process = Start-Process -FilePath "dotnet" -ArgumentList $buildArgs -Wait -PassThru -NoNewWindow

            if ($process.ExitCode -eq 0) {
                Write-Information "Build completed successfully" -InformationAction Continue
                return $true
            } else {
                Write-Warning "Build failed with exit code: $($process.ExitCode)"
                return $false
            }
        }
        catch {
            Write-Warning "Build operation failed: $($_.Exception.Message)"
            throw
        }
    }
}

function Start-BusBuddyApplication {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [string]$Configuration = "Debug",
        [string[]]$AdditionalArgs = @()
    )

    # Reference: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process
    if ($PSCmdlet.ShouldProcess("BusBuddy application", "Start")) {
        try {
            $projectPath = "BusBuddy.csproj"

            if (-not (Test-Path $projectPath)) {
                Write-Warning "Project file not found: $projectPath"
                return $false
            }

            $runArgs = @(
                "run"
                "--project", $projectPath
                "--configuration", $Configuration
            )

            if ($AdditionalArgs) {
                $runArgs += $AdditionalArgs
            }

            Write-Information "Starting BusBuddy application..." -InformationAction Continue
            Write-Information "Command: dotnet $($runArgs -join ' ')" -InformationAction Continue

            # Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet-run
            Start-Process -FilePath "dotnet" -ArgumentList $runArgs -NoNewWindow

            Write-Information "Application started successfully" -InformationAction Continue
            return $true
        }
        catch {
            Write-Warning "Failed to start application: $($_.Exception.Message)"
            throw
        }
    }
}

function Start-BusBuddyTest {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [string]$Configuration = "Release",
        [string]$Filter = "",
        [string]$Logger = "",
        [switch]$Collect,
        [string[]]$AdditionalArgs = @()
    )

    # Reference: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process
    if ($PSCmdlet.ShouldProcess("BusBuddy tests", "Run")) {
        try {
            $testProject = "BusBuddy.Tests/BusBuddy.Tests.csproj"

            if (-not (Test-Path $testProject)) {
                Write-Warning "Test project not found: $testProject"
                return $false
            }

            $testArgs = @(
                "test"
                $testProject
                "--configuration", $Configuration
            )

            if ($Filter) {
                $testArgs += "--filter", $Filter
            }

            if ($Logger) {
                $testArgs += "--logger", $Logger
            }

            if ($Collect) {
                $testArgs += "--collect:`"XPlat Code Coverage`""
            }

            if ($AdditionalArgs) {
                $testArgs += $AdditionalArgs
            }

            Write-Information "Running tests with configuration: $Configuration" -InformationAction Continue
            Write-Information "Command: dotnet $($testArgs -join ' ')" -InformationAction Continue

            # Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet-test
            # Use direct invocation instead of Start-Process to avoid parameter issues
            & dotnet @testArgs
            $exitCode = $LASTEXITCODE

            if ($exitCode -eq 0) {
                Write-Information "Tests completed successfully" -InformationAction Continue
                return $true
            } else {
                Write-Warning "Tests failed with exit code: $exitCode"
                return $false
            }
        }
        catch {
            Write-Warning "Test operation failed: $($_.Exception.Message)"
            throw
        }
    }
}

function Test-BusBuddyHealth {
    [CmdletBinding()]
    param()

    Write-Information "=== BusBuddy Health Check ===" -InformationAction Continue

    try {
        # Check PowerShell version
        # Reference: https://learn.microsoft.com/powershell/scripting/install/powershell-support-lifecycle
        $psVersion = $PSVersionTable.PSVersion
        if ($psVersion.Major -ge 7 -and $psVersion.Minor -ge 5) {
            Write-Information "✅ PowerShell version: $psVersion" -InformationAction Continue
        } else {
            Write-Warning "⚠️ PowerShell version $psVersion (7.5+ recommended)"
        }

        # Check .NET SDK
        # Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet--version
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion) {
            Write-Information "✅ .NET SDK version: $dotnetVersion" -InformationAction Continue
        } else {
            Write-Warning "❌ .NET SDK not found"
            return $false
        }

        # Check solution file
        if (Test-Path "BusBuddy.sln") {
            Write-Information "✅ Solution file found" -InformationAction Continue
        } else {
            Write-Warning "❌ Solution file not found"
            return $false
        }

        # Check project file
        if (Test-Path "BusBuddy.csproj") {
            Write-Information "✅ Project file found" -InformationAction Continue
        } else {
            Write-Warning "❌ Project file not found"
            return $false
        }

        # Check Syncfusion license key
        if ($env:SYNCFUSION_LICENSE_KEY) {
            Write-Information "✅ Syncfusion license key configured" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Syncfusion license key not set (SYNCFUSION_LICENSE_KEY)"
        }

        Write-Information "=== Health check completed ===" -InformationAction Continue
        return $true
    }
    catch {
        Write-Warning "Health check failed: $($_.Exception.Message)"
        return $false
    }
}

# Set up aliases for easier access
# Reference: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/set-alias
Set-Alias -Name "bbBuild" -Value "Invoke-BusBuddyBuild"
Set-Alias -Name "bbRun" -Value "Start-BusBuddyApplication"
Set-Alias -Name "bbTest" -Value "Start-BusBuddyTest"
Set-Alias -Name "bbHealth" -Value "Test-BusBuddyHealth"

# Mark profile as successfully loaded
$env:BUSBUDDY_PROFILE_LOADED = '1'
Write-ProfileLog "BusBuddy PowerShell profile loaded successfully - PowerShell $($PSVersionTable.PSVersion)" -Level Information

# Profile scripts don't need Export-ModuleMember - functions are automatically available globally
