@{
    RootModule           = 'BusBuddy.Commands.psm1'
    ModuleVersion        = '0.1.0'
    GUID                 = 'e9a7f1e8-4b8f-4b3d-9a7a-1b1a2a5a7d11'
    Author               = 'BusBuddy Team'
    CompanyName          = 'BusBuddy'
    Copyright            = '(c) BusBuddy. All rights reserved.'
    Description          = 'BusBuddy development commands (bb-*) exposed via a proper PowerShell module.'
    PowerShellVersion    = '7.5'
    CompatiblePSEditions = @('Core')
    FunctionsToExport    = '*'
    CmdletsToExport      = @()
    VariablesToExport    = @()
    AliasesToExport      = '*'
    PrivateData          = @{
        PSData = @{
            Tags = @('BusBuddy', 'Commands')
        }
    }
}

