#Requires -Version 5.1
# One-shot Syncfusion license diagnosis for the Windows VM (no RegisterLicense here).
# Writes C:\dev\logs\busbuddy-license-verify.txt

$ErrorActionPreference = "Stop"
$outPath = "C:\dev\logs\busbuddy-license-verify.txt"
New-Item -ItemType Directory -Force -Path (Split-Path $outPath) | Out-Null

$lines = @(
    "TimestampUtc: $((Get-Date).ToUniversalTime().ToString('o'))",
    "SyncfusionNuGetPin: 34.2.3 (Directory.Build.props)"
)

$key = $null
$keySource = $null

# Prefer keys/.env (same as App.xaml.cs LoadKeysDotEnv)
$envCandidates = @(
    "C:\dev\BusBuddy-3\keys\.env",
    "C:\dev\busbuddy\keys\.env"
)
foreach ($envPath in $envCandidates) {
    if (-not (Test-Path -LiteralPath $envPath)) { continue }
    foreach ($raw in Get-Content -LiteralPath $envPath) {
        $line = $raw.Trim()
        if ($line -match '^(?i)SYNCFUSION_LICENSE_KEY\s*=\s*(.+)$') {
            $key = $Matches[1].Trim().Trim('"').Trim("'")
            $keySource = $envPath
            break
        }
    }
    if ($key) { break }
}

$candidates = @(
    "C:\dev\busbuddy\keys\SYNCFUSION_LICENSE_KEY.txt",
    "C:\dev\busbuddy\keys\SYNCFUSION_LICENSE_KEY",
    "C:\dev\BusBuddy-3\keys\SYNCFUSION_LICENSE_KEY.txt",
    "C:\dev\BusBuddy-3\keys\SYNCFUSION_LICENSE_KEY"
)

function Get-SyncfusionKeyFromLine {
    param([string]$Line)
    $line = $Line.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { return $null }
    if ($line -match '^(?i)(GOOGLE_MAPS|Google_Maps)') { return $null }
    if ($line -match '^(?i)(SYNCFUSION_LICENSE_KEY|New Key|.*LICENSE.*)\s*[:;=]\s*(.+)$') {
        return $Matches[2].Trim().TrimEnd('.', ';', ',')
    }
    if ($line -match '^Ngo9BigBOggj') {
        return $line.TrimEnd('.', ';', ',')
    }
    if ($line.Length -ge 20 -and $line -notmatch '\$\{') {
        return $line.TrimEnd('.', ';', ',')
    }
    return $null
}

foreach ($path in $candidates) {
    if (-not (Test-Path -LiteralPath $path)) { continue }
    foreach ($raw in Get-Content -LiteralPath $path) {
        $parsed = Get-SyncfusionKeyFromLine $raw
        if ($parsed -and $parsed.Length -ge 20) {
            $key = $parsed
            $keySource = $path
        }
    }
    if ($key) { break }
}

if (-not $key) {
    $envKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "User")
    if ($envKey -and $envKey.Length -ge 20) {
        $key = $envKey.Trim()
        $keySource = "User environment"
    }
}

if ($key) {
    $lines += "KeySource: $keySource"
    $lines += "KeyLength: $($key.Length)"
    $lines += "KeyPrefix: $($key.Substring(0, [Math]::Min(6, $key.Length)))"
    $lines += "KeySuffix: $($key.Substring([Math]::Max(0, $key.Length - 4)))"
} else {
    $lines += "KeySource: (none found)"
}

$appCs = @(
    "C:\dev\busbuddy\BusBuddy.WPF\App.xaml.cs",
    "C:\dev\BusBuddy-3\BusBuddy.WPF\App.xaml.cs"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($appCs) {
    $hasSingleRegister = (Select-String -Path $appCs -Pattern "RegisterSyncfusionLicenseOnce" -Quiet)
    $registerCount = (Select-String -Path $appCs -Pattern "RegisterLicense\s*\(" -AllMatches).Matches.Count
    $lines += "App.xaml.cs: $appCs"
    $lines += "HasRegisterSyncfusionLicenseOnce: $hasSingleRegister"
    $lines += "RegisterLicenseCallsInAppCs: $registerCount"
} else {
    $lines += "App.xaml.cs: (not found — sync repo to VM)"
}

$diagPath = "C:\dev\logs\busbuddy-license-startup.txt"
if (Test-Path $diagPath) {
    $lines += "--- busbuddy-license-startup.txt ---"
    $lines += Get-Content $diagPath
}

$lines += "---"
$lines += "If ValidateLicense(UIComponent) is false or popup persists:"
$lines += "  Generate a v34.x WPF key at https://www.syncfusion.com/account/downloads"
$lines += "  Rebuild after syncing Mac repo (RegisterSyncfusionLicenseOnce in App.xaml.cs)"

$lines | Set-Content -LiteralPath $outPath -Encoding UTF8
Write-Host "Wrote $outPath"
Get-Content $outPath
