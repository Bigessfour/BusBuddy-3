#Requires -Version 5.1
<#
.SYNOPSIS
  Score Route Assignments live-session logs after desktop / UIA smoke.

.EXIT
  0 when the assignment surface loaded, a clerk action is proven, and runtime-errors is empty.
  1 when required signals are missing or runtime-errors has content.
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
    (Join-Path $Root "BusBuddy.WPF\bin\Release\logs"),
    (Join-Path $Root "BusBuddy.WPF\bin\Release\net9.0-windows\logs"),
    (Join-Path $Root "logs"),
    (Join-Path $Root "Logs")
) | Select-Object -Unique

$files = @()
foreach ($dir in $searchDirs) {
    if (Test-Path $dir) {
        $files += Get-ChildItem -Path $dir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'runtime-errors|ui-diagnostics|application|ui-interactions|log-' }
    }
}

$uiaLogs = Get-ChildItem -Path $env:TEMP -Filter "busbuddy-uia-route*.log" -ErrorAction SilentlyContinue
if ($uiaLogs) {
    $files += $uiaLogs
}

$files = $files | Sort-Object FullName -Unique
if (-not $ExtractPath) {
    $extractDir = Join-Path $Root "logs"
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    $ExtractPath = Join-Path $extractDir "route-assignment-smoke-extract.txt"
}

$sinceDt = $null
if ($Since) {
    $sinceDt = [datetime]::Parse($Since)
}

function Line-InScope([string]$line) {
    if (-not $sinceDt) { return $true }
    if ($line -match '^\d{4}-\d{2}-\d{2}[ T](\d{2}:\d{2}:\d{2})') {
        try {
            $ts = [datetime]::Parse($Matches[0].Replace('T', ' '))
            return $ts -ge $sinceDt
        } catch {
            return $true
        }
    }
    return $true
}

$patterns = @(
    'RouteAssign ButtonAdv',
    'RouteAssign Click',
    'RouteAssignmentView',
    'RouteAssignmentView visible',
    'Data refreshed successfully',
    'Refresh Route Data',
    'View Schedule',
    'Plot Route on Map',
    'UI surface Loaded View=RouteScheduleWindow',
    'UI proof Click='
)

$hits = @{}
foreach ($p in $patterns) { $hits[$p] = 0 }

$runtimeErrors = @()
$allLines = @()

foreach ($f in $files) {
    foreach ($line in Get-Content -LiteralPath $f.FullName -ErrorAction SilentlyContinue) {
        if (-not (Line-InScope $line)) { continue }
        $allLines += $line
        if ($f.Name -match 'runtime-errors' -and $line.Trim().Length -gt 0) {
            $runtimeErrors += $line
        }
        foreach ($p in $patterns) {
            if ($line -match [regex]::Escape($p) -or $line -match $p) { $hits[$p]++ }
        }
    }
}

$surfaceOk = $hits['RouteAssignmentView'] -gt 0
$actionOk = @(
    'RouteAssign ButtonAdv',
    'Data refreshed successfully',
    'UI surface Loaded View=RouteScheduleWindow',
    'RouteAssignmentView visible'
) | Where-Object { $hits[$_] -gt 0 }

$missing = @()
if (-not $surfaceOk) { $missing += 'RouteAssignmentView' }
if ($actionOk.Count -eq 0) { $missing += 'clerk-action (ButtonAdv / refresh / schedule / visible size)' }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("Route assignment log score")
[void]$sb.AppendLine("  files: $($files.Count)")
[void]$sb.AppendLine("  since: $(if ($Since) { $Since } else { '(all)' })")
[void]$sb.AppendLine("  extract: $ExtractPath")
[void]$sb.AppendLine("")
foreach ($p in $patterns) {
    [void]$sb.AppendLine(("  {0,-44} {1}" -f $p, $hits[$p]))
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("runtime-errors lines: $($runtimeErrors.Count)")
if ($missing.Count -gt 0) {
    [void]$sb.AppendLine("MISSING required: $($missing -join ', ')")
}

$tail = $allLines | Select-Object -Last 80
[void]$sb.AppendLine("")
[void]$sb.AppendLine("--- tail ---")
foreach ($t in $tail) { [void]$sb.AppendLine($t) }

$text = $sb.ToString()
Set-Content -Path $ExtractPath -Value $text -Encoding utf8
Write-Host $text

if ($runtimeErrors.Count -gt 0 -or $missing.Count -gt 0) {
    exit 1
}
exit 0
