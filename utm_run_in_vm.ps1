#Requires -Version 7.4

# PowerShell 7.4+ script to run inside the UTM Windows VM (or Parallels / other).
# Requires PowerShell 7 — NOT Windows PowerShell 5.1 (powershell.exe).
#
# Usage from inside VM:
#   pwsh -File Z:\utm_run_in_vm.ps1
#   # or from Z:\ after installing PS 7:
#   .\utm_run_in_vm.ps1
#
# Install PowerShell 7.4+ (Admin):
#   winget install --id Microsoft.PowerShell --source winget --architecture arm64 `
#     --accept-package-agreements --accept-source-agreements
#
# From Mac host (recommended):
#   ./run-wpf.sh
#
# Shared folder: UTM WebDAV share is usually Z:\ (\\localhost@9843\DavWWWRoot).
# Edit $manualOverride below if auto-detect ever needs a hint.

$manualOverride = $null  # e.g. "Z:\" — only set if auto-find fails
$localBuildRoot = "C:\dev\BusBuddy-3"  # WPF cannot build on UTM WebDAV (Z:\) — sync here first

Write-Host @"

BusBuddy VM launcher (Windows — NOT macOS)
  Mac paths like /Users/... do NOT work here.
  Docker runs on the Mac host, not in this VM.
  Do NOT run 'dotnet build' from C:\Windows\System32.

"@ -ForegroundColor DarkGray

# Fast path: UTM WebDAV share (most common)
if (-not $manualOverride -and (Test-Path -LiteralPath 'Z:\BusBuddy.sln')) {
    $manualOverride = 'Z:\'
    Write-Host "Found BusBuddy.sln on Z:\ (UTM share)." -ForegroundColor Green
}
elseif (-not $manualOverride -and (Test-Path -LiteralPath 'C:\dev\BusBuddy-3\BusBuddy.sln')) {
    $manualOverride = 'C:\dev\BusBuddy-3'
    Write-Host "Found BusBuddy.sln at C:\dev\BusBuddy-3 (local copy)." -ForegroundColor Green
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet not found. Install .NET 9 SDK: winget install --id Microsoft.DotNet.SDK.9 --source winget"
    exit 1
}

function Test-IsWebDavOrNetworkPath {
    param([string]$Path)
    if ($Path -match '^\\\\') { return $true }
    $drive = $Path.Substring(0, 1)
    $psDrive = Get-PSDrive -Name $drive -PSProvider FileSystem -ErrorAction SilentlyContinue
    if ($psDrive?.DisplayRoot -match 'localhost@|DavWWWRoot|\\\\') { return $true }
    if ($Path -match '^Z:\\') { return $true }
    return $false
}

function Sync-BusBuddyToLocal {
    param(
        [string]$Source,
        [string]$Destination
    )
    Write-Host "UTM WebDAV/network path detected — WPF must build on local disk." -ForegroundColor Yellow
    Write-Host "Syncing $Source -> $Destination (source-only; excludes large/non-WPF folders)..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    # robocopy: exit 0-7 = success; 8+ = failure
    # Exclude rag/chroma_db — WebDAV cannot copy files >~50MB (chroma.sqlite3 is ~53MB).
    # WPF build/run does not need RAG index, git history, or test artifacts.
    & robocopy $Source $Destination /MIR `
        /XD bin obj .git node_modules rag TestResults "Documentation\Archive" `
        /XF *.user `
        /NFL /NDL /NJH /NJS /nc /ns /np
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }
    Write-Host "Local sync complete." -ForegroundColor Green
}

function Find-BusBuddyRoot {
    param([string]$Override)
    if ($Override -and (Test-Path (Join-Path $Override "BusBuddy.sln"))) { return $Override }

    $slnName = "BusBuddy.sln"
    $candidates = [System.Collections.Generic.List[object]]::new()

    $fsDrives = Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Root -and (
                $_.Used -or
                $_.DisplayRoot -match 'Mac|shared|utm|host|9p|virtio|smb|localhost|spice' -or
                $_.Name -eq 'Z' -or
                $_.Root -match 'localhost@'
            )
        } |
        Select-Object -ExpandProperty Root -Unique
    $searchRoots = @($fsDrives)

    $searchRoots += @("Z:\", "Y:\", "X:\", "W:\", "E:\", "D:\", "C:\")
    $searchRoots += @(
        "Z:\Shared with Windows", "D:\Shared with Windows", "E:\Shared with Windows",
        "Z:\BusBuddy", "D:\BusBuddy", "Z:\BusBuddy-3", "D:\BusBuddy-3",
        "C:\shared", "C:\Shared with Windows"
    )
    $searchRoots += (Get-Location).Path
    $parent = Split-Path (Get-Location).Path -Parent
    if ($parent) { $searchRoots += $parent }

    foreach ($root in ($searchRoots | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $root -ErrorAction SilentlyContinue)) { continue }
        try {
            $matches = Get-ChildItem -LiteralPath $root -Filter $slnName -Recurse -Depth 6 -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.FullName -notlike '*\.git\*' -and
                    $_.FullName -notlike '*\Archive\*' -and
                    $_.FullName -notlike '*\bin\*' -and
                    $_.FullName -notlike '*\obj\*' -and
                    $_.FullName -notlike '*\node_modules\*'
                } |
                Select-Object -First 3
            foreach ($m in $matches) {
                $dir = $m.DirectoryName
                $score = 0
                if (Test-Path (Join-Path $dir "BusBuddy.WPF")) { $score += 12 }
                if (Test-Path (Join-Path $dir "BusBuddy.sln")) { $score += 5 }
                if (Test-Path (Join-Path $dir ".git")) { $score += 4 }
                if ($dir -like '*BusBuddy*') { $score += 3 }
                if ($dir -like '*Shared*') { $score += 2 }
                if ($dir -match 'BusBuddy-3$') { $score += 2 }
                $candidates.Add([pscustomobject]@{ Path = $dir; Score = $score })
            }
        }
        catch { }
    }

    if ($candidates.Count -gt 0) {
        $best = $candidates | Sort-Object -Property Score -Descending | Select-Object -First 1
        return $best.Path
    }
    return $null
}

$projectRoot = Find-BusBuddyRoot -Override $manualOverride
if (-not $projectRoot) {
    Write-Warning "Auto-discovery did not find BusBuddy.sln under common UTM shares."
    Write-Host "Current PSDrives:"
    Get-PSDrive -PSProvider FileSystem | Format-Table Name, Root, DisplayRoot, Used -AutoSize

    Write-Host ""
    Write-Host "Exploring Z:\ (UTM WebDAV / DavWWWRoot):"
    if (Test-Path Z:\) {
        Write-Host "  Contents of Z:\ (first 10 items):"
        Get-ChildItem Z:\ -ErrorAction SilentlyContinue | Select-Object -First 10 | ForEach-Object {
            Write-Host "    $($_.FullName)  [$($_.PSIsContainer ? 'DIR' : 'FILE')]"
        }
        Write-Host ""
        Write-Host "  Recursive search for BusBuddy.sln under Z:\ (depth 5):"
        Get-ChildItem Z:\ -Filter BusBuddy.sln -Recurse -Depth 5 -ErrorAction SilentlyContinue |
            Select-Object -First 5 FullName |
            ForEach-Object { Write-Host "    $_" }
    }
    else {
        Write-Host "  Z:\ is not accessible right now."
    }

    Write-Host ""
    Write-Host "Quick listing of other likely roots:"
    foreach ($r in 'Z:\', 'D:\', 'E:\', 'C:\', 'Z:\Shared with Windows', 'D:\Shared with Windows') {
        if (Test-Path $r) {
            Get-ChildItem $r -Directory -ErrorAction SilentlyContinue | Select-Object -First 8 |
                ForEach-Object { Write-Host "  $r$($_.Name)" }
        }
    }

    $projectRoot = "Z:\"
    Write-Host ""
    Write-Host "Falling back to $projectRoot (edit `$manualOverride if wrong)."
}

