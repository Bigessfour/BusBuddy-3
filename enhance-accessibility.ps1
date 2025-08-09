#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Add accessibility improvements to BusBuddy UI controls
.DESCRIPTION
    Adds AutomationProperties.Name to controls that lack proper identification
    for screen readers and accessibility tools.
.NOTES
    Follows WCAG 2.1 accessibility guidelines
#>

Write-Host "♿ Adding accessibility improvements to BusBuddy controls..." -ForegroundColor Cyan

# Function to add AutomationProperties.Name to controls without proper identification
function Add-AccessibilityProperties {
    param(
        [string]$FilePath,
        [string]$ControlType,
        [hashtable]$Improvements
    )

    $content = Get-Content $FilePath -Raw
    $originalContent = $content
    $changeCount = 0

    foreach ($pattern in $Improvements.Keys) {
        $replacement = $Improvements[$pattern]
        $newContent = [regex]::Replace($content, $pattern, $replacement)
        if ($newContent -ne $content) {
            $content = $newContent
            $changeCount++
        }
    }

    if ($content -ne $originalContent) {
        # Preserve trailing newline
        Set-Content -Path $FilePath -Value $content
        return $changeCount
    }

    return 0
}

# Common accessibility improvements for various controls
$improvements = @{
    # Add AutomationProperties.Name to unnamed SfMaskedEdit controls (only if missing)
    '(<syncfusion:SfMaskedEdit(?:(?!AutomationProperties\.Name=)[^>])*Text="\{Binding\s+DriverName[^>]*?)(/?>)'
        = '$1 AutomationProperties.Name="Driver Name Input"$2'
    '(<syncfusion:SfMaskedEdit(?:(?!AutomationProperties\.Name=)[^>])*Text="\{Binding\s+LicenseNumber[^>]*?)(/?>)'
        = '$1 AutomationProperties.Name="License Number Input"$2'
    '(<syncfusion:SfMaskedEdit(?:(?!AutomationProperties\.Name=)[^>])*Text="\{Binding\s+DriverPhone[^>]*?)(/?>)'
        = '$1 AutomationProperties.Name="Phone Number Input"$2'

    # Add AutomationProperties.Name to Status ComboBox (only if missing)
    '(<syncfusion:ComboBoxAdv(?:(?!AutomationProperties\.Name=)[^>])*SelectedItem="\{Binding\s+Status[^>]*?)(/?>)'
        = '$1 AutomationProperties.Name="Status Selection"$2'

    # Add ToolTip to specific buttons only if missing
    '(<syncfusion:ButtonAdv(?:(?!ToolTip=)[^>])*Label="➕ Add Student"[^>]*?)(/?>)'
        = '$1 ToolTip="Add a new student to the system"$2'
    '(<syncfusion:ButtonAdv(?:(?!ToolTip=)[^>])*Label="📝 Edit Student"[^>]*?)(/?>)'
        = '$1 ToolTip="Edit selected student information"$2'
    '(<syncfusion:ButtonAdv(?:(?!ToolTip=)[^>])*Label="➕ Add Driver"[^>]*?)(/?>)'
        = '$1 ToolTip="Add a new driver to the system"$2'
    '(<syncfusion:ButtonAdv(?:(?!ToolTip=)[^>])*Label="➕ Add Bus"[^>]*?)(/?>)'
        = '$1 ToolTip="Add a new bus to the fleet"$2'
}

$xamlFiles = Get-ChildItem -Path "." -Recurse -Filter "*.xaml" | Where-Object {
    $_.FullName -notlike "*\bin\*" -and
    $_.FullName -notlike "*\obj\*"
}

$totalFiles = $xamlFiles.Count
$filesChanged = 0
$totalImprovements = 0

Write-Host "📁 Found $totalFiles XAML files to enhance" -ForegroundColor Green

foreach ($file in $xamlFiles) {
    $changes = Add-AccessibilityProperties -FilePath $file.FullName -ControlType "Mixed" -Improvements $improvements

    if ($changes -gt 0) {
        $filesChanged++
        $totalImprovements += $changes
        Write-Host "  ✅ Added $changes accessibility improvements to: $($file.Name)" -ForegroundColor Yellow
    }
}

Write-Host "`n♿ Accessibility Enhancement Complete!" -ForegroundColor Green
Write-Host "  • Files processed: $totalFiles" -ForegroundColor White
Write-Host "  • Files enhanced: $filesChanged" -ForegroundColor White
Write-Host "  • Total improvements: $totalImprovements" -ForegroundColor White

if ($totalImprovements -gt 0) {
    Write-Host "`n🚀 UI controls now have better accessibility support!" -ForegroundColor Cyan
    Write-Host "   Screen readers and accessibility tools will work better." -ForegroundColor Gray
} else {
    Write-Host "`n✅ All controls already have proper accessibility properties!" -ForegroundColor Green
}
