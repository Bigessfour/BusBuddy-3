# vm-setup.ps1
# One-time bootstrap for a blank Windows 11 VM (UTM / Parallels) to run BusBuddy WPF.
#
# Run inside the VM as Administrator (right-click PowerShell -> Run as administrator):
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
#   .\vm-setup.ps1
#
# What it installs (via winget):
#   - winget itself (if missing)
#   - .NET 9 SDK (ARM64 on Apple Silicon UTM; x64 on Intel)
#   - PowerShell 7
#   - Git for Windows
#   - Windows Terminal (optional convenience)
#
# Database: BusBuddy hybrid dev uses Postgres in Docker on the Mac host.
#   Start on Mac:  docker compose --profile db up -d
#   Then in VM:    $env:BUSBUDDY_CONNECTION = "Host=<mac-ip>;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=${BUSBUDDY_PG_PASSWORD}"
#   (run ./run-wpf.sh on Mac to print your host IP)
#
# After setup, close and reopen PowerShell, then:
#   .\utm_run_in_vm.ps1

#Requires -RunAsAdministrator

param(
    [switch]$SkipTerminal,
    [switch]$SkipGit,
    [switch]$InstallSqlExpress,
    [switch]$SkipWingetBootstrap
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Test-IsAdmin {
    $current = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $current.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-MachineArch {
    switch ($env:PROCESSOR_ARCHITECTURE) {
        'ARM64' { return 'arm64' }
        'AMD64' { return 'x64' }
        'x86'   { return 'x86' }
        default { return 'x64' }
    }
}

function Install-WingetBootstrap {
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        $ver = (winget --version 2>$null)
        Write-Host "winget already present: $ver" -ForegroundColor Green
        return
    }

    Write-Step "Bootstrapping winget (Windows Package Manager)..."

    # Method 1: Microsoft.WinGet.Client module (recommended on Windows 11)
    try {
        if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue)) {
            Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force | Out-Null
        }
        if ((Get-PSRepository -Name PSGallery -ErrorAction SilentlyContinue).InstallationPolicy -ne 'Trusted') {
            Set-PSRepository -Name PSGallery -InstallationPolicy Trusted
        }
        if (-not (Get-Module -ListAvailable -Name Microsoft.WinGet.Client)) {
            Install-Module -Name Microsoft.WinGet.Client -Force -Repository PSGallery -Scope AllUsers -AllowClobber
        }
        Import-Module Microsoft.WinGet.Client -Force
        Repair-WinGetPackageManager -AllUsers
        Start-Sleep -Seconds 3
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Write-Host "winget installed via Microsoft.WinGet.Client: $(winget --version)" -ForegroundColor Green
            return
        }
    }
    catch {
        Write-Warning "WinGet.Client bootstrap failed: $($_.Exception.Message). Trying App Installer bundle..."
    }

    # Method 2: App Installer MSIX bundle (aka.ms/getwinget)
    $temp = Join-Path $env:TEMP "winget-bootstrap"
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    $bundle = Join-Path $temp "Microsoft.DesktopAppInstaller.msixbundle"

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://aka.ms/getwinget" -OutFile $bundle -UseBasicParsing

    # ARM64 guests need arm64 VCLibs + UI.Xaml dependencies
    $arch = Get-MachineArch
    if ($arch -eq 'arm64') {
        $deps = @(
            @{ Url = 'https://aka.ms/Microsoft.VCLibs.arm64.14.00.Desktop.appx'; File = 'VCLibs.arm64.appx' },
            @{ Url = 'https://aka.ms/Microsoft.UI.Xaml.2.8.arm64.appx'; File = 'UI.Xaml.arm64.appx' }
        )
    }
    else {
        $deps = @(
            @{ Url = 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx'; File = 'VCLibs.x64.appx' },
            @{ Url = 'https://aka.ms/Microsoft.UI.Xaml.2.8.x64.appx'; File = 'UI.Xaml.x64.appx' }
        )
    }

    foreach ($dep in $deps) {
        $path = Join-Path $temp $dep.File
        Invoke-WebRequest -Uri $dep.Url -OutFile $path -UseBasicParsing
        Add-AppxPackage -Path $path -ErrorAction SilentlyContinue | Out-Null
    }

    Add-AppxPackage -Path $bundle
    Start-Sleep -Seconds 3

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget still not available. Sign in to Microsoft Store, update 'App Installer', reboot, and re-run this script."
    }
    Write-Host "winget installed via App Installer bundle: $(winget --version)" -ForegroundColor Green
}

