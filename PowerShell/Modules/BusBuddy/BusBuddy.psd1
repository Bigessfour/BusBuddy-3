@{
    RootModule = 'BusBuddy.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'd9b9f7b2-0000-4000-8000-000000000001'
    Author = 'BusBuddy Team'
    CompanyName = 'BusBuddy'
    Copyright = '(c) BusBuddy'
    Description = 'Core BusBuddy helpers for build, run, test, health (WPF/Syncfusion/Azure SQL integration).'
    FunctionsToExport = @('invokeBusBuddyBuild', 'invokeBusBuddyRun', 'invokeBusBuddyTest', 'invokeBusBuddyHealthCheck', 'invokeBusBuddyRestore', 'invokeBusBuddyClean', 'invokeBusBuddyAntiRegression', 'invokeBusBuddyXamlValidation', 'testBusBuddyMvpReadiness', 'Get-BusBuddyCommands', 'Invoke-BusBuddyParallelTests', 'invokeBusBuddyRefresh')
    PrivateData = @{
        PSData = @{
            Tags = @('busbuddy', 'devops', 'wpf', 'syncfusion')
            LicenseUri = 'https://github.com/Bigessfour/BusBuddy-3/LICENSE'
            ProjectUri = 'https://github.com/Bigessfour/BusBuddy-3'
        }
    }
}
