[CmdletBinding()]
param(
    [switch] $Backup
)

# Optional global opt-out
if ($env:BUSBUDDY_SKIP_PROFILE_UPDATE -eq '1') {
    Write-Output 'BusBuddy autoload: skipped due to BUSBUDDY_SKIP_PROFILE_UPDATE=1'
    exit 0
}

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
$blockStart = '# >>> BusBuddy bootstrap BEGIN'
$blockEnd   = '# <<< BusBuddy bootstrap END'

# The profile bootstrap block that will be inserted or updated in-place
$bootstrapTemplate = @'
# >>> BusBuddy bootstrap BEGIN
$busBuddyModules = '__MODULES_PATH__'
if (Test-Path $busBuddyModules) {
    # Ensure repo modules path is first and de-duplicated
    $paths = ($env:PSModulePath -split ';') | ForEach-Object { $_.TrimEnd('\\') } | Where-Object { $_ }
    $paths = ,$busBuddyModules + ($paths | Where-Object { $_ -ne $busBuddyModules })
    $env:PSModulePath = ($paths -join ';')

    # Desired module manifests (absolute)
    $bbCmds = Join-Path $busBuddyModules 'BusBuddy.Commands/BusBuddy.Commands.psd1'
    $bbProf = Join-Path $busBuddyModules 'BusBuddy.ProfileTools/BusBuddy.ProfileTools.psd1'

    # If wrong copies are loaded (from outside the repo), unload them
    $loaded = Get-Module BusBuddy.Commands -ErrorAction SilentlyContinue
    if ($loaded -and ($loaded.Path -notlike "$busBuddyModules*")) { Remove-Module BusBuddy.Commands -Force -ErrorAction SilentlyContinue }
    $loaded = Get-Module BusBuddy.ProfileTools -ErrorAction SilentlyContinue
    if ($loaded -and ($loaded.Path -notlike "$busBuddyModules*")) { Remove-Module BusBuddy.ProfileTools -Force -ErrorAction SilentlyContinue }

    # Prefer explicit import by path, fallback to name
    if (Test-Path $bbCmds) { Import-Module $bbCmds -Force -ErrorAction SilentlyContinue }
    elseif (Get-Module -ListAvailable -Name BusBuddy.Commands) { Import-Module BusBuddy.Commands -Force -ErrorAction SilentlyContinue }

    if (Test-Path $bbProf) { Import-Module $bbProf -Force -ErrorAction SilentlyContinue }
    elseif (Get-Module -ListAvailable -Name BusBuddy.ProfileTools) { Import-Module BusBuddy.ProfileTools -Force -ErrorAction SilentlyContinue }
}
# <<< BusBuddy bootstrap END
'@

$bootstrap = $bootstrapTemplate.Replace('__MODULES_PATH__', ($busBuddyModules -replace "'","''"))

$content = Get-Content -Raw -Path $target.Path

# Sanitize any stray escaped lines from a prior faulty replace
$content = [Text.RegularExpressions.Regex]::Replace($content, '^(\\.*)$', '', [System.Text.RegularExpressions.RegexOptions]::Multiline)

# Define block regex and check current state
$blockRegex = '(?s)# >>> BusBuddy bootstrap BEGIN.*?# <<< BusBuddy bootstrap END'
$hasBlock = [Text.RegularExpressions.Regex]::IsMatch($content, $blockRegex)

if ($Backup) { Copy-Item -LiteralPath $target.Path -Destination ($target.Path + '.bak') -Force }

if ($hasBlock) {
    # Remove all existing blocks, then append a single correct block at the end
    $contentNoBlocks = [Text.RegularExpressions.Regex]::Replace($content, $blockRegex, '')
    $newContent = ($contentNoBlocks.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $bootstrap)
    Set-Content -Path $target.Path -Value $newContent -Encoding UTF8
    Write-Output "Profile blocks consolidated and updated: $($target.Path)"
} else {
    # Append the block once
    $newContent = ($content.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $bootstrap)
    Set-Content -Path $target.Path -Value $newContent -Encoding UTF8
    Write-Output "Profile block appended: $($target.Path)"
}
