# Pester tests for Cleanup Module
Describe 'Get-BusBuddyUnusedFiles' {
    It 'Detects unused file not in solution' {
        $temp = Join-Path $env:TEMP "bb-clean-test"
        if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
        New-Item -ItemType Directory -Path $temp -Force | Out-Null
        Set-Content (Join-Path $temp 'Sample.sln') 'Microsoft Visual Studio Solution File'
        Set-Content (Join-Path $temp 'Lonely.cs') 'public class Lonely {}'
        Push-Location $temp
        $unused = Get-BusBuddyUnusedFiles -Root '.'
        Pop-Location
        ($unused -join ',') | Should -Match 'Lonely.cs'
    }
}
