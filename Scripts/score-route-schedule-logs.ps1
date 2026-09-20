#Requires -Version 5.1
<#
.SYNOPSIS
  Score AM Special Needs Assignment Schedule live logs.

.EXIT
  0 when RouteScheduleWindow opened with 14 clocks in order and PdfGrid preview (no print verb).
  1 otherwise.
#>
[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$ExtractPath = "",
    [string]$Since = "",
    [int]$ExpectedStops = 14,
    [string]$ExpectedDisplayName = "AM Special Needs Bus 5"
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

$uiaLogs = @()
$uiaLogs += Get-ChildItem -Path $env:TEMP -Filter "busbuddy-uia-schedule-*.log" -ErrorAction SilentlyContinue
$uiaLogs += Get-ChildItem -Path $env:TEMP -Filter "busbuddy-uia-route-*.log" -ErrorAction SilentlyContinue
if ($uiaLogs) { $files += $uiaLogs }

$files = $files | Sort-Object FullName -Unique
if (-not $ExtractPath) {
    $extractDir = Join-Path $Root "logs"
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    $ExtractPath = Join-Path $extractDir "route-schedule-smoke-extract.txt"
}

$sinceDt = $null
if ($Since) {
    $sinceDt = [datetime]::Parse($Since)
}

function Line-InScope([string]$line) {
    if (-not $sinceDt) { return $true }
    if ($line -match '(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2})') {
        try {
            $ts = [datetime]::Parse($Matches[1].Replace('T', ' '))
            return $ts -ge $sinceDt
        } catch {
            return $true
        }
    }
    # UIA harvest lines are "HH:mm:ss message" from this guest run.
    if ($line -match '^\d{2}:\d{2}:\d{2} ') { return $true }
    return $true
}

$openLine = $null
$clocks = @()
$previewLine = $null
$renderLine = $null
$scheduleLoaded = $false
$scheduleBindingOk = $false
$previewLoaded = $false
$pdfViewer = $false
$driverCalendar = $false
$printVerb = $false
$bindingErrors = $false
$runtimeErrors = @()
$allLines = @()

foreach ($f in $files) {
    foreach ($line in Get-Content -LiteralPath $f.FullName -ErrorAction SilentlyContinue) {
        if (-not (Line-InScope $line)) { continue }
        $allLines += $line
        if ($f.Name -match 'runtime-errors' -and $line.Trim().Length -gt 0) {
            $runtimeErrors += $line
        }
        if ($line -match 'Opened route schedule') { $openLine = $line }
        if ($line -match 'Route schedule preview' -and $line -match 'Grid=PdfGrid') { $previewLine = $line }
        if ($line -match 'Rendered PdfGrid route sheet') { $renderLine = $line }
        if ($line -match 'UI surface Loaded View=RouteScheduleWindow') { $scheduleLoaded = $true }
        if ($line -match 'UI surface idle inspect View=RouteScheduleWindow' -and $line -match 'BindingErrors=0') { $scheduleBindingOk = $true }
        if ($line -match 'UI surface Loaded View=PdfPreviewWindow') { $previewLoaded = $true }
        if ($line -match 'PDF loaded into internal viewer') { $pdfViewer = $true }
        if ($line -match 'DriverScheduleView') { $driverCalendar = $true }
        if ($line -match 'Verb=print' -or $line -match 'UseShellExecute=.+print') { $printVerb = $true }
        if ($line -match 'RouteScheduleWindow' -and $line -match 'BindingErrors=(?!0)\d+') { $bindingErrors = $true }
        if ($line -match 'CLOCK text=(\d{2}:\d{2})$') { $clocks += $Matches[1] }
    }
}

$missing = @()
$stopsOk = $false
$orderOk = $false
$displayOk = $false
$parsedClocks = @()

if ($openLine) {
    $displayOk = $openLine -like "*$ExpectedDisplayName*"
    if ($openLine -match 'Stops=(\d+)') {
        $stopsOk = [int]$Matches[1] -eq $ExpectedStops
    }
    if ($openLine -match 'Clocks="?([0-9: ]+)"?') {
        $parsedClocks = @($Matches[1].Trim() -split '\s+' | Where-Object { $_ -match '^\d{2}:\d{2}$' })
        $orderOk = $parsedClocks.Count -eq $ExpectedStops
        for ($i = 1; $i -lt $parsedClocks.Count; $i++) {
            if ($parsedClocks[$i] -lt $parsedClocks[$i - 1]) { $orderOk = $false }
        }
    }
} else {
    $missing += 'Opened route schedule'
}

if (-not $displayOk) { $missing += "DisplayName~$ExpectedDisplayName" }
if (-not $stopsOk) { $missing += "Stops=$ExpectedStops" }
if (-not $orderOk) { $missing += 'Clocks in order' }
if (-not $scheduleLoaded) { $missing += 'RouteScheduleWindow Loaded' }
if (-not $scheduleBindingOk) { $missing += 'RouteScheduleWindow BindingErrors=0' }
if (-not $previewLine -and -not $renderLine) { $missing += 'PdfGrid render/preview' }
if (-not $previewLoaded -and -not $pdfViewer) { $missing += 'PdfPreviewWindow / viewer' }
if ($driverCalendar) { $missing += 'opened DriverScheduleView' }
if ($printVerb) { $missing += 'print verb' }
if ($bindingErrors) { $missing += 'RouteScheduleWindow BindingErrors' }
if ($runtimeErrors.Count -gt 0) { $missing += 'runtime-errors' }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("Route schedule smoke score")
[void]$sb.AppendLine("  files: $($files.Count)")
[void]$sb.AppendLine("  since: $(if ($Since) { $Since } else { '(all)' })")
[void]$sb.AppendLine("  extract: $ExtractPath")
[void]$sb.AppendLine("  open: $openLine")
[void]$sb.AppendLine("  clocks: $($parsedClocks -join ' ')")
[void]$sb.AppendLine("  preview: $previewLine")
[void]$sb.AppendLine("  render: $renderLine")
    [void]$sb.AppendLine("  scheduleLoaded=$scheduleLoaded bindingOk=$scheduleBindingOk previewLoaded=$previewLoaded viewer=$pdfViewer")
[void]$sb.AppendLine("  uiaClockTexts: $($clocks -join ' ')")
[void]$sb.AppendLine("  runtime-errors: $($runtimeErrors.Count)")
if ($missing.Count -gt 0) {
    [void]$sb.AppendLine("MISSING required: $($missing -join ', ')")
}

$tail = $allLines | Select-Object -Last 60
[void]$sb.AppendLine("")
[void]$sb.AppendLine("--- tail ---")
foreach ($t in $tail) { [void]$sb.AppendLine($t) }

$text = $sb.ToString()
Set-Content -Path $ExtractPath -Value $text -Encoding utf8
Write-Host $text

if ($missing.Count -gt 0) { exit 1 }
exit 0
