#Requires -Version 5.1
# BusBuddy WPF launcher for UTM / Parallels Windows VM.
# Works in Windows PowerShell 5.1 (powershell.exe) AND PowerShell 7 (pwsh).
#
# In the VM Terminal / PowerShell:
#   cd Z:\
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\utm_run_in_vm.ps1
#
# Or double-click: utm_run_in_vm.cmd
#
# From Mac host (preflight only): ./run-wpf.sh
#
# Hot Reload (faster UI iteration — no full restart for many C# edits):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\utm_run_in_vm.ps1 -Watch
#   (or double-click utm_watch_in_vm.cmd)

param(
    [switch]$Watch
)

$ErrorActionPreference = "Stop"

$manualOverride = $null  # e.g. "Z:\" if auto-find fails
$localBuildRoot = "C:\dev\BusBuddy-3"
$alternateLocalRoots = @("C:\dev\busbuddy", "C:\dev\BusBuddy")

Write-Host ""
Write-Host "BusBuddy VM launcher (Windows — NOT macOS)" -ForegroundColor Cyan
Write-Host "  Mac paths like /Users/... do NOT work here." -ForegroundColor DarkGray
Write-Host "  Run this inside the UTM Windows desktop PowerShell." -ForegroundColor DarkGray
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: dotnet not found." -ForegroundColor Red
    Write-Host "Install .NET 9 SDK in the VM:" -ForegroundColor Yellow
    Write-Host "  winget install --id Microsoft.DotNet.SDK.9 --source winget"
    exit 1
}

function Test-IsWebDavOrNetworkPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ($Path -match '^\\\\') { return $true }
    if ($Path -match '^[Zz]:\\') { return $true }
    try {
        $driveLetter = $Path.Substring(0, 1)
        $psDrive = Get-PSDrive -Name $driveLetter -PSProvider FileSystem -ErrorAction SilentlyContinue
        if ($null -ne $psDrive -and $null -ne $psDrive.DisplayRoot) {
            if ($psDrive.DisplayRoot -match 'localhost@|DavWWWRoot|\\\\') { return $true }
        }
    } catch { }
    return $false
}

