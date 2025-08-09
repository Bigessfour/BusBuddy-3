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


# Optimize .NET CLI for 12 logical processors (hyperthreading)
[System.Environment]::SetEnvironmentVariable('DOTNET_CLI_MAX_PARALLELISM','12','Process')

try {

    $ModulesRoot = Join-Path (Split-Path $PSScriptRoot -Parent) "Modules"
    $FunctionsRoot = Join-Path (Split-Path $PSScriptRoot -Parent) "Functions"
    $ScriptsRoot = Join-Path (Split-Path $PSScriptRoot -Parent) "Scripts"
    $ValidationRoot = Join-Path (Split-Path $PSScriptRoot -Parent) "Validation"
    $MainModule = Join-Path (Split-Path $PSScriptRoot -Parent) "BusBuddy.psm1"
    $XamlValidationModule = Join-Path (Split-Path $PSScriptRoot -Parent) "XamlValidation.psm1"

    # Import main BusBuddy modules if present
    $ModuleList = @(
        Join-Path $ModulesRoot "BusBuddy.Testing" "BusBuddy.Testing.psd1"
        Join-Path $ModulesRoot "BusBuddy.BuildOutput" "BusBuddy.BuildOutput.psd1"
        Join-Path $ModulesRoot "BusBuddy.TestOutput" "BusBuddy.TestOutput.psd1"
        Join-Path $ModulesRoot "BusBuddy.Utilities" "BusBuddy.Utilities.psd1"
        Join-Path $ModulesRoot "BusBuddy.ValidationHelpers" "BusBuddy.ValidationHelpers.psd1"
        Join-Path $ModulesRoot "BusBuddy.Rules.psd1"
        Join-Path $ModulesRoot "BusBuddy.ExceptionCapture.psd1"
        Join-Path $ModulesRoot "BusBuddy.Commands" "BusBuddy.Commands.psd1"
        Join-Path $ModulesRoot "BusBuddy.Validation" "BusBuddy.Validation.psd1"
        Join-Path $ModulesRoot "BusBuddy" "BusBuddy.psd1"
        Join-Path $ModulesRoot "BusBuddy.ProfileTools" "BusBuddy.ProfileTools.psd1"
        $MainModule
        $XamlValidationModule
    )
    foreach ($mod in $ModuleList) {
        if (Test-Path $mod) {
            Import-Module -Name $mod -Force -ErrorAction SilentlyContinue
        }
    }

    # Dot-source all utility and script files for full function/alias availability
    # All profile utility scripts are now loaded via BusBuddy.ProfileTools module
    Write-Verbose "All BusBuddy PowerShell tools loaded."

}
catch {
    Write-Error "Failed to initialize BusBuddy Testing environment: $($_.Exception.Message)"
}
