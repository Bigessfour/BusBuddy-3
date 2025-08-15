@{
    RootModule = 'BusBuddy.Testing.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'd9b9f7b2-0000-4000-8000-000000000002'
    Author = 'BusBuddy Team'
    Description = 'Testing utilities for BusBuddy.'
    FunctionsToExport = @('invokeBusBuddyMvpCheck')
    PrivateData = @{
        PSData = @{
            Tags = @('busbuddy','testing')
            LicenseUri = 'https://github.com/your/repo/LICENSE'
            ProjectUri = 'https://github.com/your/repo'
        }
    }
}
