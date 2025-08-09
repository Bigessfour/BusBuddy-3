@{
    # Module Manifest for BusBuddy Testing Infrastructure
    RootModule = 'BusBuddy.Testing.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'e3b8f6c9-2a7d-4e5f-9c1a-8b3d7e9f2a5c'

    Author = 'BusBuddy Development Team'
    CompanyName = 'BusBuddy'
    Copyright = '(c) 2025 BusBuddy. All rights reserved.'

    Description = 'Microsoft PowerShell standards-compliant testing module for BusBuddy with VS Code NUnit Test Runner integration'

    # Minimum PowerShell version
    PowerShellVersion = '7.5.0'

    # Required modules
    RequiredModules = @()

    # Functions to export
    FunctionsToExport = @(
        'Start-BusBuddyTest'
        'Start-BusBuddyTestWatch'
        'New-BusBuddyTestReport'
        'Get-BusBuddyTestStatus'
        'Initialize-BusBuddyTestEnvironment'
        'Test-BusBuddyCompliance'
    )

    # Cmdlets to export
    CmdletsToExport = @()

    # Variables to export
    VariablesToExport = @()

    # Aliases to export
    AliasesToExport = @(
        'bb-test-watch'
        'bb-test-report'
        'bb-test-status'
        'bb-test-init'
        'bb-test-compliance'
    )

    # Private data
    PrivateData = @{
        PSData = @{
            Tags = @('BusBuddy', 'Testing', 'NUnit', 'VSCode', 'PowerShell')
            LicenseUri = ''
            ProjectUri = ''
            ReleaseNotes = 'Initial release of BusBuddy Testing Module with full NUnit Test Runner integration'
        }
    }

    # Help info
    HelpInfoURI = ''
}
