#Requires -Version 7.0
<#
.SYNOPSIS
  Rebuild BusBuddy WPF (Debug) and launch it from the UTM Windows guest.

.DESCRIPTION
  PowerShell 7+ only. Run inside the logged-in Windows desktop (not over SSH).

  From C:\dev:
    pwsh -NoProfile -File .\Launch-BusBuddy.ps1

  From C:\dev\BusBuddy-3:
    pwsh -NoProfile -File .\Launch-BusBuddy.ps1

  Double-click Launch-BusBuddy.cmd in either folder (uses Microsoft.PowerShell 7.6.6).

  Never build from Z:\ (UTM WebDAV). Runtime tree is C:\dev\BusBuddy-3.

.PARAMETER SkipBuild
  Start the existing Debug exe without restore/build.

.PARAMETER SkipPostgresCheck
  Do not probe Mac Docker Postgres at :5432.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipPostgresCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$NtfsRoot = 'C:\dev\BusBuddy-3'
$DevFolder = 'C:\dev'

function Write-Step {
    param([string]$Message, [string]$Color = 'Cyan')
    Write-Host $Message -ForegroundColor $Color
}

function Get-RepoRoot {
    $candidates = @(
        $PSScriptRoot
        (Join-Path $PSScriptRoot 'BusBuddy-3')
        $NtfsRoot
    )
    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        $sln = Join-Path $candidate 'BusBuddy.sln'
        if (Test-Path -LiteralPath $sln) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

function Test-WebDavPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ($Path -match '^\\\\') { return $true }
    if ($Path -match '^[Zz]:\\') { return $true }
    try {
        $letter = $Path.Substring(0, 1)
        $drive = Get-PSDrive -Name $letter -PSProvider FileSystem -ErrorAction SilentlyContinue
        if ($drive -and $drive.DisplayRoot -match 'localhost@|DavWWWRoot|\\\\') {
            return $true
        }
    } catch { }
    return $false
}

function Install-DevFolderLaunchers {
    param([string]$RepoRoot)
    if (-not (Test-Path -LiteralPath $DevFolder)) {
        return
    }

    $srcPs1 = Join-Path $RepoRoot 'Launch-BusBuddy.ps1'
    $srcCmd = Join-Path $RepoRoot 'Launch-BusBuddy.cmd'
    $destPs1 = Join-Path $DevFolder 'Launch-BusBuddy.ps1'
    $destCmd = Join-Path $DevFolder 'Launch-BusBuddy.cmd'

    if (Test-Path -LiteralPath $srcPs1) {
        Copy-Item -LiteralPath $srcPs1 -Destination $destPs1 -Force
    }
    if (Test-Path -LiteralPath $srcCmd) {
        Copy-Item -LiteralPath $srcCmd -Destination $destCmd -Force
    }
}

function Get-MacHostIp {
    param([string]$RepoRoot)
    $ipFile = Join-Path $RepoRoot 'keys\mac-host-ip.txt'
    if (Test-Path -LiteralPath $ipFile) {
        $ip = (Get-Content -LiteralPath $ipFile -Raw).Trim()
        if ($ip -match '^\d{1,3}(\.\d{1,3}){3}$') {
            return $ip
        }
    }
    $existing = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'User')
    if ($existing -match 'Host=([^;]+)') {
        return $Matches[1]
    }
    return '192.168.64.1'
}

function Import-KeyEnvFile {
    param([string]$RepoRoot)
    $envFile = Join-Path $RepoRoot 'keys\.env'
    if (-not (Test-Path -LiteralPath $envFile)) {
        Write-Step 'WARNING: keys\.env not found — Maps/Syncfusion keys may be missing.' 'Yellow'
        return
    }
    foreach ($raw in Get-Content -LiteralPath $envFile) {
        $line = $raw.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
        $eq = $line.IndexOf('=')
        if ($eq -le 0) { continue }
        $name = $line.Substring(0, $eq).Trim()
        $value = $line.Substring($eq + 1).Trim().Trim('"').Trim("'")
        if ($name.Length -eq 0) { continue }
        if ($name -eq 'DatabaseProvider' -and $value -eq 'Azure') { continue }
        Set-Item -Path "env:$name" -Value $value
    }
    Write-Step 'Loaded keys\.env'
}

