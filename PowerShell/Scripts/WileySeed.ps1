# WileySeed.ps1
# Calls StudentService.SeedWileySchoolDistrictDataAsync via dotnet run --project BusBuddy.WPF
# Usage: bb-wiley-seed

param(
    [string]$ProjectPath = "BusBuddy.WPF/BusBuddy.WPF.csproj"
)

Write-Output "Starting Wiley School District data seeding..."

# Run the seeding method via dotnet run (assumes CLI arg triggers seeding)
$seedResult = dotnet run --project $ProjectPath -- WileySeed

Write-Output $seedResult
Write-Output "Wiley School District seeding complete."
