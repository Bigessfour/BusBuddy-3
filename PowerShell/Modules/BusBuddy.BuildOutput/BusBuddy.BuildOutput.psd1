@{
    RootModule = 'BusBuddy.BuildOutput.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'
    Author = 'BusBuddy Development Team'
    CompanyName = 'BusBuddy Project'
    Copyright = '(c) 2025 BusBuddy Project. All rights reserved.'
    Description = 'Enhanced build output capture for BusBuddy - eliminates truncated terminal output'

    PowerShellVersion = '7.5'

    FunctionsToExport = @(
        'Get-BusBuddyBuildOutput',
        'bb-build-full',
        'bb-build-errors',
        'bb-build-log'
    )

    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()

    PrivateData = @{
        PSData = @{
            Tags = @('BusBuddy', 'Build', 'Output', 'Capture', 'NoTruncation')
            ProjectUri = 'https://github.com/Bigessfour/BusBuddy-2'
            ReleaseNotes = @'
v1.0.0 - Initial release
- Complete build output capture without truncation
- Automatic error parsing and highlighting
- Timestamped log file generation
- Enhanced terminal buffer configuration
'@
        }
    }
}
