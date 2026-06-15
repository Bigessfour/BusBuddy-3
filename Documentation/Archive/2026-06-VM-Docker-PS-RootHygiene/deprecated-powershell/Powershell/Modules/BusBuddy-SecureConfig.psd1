#
# Module manifest for BusBuddy-SecureConfig
#

@{
    RootModule = 'BusBuddy-SecureConfig.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'b1234567-89ab-cdef-0123-456789abcdef'
    Author = 'BusBuddy Development Team'
    CompanyName = 'BusBuddy'
    Copyright = '(c) 2025 BusBuddy. All rights reserved.'
    Description = 'Secure configuration management for BusBuddy using Microsoft SecretManagement'
    PowerShellVersion = '7.0'

    RequiredModules = @(
        'Microsoft.PowerShell.SecretManagement',
        'Microsoft.PowerShell.SecretStore'
    )

    FunctionsToExport = @(
        'Initialize-SecureGrokConfig',
        'Get-SecureApiKey',
        'Set-SecureApiKey',
        'ConvertFrom-SecureApiKey',
        'Test-SecureApiKey'
    )

    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()

    PrivateData = @{
        PSData = @{
            Tags = @('BusBuddy', 'Security', 'SecretManagement', 'API')
            ProjectUri = 'https://github.com/Bigessfour/BusBuddy-3'
            RequireLicenseAcceptance = $false
        }
    }
}
