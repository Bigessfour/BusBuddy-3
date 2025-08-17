@{
    RootModule = 'BusBuddy.ProfileTools.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'd3f2b3c9-4f1a-4e2a-9f3e-2b9f8c5b6a3d'
    Author = 'BusBuddy'
    CompanyName = 'BusBuddy'
    Copyright = '(c) BusBuddy'
    PowerShellVersion = '7.0'
    FunctionsToExport = @('initializeBusBuddyProfileLoaded','getBusBuddyPwshProcesses','stopBusBuddyPwshProcesses')
    AliasesToExport = @()
    CmdletsToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('BusBuddy','Profile','Tools')
            LicenseUri = 'https://opensource.org/licenses/MIT'
            ProjectUri = 'https://example.org/BusBuddy'
        }
    }
}
