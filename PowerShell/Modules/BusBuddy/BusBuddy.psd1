#Requires -Version 7.5

@{
    # Script module or binary module file associated with this manifest.
    RootModule           = 'BusBuddy.psm1'

    # Version number of this module.
    ModuleVersion        = '2.0.0'

    # Supported PSEditions
    CompatiblePSEditions = @('Core')

    # ID used to uniquely identify this module
    GUID                 = '21ff42ae-cd8d-4bb8-8f99-b31f63be42b5'

    # Author of this module
    Author               = 'Bus Buddy Development Team'

    # Company or vendor of this module
    CompanyName          = 'BusBuddy Team'

    # Copyright statement for this module
    Copyright            = '(c) 2025 BusBuddy. All rights reserved.'

    # Description of the functionality provided by this module
    Description          = 'Microsoft PowerShell 7.5.2 compliant modular development module for BusBuddy. Provides category-based function loading following Microsoft standards.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion    = '7.5'

    # Functions to export from this module
    FunctionsToExport    = @(
        # Module management
        'Import-BusBuddyFunction',
        'Import-BusBuddyFunctionCategory',

        # Core functions
        'Get-BusBuddyProjectRoot',
        'Write-BusBuddyStatus',
        'Write-BusBuddyError',
        'Invoke-BusBuddyDotNetCommand',

        # Build functions
        'Invoke-BusBuddyBuild',
        'Invoke-BusBuddyRun',
        'Invoke-BusBuddyTest',
        'Invoke-BusBuddyClean',
        'Invoke-BusBuddyRestore',

        # Development functions
        'Invoke-BusBuddyHealthCheck',
        'Test-BusBuddyHealth',
        'Start-BusBuddyDevSession',
    # 'Test-BusBuddyDatabase',
        'Get-BusBuddyCommand',
        'Get-BusBuddyInfo',
    'Show-BusBuddyWelcome',
    # Quick-win thematic modules
    'Test-BusBuddyThemeConsistency',
    'Invoke-BusBuddyThemeRemediation',
    'Test-BusBuddyAzureSql',
    'Get-BusBuddySqlStatus',
    'Start-BusBuddyTestWatchAdvanced',
    'Stop-BusBuddyTestWatchAdvanced',
    'Invoke-BusBuddyCleanup',
    'Get-BusBuddyUnusedFiles',
    'Remove-BusBuddyUnusedFiles'
    )

    # Cmdlets to export from this module
    CmdletsToExport      = @()

    # Variables to export from this module
    VariablesToExport    = @()

    # Aliases to export from this module (defer to Set-Alias in psm1; keep empty to avoid duplication across modules)
    AliasesToExport      = @()

    # List of all files packaged with this module
    FileList             = @(
        'BusBuddy.psm1',
        'BusBuddy.psd1',
        'BusBuddy.settings.ini',
        'bb-validate-database.ps1',
        # Added quick-win module files
        'BusBuddy.ThemeValidation/BusBuddy.ThemeValidation.psm1',
        'BusBuddy.AzureSqlHealth/BusBuddy.AzureSqlHealth.psm1',
        'BusBuddy.TestWatcher/BusBuddy.TestWatcher.psm1',
        'BusBuddy.Cleanup/BusBuddy.Cleanup.psm1'
    )

    # Private data to pass to the module
    PrivateData          = @{
        PSData = @{
            Tags         = @('BusBuddy', 'Development', 'WPF', 'DotNet')
            LicenseUri   = 'https://github.com/Bigessfour/BusBuddy-2/blob/main/LICENSE'
            ProjectUri   = 'https://github.com/Bigessfour/BusBuddy-2'
            ReleaseNotes = 'Streamlined core module for reliable development workflow'
        }
    }
}
