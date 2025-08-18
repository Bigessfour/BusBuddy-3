# Sets Azure SQL environment variables safely for the current session or persistently.
# Microsoft PowerShell standards reference:
# - Cmdlet Dev Guidelines: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines
# - Output Streams: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

[CmdletBinding()] param(
    [Parameter()] [string] $User,
    [Parameter()] [SecureString] $Password,
    [Parameter()] [switch] $Prompt,
    [Parameter()] [switch] $Persist
)

function Convert-FromSecureStringPlain {
    param([Parameter(Mandatory)][SecureString] $Secure)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { if ($bstr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) } }
}

if ($Prompt) {
    if (-not $User) { $User = Read-Host "Enter Azure SQL user (e.g., busbuddy_admin)" }
    if (-not $Password) {
        $sec = Read-Host "Enter Azure SQL password" -AsSecureString
        $Password = Convert-FromSecureStringPlain -Secure $sec
    }
}

if (-not $User) { Write-Error "AZURE_SQL_USER is required. Provide -User or use -Prompt."; exit 1 }
if (-not $Password) { Write-Error "AZURE_SQL_PASSWORD is required. Provide -Password or use -Prompt."; exit 1 }

# Set for current session
$env:AZURE_SQL_USER = $User
$env:AZURE_SQL_PASSWORD = $Password

Write-Information "Session variables set: AZURE_SQL_USER, AZURE_SQL_PASSWORD" -InformationAction Continue

if ($Persist) {
    [Environment]::SetEnvironmentVariable('AZURE_SQL_USER', $User, 'User')
    [Environment]::SetEnvironmentVariable('AZURE_SQL_PASSWORD', $Password, 'User')
    Write-Information "Persisted AZURE_SQL_USER and AZURE_SQL_PASSWORD at User scope." -InformationAction Continue
}

Write-Output ([pscustomobject]@{
        User           = $env:AZURE_SQL_USER
        PasswordLength = ($env:AZURE_SQL_PASSWORD | ForEach-Object { $_.Length })
        Persisted      = [bool]$Persist
    })