function Sync-BusBuddyToLocal {
    param(
        [string]$Source,
        [string]$Destination
    )
    Write-Host "WebDAV/network path detected — syncing to local disk for WPF build..." -ForegroundColor Yellow
    Write-Host "  $Source -> $Destination" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    & robocopy $Source $Destination /MIR `
        /XD bin obj .git node_modules rag TestResults "Documentation\Archive" `
        /XF *.user `
        /NFL /NDL /NJH /NJS /nc /ns /np
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }
    Write-Host "Local sync complete." -ForegroundColor Green
}

function Get-MacHostIpForPostgres {
    param([string]$ProjectRoot)

    $ipFile = Join-Path $ProjectRoot "keys\mac-host-ip.txt"
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

    return $null
}

function Set-BusBuddyPostgresConnection {
    param([string]$HostIp)

    if ([string]::IsNullOrWhiteSpace($HostIp)) {
        Write-Host "WARNING: Mac host IP unknown — set keys\mac-host-ip.txt from the Mac (run ./run-wpf.sh) or BUSBUDDY_CONNECTION manually." -ForegroundColor Yellow
        return
    }

    $conn = "Host=$HostIp;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev;Include Error Detail=true"
    $env:BUSBUDDY_CONNECTION = $conn
    $env:DatabaseProvider = 'Postgres'

    $current = [Environment]::GetEnvironmentVariable('BUSBUDDY_CONNECTION', 'User')
    if ($current -ne $conn) {
        [Environment]::SetEnvironmentVariable('BUSBUDDY_CONNECTION', $conn, 'User')
        Write-Host "Updated BUSBUDDY_CONNECTION -> Host=$HostIp" -ForegroundColor Cyan
    } else {
        Write-Host "BUSBUDDY_CONNECTION -> Host=$HostIp" -ForegroundColor Cyan
    }
}

function Invoke-MacEnsurePostgresDocker {
    param([string]$ProjectRoot)

    if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
        return
    }

    $remoteConfig = Join-Path $ProjectRoot "keys\mac-docker-remote.txt"
    if (-not (Test-Path -LiteralPath $remoteConfig)) {
        return
    }

    $remoteLine = (Get-Content -LiteralPath $remoteConfig -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($remoteLine) -or $remoteLine.StartsWith('#')) {
        return
    }

    $sshTarget = $null
    $repoPath = $null
    if ($remoteLine -match '^(?<ssh>[^:]+):(?<repo>.+)$') {
        $sshTarget = $Matches['ssh'].Trim()
        $repoPath = $Matches['repo'].Trim()
    } else {
        $sshTarget = $remoteLine
    }

    Write-Host "Requesting Postgres start on Mac via SSH ($sshTarget)..." -ForegroundColor Cyan
    if ($repoPath) {
        $remoteCmd = "cd '$repoPath' && ./Scripts/ensure-postgres-docker.sh"
    } else {
        $remoteCmd = "./Scripts/ensure-postgres-docker.sh"
    }

    & ssh -o BatchMode=yes -o ConnectTimeout=10 -o StrictHostKeyChecking=accept-new $sshTarget $remoteCmd 2>$null
}

function Ensure-MacPostgresReady {
    param(
        [string]$MacHostIp,
        [string]$ProjectRoot
    )

    if ([string]::IsNullOrWhiteSpace($MacHostIp)) {
        Write-Host "WARNING: Cannot verify Postgres without Mac host IP." -ForegroundColor Yellow
        return
    }

    Invoke-MacEnsurePostgresDocker -ProjectRoot $ProjectRoot

    Write-Host "Checking Postgres at ${MacHostIp}:5432..." -ForegroundColor Cyan
    $maxAttempts = 30
    for ($i = 1; $i -le $maxAttempts; $i++) {
        $probe = Test-NetConnection -ComputerName $MacHostIp -Port 5432 -WarningAction SilentlyContinue
        if ($probe.TcpTestSucceeded) {
            Write-Host "Postgres is reachable at ${MacHostIp}:5432." -ForegroundColor Green
            return
        }

        if ($i -eq 1) {
            Write-Host "Postgres not ready yet — waiting for Mac Docker (run ./run-wpf.sh on the Mac if this persists)..." -ForegroundColor Yellow
        }
        Start-Sleep -Seconds 2
    }

    throw "Postgres is not available at ${MacHostIp}:5432. On the Mac host run: ./Scripts/ensure-postgres-docker.sh (or ./run-wpf.sh)."
}

function Find-BusBuddyRoot {
    param([string]$Override)

    if ($Override -and (Test-Path (Join-Path $Override "BusBuddy.sln"))) {
        return (Resolve-Path (Join-Path $Override ".")).Path
    }

    $quick = @(
        "Z:\",
        "C:\dev\BusBuddy-3",
        "C:\dev\busbuddy",
        "C:\dev\BusBuddy",
        "Z:\Shared with Windows",
        "Z:\BusBuddy-3",
        "D:\Shared with Windows",
        "D:\BusBuddy-3"
    )
    foreach ($q in $quick) {
        $sln = Join-Path $q "BusBuddy.sln"
        if (Test-Path -LiteralPath $sln) {
            Write-Host "Found BusBuddy.sln at $q" -ForegroundColor Green
            return (Resolve-Path $q).Path
        }
    }

    $candidates = @()
    $searchRoots = @("Z:\", "Y:\", "X:\", "W:\", "E:\", "D:\", "C:\")
    try {
        Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Root) { $searchRoots += $_.Root }
        }
    } catch { }
    $searchRoots += (Get-Location).Path

    foreach ($root in ($searchRoots | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $root -ErrorAction SilentlyContinue)) { continue }
        try {
            $matches = Get-ChildItem -LiteralPath $root -Filter "BusBuddy.sln" -Recurse -Depth 5 -ErrorAction SilentlyContinue |
            Where-Object {
                $_.FullName -notlike '*\.git\*' -and
                $_.FullName -notlike '*\bin\*' -and
                $_.FullName -notlike '*\obj\*'
            } |
            Select-Object -First 3
            foreach ($m in $matches) {
                $dir = $m.DirectoryName
                $score = 0
                if (Test-Path (Join-Path $dir "BusBuddy.WPF")) { $score += 12 }
                if ($dir -like '*BusBuddy*') { $score += 3 }
                $candidates += [pscustomobject]@{ Path = $dir; Score = $score }
            }
        } catch { }
    }

    if ($candidates.Count -gt 0) {
        $best = $candidates | Sort-Object Score -Descending | Select-Object -First 1
        Write-Host "Found BusBuddy.sln at $($best.Path)" -ForegroundColor Green
        return $best.Path
    }
    return $null
}

try {
    $projectRoot = Find-BusBuddyRoot -Override $manualOverride
    if (-not $projectRoot) {
        Write-Host "ERROR: Could not find BusBuddy.sln." -ForegroundColor Red
        Write-Host "In Explorer, open the UTM shared folder, then in PowerShell:" -ForegroundColor Yellow
        Write-Host "  cd <that-folder>"
        Write-Host "  dir BusBuddy.sln"
        Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\utm_run_in_vm.ps1"
        Write-Host ""
        Write-Host "PSDrives:" -ForegroundColor Yellow
        Get-PSDrive -PSProvider FileSystem | Format-Table Name, Root -AutoSize
        if (Test-Path Z:\) {
            Write-Host "Z:\ top-level:" -ForegroundColor Yellow
            Get-ChildItem Z:\ -ErrorAction SilentlyContinue | Select-Object -First 15 Name
        } else {
            Write-Host "Z:\ is not mounted. In UTM, enable directory sharing for BusBuddy-3." -ForegroundColor Red
        }
        exit 1
    }

    $sharedRoot = $projectRoot
    $zDriveRoot = $null
    if (Test-Path -LiteralPath "Z:\BusBuddy.sln") {
        $zDriveRoot = (Resolve-Path "Z:\").Path
    }

    if (Test-IsWebDavOrNetworkPath -Path $projectRoot) {
        Sync-BusBuddyToLocal -Source $sharedRoot -Destination $localBuildRoot
        $projectRoot = $localBuildRoot
    } elseif ($zDriveRoot -and $projectRoot -ne $localBuildRoot) {
        $localSln = Join-Path $localBuildRoot "BusBuddy.sln"
        $shouldSync = -not (Test-Path -LiteralPath $localSln)
        if (-not $shouldSync) {
            $zTime = (Get-Item -LiteralPath (Join-Path $zDriveRoot "BusBuddy.sln")).LastWriteTimeUtc
            $localTime = (Get-Item -LiteralPath $localSln).LastWriteTimeUtc
            $shouldSync = $zTime -gt $localTime
        }
        if ($shouldSync) {
            Write-Host "Shared folder is newer than C:\dev\BusBuddy-3 — syncing before build..." -ForegroundColor Yellow
            Sync-BusBuddyToLocal -Source $zDriveRoot -Destination $localBuildRoot
        }
        $projectRoot = $localBuildRoot
    }

    Set-Location -LiteralPath $projectRoot
    Write-Host "Building from: $projectRoot" -ForegroundColor Green

    $stampPath = Join-Path $projectRoot "BUILD-STAMP.txt"
    if (Test-Path -LiteralPath $stampPath) {
        $stamp = (Get-Content -LiteralPath $stampPath -Raw).Trim()
        Write-Host "Build stamp: $stamp" -ForegroundColor Magenta
        Write-Host "  Student form title should show: Add New Student · UX v3 2026-09-02" -ForegroundColor Magenta
        Write-Host "  If you still see an 'Action blocked' popup, you are NOT running this build." -ForegroundColor Magenta
    }

    $macHostIp = Get-MacHostIpForPostgres -ProjectRoot $sharedRoot
    if (-not $macHostIp) {
        $macHostIp = Get-MacHostIpForPostgres -ProjectRoot $projectRoot
    }
    Ensure-MacPostgresReady -MacHostIp $macHostIp -ProjectRoot $sharedRoot
    Set-BusBuddyPostgresConnection -HostIp $macHostIp

    $keyPath = Join-Path $sharedRoot "keys\bus-buddy-gee-key.json"
    if (Test-Path -LiteralPath $keyPath) {
        $env:GOOGLE_APPLICATION_CREDENTIALS = $keyPath
        Write-Host "GEE key loaded from share." -ForegroundColor Cyan
    }

    $envFile = Join-Path $sharedRoot "keys\.env"
    if (Test-Path -LiteralPath $envFile) {
        foreach ($raw in Get-Content -LiteralPath $envFile) {
            $line = $raw.Trim()
            if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
            $eq = $line.IndexOf('=')
            if ($eq -le 0) { continue }
            $name = $line.Substring(0, $eq).Trim()
            $value = $line.Substring($eq + 1).Trim().Trim('"').Trim("'")
            if ($name.Length -gt 0) {
                Set-Item -Path "env:$name" -Value $value
            }
        }
        Write-Host "Loaded keys/.env from share." -ForegroundColor Cyan
    } else {
        Write-Host "WARNING: keys\.env not found — create from Documentation/keys-dotenv.example" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Restoring..." -ForegroundColor Cyan
    & dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)" }

    Write-Host "Building WPF..." -ForegroundColor Cyan
    & dotnet build BusBuddy.WPF\BusBuddy.WPF.csproj -c Debug -p:EnableWindowsTargeting=true --no-restore --no-incremental
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

    if ($Watch) {
        Write-Host ""
        Write-Host "Starting BusBuddy with Hot Reload (dotnet watch)..." -ForegroundColor Green
        Write-Host "  Edit C# / XAML on Mac, sync to C:\dev\BusBuddy-3, and save — supported changes apply without a full restart." -ForegroundColor DarkGray
        Write-Host "  Ctrl+C to stop. Ctrl+R in this window forces a rebuild/restart." -ForegroundColor DarkGray
        # Polling helps when the tree is on a UTM share (Z:) instead of C:\dev.
        $env:DOTNET_USE_POLLING_FILE_WATCHER = '1'
        $env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = '1'
        & dotnet watch run --project BusBuddy.WPF\BusBuddy.WPF.csproj -c Debug -p:EnableWindowsTargeting=true --non-interactive
        if ($LASTEXITCODE -ne 0) { throw "dotnet watch failed ($LASTEXITCODE)" }
        return
    }

    Write-Host ""
    Write-Host "Launching BusBuddy WPF..." -ForegroundColor Green
    $exe = Join-Path $projectRoot "BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Built executable not found: $exe"
    }
    $launch = Start-Process -FilePath $exe `
        -WorkingDirectory (Split-Path -Parent $exe) `
        -WindowStyle Normal `
        -PassThru
    Start-Sleep -Seconds 3
    if ($null -eq $launch -or $launch.HasExited) {
        $log = Join-Path (Split-Path -Parent $exe) "logs\runtime-errors.log"
        $hint = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 5 -ErrorAction SilentlyContinue } else { @() }
        throw "BusBuddy exited during startup. Check logs under $(Split-Path -Parent $exe)\logs and Logs. $hint"
    }
    Write-Host "BusBuddy running (PID $($launch.Id)) — check the VM desktop for the main window." -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
