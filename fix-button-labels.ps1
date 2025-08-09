#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix Syncfusion ButtonAdv controls to use Label instead of Content
.DESCRIPTION
    Syncfusion ButtonAdv controls should use Label property instead of Content property.
    This script systematically replaces Content= with Label= for all ButtonAdv controls.
.NOTES
    Based on Syncfusion WPF 30.1.42 documentation requirements
#>

Write-Host "🔧 Fixing Syncfusion ButtonAdv labels throughout BusBuddy application..." -ForegroundColor Cyan

# Get all XAML files in the project
$xamlFiles = Get-ChildItem -Path "." -Recurse -Filter "*.xaml" | Where-Object {
    $_.FullName -notlike "*\bin\*" -and
    $_.FullName -notlike "*\obj\*"
}

$totalFiles = $xamlFiles.Count
$filesChanged = 0
$totalReplacements = 0

Write-Host "📁 Found $totalFiles XAML files to process" -ForegroundColor Green

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Replace ButtonAdv Content= with Label= even when other attributes appear before Content
    # Example match: <syncfusion:ButtonAdv ... Content="...">
    $pattern = '(<[^>]*?ButtonAdv[^>]*?)\sContent='
    $content = [regex]::Replace($content, $pattern, '${1} Label=')

    # Count replacements in this file (based on original content)
    $replacements = ([regex]::Matches($originalContent, $pattern)).Count

    # Add default Label to ButtonAdv tags missing both Label and Content (handles multiline)
    $buttonPattern = '(?s)<\s*syncfusion:ButtonAdv\b([^>]*)>'
    $addedLabels = 0
    $content = [regex]::Replace($content, $buttonPattern, {
        param($m)
        $attrs = $m.Groups[1].Value
        if ($attrs -match '\bLabel\s*=') {
            return $m.Value
        }
        if ($attrs -match '\bContent\s*=') {
            # Will already be replaced to Label= by previous step in most cases
            return $m.Value
        }
        $addedLabels++
        # Insert Label after tag name, before existing attributes
        return "<syncfusion:ButtonAdv Label=\"Action\"$attrs>"
    })

    if ($content -ne $originalContent) {
        # Preserve a final newline per repo standards
        Set-Content -Path $file.FullName -Value $content
        $filesChanged++
        $totalReplacements += ($replacements + $addedLabels)
        $msg = "  ✅ Fixed $replacements ButtonAdv Content=>Label conversions; added $addedLabels missing Labels in: $($file.Name)"
        Write-Host $msg -ForegroundColor Yellow
    }
}

Write-Host "`n🎯 ButtonAdv Label Fix Complete!" -ForegroundColor Green
Write-Host "  • Files processed: $totalFiles" -ForegroundColor White
Write-Host "  • Files changed: $filesChanged" -ForegroundColor White
Write-Host "  • Total ButtonAdv controls fixed: $totalReplacements" -ForegroundColor White

if ($totalReplacements -gt 0) {
    Write-Host "`n🚀 All Syncfusion ButtonAdv controls now use the correct Label property!" -ForegroundColor Cyan
    Write-Host "   Run the application to verify buttons display their labels correctly." -ForegroundColor Gray
} else {
    Write-Host "`n✅ No ButtonAdv Content properties found - all already using Label correctly!" -ForegroundColor Green
}
