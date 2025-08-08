[CmdletBinding()] param(
    [Parameter()] [string] $User,
    [Parameter()] [string] $Password,
    [Parameter()] [switch] $Prompt,
    [Parameter()] [switch] $Persist
)

$delegate = Join-Path $PSScriptRoot 'PowerShell\Azure\Set-AzureSqlEnv.ps1'
if (-not (Test-Path $delegate)) { Write-Error "Delegate script not found: $delegate"; exit 1 }

& $delegate -User $User -Password $Password -Prompt:$Prompt -Persist:$Persist
$LASTEXITCODE
