@{
    RootModule        = 'BusBuddy.Validation.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'd3d2d8a1-3a0f-4a42-86a3-3a2d0b0f2c21'
    Author            = 'BusBuddy Team'
    CompanyName       = 'BusBuddy'
    Description       = 'Validation commands for BusBuddy (database health checks)'
    PowerShellVersion = '7.5'
    FunctionsToExport = @(
        'Test-BusBuddyDatabase'
    )
    CmdletsToExport   = @()
    VariablesToExport = @()
    AliasesToExport   = @()
}
