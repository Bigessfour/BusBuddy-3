# Import BusBuddy bb-* commands as a proper module (no dot-sourcing required)
$modulePath = Join-Path $PSScriptRoot 'PowerShell/Modules/BusBuddy.Commands/BusBuddy.Commands.psd1'
Import-Module $modulePath -Force -ErrorAction Stop
Write-Information "BusBuddy commands loaded. Try: bb-commands" -InformationAction Continue
