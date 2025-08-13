# Pester tests for Azure SQL Health Module (offline fallback)
Describe 'Test-BusBuddyAzureSql' {
    It 'Returns false when settings file missing' {
        $result = Test-BusBuddyAzureSql -SettingsPath 'nonexistent.json'
        $result | Should -BeFalse
    }
}

Describe 'Get-BusBuddySqlStatus' {
    It 'Returns hashtable with expected keys' {
        $status = Get-BusBuddySqlStatus -AzureSettingsPath 'nonexistent.json' -LocalSettingsPath 'nonexistent.local.json'
        $status.Keys | Should -Contain 'PrimaryDataSource'
    }
}
