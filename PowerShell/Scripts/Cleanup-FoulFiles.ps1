#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Force,
    [switch]$IncludeAzureConfig,
    [string]$Root = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Header([string]$text) {
    Write-Host "`n=== $text ===" -ForegroundColor Cyan
}

function Test-References {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Workspace
    )
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $name = Split-Path -Leaf $Path
    $pattern = [Regex]::Escape($name)
    $excludeDirs = @('bin','obj','.git','.trash','.vs')
    $searchPaths = Get-ChildItem -LiteralPath $Workspace -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $excludeDirs -notcontains ($_.DirectoryName | Split-Path -Leaf) }
    $hits = @()
    foreach ($file in $searchPaths) {
        try {
            $m = Select-String -Path $file.FullName -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
            if ($m) { $hits += $m }
        } catch {}
    }
    return $hits
}

Write-Header "Validating cleanup plan"
$targets = @(
  'appsettings (1).json',
  'appsettings.staging.json',
  'appsettings.azure.json',
  '.editorconfig (1)',
  '.editorconfig (2)',
  '.gitattributes (1)',
  'global (1).json',
  'Directory (1).Build.props',
  'BusBuddy.WPF/BusBuddy.WPF_0dy3nte3_wpftmp.csproj',
  'BusBuddy.WPF/BusBuddy.WPF_1kpo3wuk_wpftmp.csproj',
  'BusBuddy.WPF/BusBuddy.WPF_ozpq1ooy_wpftmp.csproj',
  'BusBuddy.WPF/BusBuddy.WPF_x2j2zbh1_wpftmp.csproj',
  'BusBuddy.WPF/runtime-errors-fixed.log'
)

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$trash = Join-Path $rootFull ".trash/$timestamp"
New-Item -ItemType Directory -Path $trash -Force | Out-Null

$plan = @()
foreach ($rel in $targets) {
    $full = Join-Path $rootFull $rel
    $exists = Test-Path -LiteralPath $full
    $refs = if ($exists) { Test-References -Path $full -Workspace $rootFull } else { @() }
    $item = [PSCustomObject]@{
        File = $rel
        Exists = $exists
        References = $refs.Count
        Action = if ($exists) { 'Archive' } else { 'Skip (missing)' }
        Notes = ''
    }
    if ($rel -ieq 'appsettings.azure.json') {
        if ($refs.Count -gt 0 -and -not $IncludeAzureConfig -and -not $Force) {
            $item.Action = 'Skip (referenced)'
            $item.Notes = 'Referenced in code/scripts; pass -IncludeAzureConfig or -Force to archive.'
        }
        elseif ($refs.Count -gt 0 -and ($IncludeAzureConfig -or $Force)) {
            $item.Notes = 'Referenced but will be archived as requested.'
        }
    }
    $plan += $item
}

$plan | Format-Table -AutoSize | Out-Host

Write-Header "Dry-run summary"
$plan | ForEach-Object {
    if ($_.Action -like 'Archive') {
        Write-Host ("Would archive: {0}" -f $_.File) -ForegroundColor Yellow
    } else {
        Write-Host ("{0}: {1}" -f $_.Action, $_.File) -ForegroundColor DarkGray
    }
}

if (-not $PSCmdlet.ShouldProcess($rootFull, 'Archive listed files to .trash')) { return }

Write-Header "Archiving files"
foreach ($p in $plan) {
    if ($p.Action -ne 'Archive') { continue }
    $src = Join-Path $rootFull $p.File
    if (Test-Path -LiteralPath $src) {
        $dest = Join-Path $trash (Split-Path -Leaf $p.File)
        try {
            Move-Item -LiteralPath $src -Destination $dest -Force -ErrorAction Stop
            Write-Host ("Archived: {0}" -f $p.File) -ForegroundColor Green
        }
        catch {
            Write-Warning ("Failed to archive {0}: {1}" -f $p.File, $_.Exception.Message)
        }
    }
}

Write-Header "Done"
Write-Host "Archived to: $trash" -ForegroundColor Cyan
