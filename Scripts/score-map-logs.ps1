# Requires -Version 5.1
<#
.SYNOPSIS
  Score District Map live-session logs (Cursor harvest after desktop launch).

.DESCRIPTION
  Exit 1 if ancestor / CustomDataSymbol / unhandled TransformToVisual / createSession-403
  quota retry appear, or if MapsOptionsBound QuotaSource=none / WithSource>0 are missing.

  Default search roots:
    <Root>\BusBuddy.WPF\bin\Debug\logs                  (Serilog + ui-diagnostics)
    <Root>\BusBuddy.WPF\bin\Debug\net9.0-windows\logs   (runtime-errors.log)
    <Root>\logs
    cwd\logs

  Use -Since to ignore earlier same-day noise (e.g. '2026-09-17 12:00:00').
#>
[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$ExtractPath = "",
    [string]$Since = ""
)

$ErrorActionPreference = "Stop"

if (-not $Root) {
    $here = Split-Path -Parent $PSScriptRoot
    if (Test-Path (Join-Path $here "BusBuddy.WPF")) {
        $Root = $here
    } else {
        $Root = (Get-Location).Path
    }
}

$searchDirs = @(
    (Join-Path $Root "BusBuddy.WPF\bin\Debug\logs"),
    (Join-Path $Root "BusBuddy.WPF\bin\Debug\net9.0-windows\logs"),
    (Join-Path $Root "logs"),
    (Join-Path (Get-Location) "logs")
) | Select-Object -Unique

$files = @()
foreach ($dir in $searchDirs) {
    if (Test-Path $dir) {
        $files += Get-ChildItem -Path $dir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'runtime-errors|ui-diagnostics|map-interactions|application|\.log$' }
    }
}

# Also the repo-root runtime-errors.log if someone launched with a different cwd
$loose = @(
    (Join-Path $Root "runtime-errors.log"),
    (Join-Path $Root "BusBuddy.WPF\bin\Debug\net9.0-windows\runtime-errors.log")
)
foreach ($p in $loose) {
    if (Test-Path $p) { $files += Get-Item $p }
}

$files = $files | Sort-Object FullName -Unique
if (-not $ExtractPath) {
    $extractDir = Join-Path $Root "logs"
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    $ExtractPath = Join-Path $extractDir "map-smoke-extract.txt"
}

$patterns = @(
    'do not share a common ancestor',
    'CustomDataSymbol',
    'TransformToVisual',
    'WithSource',
    'MapsOptionsBound',
    'QuotaSource',
    'createSession',
    'Show Schools',
    'Plot Pickup',
    'Export Route',
    'ApplyClerkOverride',
    'Layout transient',
    'serviceUsageConsumer'
)

$hits = @()
foreach ($f in $files) {
    try {
        $found = Select-String -Path $f.FullName -Pattern $patterns -ErrorAction SilentlyContinue
        if ($found) { $hits += $found }
    } catch {
        # skip unreadable
    }
}

$sinceTime = $null
if ($Since) {
    $sinceTime = [datetime]::Parse($Since)
    $hits = @($hits | Where-Object {
            if ($_.Line -match '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})') {
                [datetime]$Matches[1] -ge $sinceTime
            } else {
                $false
            }
        })
}

$hits | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line)" } |
Out-File -FilePath $ExtractPath -Encoding utf8

function Count-Pattern([string]$re) {
    @($hits | Where-Object { $_.Line -match $re }).Count
}

$ancestor = Count-Pattern 'do not share a common ancestor'
$custom = @($hits | Where-Object {
        $_.Line -match 'CustomDataSymbol' -and (
            $_.Line -match 'Binding|Warning|Error|path error|Cannot find'
        )
    }).Count
$transformUnhandled = @($hits | Where-Object {
        $_.Line -match 'TransformToVisual' -and $_.Line -notmatch 'Layout transient|swallowed'
    }).Count
$mapsBound = Count-Pattern 'MapsOptionsBound'
$quotaNone = Count-Pattern 'QuotaSource=none|QuotaSource":"none'
$withSourcePositive = @($hits | Where-Object {
        $_.Line -match 'WithSource=(\d+)' -and [int]$Matches[1] -gt 0
    }).Count
if ($withSourcePositive -eq 0) {
    $withSourcePositive = @($hits | Where-Object { $_.Line -match 'WithSource' }).Count
}
$create403 = @($hits | Where-Object {
        $_.Line -match 'createSession' -and $_.Line -match '403'
    }).Count
$quotaRetry = Count-Pattern 'serviceUsageConsumer|QuotaProject is set'

$fail = @()
if ($ancestor -gt 0) { $fail += "ancestor exceptions: $ancestor" }
if ($custom -gt 0) { $fail += "CustomDataSymbol binding warnings: $custom" }
if ($transformUnhandled -gt 0) { $fail += "unhandled TransformToVisual: $transformUnhandled" }
if ($mapsBound -eq 0) { $fail += "MapsOptionsBound missing" }
if ($quotaNone -eq 0 -and $mapsBound -gt 0) { $fail += "QuotaSource=none missing on MapsOptionsBound" }
if ($quotaNone -eq 0 -and $mapsBound -eq 0) { $fail += "QuotaSource=none missing" }
if ($withSourcePositive -eq 0) { $fail += "WithSource >> 0 missing" }
if ($create403 -gt 0 -and $quotaRetry -gt 0) { $fail += "createSession 403 + quota retry present" }

Write-Host "Map log score"
Write-Host "  files: $($files.Count)"
Write-Host "  since: $(if ($Since) { $Since } else { '(all)' })"
Write-Host "  extract: $ExtractPath"
Write-Host "  ancestor=$ancestor customBind=$custom transformUnhandled=$transformUnhandled"
Write-Host "  MapsOptionsBound=$mapsBound QuotaSource=none hits=$quotaNone WithSource>0=$withSourcePositive"
Write-Host "  createSession403=$create403 quotaRetry=$quotaRetry"

if ($fail.Count -gt 0) {
    Write-Host "FAIL:" -ForegroundColor Red
    $fail | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host "PASS" -ForegroundColor Green
exit 0
