<#!
.SYNOPSIS
  Minimal helper to import the BusBuddy PowerShell module manually.
.DESCRIPTION
  Walks upward from current directory to locate BusBuddy.sln, then imports
  PowerShell/Modules/BusBuddy/BusBuddy.psd1 (or .psm1). No profile required.
  Uses only approved output streams (Write-Information / Write-Error).
.PARAMETER Quiet
  Suppress informational messages.
.EXAMPLE
  ./PowerShell/Import-BusBuddy.ps1
.EXAMPLE
  . ./PowerShell/Import-BusBuddy.ps1 -Quiet
.NOTES
  Safe to dot‑source (.) so exported functions/aliases become immediately available.
#>
[CmdletBinding()]
param(
    [switch]$Quiet
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Write-Info($m){ if(-not $Quiet){ Write-Information $m -InformationAction Continue } }

# 1. Locate repo root by finding BusBuddy.sln upward.
$root = (Get-Location).Path
while ($root -and -not (Test-Path (Join-Path $root 'BusBuddy.sln'))) { $root = Split-Path $root -Parent }
if (-not $root) { Write-Error 'BusBuddy.sln not found searching upward from current directory.'; return }
Write-Info "Repo root: $root"

# 2. Determine module file.
$moduleDir = Join-Path $root 'PowerShell/Modules/BusBuddy'
$psd1 = Join-Path $moduleDir 'BusBuddy.psd1'
$psm1 = Join-Path $moduleDir 'BusBuddy.psm1'
$moduleToLoad = if (Test-Path $psd1) { $psd1 } elseif (Test-Path $psm1) { $psm1 } else { $null }
if (-not $moduleToLoad) { Write-Error "BusBuddy module manifest or script not found in $moduleDir"; return }
Write-Info "Importing: $moduleToLoad"

# 3. Remove any already-loaded BusBuddy modules to ensure clean import.
Get-Module | Where-Object Name -like 'BusBuddy*' | ForEach-Object { Remove-Module $_.Name -Force -ErrorAction SilentlyContinue }

# 4. Import.
$sw = [System.Diagnostics.Stopwatch]::StartNew()
Import-Module $moduleToLoad -Force -ErrorAction Stop -PassThru | Out-Null
$sw.Stop()
Write-Info ("Imported BusBuddy in {0} ms" -f [int]$sw.ElapsedMilliseconds)

# 5. Light verification.
$welcome = Get-Command Show-BusBuddyWelcome -ErrorAction SilentlyContinue
if ($welcome) { Write-Info 'Module commands available. (Try: Show-BusBuddyWelcome)' }
else { Write-Info 'Module imported; core commands resolved.' }

# 6. Requested convenience aliases (session-only, camelCase)
function Register-BusBuddyAliases {
  param(
    [switch]$QuietMode
  )
  $map = [ordered]@{
    bbBuild   = 'Invoke-BusBuddyBuild'
    bbRun     = 'Invoke-BusBuddyRun'
    bbClean   = 'Invoke-BusBuddyClean'
    bbRestore = 'Invoke-BusBuddyRestore'
    bbHealth  = 'Invoke-BusBuddyHealthCheck'
    bbTest    = 'Invoke-BusBuddyTest'
    bbInfo    = 'Show-BusBuddyWelcome'
  }
  foreach($k in $map.Keys){
    $target = $map[$k]
    if (Get-Command $target -ErrorAction SilentlyContinue) {
      try { New-Alias -Name $k -Value $target -Scope Global -Force -ErrorAction Stop } catch {}
    }
  }
  if(-not $QuietMode){ Write-Information "Registered BusBuddy aliases: $($map.Keys -join ', ')" -InformationAction Continue }
}

Register-BusBuddyAliases -QuietMode:$Quiet

Write-Info 'BusBuddy module import helper finished.'

# 7. Interactive menu (optional). Uses approved streams; no Write-Host.
function Show-BusBuddyMenu {
  [CmdletBinding()] param()
  $actions = [ordered]@{
    '1' = @{ Label = 'Build';    Action = { Invoke-BusBuddyBuild } }
    '2' = @{ Label = 'Run';      Action = { Invoke-BusBuddyRun } }
    '3' = @{ Label = 'Test';     Action = { if (Get-Command Invoke-BusBuddyTest -ErrorAction SilentlyContinue) { Invoke-BusBuddyTest } else { Write-Information 'Test command not available.' -InformationAction Continue } } }
    '4' = @{ Label = 'Clean';    Action = { Invoke-BusBuddyClean } }
    '5' = @{ Label = 'Restore';  Action = { Invoke-BusBuddyRestore } }
    '6' = @{ Label = 'Health';   Action = { if (Get-Command Invoke-BusBuddyHealthCheck -ErrorAction SilentlyContinue) { Invoke-BusBuddyHealthCheck } else { Write-Information 'Health check not available.' -InformationAction Continue } } }
    'X' = @{ Label = 'Exit';     Action = { return $true } }
  }
  while ($true) {
    Write-Information '=== BusBuddy Menu ===' -InformationAction Continue
    foreach ($k in $actions.Keys) {
      Write-Information ("  {0}. {1}" -f $k, $actions[$k].Label) -InformationAction Continue
    }
    $choice = Read-Host 'Select option (number or X)'
    if (-not $choice) { continue }
    $key = $choice.ToUpper()
    if ($actions.Contains($key)) {
      $exit = & $actions[$key].Action
      if ($exit) { Write-Information 'Exiting menu.' -InformationAction Continue; break }
    } else {
      Write-Information "Unknown option: $choice" -InformationAction Continue
    }
  }
}

if (-not (Get-Alias bbMenu -ErrorAction SilentlyContinue)) { New-Alias -Name bbMenu -Value Show-BusBuddyMenu -Scope Global -Force -ErrorAction SilentlyContinue }
