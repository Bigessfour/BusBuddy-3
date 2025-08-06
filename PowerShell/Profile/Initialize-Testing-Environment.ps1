#Requires -Version 7.5

<#
.SYNOPSIS
    Loads the BusBuddy Testing module and initializes its functions.
.DESCRIPTION
    This script is intended to be sourced by the main BusBuddy profile.
    It ensures that the testing environment, including all 'bb-test-*' aliases,
    is available in the PowerShell session.
#>

param()

$ErrorActionPreference = 'Stop'

try {
    $TestingModulePath = Join-Path $PSScriptRoot "Modules" "BusBuddy.Testing" "BusBuddy.Testing.psd1"

    if (-not (Test-Path $TestingModulePath)) {
        Write-Warning "BusBuddy.Testing module manifest not found at $TestingModulePath"
        return
    }

    Import-Module -Name $TestingModulePath -Force
    Write-Verbose "BusBuddy.Testing module loaded."

}
catch {
    Write-Error "Failed to initialize BusBuddy Testing environment: $($_.Exception.Message)"
}