function Install-WingetPackage {
    param(
        [Parameter(Mandatory)][string]$Id,
        [string]$DisplayName = $Id,
        [string]$Architecture,
        [string[]]$ExtraArgs = @()
    )

    Write-Step "Installing $DisplayName ($Id)..."

    $args = @(
        'install', '--id', $Id,
        '--accept-package-agreements',
        '--accept-source-agreements',
        '--disable-interactivity'
    )
    if ($Architecture) {
        $args += @('--architecture', $Architecture)
    }
    $args += $ExtraArgs

    & winget @args
    if ($LASTEXITCODE -gt 1) {
        throw "winget install failed for $Id (exit $LASTEXITCODE)"
    }
}

function Test-DotNetSdk9 {
    try {
        $ver = & dotnet --version 2>$null
        return ($ver -match '^9\.')
    }
    catch { return $false }
}

# ---- main ----

if (-not (Test-IsAdmin)) {
    Write-Error "Re-run PowerShell as Administrator, then: .\vm-setup.ps1"
    exit 1
}

Write-Host @"

BusBuddy VM setup (blank slate -> ready for WPF)
Architecture: $(Get-MachineArch)
"@ -ForegroundColor White

if (-not $SkipWingetBootstrap) {
    Install-WingetBootstrap
}

# Refresh PATH in this session (winget/dotnet may not be visible until new shell)
$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$env:Path = "$machinePath;$userPath"

$arch = Get-MachineArch

# Core: .NET 9 SDK (includes WPF / Windows Desktop targeting)
if (-not (Test-DotNetSdk9)) {
    Install-WingetPackage -Id 'Microsoft.DotNet.SDK.9' -DisplayName '.NET 9 SDK' -Architecture $arch
}
else {
    Write-Host ".NET 9 SDK already installed: $(dotnet --version)" -ForegroundColor Green
}

# PowerShell 7 (better scripting; utm_run_in_vm.ps1 works on 5.1 too)
Install-WingetPackage -Id 'Microsoft.PowerShell' -DisplayName 'PowerShell 7'

if (-not $SkipGit) {
    Install-WingetPackage -Id 'Git.Git' -DisplayName 'Git for Windows' -Architecture $arch
}

if (-not $SkipTerminal) {
    Install-WingetPackage -Id 'Microsoft.WindowsTerminal' -DisplayName 'Windows Terminal'
}

if ($InstallSqlExpress) {
    # Optional: only if you want local SQL Server instead of Mac Docker Postgres
    Install-WingetPackage -Id 'Microsoft.SQLServer.2022.Express' -DisplayName 'SQL Server 2022 Express' -ExtraArgs @('--override', '/quiet /norestart')
}

Write-Step "Verifying installations..."

# Refresh PATH again after installs
$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$env:Path = "$machinePath;$userPath"

$checks = @(
    @{ Name = 'winget';  Cmd = { winget --version } },
    @{ Name = 'dotnet';  Cmd = { dotnet --version } },
    @{ Name = 'pwsh';    Cmd = { pwsh -NoProfile -Command '$PSVersionTable.PSVersion.ToString()' } },
    @{ Name = 'git';     Cmd = { git --version }; Skip = $SkipGit }
)

foreach ($c in $checks) {
    if ($c.Skip) { continue }
    try {
        $out = & $c.Cmd 2>$null
        Write-Host ("  OK  {0,-8} {1}" -f $c.Name, $out) -ForegroundColor Green
    }
    catch {
        Write-Host ("  ??  {0,-8} not in PATH yet — open a NEW PowerShell window after reboot" -f $c.Name) -ForegroundColor Yellow
    }
}

Write-Step "Optional environment variables (set after you have keys)"

Write-Host @"
  # Syncfusion (removes trial watermark) — User scope, one time:
  [Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'your-key-here', 'User')

  # Postgres on Mac Docker (replace <mac-ip> — run ./run-wpf.sh on Mac for the IP):
  [Environment]::SetEnvironmentVariable('BUSBUDDY_CONNECTION',
    'Host=<mac-ip>;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=${BUSBUDDY_PG_PASSWORD}', 'User')

  # Or drop keys\SYNCFUSION_LICENSE_KEY.txt and keys\bus-buddy-gee-key.json on the Mac side;
  # they sync via the UTM shared folder and utm_run_in_vm.ps1 picks them up per session.
"@ -ForegroundColor DarkGray

Write-Step "Next steps"

Write-Host @"
  1. Close this window and open PowerShell 7 (pwsh) or Windows Terminal.
  2. cd to your shared BusBuddy folder (often Z:\).
  3. Run:  pwsh -File .\utm_run_in_vm.ps1

  From Mac you can also run:  ./run-wpf.sh
"@ -ForegroundColor White

Write-Host "`nSetup complete." -ForegroundColor Green