function Set-PostgresEnv {
    param([string]$HostIp)
    $conn = "Host=$HostIp;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev;Include Error Detail=true;Timeout=5"
    $env:BUSBUDDY_CONNECTION = $conn
    $env:DatabaseProvider = 'Postgres'
    $userConn = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'User')
    if ($userConn -ne $conn) {
        [Environment]::SetEnvironmentVariable('BUSBUDDY_CONNECTION', $conn, 'User')
    }
    $userProvider = [Environment]::GetEnvironmentVariable('DatabaseProvider', 'User')
    if ($userProvider -ne 'Postgres') {
        [Environment]::SetEnvironmentVariable('DatabaseProvider', 'Postgres', 'User')
    }
    Write-Step "BUSBUDDY_CONNECTION Host=$HostIp  DatabaseProvider=Postgres"
}

function Test-PostgresPort {
    param([string]$HostIp)
    try {
        return [bool](Test-Connection -TargetName $HostIp -TcpPort 5432 -TimeoutSeconds 2 -Quiet)
    } catch {
        return $false
    }
}

function Stop-RunningApp {
    $procs = @(Get-Process -Name 'BusBuddy.WPF' -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) { return }
    Write-Step "Stopping $($procs.Count) leftover BusBuddy.WPF process(es) so Debug can rebuild..." 'Yellow'
    $procs | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

try {
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw "PowerShell 7+ required (this is $($PSVersionTable.PSVersion)). Install: winget install --id Microsoft.PowerShell --source winget"
    }

    $repoRoot = Get-RepoRoot
    if (-not $repoRoot) {
        throw "BusBuddy.sln not found. Expected $NtfsRoot. Do not run this from Z:\"
    }
    if (Test-WebDavPath -Path $repoRoot) {
        throw "Refusing to build on WebDAV/share ($repoRoot). Use $NtfsRoot."
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet not found. Install .NET 9 SDK: winget install --id Microsoft.DotNet.SDK.9 --source winget'
    }

    Install-DevFolderLaunchers -RepoRoot $repoRoot
    Set-Location -LiteralPath $repoRoot
    Write-Step "BusBuddy launch (pwsh $($PSVersionTable.PSVersion)) from $repoRoot" 'Green'

    $macIp = Get-MacHostIp -RepoRoot $repoRoot
    Import-KeyEnvFile -RepoRoot $repoRoot
    Set-PostgresEnv -HostIp $macIp

    if (-not $SkipPostgresCheck) {
        if (Test-PostgresPort -HostIp $macIp) {
            Write-Step "Postgres reachable at ${macIp}:5432" 'Green'
        } else {
            Write-Step "WARNING: Postgres not reachable at ${macIp}:5432. On the Mac run ./run-wpf.sh (or ./Scripts/ensure-postgres-docker.sh)." 'Yellow'
        }
    }

    Stop-RunningApp

    $exeDir = Join-Path $repoRoot 'BusBuddy.WPF\bin\Debug\net9.0-windows'
    $exe = Join-Path $exeDir 'BusBuddy.WPF.exe'

    if (-not $SkipBuild) {
        Write-Step 'Restoring...'
        & dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)" }

        Write-Step 'Building WPF Debug (full rebuild)...'
        & dotnet build BusBuddy.WPF\BusBuddy.WPF.csproj -c Debug -p:EnableWindowsTargeting=true --no-restore --no-incremental
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
    }

    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Built executable not found: $exe"
    }

    Write-Step 'Launching BusBuddy WPF...' 'Green'
    $proc = Start-Process -FilePath $exe -WorkingDirectory $exeDir -WindowStyle Normal -PassThru
    Start-Sleep -Seconds 2
    $proc.Refresh()
    if ($null -eq $proc -or $proc.HasExited) {
        throw "BusBuddy exited during startup. Check $exeDir\logs"
    }
    Write-Step "BusBuddy running (PID $($proc.Id)). Look at the Windows desktop for the main window." 'Green'
    Write-Step "Also available as $DevFolder\Launch-BusBuddy.ps1 and .cmd"
} catch {
    Write-Host ""
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ScriptStackTrace) {
        Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    }
    exit 1
}
