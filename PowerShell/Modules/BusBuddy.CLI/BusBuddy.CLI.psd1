# BusBuddy CLI Integration Module
# Integrates GitHub CLI (PowerShellForGitHub), Azure CLI (Az), and GitKraken CLI
# Standards: PowerShell 7.5+, lazy loading, PowerShell Gallery modules preferred
# Refs: PowerShellForGitHub[](https://github.com/microsoft/PowerShellForGitHub), Az[](https://learn.microsoft.com/powershell/azure/), dotnet CLI[](https://learn.microsoft.com/dotnet/core/tools/)

@{
    ModuleVersion = '1.0.0'
    GUID = 'a1b2c3d4-e5f6-7890-1234-56789abcdef0'
    Author = 'BusBuddy Team'
    CompanyName = 'BusBuddy'
    Copyright = '(c) 2025 BusBuddy. Licensed under MIT.'
    Description = 'CLI integration for GitHub, Azure, and GitKraken using PowerShell Gallery modules'
    PowerShellVersion = '7.5'
    # Note: Modules are lazy-loaded to avoid dependency conflicts
    # RequiredModules removed to prevent Az sub-module version conflicts
    FunctionsToExport = @(
        'Invoke-BusBuddyGitHub',
        'Invoke-BusBuddyAzure',
        'Invoke-BusBuddyGitKraken',
        'Get-BusBuddyWorkflows',
        'Start-BusBuddyCiScan',
        'Get-BusBuddyAzureResources',
        'Get-BusBuddyRepositories',
        'Invoke-BusBuddyFullScan'
    )
    AliasesToExport = @(
        'bbGh', 'bbAz', 'bbGk',
        'bbWorkflows', 'bbCiScan', 'bbAzResources', 'bbRepos', 'bbFullScan'
    )
    CmdletsToExport = @()
    VariablesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('BusBuddy', 'CLI', 'GitHub', 'Azure', 'GitKraken', 'DevOps')
            ProjectUri = 'https://github.com/Bigessfour/BusBuddy-3'
            LicenseUri = 'https://github.com/Bigessfour/BusBuddy-3/blob/main/LICENSE'
        }
    }
}
