# Load Environment Variables for MCP Servers
# This script loads the .env file and sets environment variables for MCP servers

param(
    [switch]$Force
)

$envFile = Join-Path $PSScriptRoot "..\..\.env"
$envFile = Resolve-Path $envFile

if (-not (Test-Path $envFile)) {
    Write-Warning "Environment file not found: $envFile"
    return
}

Write-Host "🔧 Loading environment variables from: $envFile" -ForegroundColor Cyan

Get-Content $envFile | Where-Object {
    $_ -match '^[^#]*=' -and $_ -notmatch '^\s*$'
} | ForEach-Object {
    $line = $_.Trim()
    if ($line -match '^([^=]+)=(.*)$') {
        $key = $matches[1].Trim()
        $value = $matches[2].Trim()

        # Remove quotes if present
        if ($value -match '^"(.*)"$') {
            $value = $matches[1]
        } elseif ($value -match "^'(.*)'$") {
            $value = $matches[1]
        }

        # Set the environment variable at user level
        [Environment]::SetEnvironmentVariable($key, $value, 'User')
        Write-Host "✅ Set $key" -ForegroundColor Green
    }
}

Write-Host "🎉 Environment variables loaded successfully!" -ForegroundColor Green
Write-Host "Note: Restart VS Code to pick up the new environment variables for MCP servers." -ForegroundColor Yellow
