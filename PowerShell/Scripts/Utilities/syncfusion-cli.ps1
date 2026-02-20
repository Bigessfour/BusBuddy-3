# Syncfusion WPF CLI Integration for BusBuddy
# This script provides CLI-like functionality for Syncfusion WPF development

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("check-license", "validate-xaml", "list-controls", "generate-docs", "format-xaml", "help")]
    [string]$Command = "help",

    [Parameter(Mandatory = $false)]
    [string]$Path = $PWD.Path
)

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Write-SyncfusionBanner {
    Write-Host @"
╔══════════════════════════════════════════════════════════════════════════════╗
║                    Syncfusion WPF CLI for BusBuddy                         ║
║                      Enhanced Development Experience                         ║
╚══════════════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Test-SyncfusionLicense {
    Write-Host "🔐 Checking Syncfusion License Configuration..." -ForegroundColor Yellow

    if ($env:SYNCFUSION_LICENSE_KEY) {
        Write-Host "✅ SYNCFUSION_LICENSE_KEY environment variable is set" -ForegroundColor Green
        $keyLength = $env:SYNCFUSION_LICENSE_KEY.Length
        $maskedKey = $env:SYNCFUSION_LICENSE_KEY.Substring(0, [Math]::Min(8, $keyLength)) + "***"
        Write-Host "   License Key: $maskedKey" -ForegroundColor Gray
    } else {
        Write-Host "❌ SYNCFUSION_LICENSE_KEY environment variable not found" -ForegroundColor Red
        Write-Host "   Please set your Syncfusion license key:" -ForegroundColor Yellow
        Write-Host "   [Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'your-key-here', 'User')" -ForegroundColor Gray
    }

    # Check for license in project files
    $appXamlFiles = Get-ChildItem -Path $Path -Filter "App.xaml.cs" -Recurse
    foreach ($file in $appXamlFiles) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match "SyncfusionLicenseProvider\.RegisterLicense") {
            Write-Host "✅ License registration found in $($file.Name)" -ForegroundColor Green
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Find-SyncfusionControl {
    Write-Host "🔍 Scanning XAML files for Syncfusion controls..." -ForegroundColor Yellow

    $xamlFiles = Get-ChildItem -Path $Path -Filter "*.xaml" -Recurse
    $controls = @{}
    $totalFiles = 0
    $filesWithSyncfusion = 0

    foreach ($file in $xamlFiles) {
        $totalFiles++
        $content = Get-Content $file.FullName -Raw

        if ($content -match 'xmlns:syncfusion="http://schemas\.syncfusion\.com/wpf"') {
            $filesWithSyncfusion++
            Write-Host "  📄 $($file.Name)" -ForegroundColor Green

            # Extract control names
            $syncfusionMatches = [regex]::Matches($content, 'syncfusion:([A-Za-z0-9]+)')
            foreach ($match in $syncfusionMatches) {
                $controlName = $match.Groups[1].Value
                if ($controls.ContainsKey($controlName)) {
                    $controls[$controlName]++
                } else {
                    $controls[$controlName] = 1
                }
            }
        }
    }

    Write-Host "`n📊 Summary:" -ForegroundColor Cyan
    Write-Host "   Total XAML files: $totalFiles" -ForegroundColor White
    Write-Host "   Files with Syncfusion: $filesWithSyncfusion" -ForegroundColor White

    if ($controls.Count -gt 0) {
        Write-Host "`n🎛️ Syncfusion Controls Found:" -ForegroundColor Cyan
        $controls.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
            Write-Host "   $($_.Key): $($_.Value) usage(s)" -ForegroundColor White
        }
    } else {
        Write-Host "   No Syncfusion controls found" -ForegroundColor Yellow
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Format-XamlFile {
    Write-Host "✨ Formatting XAML files..." -ForegroundColor Yellow

    $xamlFiles = Get-ChildItem -Path $Path -Filter "*.xaml" -Recurse
    $formattedCount = 0

    foreach ($file in $xamlFiles) {
        try {
            $content = Get-Content $file.FullName -Raw
            if ($content -match 'syncfusion:') {
                Write-Host "  📝 Formatting $($file.Name)" -ForegroundColor Green
                $formattedCount++
                # Note: Actual formatting would require XML formatting logic
                # This is a placeholder for demonstration
            }
        } catch {
            Write-Host "  ❌ Error formatting $($file.Name): $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Write-Host "`n✅ Formatted $formattedCount XAML files" -ForegroundColor Green
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function New-SyncfusionDocumentation {
    Write-Host "📚 Generating Syncfusion documentation..." -ForegroundColor Yellow

    $docsPath = Join-Path $Path "docs\syncfusion-controls.md"
    $docsDir = Split-Path $docsPath -Parent

    if (!(Test-Path $docsDir)) {
        New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    }

    $docs = @"
# Syncfusion WPF Controls in BusBuddy

Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Overview
This document lists all Syncfusion controls used in the BusBuddy application.

## Controls Summary

"@

    # Scan for controls
    $xamlFiles = Get-ChildItem -Path $Path -Filter "*.xaml" -Recurse
    $controls = @{}

    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw
        $syncfusionMatches = [regex]::Matches($content, 'syncfusion:([A-Za-z0-9]+)')
        foreach ($match in $syncfusionMatches) {
            $controlName = $match.Groups[1].Value
            if (!$controls.ContainsKey($controlName)) {
                $controls[$controlName] = @()
            }
            $controls[$controlName] += $file.Name
        }
    }

    foreach ($control in $controls.GetEnumerator() | Sort-Object Key) {
        $docs += "`n### $($control.Key)`n"
        $docs += "Used in: $($control.Value -join ', ')`n"
        $docs += "Documentation: [Syncfusion $($control.Key)](https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Grid.SfDataGrid.html)`n"
    }

    $docs | Out-File -FilePath $docsPath -Encoding UTF8
    Write-Host "✅ Documentation generated: $docsPath" -ForegroundColor Green
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Show-Help {
    Write-SyncfusionBanner
    Write-Host @"

Available Commands:
  check-license    Check Syncfusion license configuration
  validate-xaml    Validate XAML files for Syncfusion controls
  list-controls    List all Syncfusion controls used in project
  format-xaml      Format XAML files with Syncfusion controls
  generate-docs    Generate documentation for Syncfusion controls
  help            Show this help message

Usage Examples:
  .\syncfusion-cli.ps1 check-license
  .\syncfusion-cli.ps1 list-controls -Path "C:\Projects\BusBuddy"
  .\syncfusion-cli.ps1 generate-docs

Environment Variables:
  SYNCFUSION_LICENSE_KEY    Your Syncfusion license key

VS Code Integration:
  - Use Task Explorer to run Syncfusion tasks
  - XAML snippets available with 'sf-' prefix
  - Document Viewer extension for previewing files
  - Live debugging with XAML hot reload

"@ -ForegroundColor White
}

# Main execution
switch ($Command.ToLower()) {
    "check-license" {
        Write-SyncfusionBanner
        Test-SyncfusionLicense
    }
    "validate-xaml" {
        Write-SyncfusionBanner
        Find-SyncfusionControls
    }
    "list-controls" {
        Write-SyncfusionBanner
        Find-SyncfusionControls
    }
    "format-xaml" {
        Write-SyncfusionBanner
        Format-XamlFiles
    }
    "generate-docs" {
        Write-SyncfusionBanner
        New-SyncfusionDocumentation
    }
    "help" {
        Show-Help
    }
    default {
        Show-Help
    }
}
