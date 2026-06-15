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

Write-Host "🔧 Fixing VehicleId -> BusId references in BusBuddy codebase" -ForegroundColor Green

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

$totalReplacements = 0

foreach ($relativeFile in $filesToUpdate) {
    $filePath = Join-Path $WorkspaceRoot $relativeFile

    if (-not (Test-Path $filePath)) {
        Write-Warning "File not found: $filePath"
        continue
    }

    Write-Host "📝 Processing: $relativeFile" -ForegroundColor Yellow

    try {
        $content = Get-Content $filePath -Raw
        $originalContent = $content

        # Simple string replacements
        $content = $content -replace "\.VehicleId", ".BusId"
        $content = $content -replace "VehicleId\s*=", "BusId ="
        $content = $content -replace "VehicleId\s*,", "BusId,"
        $content = $content -replace "VehicleId\s*\)", "BusId)"
        $content = $content -replace "VehicleId\s*\}", "BusId}"
        $content = $content -replace "VehicleId\s*:", "BusId:"
        $content = $content -replace "vehicleId\s*:", "busId:"

        if ($content -ne $originalContent) {
            Set-Content $filePath -Value $content -NoNewline
            Write-Host "  ✅ Updated with replacements" -ForegroundColor Green
            $totalReplacements++
        } else {
            Write-Host "  ℹ️  No changes needed" -ForegroundColor Blue
        }
    }
    catch {
        Write-Error "Failed to process ${filePath}: $($_.Exception.Message)"
    }
}

# Also fix BusViewModel BusId property
$busViewModelFile = Join-Path $WorkspaceRoot "BusBuddy.WPF\ViewModels\Bus\BusViewModel.cs"
if (Test-Path $busViewModelFile) {
    Write-Host "📝 Fixing BusViewModel.BusId property" -ForegroundColor Yellow

    $content = Get-Content $busViewModelFile -Raw

    # Replace VehicleId property with BusId property
    $content = $content -replace "_vehicleId", "_busId"
    $content = $content -replace "public int VehicleId", "public int BusId"
    $content = $content -replace "get => _vehicleId;", "get => _busId;"
    $content = $content -replace "set => SetProperty\(ref _vehicleId, value\);", "set => SetProperty(ref _busId, value);"

    Set-Content $busViewModelFile -Value $content -NoNewline
    Write-Host "  ✅ BusViewModel updated" -ForegroundColor Green
}

# Also fix SportsEvent.BusId issue
$sportsEventFile = Join-Path $WorkspaceRoot "BusBuddy.Core\Models\SportsEvent.cs"
if (Test-Path $sportsEventFile) {
    Write-Host "📝 Fixing SportsEvent.BusId property" -ForegroundColor Yellow

    $content = Get-Content $sportsEventFile -Raw

    # Replace _vehicleId with _busId
    $content = $content -replace "_vehicleId", "_busId"

    # Replace VehicleId property with BusId property
    $content = $content -replace "public int\? VehicleId", "public int? BusId"
    $content = $content -replace "get => _vehicleId;", "get => _busId;"
    $content = $content -replace "set => _vehicleId = value;", "set => _busId = value;"

    Set-Content $sportsEventFile -Value $content -NoNewline
    Write-Host "  ✅ SportsEvent updated" -ForegroundColor Green
}

Write-Host "🎯 Files processed: $totalReplacements" -ForegroundColor Green
Write-Host "🏁 VehicleId -> BusId fix completed!" -ForegroundColor Green
Write-Host "Run 'bb-build' to verify fixes" -ForegroundColor Cyan
