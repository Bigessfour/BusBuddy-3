# Requires -Modules Pester

Describe "BusRouteSync.ps1" {
    It "Syncs routes without errors" {
        # Run the script and ensure it does not throw
        { & "${PSScriptRoot}\..\..\scripts\BusRouteSync.ps1" } | Should -Not -Throw
    }
}
