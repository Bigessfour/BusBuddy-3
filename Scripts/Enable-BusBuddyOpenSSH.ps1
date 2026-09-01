# Enable OpenSSH Server in the UTM Windows VM and install a Mac host pubkey.
# Safe to re-run. Typically invoked as SYSTEM via `utmctl exec`, or as Admin.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Enable-BusBuddyOpenSSH.ps1
#
# Optional: -PublicKeyPath C:\Users\Public\busbuddy-utm.pub

param(
    [string]$PublicKeyPath = 'C:\Users\Public\busbuddy-utm.pub',
    [string]$InteractiveUser = 'Macbook',
    [string]$LogPath = 'C:\Users\Public\busbuddy-ssh-setup.log'
)

$ErrorActionPreference = 'Continue'
function Write-Log([string]$Message) {
    $line = "$(Get-Date -Format o)  $Message"
    Add-Content -Path $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}

Set-Content -Path $LogPath -Value "BusBuddy OpenSSH setup" -Encoding UTF8
Write-Log "User=$env:USERNAME InteractiveUser=$InteractiveUser"

$cap = Get-WindowsCapability -Online | Where-Object { $_.Name -like 'OpenSSH.Server*' } | Select-Object -First 1
if (-not $cap) {
    Write-Log "ERROR: OpenSSH.Server capability not found"
    exit 1
}
Write-Log "Capability $($cap.Name) state=$($cap.State)"
if ($cap.State -ne 'Installed') {
    Write-Log "Installing OpenSSH.Server (can take several minutes)..."
    $install = Add-WindowsCapability -Online -Name $cap.Name
    Write-Log "Install RestartNeeded=$($install.RestartNeeded)"
}

Start-Service sshd -ErrorAction SilentlyContinue
Set-Service -Name sshd -StartupType Automatic
Set-Service -Name ssh-agent -StartupType Manual -ErrorAction SilentlyContinue
Write-Log "sshd status=$((Get-Service sshd).Status)"

$fw = Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue
if (-not $fw) {
    New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH SSH Server (sshd)' `
        -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
    Write-Log "Created firewall rule OpenSSH-Server-In-TCP"
} else {
    Enable-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' | Out-Null
    Write-Log "Firewall rule already present"
}

if (-not (Test-Path $PublicKeyPath)) {
    Write-Log "ERROR: pubkey missing at $PublicKeyPath"
    exit 2
}
$pub = (Get-Content -Path $PublicKeyPath -Raw).Trim()
if ($pub -notmatch '^ssh-(ed25519|rsa) ') {
    Write-Log "ERROR: pubkey does not look like an OpenSSH public key"
    exit 3
}

$userProfile = Join-Path $env:SystemDrive "Users\$InteractiveUser"
$userSsh = Join-Path $userProfile '.ssh'
New-Item -ItemType Directory -Force -Path $userSsh | Out-Null
$userKeys = Join-Path $userSsh 'authorized_keys'
if ((Test-Path $userKeys) -and (Get-Content $userKeys -Raw -ErrorAction SilentlyContinue) -match [regex]::Escape($pub)) {
    Write-Log "User authorized_keys already has this key"
} else {
    Add-Content -Path $userKeys -Value $pub -Encoding ascii
    Write-Log "Wrote $userKeys"
}

$adminKeys = 'C:\ProgramData\ssh\administrators_authorized_keys'
$sshDir = 'C:\ProgramData\ssh'
New-Item -ItemType Directory -Force -Path $sshDir | Out-Null
if ((Test-Path $adminKeys) -and (Get-Content $adminKeys -Raw -ErrorAction SilentlyContinue) -match [regex]::Escape($pub)) {
    Write-Log "administrators_authorized_keys already has this key"
} else {
    Add-Content -Path $adminKeys -Value $pub -Encoding ascii
    Write-Log "Wrote $adminKeys"
}
icacls $adminKeys /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F" | Out-Null
Write-Log "ACL set on administrators_authorized_keys"

# Default Windows sshd ignores per-user authorized_keys for Administrators.
# Keep that Match block; we populated administrators_authorized_keys.

Restart-Service sshd
Write-Log "sshd restarted Status=$((Get-Service sshd).Status)"
Write-Log "DONE"
exit 0
