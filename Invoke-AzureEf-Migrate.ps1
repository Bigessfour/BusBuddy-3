[CmdletBinding()] param(
    [Parameter()] [string] $Project = "BusBuddy.Core/BusBuddy.Core.csproj",
    [Parameter()] [string] $StartupProject = "BusBuddy.WPF/BusBuddy.WPF.csproj",
    [Parameter()] [switch] $AddInitialIfMissing
)

$delegate = Join-Path $PSScriptRoot 'PowerShell\Azure\Invoke-AzureEfMigrations.ps1'
if (-not (Test-Path $delegate)) { Write-Error "Delegate script not found: $delegate"; exit 1 }

& $delegate -Project $Project -StartupProject $StartupProject -AddInitialIfMissing:$AddInitialIfMissing
$LASTEXITCODE
