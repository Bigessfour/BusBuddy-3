#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Fix VehicleId references to BusId throughout the BusBuddy codebase
.DESCRIPTION
    Systematically replaces VehicleId property references with BusId to resolve compilation errors
    after the Bus model refactoring.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WorkspaceRoot = "c:\Users\biges\Desktop\BusBuddy"
)

Write-Information "🔧 Fixing VehicleId -> BusId references in BusBuddy codebase" -InformationAction Continue

# Define the files to update based on build errors
$filesToUpdate = @(
    "BusBuddy.WPF\ViewModels\Vehicle\VehiclesViewModel.cs",
    "BusBuddy.WPF\Views\Activity\ActivityScheduleEditDialog.xaml.cs",
    "BusBuddy.WPF\ViewModels\Vehicle\VehicleManagementViewModel.cs",
    "BusBuddy.WPF\Mapping\MappingProfile.cs",
    "BusBuddy.WPF\Services\DataIntegrityService.cs",
    "BusBuddy.WPF\ViewModels\Fuel\FuelManagementViewModel.cs",
    "BusBuddy.WPF\ViewModels\Sports\SportsSchedulerViewModel.cs",
    "BusBuddy.WPF\ViewModels\Route\RouteAssignmentViewModel.cs"
)

$replacements = @{
    # Bus model property references
    "\.VehicleId" = ".BusId"
    "VehicleId\s*=" = "BusId ="
    "VehicleId\s*," = "BusId,"
    "VehicleId\s*\)" = "BusId)"
    "VehicleId\s*\}" = "BusId}"
    "VehicleId\s*:" = "BusId:"
    "vehicleId\s*:" = "busId:"
}$totalReplacements = 0

foreach ($relativeFile in $filesToUpdate) {
    $filePath = Join-Path $WorkspaceRoot $relativeFile

    if (-not (Test-Path $filePath)) {
        Write-Warning "File not found: $filePath"
        continue
    }

    Write-Information "📝 Processing: $relativeFile" -InformationAction Continue

    try {
        $content = Get-Content $filePath -Raw
        $originalContent = $content

        foreach ($pattern in $replacements.Keys) {
            $replacement = $replacements[$pattern]
            $matches = [regex]::Matches($content, $pattern)
            if ($matches.Count -gt 0) {
                Write-Verbose "  Replacing '$pattern' -> '$replacement' ($($matches.Count) matches)"
                $content = $content -replace $pattern, $replacement
                $totalReplacements += $matches.Count
            }
        }

        if ($content -ne $originalContent) {
            Set-Content $filePath -Value $content -NoNewline
            Write-Information "  ✅ Updated with replacements" -InformationAction Continue
        } else {
            Write-Information "  ℹ️  No changes needed" -InformationAction Continue
        }
    }
    catch {
        Write-Error "Failed to process ${filePath}: $($_.Exception.Message)"
    }
}

Write-Information "🎯 Total replacements made: $totalReplacements" -InformationAction Continue

# Also fix SportsEvent.BusId issue
$sportsEventFile = Join-Path $WorkspaceRoot "BusBuddy.Core\Models\SportsEvent.cs"
if (Test-Path $sportsEventFile) {
    Write-Information "📝 Fixing SportsEvent.BusId property" -InformationAction Continue

    $content = Get-Content $sportsEventFile -Raw

    # Replace _vehicleId with _busId
    $content = $content -replace "_vehicleId", "_busId"

    # Replace VehicleId property with BusId property
    $content = $content -replace "public int\? VehicleId", "public int? BusId"
    $content = $content -replace "get => _vehicleId;", "get => _busId;"
    $content = $content -replace "set => _vehicleId = value;", "set => _busId = value;"

    Set-Content $sportsEventFile -Value $content -NoNewline
    Write-Information "  ✅ SportsEvent updated" -InformationAction Continue
}

Write-Information "🏁 VehicleId -> BusId fix completed!" -InformationAction Continue
Write-Information "Run 'dotnet build BusBuddy.sln' to verify fixes" -InformationAction Continue
