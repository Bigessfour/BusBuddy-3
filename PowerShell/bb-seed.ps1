# bb-seed.ps1
# PowerShell script to trigger BusBuddy development data seeding via .NET
# Usage: pwsh PowerShell/bb-seed.ps1

param(
    [string]$Project = "BusBuddy.Core"
)

Write-Output "[bb-seed] Running development data seeding..."

# Run the .NET seeding method (SeedAllAsync)
dotnet run --project $Project --no-build -- SeedAllAsync

Write-Output "[bb-seed] Seeding complete."
