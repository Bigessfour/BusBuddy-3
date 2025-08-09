@{
    RootModule           = 'BusBuddy.Commands.psm1'
    ModuleVersion        = '0.1.0'
    GUID                 = 'e9a7f1e8-4b8f-4b3d-9a7a-1b1a2a5a7d11'
    Author               = 'BusBuddy Team'
    CompanyName          = 'BusBuddy'
    Copyright           = '(c) BusBuddy. All rights reserved.'
    Description          = 'BusBuddy development commands (bb-*) exposed via a proper PowerShell module.'
    PowerShellVersion    = '7.5'
    FunctionsToExport    = @(
        'bb-anti-regression',
        'bb-xaml-validate',
    # 'bb-build' provided by core BusBuddy module to avoid duplicates
    'bb-commands',
        'Get-BbCommands',
        'Test-BbAntiRegression',
        'Test-BbXaml',
        'Invoke-BbBuild',
        'Get-BbWriteHost',
    'Update-BbWriteHost',
    'Show-BbWelcome'
    )
    CmdletsToExport      = @()
    VariablesToExport    = @()
    AliasesToExport      = @()
    PrivateData          = @{ }
}
