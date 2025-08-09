[CmdletBinding()]
param()

# Locate an existing profile (do not create a new one)
$targets = @(
    [pscustomobject]@{ Name='CurrentUserCurrentHost'; Path=$PROFILE; Exists=(Test-Path $PROFILE) },
    [pscustomobject]@{ Name='CurrentUserAllHosts'; Path=$PROFILE.CurrentUserAllHosts; Exists=(Test-Path $PROFILE.CurrentUserAllHosts) },
    [pscustomobject]@{ Name='AllUsersCurrentHost'; Path=$PROFILE.AllUsersCurrentHost; Exists=(Test-Path $PROFILE.AllUsersCurrentHost) },
    [pscustomobject]@{ Name='AllUsersAllHosts'; Path=$PROFILE.AllUsersAllHosts; Exists=(Test-Path $PROFILE.AllUsersAllHosts) }
)
$target = $targets | Where-Object Exists | Select-Object -First 1
if (-not $target) {
    Write-Error 'No existing PowerShell profile found. Not creating a new one.'
    exit 1
}

# Resolve repo root and Modules path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..' '..'))
$busBuddyModules = Join-Path $repoRoot 'PowerShell/Modules'
if (-not (Test-Path $busBuddyModules)) {
    Write-Error "Modules path not found: $busBuddyModules"
    exit 1
}

# Idempotent insert of a bootstrap block
$blockStart = '# >>> BusBuddy.Commands bootstrap BEGIN'
$blockEnd   = '# <<< BusBuddy.Commands bootstrap END'

$bootstrapTemplate = @'
# >>> BusBuddy.Commands bootstrap BEGIN
$busBuddyModules = '__MODULES_PATH__'
if (Test-Path $busBuddyModules) {
    $paths = ($env:PSModulePath -split ';') | ForEach-Object { $_.TrimEnd('\') }
    if ($paths -notcontains $busBuddyModules) {
        $env:PSModulePath = "$busBuddyModules;$env:PSModulePath"
    }
    if (Get-Module -ListAvailable -Name BusBuddy.Commands) {
        Import-Module BusBuddy.Commands -Force -ErrorAction SilentlyContinue
    } else {
        $psd1 = Join-Path $busBuddyModules 'BusBuddy.Commands/BusBuddy.Commands.psd1'
        if (Test-Path $psd1) {
            Import-Module $psd1 -Force -ErrorAction SilentlyContinue
        }
    }
}
# <<< BusBuddy.Commands bootstrap END
'@

$bootstrap = $bootstrapTemplate.Replace('__MODULES_PATH__', ($busBuddyModules -replace "'","''"))

$content = Get-Content -Raw -Path $target.Path
if ($content -match [regex]::Escape($blockStart)) {
    # Replace existing block
    $newContent = [Text.RegularExpressions.Regex]::Replace($content, '(?s)# >>> BusBuddy.Commands bootstrap BEGIN.*?# <<< BusBuddy.Commands bootstrap END', '')
    $newContent = ($newContent.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $bootstrap)
    Set-Content -Path $target.Path -Value $newContent -Encoding UTF8
} else {
    # Append new block
    $newContent = ($content.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $bootstrap)
    Set-Content -Path $target.Path -Value $newContent -Encoding UTF8
}

Write-Output "Updated $($target.Name) profile: $($target.Path)"
