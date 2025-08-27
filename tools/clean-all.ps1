# Clean All Script
# Cleans the solution and removes build artifacts

try {
    dotnet clean BusBuddy.sln --verbosity minimal
    Write-Information 'Solution cleaned successfully' -InformationAction Continue
} catch {
    Write-Error -Message 'Clean failed' -ErrorAction Continue
}

try {
    Get-ChildItem -Path '.' -Directory -Recurse | Where-Object { $_.Name -eq 'bin' -or $_.Name -eq 'obj' } | Remove-Item -Recurse -Force
    Write-Information 'Build artifacts removed' -InformationAction Continue
} catch {
    Write-Warning -Message 'Some artifacts could not be removed' -WarningAction Continue
}
