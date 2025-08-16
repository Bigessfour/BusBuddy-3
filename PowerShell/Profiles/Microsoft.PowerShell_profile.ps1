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
} elseif (-not $localDbExe) {
    Write-Verbose "⚠️  SqlLocalDB.exe not found. Install SQL Server Express LocalDB: https://www.microsoft.com/en-us/sql-server/sql-server-downloads"
}

# === WINGET PATH DETECTION ===
# Ensure winget.exe is available in PATH for package management
# Reference: https://learn.microsoft.com/windows/package-manager/winget/
$wingetPath = "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps"
if ((Test-Path "$wingetPath\winget.exe") -and ($wingetPath -notin ($env:PATH -split ';'))) {
    $env:PATH = "$env:PATH;$wingetPath"
    Write-Verbose "✅ Added winget to PATH: $wingetPath"
} elseif (-not (Test-Path "$wingetPath\winget.exe")) {
    Write-Verbose "⚠️  winget.exe not found. Install from Microsoft Store or GitHub: https://github.com/microsoft/winget-cli"
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
} elseif (-not $grokExe) {
    Write-Verbose "⚠️  grok.exe not found. Install Grok CLI: Install-BusBuddyGrokCli"
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
} elseif (-not $gcloudExe) {
    Write-Verbose "⚠️  gcloud.cmd not found. Install Google Cloud CLI: Install-BusBuddyGoogleCli"
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
} elseif (-not $earthengineExe) {
    Write-Verbose "⚠️  earthengine.exe not found. Install Earth Engine CLI: pip install earthengine-api"
}

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
