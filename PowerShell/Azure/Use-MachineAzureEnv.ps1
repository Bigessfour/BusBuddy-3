# Loads AZURE_SQL_USER/PASSWORD from User/Machine scope into current session
[CmdletBinding()] param()

$u = [Environment]::GetEnvironmentVariable('AZURE_SQL_USER', 'Process')
if (-not $u) { $u = [Environment]::GetEnvironmentVariable('AZURE_SQL_USER', 'User') }
if (-not $u) { $u = [Environment]::GetEnvironmentVariable('AZURE_SQL_USER', 'Machine') }

$p = [Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD', 'Process')
if (-not $p) { $p = [Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD', 'User') }
if (-not $p) { $p = [Environment]::GetEnvironmentVariable('AZURE_SQL_PASSWORD', 'Machine') }

if (-not $u -or -not $p) {
    Write-Error 'AZURE_SQL_USER/AZURE_SQL_PASSWORD not found in Process/User/Machine scopes.'
    exit 1
}

$env:AZURE_SQL_USER = $u
$env:AZURE_SQL_PASSWORD = $p

Write-Output ([pscustomobject]@{ User=$env:AZURE_SQL_USER; PasswordLength=$env:AZURE_SQL_PASSWORD.Length })
