# Convenience wrapper
param([int]$Top = 10)
& "$PSScriptRoot\PowerShell\Scripts\Query-Students-Azure.ps1" -Top $Top
