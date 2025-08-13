# Pester tests for Theme Validation Module
# Requires: Import-Module PowerShell/Modules/BusBuddy/BusBuddy.psd1 prior to run
Describe 'Test-BusBuddyThemeConsistency' {
    It 'Returns Failed when inline properties exist' {
        $temp = Join-Path $env:TEMP "bb-theme-test"
        if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
        New-Item -ItemType Directory -Path (Join-Path $temp 'BusBuddy.WPF/Views') -Force | Out-Null
        $xaml = '<UserControl xmlns:syncfusion="http://schemas.syncfusion.com/wpf"><syncfusion:SfDataGrid RowHeight="30" /></UserControl>'
        $file = Join-Path $temp 'BusBuddy.WPF/Views/Bad.xaml'
        $xaml | Set-Content $file -Encoding UTF8
        Push-Location $temp
        $result = Test-BusBuddyThemeConsistency -ViewsPath 'BusBuddy.WPF/Views'
        Pop-Location
        $result.Status | Should -Be 'Failed'
        $result.Issues | Should -ContainMatch 'Inline SfDataGrid'
    }
}