Write-Host "Using project root: $projectRoot" -ForegroundColor Green

$sharedRoot = $projectRoot
if (Test-IsWebDavOrNetworkPath -Path $projectRoot) {
    Sync-BusBuddyToLocal -Source $sharedRoot -Destination $localBuildRoot
    $projectRoot = $localBuildRoot
    Write-Host "Building from local copy: $projectRoot" -ForegroundColor Green
}

Set-Location -LiteralPath $projectRoot

# Keys live on the Mac share — read from sharedRoot, run from local projectRoot
$keyPath = Join-Path $sharedRoot "keys\bus-buddy-gee-key.json"
if (Test-Path -LiteralPath $keyPath) {
    $env:GOOGLE_APPLICATION_CREDENTIALS = $keyPath
    $env:GEE_PROJECT_ID = "ee-bigessfour"
    $env:GEE_SERVICE_ACCOUNT_EMAIL = "bus-buddy-gee@ee-bigessfour.iam.gserviceaccount.com"
    Write-Host "GEE creds configured from shared key: $keyPath" -ForegroundColor Cyan
}
else {
    Write-Host "GEE: no shared key at $keyPath (GEE will use env or placeholders)." -ForegroundColor Yellow
}

$licFile = Join-Path $sharedRoot "keys\SYNCFUSION_LICENSE_KEY.txt"
if (Test-Path -LiteralPath $licFile) {
    $lic = (Get-Content -LiteralPath $licFile -Raw).Trim()
    if ($lic -and $lic.Length -gt 10) {
        $env:SYNCFUSION_LICENSE_KEY = $lic
        Write-Host "SYNCFUSION_LICENSE_KEY loaded from shared keys file for this session." -ForegroundColor Cyan
    }
}
else {
    if (-not $env:SYNCFUSION_LICENSE_KEY) {
        Write-Host "Syncfusion: No SYNCFUSION_LICENSE_KEY in env and no keys/SYNCFUSION_LICENSE_KEY.txt found." -ForegroundColor Yellow
        Write-Host "  Set User env var or drop the key in keys/SYNCFUSION_LICENSE_KEY.txt on the Mac side." -ForegroundColor Yellow
    }
}

$hostIpHint = "run ./run-wpf.sh on Mac for host IP (or: ipconfig getifaddr en0)"
Write-Host "Postgres/Docker hint: from VM use Mac host IP — $hostIpHint" -ForegroundColor DarkGray

Write-Host "`nBuilding BusBuddy (WPF) with EnableWindowsTargeting..." -ForegroundColor Cyan
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true --verbosity minimal
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed"; exit 1 }

dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj -c Debug -p:EnableWindowsTargeting=true --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

Write-Host "`nLaunching BusBuddy WPF (the GUI will appear in this VM desktop)..." -ForegroundColor Green
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "BusBuddy.WPF/BusBuddy.WPF.csproj" -WorkingDirectory $projectRoot -WindowStyle Normal

Write-Host "WPF launch requested. Switch to the app window in the VM (Dashboard, Reports, Map, etc.)." -ForegroundColor Green
