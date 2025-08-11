#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter()] [string] $Folder = (Join-Path $PSScriptRoot '..'),
    [Parameter()] [string] $Pattern = 'application*.log',
    [Parameter()] [string] $FilterRegex = '',
    [Parameter()] [int] $Tail = 50
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

function Get-NewestLogFile {
    param([string]$Path,[string]$Filter)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    Get-ChildItem -LiteralPath $Path -Filter $Filter -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if (-not (Test-Path -LiteralPath $Folder)) {
    Write-Information "[Watch-Logs] Folder not found: $Folder" -InformationAction Continue
    while (-not (Test-Path -LiteralPath $Folder)) { Start-Sleep -Seconds 2 }
}

Write-Information "[Watch-Logs] Watching folder: $Folder pattern: $Pattern" -InformationAction Continue

while ($true) {
    $log = Get-NewestLogFile -Path $Folder -Filter $Pattern
    if ($null -eq $log) { Start-Sleep -Seconds 2; continue }

    Write-Information "[Watch-Logs] Tailing: $($log.FullName)" -InformationAction Continue
    if ([string]::IsNullOrWhiteSpace($FilterRegex)) {
        Get-Content -Path $log.FullName -Tail $Tail -Wait
    }
    else {
        Get-Content -Path $log.FullName -Tail $Tail -Wait |
            Where-Object { $_ -match $FilterRegex } |
            ForEach-Object { Write-Information "[Alert] $_" -InformationAction Continue }
    }

    # If tail ends (log rolled or deleted), loop to attach again
}
