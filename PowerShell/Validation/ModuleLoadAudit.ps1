#requires -Version 7.5
# Docs:
# - Import-Module: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
# - Write-Information: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/write-information
# - about_Functions: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Functions
# - Get-WinEvent: https://learn.microsoft.com/powershell/module/microsoft.powershell.diagnostics/get-winevent
# - Register-WmiEvent: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/register-wmievent
# - Win32_ProcessStopTrace: https://learn.microsoft.com/windows/win32/wmisdk/win32-processstoptrace
# - about_Preference_Variables: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Preference_Variables
# - about_Requires: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Requires
# - DateTime.ParseExact: https://learn.microsoft.com/dotnet/api/system.datetime.parseexact
# cspell:ignore OPTOUT

param()

$script:BusBuddy_ModuleAuditState = $script:BusBuddy_ModuleAuditState ?? @{
    Enabled   = $false
    LogPath   = Join-Path -Path $PSScriptRoot -ChildPath "..\..\logs\module-audit.log" | Resolve-Path -ErrorAction SilentlyContinue
    Counters  = @{}
}

function Start-ModuleLoadAudit {
    [CmdletBinding()]
    param(
        [string]$LogPath
    )
    if ($script:BusBuddy_ModuleAuditState.Enabled) { return }

    if ($LogPath) {
        $script:BusBuddy_ModuleAuditState.LogPath = $LogPath
    } elseif (-not $script:BusBuddy_ModuleAuditState.LogPath) {
        $script:BusBuddy_ModuleAuditState.LogPath = Join-Path $PSScriptRoot "module-audit.log"
    }

    $logDir = Split-Path $script:BusBuddy_ModuleAuditState.LogPath -Parent
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

    # Define a shim that wraps the built-in Import-Module and logs usage.
    function Import-Module {
        [CmdletBinding(DefaultParameterSetName='All')]
        param(
            [Parameter(Position=0, ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
            [Alias('Name')]
            $Module,
            [switch]$Force,
            [switch]$PassThru,
            [switch]$DisableNameChecking,
            [switch]$Global,
            [switch]$NoClobber,
            [string]$Prefix
        )
        begin {
            try {
                $ts = Get-Date -Format o
                $caller = ($MyInvocation.PSCommandPath) ?? '<Interactive>'
                $stack  = (Get-PSCallStack | ForEach-Object { "{0}:{1}" -f $_.ScriptName, $_.FunctionName }) -join " | "
                $line   = "[{0}] Import-Module Module='{1}' Force={2} Global={3} Prefix='{4}' Caller='{5}' Stack='{6}'" -f $ts, $Module, $Force, $Global, $Prefix, $caller, $stack
                Add-Content -Path $script:BusBuddy_ModuleAuditState.LogPath -Value $line -Encoding UTF8

                # Count occurrences by module name
                $key = ($Module ?? '<null>')
                if (-not $script:BusBuddy_ModuleAuditState.Counters.ContainsKey($key)) { $script:BusBuddy_ModuleAuditState.Counters[$key] = 0 }
                $script:BusBuddy_ModuleAuditState.Counters[$key]++
            } catch {
                # Avoid Write-Host per standards; keep audit resilient
                Write-Information ("Audit error: {0}" -f $_.Exception.Message) -InformationAction Continue
            }
        }
        process {
            # Delegate to built-in, documented cmdlet
            Microsoft.PowerShell.Core\Import-Module @PSBoundParameters
        }
    }

    $script:BusBuddy_ModuleAuditState.Enabled = $true
    Write-Information ("Module load audit started. Log: {0}" -f $script:BusBuddy_ModuleAuditState.LogPath) -InformationAction Continue
}

function Stop-ModuleLoadAudit {
    [CmdletBinding()]
    param()
    if (-not $script:BusBuddy_ModuleAuditState.Enabled) { return }
    if (Get-Command Import-Module -CommandType Function -ErrorAction SilentlyContinue) {
        Remove-Item Function:\Import-Module -Force -ErrorAction SilentlyContinue
    }
    $script:BusBuddy_ModuleAuditState.Enabled = $false

    # Summary
    $summary = "=== Module Import Summary ===`n" + ($script:BusBuddy_ModuleAuditState.Counters.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0} : {1}" -f $_.Key, $_.Value }) -join "`n"
    Add-Content -Path $script:BusBuddy_ModuleAuditState.LogPath -Value $summary -Encoding UTF8
    Write-Information "Module load audit stopped." -InformationAction Continue
}

# === New: Verbose/Information stream helpers (per about_Preference_Variables) ===
function Enable-VerboseLogging {
    [CmdletBinding()]
    param()
    # Capture current preferences so they can be restored
    $script:BusBuddy_OutputPreferences = @{
        VerbosePreference      = $VerbosePreference
        InformationPreference  = $InformationPreference
        DebugPreference        = $DebugPreference
    }
    $VerbosePreference = 'Continue'
    $InformationPreference = 'Continue'
    $DebugPreference = 'Continue'
    Write-Information "Verbose/Information/Debug preferences enabled." -InformationAction Continue
}

function Restore-OutputPreferences {
    [CmdletBinding()]
    param()
    if ($script:BusBuddy_OutputPreferences) {
        $VerbosePreference = $script:BusBuddy_OutputPreferences.VerbosePreference
        $InformationPreference = $script:BusBuddy_OutputPreferences.InformationPreference
        $DebugPreference = $script:BusBuddy_OutputPreferences.DebugPreference
        Write-Information "Output preferences restored." -InformationAction Continue
    }
}

# === New: Crash monitor using Win32_ProcessStopTrace (per Register-WmiEvent, Win32_ProcessStopTrace docs) ===
$script:BusBuddy_CrashMonState = $script:BusBuddy_CrashMonState ?? @{
    Enabled       = $false
    ProcessName   = $null
    LogPath       = $null
    SourceId      = $null
}

function Start-AppCrashMonitor {
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$ProcessName = 'BusBuddy.exe',
        [Parameter()]
        [string]$LogPath
    )
    if ($script:BusBuddy_CrashMonState.Enabled) { return }

    # Default log path next to this validation script
    if (-not $LogPath) {
        $LogPath = Join-Path $PSScriptRoot "app-crash-monitor.log"
    }
    $dir = Split-Path $LogPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $query = "SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName='{0}'" -f $ProcessName
    $source = "BusBuddy.ProcessStop.{0}" -f ([guid]::NewGuid())

    # Action runs in background per Register-WmiEvent docs; avoid Write-Host, log to file
    $null = Register-WmiEvent -Query $query -SourceIdentifier $source -Action {
        try {
            $e = $Event.SourceEventArgs.NewEvent
            $ts = Get-Date -Format o
            $line = "[{0}] ProcessStop Name='{1}' PID={2} ExitStatus={3}" -f $ts, $e.ProcessName, $e.ProcessID, $e.ExitStatus
            Add-Content -Path $using:LogPath -Value $line -Encoding UTF8
        } catch {
            Write-Information ("Crash monitor action error: {0}" -f $_.Exception.Message) -InformationAction Continue
        }
    }

    $script:BusBuddy_CrashMonState.Enabled = $true
    $script:BusBuddy_CrashMonState.ProcessName = $ProcessName
    $script:BusBuddy_CrashMonState.LogPath = $LogPath
    $script:BusBuddy_CrashMonState.SourceId = $source
    Write-Information ("Crash monitor started for {0}. Log: {1}" -f $ProcessName, $LogPath) -InformationAction Continue
}

function Stop-AppCrashMonitor {
    [CmdletBinding()]
    param()
    if (-not $script:BusBuddy_CrashMonState.Enabled) { return }
    if ($script:BusBuddy_CrashMonState.SourceId) {
        Unregister-Event -SourceIdentifier $script:BusBuddy_CrashMonState.SourceId -ErrorAction SilentlyContinue
        Remove-Event -SourceIdentifier $script:BusBuddy_CrashMonState.SourceId -ErrorAction SilentlyContinue
    }
    $script:BusBuddy_CrashMonState.Enabled = $false
    $script:BusBuddy_CrashMonState.SourceId = $null
    Write-Information "Crash monitor stopped." -InformationAction Continue
}

# === New: Event Viewer query for common crash events (per Get-WinEvent docs) ===
function Get-RecentAppCrashes {
    [CmdletBinding()]
    param(
        [int]$Minutes = 15,
        [string]$ProcessName
    )
    $start = (Get-Date).AddMinutes(-[math]::Abs($Minutes))
    $providers = @('.NET Runtime', 'Application Error', 'Windows Error Reporting')
    $events = Get-WinEvent -FilterHashtable @{
        LogName   = 'Application'
        StartTime = $start
        Level     = 2 # Error
    } | Where-Object { $providers -contains $_.ProviderName }

    if ($ProcessName) {
        # Basic string contains match; details format differs by provider
        $events = $events | Where-Object { $_.Message -like "*$ProcessName*" }
    }

    $events | Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message
}

# Correlate quick-close signals — module loads, crash monitor, and Event Viewer errors
# Docs:
# - Get-WinEvent: https://learn.microsoft.com/powershell/module/microsoft.powershell.diagnostics/get-winevent
# - Select-String: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/select-string
function Get-ModuleLoadTimeline {
    [CmdletBinding()]
    param(
        [int]$Minutes = 15
    )
    $logPath = $script:BusBuddy_ModuleAuditState.LogPath
    if (-not $logPath -or -not (Test-Path $logPath)) { return @() }

    $cutoff = (Get-Date).AddMinutes(-[math]::Abs($Minutes))
    $rx = '^\[(?<ts>.+?)\]\s(?<rest>.+)$'
    $out = New-Object System.Collections.Generic.List[object]
    foreach ($line in Get-Content -Path $logPath -ErrorAction SilentlyContinue) {
        $m = [regex]::Match($line, $rx)
        if (-not $m.Success) { continue }
        $ts = $null
        # Culture-safe parse for ISO 8601 'o' format
        if ([datetime]::TryParseExact($m.Groups['ts'].Value, 'o', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$ts) -and $ts -ge $cutoff) {
            $out.Add([pscustomobject]@{
                Time    = $ts
                Message = $m.Groups['rest'].Value
            })
        }
    }
    $out
}

# Pulls crash signals together for a timeframe
function Get-AppQuickCloseSignals {
    [CmdletBinding()]
    param(
        [int]$Minutes = 15,
        [string]$ProcessName = 'BusBuddy.exe'
    )

    $cutoff = (Get-Date).AddMinutes(-[math]::Abs($Minutes))

    # Crash monitor log (if Start-AppCrashMonitor was used)
    $monitorLog = @()
    if ($script:BusBuddy_CrashMonState.LogPath -and (Test-Path $script:BusBuddy_CrashMonState.LogPath)) {
        $rx = '^\[(?<ts>.+?)\]\s(?<rest>.+)$'
        foreach ($line in Get-Content -Path $script:BusBuddy_CrashMonState.LogPath -ErrorAction SilentlyContinue) {
            $m = [regex]::Match($line, $rx)
            if (-not $m.Success) { continue }
            $ts = $null
            # Culture-safe parse for ISO 8601 'o' format
            if ([datetime]::TryParseExact($m.Groups['ts'].Value, 'o', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$ts) -and $ts -ge $cutoff) {
                $monitorLog += [pscustomobject]@{
                    Time    = $ts
                    Message = $m.Groups['rest'].Value
                }
            }
        }
    }

    # Event Viewer (uses existing Get-RecentAppCrashes)
    $events = Get-RecentAppCrashes -Minutes $Minutes -ProcessName $ProcessName

    # Module-load timeline (uses existing audit log)
    $moduleLoads = Get-ModuleLoadTimeline -Minutes $Minutes

    [pscustomobject]@{
        SinceMinutes = $Minutes
        ProcessName  = $ProcessName
        CrashEvents  = $events
        MonitorLog   = $monitorLog
        ModuleLoads  = $moduleLoads
    }
}

# Syncfusion licensing/version pre-check (docs-first)
# Docs:
# - about_Environment_Variables: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Environment_Variables
# - Syncfusion WPF licensing: https://help.syncfusion.com/wpf/licensing/overview
function Test-SyncfusionLicensePrereqs {
    [CmdletBinding()]
    param(
        [switch]$Detailed
    )

    # Check env var presence (Process → User → Machine), per about_Environment_Variables
    $licenseKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Process')
    $source = 'Process'
    if ([string]::IsNullOrWhiteSpace($licenseKey)) {
        $licenseKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User')
        $source = 'User'
    }
    if ([string]::IsNullOrWhiteSpace($licenseKey)) {
        $licenseKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Machine')
        $source = 'Machine'
    }

    # Attempt to locate central package version(s) from Directory.Build.props (best-effort)
    $repoRoot = try { (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path } catch { $null }
    $propsPath = if ($repoRoot) { Join-Path $repoRoot 'Directory.Build.props' }
    $syncfusionVersions = @()
    if ($propsPath -and (Test-Path $propsPath)) {
        $lines = Get-Content -Path $propsPath -ErrorAction SilentlyContinue
        # Basic scan for Syncfusion package Version="x.y.z"
        foreach ($l in ($lines | Where-Object { $_ -match 'Syncfusion\..+WPF' })) {
            $m = [regex]::Match($l, 'Version\s*=\s*"(?<ver>[0-9\.]+)"')
            if ($m.Success) { $syncfusionVersions += $m.Groups['ver'].Value }
        }
        $syncfusionVersions = $syncfusionVersions | Sort-Object -Unique
    }

    $result = [pscustomobject]@{
        LicenseKeyPresent   = -not [string]::IsNullOrWhiteSpace($licenseKey)
        LicenseKeySource    = if (-not [string]::IsNullOrWhiteSpace($licenseKey)) { $source } else { $null }
        SyncfusionVersions  = $syncfusionVersions
        Recommendation      = $null
    }

    if (-not $result.LicenseKeyPresent) {
        $result.Recommendation = "Set SYNCFUSION_LICENSE_KEY (User or Machine) and register in App constructor per Syncfusion docs."
        Write-Warning "SYNCFUSION_LICENSE_KEY not found. See Syncfusion WPF licensing documentation."
    } elseif ($syncfusionVersions -and ($syncfusionVersions -notcontains '30.2.4')) {
        $result.Recommendation = "Consider upgrading Syncfusion WPF packages to 30.2.4 and verify license registration."
        Write-Information "Detected Syncfusion versions: $($syncfusionVersions -join ', '). Consider 30.2.4 if compatible." -InformationAction Continue
    }

    if ($Detailed) { return $result } else { return $result }
}

# Azure SQL validation helpers — CI manages firewall; this only validates client settings
# Docs:
# - SqlConnection: https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection
# - Connection strings: https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax
# - Secure connections (Encrypt/TrustServerCertificate): https://learn.microsoft.com/sql/connect/ado-net/secure-sql-connection
function Test-AzureSqlConnectionString {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$ConnectionString
    )

    # Parse into case-insensitive map
    $kv = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($pair in ($ConnectionString -split ';' | Where-Object { $_.Trim() })) {
        $idx = $pair.IndexOf('=')
        if ($idx -gt 0) {
            $k = $pair.Substring(0, $idx).Trim()
            $v = $pair.Substring($idx + 1).Trim()
            if ($k) { $kv[$k] = $v }
        }
    }

    # Normalized lookups
    function Get-Val([string]$name, [string[]]$aliases) {
        if ($kv.ContainsKey($name)) { return $kv[$name] }
        foreach ($a in $aliases) { if ($kv.ContainsKey($a)) { return $kv[$a] } }
        return $null
    }

    $server  = Get-Val -name 'Server' -aliases @('Data Source')
    $isAzure = $server -and ($server -like '*.database.windows.net*' -or $server -like 'tcp:*database.windows.net*')

    $encrypt = Get-Val -name 'Encrypt' -aliases @()
    $tsc     = Get-Val -name 'TrustServerCertificate' -aliases @()
    $psi     = Get-Val -name 'Persist Security Info' -aliases @('PersistSecurityInfo')
    $mars    = Get-Val -name 'MultipleActiveResultSets' -aliases @('MARS')
    $cto     = Get-Val -name 'Connect Timeout' -aliases @('Connection Timeout')

    # Parse Connect Timeout safely (per .NET TryParse)
    # Docs: https://learn.microsoft.com/dotnet/api/system.int32.tryparse
    [int]$ctoInt = 0
    if ($cto) { [void][int]::TryParse($cto, [ref]$ctoInt) }

    $checks = [ordered]@{
        IsAzureSqlTarget                 = [bool]$isAzure
        EncryptExplicitTrue              = ($encrypt -and $encrypt.ToString().Trim().ToLower() -eq 'true')
        EncryptMissingButDefaultIsSecure = (-not $encrypt) # SqlClient defaults Encrypt=True
        TrustServerCertificateIsFalse    = ($tsc -and $tsc.ToString().Trim().ToLower() -eq 'false') -or (-not $tsc)
        PersistSecurityInfoIsFalse       = ($psi -and $psi.ToString().Trim().ToLower() -eq 'false') -or (-not $psi)
        MultipleActiveResultSetsEnabled  = ($mars -and $mars.ToString().Trim().ToLower() -eq 'true')
        ConnectTimeoutAtLeast30          = ($ctoInt -ge 30)
    }

    # Replace invalid '-implies' with explicit boolean logic
    $compliant = if ($checks.IsAzureSqlTarget) {
        $checks.TrustServerCertificateIsFalse -and ($checks.EncryptExplicitTrue -or $checks.EncryptMissingButDefaultIsSecure)
    } else {
        $true
    }

    # Docs:
    # - Secure SQL connections (Encrypt/TrustServerCertificate): https://learn.microsoft.com/sql/connect/ado-net/secure-sql-connection
    # Build recommendations list
    $recommendations = [System.Collections.Generic.List[string]]::new()
    if ($checks.IsAzureSqlTarget) {
        if (-not $encrypt) { [void]$recommendations.Add("Add Encrypt=True explicitly (SqlClient defaults to True).") }
        elseif (-not $checks.EncryptExplicitTrue) { [void]$recommendations.Add("Set Encrypt=True.") }

        if (-not $tsc) { [void]$recommendations.Add("Add TrustServerCertificate=False explicitly (recommended).") }
        elseif (-not $checks.TrustServerCertificateIsFalse) { [void]$recommendations.Add("Set TrustServerCertificate=False.") }

        if ($psi -and -not $checks.PersistSecurityInfoIsFalse) { [void]$recommendations.Add("Set Persist Security Info=False.") }

        if (-not $checks.ConnectTimeoutAtLeast30) { [void]$recommendations.Add("Set Connect Timeout=30 (or higher) for Azure SQL.") }
    }

    [pscustomobject]@{
        IsAzureSqlTarget           = $checks.IsAzureSqlTarget
        Server                     = $server
        Encrypt                    = $encrypt
        TrustServerCertificate     = $tsc
        PersistSecurityInfo        = $psi
        MultipleActiveResultSets   = $mars
        ConnectTimeout             = $cto
        Compliant                  = $compliant
        Recommendations            = $recommendations
    }
}

function Test-AzureSqlConnectivity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$ConnectionString,
        [switch]$TryConnect
    )

    # Prefer Microsoft.Data.SqlClient; fall back to System.Data.SqlClient if unavailable
    $provider = $null
    try {
        Add-Type -AssemblyName 'Microsoft.Data.SqlClient' -ErrorAction Stop
        $provider = 'Microsoft.Data.SqlClient'
    } catch {
        try {
            Add-Type -AssemblyName 'System.Data' -ErrorAction Stop
            $provider = 'System.Data.SqlClient'
            Write-Information "Microsoft.Data.SqlClient not found; falling back to System.Data.SqlClient." -InformationAction Continue
        } catch {
            Write-Error "No SqlClient provider available. Install Microsoft.Data.SqlClient package." -ErrorAction Stop
        }
    }

    $result = [pscustomobject]@{
        Provider       = $provider
        CanInstantiate = $false
        Connected      = $false
        ErrorMessage   = $null
    }

    try {
        # Explicit if/else for connection creation (avoid assignment-with-if expression)
        if ($provider -eq 'Microsoft.Data.SqlClient') {
            $conn = [Microsoft.Data.SqlClient.SqlConnection]::new($ConnectionString)
        } else {
            $conn = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
        }
        $result.CanInstantiate = $true

        if ($TryConnect) {
            $opened = $false
            try {
                $conn.Open()
                $opened = $true
                $result.Connected = $true
            } catch {
                $result.ErrorMessage = $_.Exception.Message
            }
            # Safe cleanup without finally (prevents parser issues)
            if ($null -ne $conn) {
                if ($opened -and $conn.State -eq 'Open') { $conn.Close() }
                $conn.Dispose()
            }
        } else {
            # Dispose even when not connecting to avoid leaks
            if ($null -ne $conn) { $conn.Dispose() }
        }
    } catch {
        $result.ErrorMessage = $_.Exception.Message
    }

    return $result
}

# === CI local helpers (simulate GitHub Actions locally) ===
# Docs:
# - Start-Transcript/Stop-Transcript: https://learn.microsoft.com/powershell/module/microsoft.powershell.host/start-transcript
# - about_Automatic_Variables ($PSVersionTable): https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Automatic_Variables
# - about_Environment_Variables: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Environment_Variables
function Start-CILocalSession {
    [CmdletBinding()]
    param(
        [string]$TranscriptPath,
        # Avoid PSAvoidDefaultValueSwitchParameter by using [bool] default
        [bool]$SetCIEnv = $true
    )

    if (-not $TranscriptPath) {
        $TranscriptPath = Join-Path $PSScriptRoot "ci-local.log"
    }
    $dir = Split-Path $TranscriptPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    try { Start-Transcript -Path $TranscriptPath -Append | Out-Null } catch {
        Write-Information ("Start-Transcript failed: {0}" -f $_.Exception.Message) -InformationAction Continue
    }

    if ($SetCIEnv) {
        $env:CI = 'true'
        $env:GITHUB_ACTIONS = 'true'
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = $env:DOTNET_CLI_TELEMETRY_OPTOUT ?? '1'
        $env:DOTNET_NOLOGO = $env:DOTNET_NOLOGO ?? '1'
    }

    Write-Information ("CI local session started. Transcript: {0}" -f $TranscriptPath) -InformationAction Continue
}

function Stop-CILocalSession {
    [CmdletBinding()]
    param()
    try { Stop-Transcript | Out-Null } catch { }
    Write-Information "CI local session stopped." -InformationAction Continue
}

# Load key=value pairs from a .env-style file into the current Process environment.
# Docs:
# - Get-Content: https://learn.microsoft.com/powershell/module/microsoft.powershell.management/get-content
# - about_Environment_Variables: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Environment_Variables
function Import-EnvFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateScript({ Test-Path $_ })]
        [string]$Path,
        [switch]$Overwrite
    )

    foreach ($raw in (Get-Content -Path $Path -ErrorAction Stop)) {
        $line = $raw.Trim()
        if (-not $line -or $line.StartsWith('#')) { continue }
        $idx = $line.IndexOf('=')
        if ($idx -le 0) { continue }
        $name  = $line.Substring(0, $idx).Trim()
        $value = $line.Substring($idx + 1).Trim().Trim('"')
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if (-not $Overwrite -and [System.Environment]::GetEnvironmentVariable($name,'Process')) { continue }
        [System.Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
    Write-Information ("Imported environment variables from {0}" -f (Resolve-Path $Path)) -InformationAction Continue
}

# Validate CI-local prerequisites: PowerShell version, bb-* commands, and essential secrets
# Docs:
# - Get-Command: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/get-command
# - about_Automatic_Variables: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Automatic_Variables
# - about_Environment_Variables: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Environment_Variables
function Test-CILocalPrereqs {
    [CmdletBinding()]
    param(
        [switch]$CheckAzureSql
    )

    $requiredCommands = @('bb-health','bb-build','bb-test','bb-mvp-check','bb-anti-regression','bb-xaml-validate','bb-run')
    $cmdStatus = @{}
    foreach ($c in $requiredCommands) {
        $cmdStatus[$c] = [bool](Get-Command $c -ErrorAction SilentlyContinue)
    }

    $psOk = ($PSVersionTable.PSVersion.Major -gt 7) -and `
            ($PSVersionTable.PSVersion.Major -gt 7 -or $PSVersionTable.PSVersion -ge [version]'7.5.2')

    $syncfusionKey = [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','Process') `
                  ?? [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','User') `
                  ?? [System.Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','Machine')

    $azureUser = $null; $azurePwd = $null
    if ($CheckAzureSql) {
        $azureUser = [System.Environment]::GetEnvironmentVariable('AZURE_SQL_USER','Process') `
                 ?? [System.Environment]::GetEnvironmentVariable('AZURE_SQL_USER','User') `
                 ?? [System.Environment]::GetEnvironmentVariable('AZURE_SQL_USER','Machine')
        $azurePwd  = [System.Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD','Process') `
                 ?? [System.Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD','User') `
                 ?? [System.Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD','Machine')
    }

    [pscustomobject]@{
        PowerShellVersionOk       = $psOk
        PowerShellVersion         = $PSVersionTable.PSVersion.ToString()
        CommandsFound             = $cmdStatus
        SyncfusionLicensePresent  = -not [string]::IsNullOrWhiteSpace($syncfusionKey)
        AzureSqlUserPresent       = if ($CheckAzureSql) { -not [string]::IsNullOrWhiteSpace($azureUser) } else { $null }
        AzureSqlPasswordPresent   = if ($CheckAzureSql) { -not [string]::IsNullOrWhiteSpace($azurePwd) } else { $null }
        Recommendations           = @(
            if (-not $psOk) { "Use PowerShell 7.5.2+ (see applyTo and global tools requirements)." }
            if ( ($cmdStatus.Values) -notcontains $true ) { "Load BusBuddy PowerShell profile to register bb-* commands." }
            if (-not $cmdStatus['bb-build']) { "Ensure bb-* commands are in scope (see bb-commands)." }
            if ([string]::IsNullOrWhiteSpace($syncfusionKey)) { "Set SYNCFUSION_LICENSE_KEY as User or Machine variable." }
            if ($CheckAzureSql -and ([string]::IsNullOrWhiteSpace($azureUser) -or [string]::IsNullOrWhiteSpace($azurePwd))) { "Set AZURE_SQL_USER and AZURE_SQL_PASSWORD for Azure SQL scenarios." }
        )
    }
}

# Run a CI-like sequence locally using bb-* commands
# Docs:
# - Call operator &: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Operators#call-operator-
function Invoke-CILocalSuite {
    [CmdletBinding()]
    param(
        [switch]$SkipHealth,
        [switch]$SkipSecurity,
        [switch]$SkipXamlValidate,
        [switch]$SkipTests,
        [switch]$SkipMvpCheck
    )

    $steps = @()

    if (-not $SkipHealth)      { $steps += @{ Name='bb-health';           Invoke={ & bb-health } } }
    if (-not $SkipSecurity)    { $steps += @{ Name='bb-anti-regression';   Invoke={ & bb-anti-regression } } }
    if (-not $SkipXamlValidate){ $steps += @{ Name='bb-xaml-validate';     Invoke={ & bb-xaml-validate } } }
    $steps += @{ Name='bb-build';          Invoke={ & bb-build } }
    if (-not $SkipTests)       { $steps += @{ Name='bb-test';             Invoke={ & bb-test } } }
    if (-not $SkipMvpCheck)    { $steps += @{ Name='bb-mvp-check';        Invoke={ & bb-mvp-check } } }

    $results = New-Object System.Collections.Generic.List[object]
    foreach ($s in $steps) {
        $ok = $false; $err = $null; $started = Get-Date
        try {
            Write-Information ("[CI-Local] Running {0}..." -f $s.Name) -InformationAction Continue
            & $s.Invoke
            $ok = $?
        } catch {
            $err = $_.Exception.Message
        }
        $ended = Get-Date
        $results.Add([pscustomobject]@{
            Step   = $s.Name
            OK     = $ok
            Error  = $err
            Start  = $started
            End    = $ended
            ElapsedSeconds = [int]($ended - $started).TotalSeconds
        })
        if (-not $ok) { Write-Warning ("[CI-Local] Step {0} reported failure." -f $s.Name) }
    }
    return $results
}

# Update exports to include new helpers
Export-ModuleMember -Function Start-ModuleLoadAudit, Stop-ModuleLoadAudit, Enable-VerboseLogging, Restore-OutputPreferences, Start-AppCrashMonitor, Stop-AppCrashMonitor, Get-RecentAppCrashes, Get-AppQuickCloseSignals, Get-ModuleLoadTimeline, Test-SyncfusionLicensePrereqs, Test-AzureSqlConnectionString, Test-AzureSqlConnectivity, Start-CILocalSession, Stop-CILocalSession, Import-EnvFile, Test-CILocalPrereqs, Invoke-CILocalSuite
