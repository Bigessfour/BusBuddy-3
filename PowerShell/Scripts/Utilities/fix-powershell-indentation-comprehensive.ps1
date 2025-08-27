#!/usr/bin/env pwsh
<#
.SYNOPSIS
Fix PowerShell indentation issues across all PowerShell files
.DESCRIPTION
Fixes indentation issues in PowerShell module manifests (.psd1) and scripts (.ps1, .psm1)
according to PSScriptAnalyzer PSUseConsistentIndentation rule
.NOTES
Author: AI Assistant
Date: $(Get-Date -Format 'yyyy-MM-dd')
#>

param(
    [string]$Path = ".",
    [int]$IndentSize = 4,
    [switch]$WhatIf
)

Write-Information "🔧 PowerShell Indentation Fixer" -InformationAction Continue
Write-Information "===============================" -InformationAction Continue

# Find all PowerShell files
$psFiles = Get-ChildItem -Path $Path -Recurse -Include "*.ps1", "*.psm1", "*.psd1" | Where-Object {
    $_.FullName -notlike "*\.git\*" -and
    $_.FullName -notlike "*\.vs\*" -and
    $_.FullName -notlike "*\.trunk\*"
}

Write-Information "Found $($psFiles.Count) PowerShell files to process..." -InformationAction Continue

foreach ($file in $psFiles) {
    Write-Information "`nProcessing: $($file.Name)" -InformationAction Continue

    try {
        $content = Get-Content $file.FullName -Raw
        $lines = $content -split "`r?`n"
        $fixedLines = @()
        $indentLevel = 0
        $inHashTable = $false
        $inArray = $false

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $trimmedLine = $line.TrimStart()

            # Skip empty lines
            if ([string]::IsNullOrWhiteSpace($trimmedLine)) {
                $fixedLines += ""
                continue
            }

            # Handle comments and special cases
            if ($trimmedLine.StartsWith("#")) {
                $fixedLines += (' ' * ($indentLevel * $IndentSize)) + $trimmedLine
                continue
            }

            # Handle closing braces/brackets/parentheses
            if ($trimmedLine -match '^[\}\]\)]') {
                $indentLevel = [Math]::Max(0, $indentLevel - 1)
                if ($trimmedLine -eq '}') { $inHashTable = $false }
                if ($trimmedLine -eq ')') { $inArray = $false }
            }

            # Apply indentation
            $currentIndent = ' ' * ($indentLevel * $IndentSize)
            $fixedLines += $currentIndent + $trimmedLine

            # Handle opening braces/brackets/parentheses
            if ($trimmedLine -match '[\{\[\(]$') {
                $indentLevel++
                if ($trimmedLine -match '\{$') { $inHashTable = $true }
                if ($trimmedLine -match '\($') { $inArray = $true }
            }

            # Handle PowerShell data file (.psd1) specific patterns
            if ($file.Extension -eq ".psd1") {
                # Handle key-value pairs in hash tables
                if ($trimmedLine -match '^\w+\s*=\s*@\{' -or
                    $trimmedLine -match '^\w+\s*=\s*@\(' -or
                    $trimmedLine -match '=\s*\{$') {
                    $indentLevel++
                }
            }

            # Handle function definitions and control structures
            if ($trimmedLine -match '^function\s+' -or
                $trimmedLine -match '^if\s*\(' -or
                $trimmedLine -match '^foreach\s*\(' -or
                $trimmedLine -match '^while\s*\(' -or
                $trimmedLine -match '^try\s*$' -or
                $trimmedLine -match '^catch\s*[\{\(]?' -or
                $trimmedLine -match '^finally\s*$') {
                if (-not ($trimmedLine -match '\{\s*$')) {
                    $indentLevel++
                }
            }
        }

        # Join lines and write back
        $fixedContent = $fixedLines -join "`r`n"

        if ($WhatIf) {
            Write-Information "   Would fix indentation in $($file.Name)" -InformationAction Continue
        } else {
            Set-Content -Path $file.FullName -Value $fixedContent -Encoding UTF8 -NoNewline
            Write-Information "   ✅ Fixed indentation in $($file.Name)" -InformationAction Continue
        }

    } catch {
        Write-Information "   ❌ Error processing $($file.Name): $($_.Exception.Message)" -InformationAction Continue
    }
}

Write-Information "`n✅ PowerShell indentation fix complete!" -InformationAction Continue

# Run PSScriptAnalyzer to verify fixes
if (-not $WhatIf) {
    Write-Information "`n🔍 Running PSScriptAnalyzer to verify fixes..." -InformationAction Continue
    try {
        $analyzerResults = Invoke-ScriptAnalyzer -Path $Path -Recurse -IncludeRule "PSUseConsistentIndentation" -Settings "PSScriptAnalyzerSettings.psd1" 2>$null

        if ($analyzerResults) {
            Write-Information "   ⚠️  $($analyzerResults.Count) indentation issues remain:" -InformationAction Continue
            $analyzerResults | Group-Object ScriptPath | ForEach-Object {
                Write-Information "      $($_.Name): $($_.Count) issues" -InformationAction Continue
            }
        } else {
            Write-Information "   ✅ All indentation issues resolved!" -InformationAction Continue
        }
    } catch {
        Write-Information "   ⚠️  Could not run PSScriptAnalyzer verification" -InformationAction Continue
    }
}
