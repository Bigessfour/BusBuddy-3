#Requires -Version 7.5

@{
    # Script module or binary module file associated with this manifest.
    RootModule           = 'BusBuddy.ExceptionCapture.psm1'

    # Version number of this module.
    ModuleVersion        = '1.0.0'

    # Supported PSEditions
    CompatiblePSEditions = @('Core')

    # ID used to uniquely identify this module
    GUID                 = 'f4a8b2c6-d9e1-4f3a-8b7c-2e5d6f9a1b3c'

    # Author of this module
    Author               = 'BusBuddy Development Team'

    # Company or vendor of this module
    CompanyName          = 'BusBuddy Project'

    # Copyright statement for this module
    Copyright            = '(c) 2025 BusBuddy Project. All rights reserved.'

    # Description of the functionality provided by this module
    Description          = 'Professional exception handling and error capture module for BusBuddy development. Provides structured exception handling, error logging, and application monitoring capabilities following Microsoft PowerShell 7.5.2 standards.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion    = '7.5'

    # Functions to export from this module
    FunctionsToExport    = @(
        'Invoke-BusBuddyWithExceptionCapture',
        'Write-BusBuddyException',
        'Write-BusBuddyExecutionLog',
        'Start-BusBuddyErrorMonitoring',
        'Get-BusBuddyExceptionSummary',
        'Start-BusBuddyWithCapture'
    )

    # Cmdlets to export from this module
    CmdletsToExport      = @()

    # Variables to export from this module
    VariablesToExport    = @()

    # Aliases to export from this module
    AliasesToExport      = @(
        'bb-catch-errors',
        'bb-error-monitor',
        'bb-exception-summary',
        'bb-run-safe'
    )

    # List of all files packaged with this module
    FileList             = @(
        'BusBuddy.ExceptionCapture.psm1',
        'BusBuddy.ExceptionCapture.psd1'
    )

    # Private data to pass to the module specified in RootModule
    PrivateData          = @{
        PSData = @{
            # Tags applied to this module
            Tags         = @('BusBuddy', 'Exception', 'ErrorHandling', 'Logging', 'PowerShell75')

            # A URL to the license for this module.
            LicenseUri   = ''

            # A URL to the main website for this project.
            ProjectUri   = ''

            # A URL to an icon representing this module.
            IconUri      = ''

            # Release notes for this module
            ReleaseNotes = @'
Version 1.0.0:
- Initial implementation of BusBuddy exception capture module
- Microsoft PowerShell 7.5.2 compliant structure and patterns
- Comprehensive exception handling with structured logging
- Real-time error monitoring capabilities
- Exception analysis and summary reporting
- Integration with BusBuddy development workflow
'@
        }
    }
}
