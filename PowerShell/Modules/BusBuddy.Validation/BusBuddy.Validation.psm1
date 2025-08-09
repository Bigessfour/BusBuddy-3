# BusBuddy Validation Module

# Import original script content (migrated) — function name preserved
. $PSScriptRoot\..\..\Modules\BusBuddy\bb-validate-database.ps1

# Export the validation function
Export-ModuleMember -Function Test-BusBuddyDatabase

# Maintain existing aliases
New-Alias -Name "bb-validate-database" -Value "Test-BusBuddyDatabase" -Force
New-Alias -Name "bb-db-validate" -Value "Test-BusBuddyDatabase" -Force
