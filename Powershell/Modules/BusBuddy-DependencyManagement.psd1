# 🚌 BusBuddy Dependency Management Module Manifest
# PowerShell 7.5.2 compliant module with modern syntax enforcement

@{
    # Module metadata
    ModuleVersion = '2.1.0'
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'
    Author = 'BusBuddy Development Team'
    CompanyName = 'BusBuddy'
    Copyright = '(c) 2025 BusBuddy. All rights reserved.'
    Description = 'Enhanced dependency management functions for BusBuddy PowerShell development with PSScriptAnalyzer compliance and modern syntax patterns.'
    
    # PowerShell version requirements
    PowerShellVersion = '7.0'
    PowerShellHostName = ''
    PowerShellHostVersion = ''
    
    # Supported environments
    CompatiblePSEditions = @('Core')
    
    # Required modules
    RequiredModules = @(
        @{ModuleName='PSScriptAnalyzer'; ModuleVersion='1.21.0'},
        @{ModuleName='Microsoft.PowerShell.SecretManagement'; ModuleVersion='1.1.2'},
        @{ModuleName='Microsoft.PowerShell.SecretStore'; ModuleVersion='1.0.6'}
    )
    
    # Required assemblies
    RequiredAssemblies = @()
    
    # Script files and type files
    ScriptsToProcess = @()
    TypesToProcess = @()
    FormatsToProcess = @()
    
    # Module components to export
    FunctionsToExport = @(
        'Install-BusBuddyRequiredModules',
        'Set-BusBuddySecret',
        'Get-BusBuddySecret',
        'Test-BusBuddyAzureConnection',
        'Invoke-BusBuddyDatabaseQuery'
    )
    
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @(
        'bb-install-modules',
        'bb-set-secret',
        'bb-get-secret',
        'bb-test-azure',
        'bb-query-db'
    )
    
    # Module configuration
    PrivateData = @{
        PSData = @{
            # Tags for PowerShell Gallery
            Tags = @('BusBuddy', 'Dependencies', 'Azure', 'PowerShell7', 'SecretManagement', 'PSScriptAnalyzer')
            
            # License and project URLs
            LicenseUri = 'https://github.com/BusBuddy/LICENSE'
            ProjectUri = 'https://github.com/BusBuddy'
            IconUri = ''
            
            # Release notes
            ReleaseNotes = @'
Version 2.1.0:
- Enhanced PowerShell 7.5.2 syntax compliance
- Integrated PSScriptAnalyzer enforcement
- Modern parameter validation patterns
- Structured error handling with proper output streams
- Zero Write-Host usage (Microsoft compliant)
- Advanced splatting and ternary operator usage
'@
            
            # External module dependencies
            ExternalModuleDependencies = @('Az.Accounts', 'Az.Storage', 'Az.KeyVault')
        }
        
        # Custom configuration for BusBuddy
        BusBuddyConfig = @{
            PSScriptAnalyzerSettings = 'PSScriptAnalyzerSettings.psd1'
            RequiredPowerShellVersion = '7.5.2'
            EnforceModernSyntax = $true
            PreventWriteHost = $true
        }
    }
    
    # Help information
    HelpInfoURI = 'https://docs.busbuddy.com/powershell'
}
