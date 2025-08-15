@{
    RootModule = 'BusBuddy.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'd9b9f7b2-0000-4000-8000-000000000001'
    Author = 'BusBuddy Team'
    CompanyName = 'BusBuddy'
    Copyright = '(c) BusBuddy'
    Description = 'Core BusBuddy PowerShell helper module.'
    FunctionsToExport = @('invokeBusBuddyBuild','invokeBusBuddyRun','invokeBusBuddyTest')
    PrivateData = @{
        PSData = @{
            Tags = @('busbuddy','devops')
            LicenseUri = 'https://github.com/your/repo/LICENSE'
            ProjectUri = 'https://github.com/your/repo'
        }
    }
}
