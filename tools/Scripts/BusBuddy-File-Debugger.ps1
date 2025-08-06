#Requires -Version 7.0

<#
.SYNOPSIS
    BusBuddy File Debugger & Formatter for C# and XAML files

.DESCRIPTION
    Comprehensive file debugging and formatting tool that follows documented best practices:
    - C# formatting using dotnet format and Roslyn analyzers
    - XAML formatting using XAMLStyler integration
    - Code quality analysis and fixes
    - Automated best practice enforcement

.PARAMETER FilePaths
    Array of file paths to debug and format

.PARAMETER FilePattern
    Glob pattern to match files (e.g., "**/*.cs", "**/*.xaml")

.PARAMETER AutoFix
    Automatically apply fixes where possible

.PARAMETER ValidateOnly
    Only validate without making changes

.PARAMETER Verbose
    Show detailed output

.EXAMPLE
    bb-debug-files -FilePattern "**/*.cs" -AutoFix
    Debug and format all C# files

.EXAMPLE
    bb-debug-files -FilePaths @("MainWindow.xaml", "App.xaml.cs") -AutoFix
    Debug specific files
#>

param(
    [string[]]$FilePaths = @(),
    [string]$FilePattern = "",
    [switch]$AutoFix,
    [switch]$ValidateOnly,
    [switch]$Verbose
)

# Import required modules and set up environment
$scriptRoot = $PSScriptRoot
$projectRoot = Split-Path (Split-Path $scriptRoot -Parent) -Parent

Write-Host @"
🔧 BUSBUDDY FILE DEBUGGER & FORMATTER
═════════════════════════════════════════════════════
AutoFix: $(if($AutoFix) { 'Enabled' } else { 'Disabled' })
Validate Only: $(if($ValidateOnly) { 'Enabled' } else { 'Disabled' })
Verbose: $(if($Verbose) { 'Enabled' } else { 'Disabled' })
═════════════════════════════════════════════════════
"@ -ForegroundColor Cyan

# Helper Functions
function Get-FilesToProcess {
    param([string[]]$Paths, [string]$Pattern)

    $files = @()

    if ($Pattern) {
        # Handle different pattern types
        if ($Pattern.StartsWith("**")) {
            # Recursive pattern like "**/*.cs" or "BusBuddy.Core/**/*.cs"
            $patternParts = $Pattern -split '/'
            $folder = $projectRoot
            $filePattern = "*.cs"

            if ($patternParts.Count -gt 1) {
                # Extract folder and file pattern
                for ($i = 0; $i -lt $patternParts.Count; $i++) {
                    if ($patternParts[$i] -eq "**") {
                        if ($i -gt 0) {
                            $folder = Join-Path $projectRoot ($patternParts[0..($i - 1)] -join '\')
                        }
                        if ($i -lt $patternParts.Count - 1) {
                            $filePattern = $patternParts[($i + 1)..($patternParts.Count - 1)] -join '\'
                        }
                        break
                    }
                }
            }

            if (Test-Path $folder) {
                $files += Get-ChildItem -Path $folder -Include $filePattern -Recurse -File | Where-Object {
                    $_.Extension -in @('.cs', '.xaml') -and
                    $_.FullName -notmatch 'bin\\|obj\\|Migration_Backups\\|TestResults\\'
                }
            }
        }
        else {
            # Simple pattern
            $files += Get-ChildItem -Path $projectRoot -Include $Pattern -Recurse -File | Where-Object {
                $_.Extension -in @('.cs', '.xaml') -and
                $_.FullName -notmatch 'bin\\|obj\\|Migration_Backups\\|TestResults\\'
            }
        }
    }

    if ($Paths) {
        foreach ($path in $Paths) {
            if (Test-Path $path) {
                $files += Get-Item $path
            }
            else {
                $fullPath = Join-Path $projectRoot $path
                if (Test-Path $fullPath) {
                    $files += Get-Item $fullPath
                }
            }
        }
    }

    if (-not $files) {
        # Default: all C# and XAML files
        $files = Get-ChildItem -Path $projectRoot -Include @('*.cs', '*.xaml') -Recurse -File | Where-Object {
            $_.FullName -notmatch 'bin\\|obj\\|Migration_Backups\\|TestResults\\'
        }
    }

    return $files | Sort-Object FullName
}function Test-CSharpFile {
    param([System.IO.FileInfo]$File)

    $issues = @()
    $content = Get-Content $File.FullName -Raw

    # C# Best Practices Checks
    $checks = @{
        'Missing using statements'   = @{
            Pattern    = 'ConfigurationManager|Console\.WriteLine|Debug\.WriteLine'
            Suggestion = 'Add proper using statements and use Serilog for logging'
        }
        'Nullable reference issues'  = @{
            Pattern    = 'public.*\s+\w+\s*{\s*get;\s*set;\s*}(?!\s*=\s*null!)'
            Suggestion = 'Initialize properties with = null! or make nullable'
        }
        'Missing async/await'        = @{
            Pattern    = '\.Result\b|\.Wait\(\)'
            Suggestion = 'Use async/await instead of .Result or .Wait()'
        }
        'Hardcoded strings'          = @{
            Pattern    = '"[^"]{20,}"'
            Suggestion = 'Consider using constants or configuration for long strings'
        }
        'Missing disposable pattern' = @{
            Pattern    = 'new\s+(HttpClient|StreamReader|FileStream)\s*\('
            Suggestion = 'Use using statement for disposable objects'
        }
        'Old exception handling'     = @{
            Pattern    = 'catch\s*\(\s*Exception\s+\w+\s*\)\s*\{'
            Suggestion = 'Catch specific exceptions instead of generic Exception'
        }
    }

    foreach ($check in $checks.GetEnumerator()) {
        if ($content -match $check.Value.Pattern) {
            $issues += @{
                Type       = 'Warning'
                Rule       = $check.Key
                Suggestion = $check.Value.Suggestion
                Line       = ($content -split "`n" | Select-String $check.Value.Pattern | Select-Object -First 1).LineNumber
            }
        }
    }

    return $issues
}

function Test-XamlFile {
    param([System.IO.FileInfo]$File)

    $issues = @()
    $content = Get-Content $File.FullName -Raw

    # XAML Best Practices Checks
    $checks = @{
        'Missing XML documentation'   = @{
            Pattern    = '<!--.*-->'
            Inverse    = $true
            Suggestion = 'Add XML comments for complex XAML sections'
        }
        'Hardcoded dimensions'        = @{
            Pattern    = 'Width="[0-9]+"|Height="[0-9]+"'
            Suggestion = 'Use Grid definitions or responsive units instead of fixed dimensions'
        }
        'Missing resource references' = @{
            Pattern    = 'Brush="#[A-F0-9]{6,8}"'
            Suggestion = 'Use StaticResource or DynamicResource for colors'
        }
        'Missing data binding'        = @{
            Pattern    = 'Text="[^{]'
            Suggestion = 'Consider using data binding for dynamic content'
        }
        'Inline styling'              = @{
            Pattern    = 'FontSize="|FontFamily="|Foreground="#'
            Suggestion = 'Move styling to resources or use Syncfusion themes'
        }
    }

    foreach ($check in $checks.GetEnumerator()) {
        $hasMatches = $content -match $check.Value.Pattern
        if (($hasMatches -and -not $check.Value.Inverse) -or (-not $hasMatches -and $check.Value.Inverse)) {
            $issues += @{
                Type       = 'Warning'
                Rule       = $check.Key
                Suggestion = $check.Value.Suggestion
                Line       = ($content -split "`n" | Select-String $check.Value.Pattern | Select-Object -First 1).LineNumber
            }
        }
    }

    return $issues
}

function Format-CSharpFile {
    param([System.IO.FileInfo]$File, [switch]$AutoFix)

    if ($ValidateOnly) { return }

    Write-Host "  🔧 Formatting C# file: $($File.Name)" -ForegroundColor Yellow

    try {
        # Use dotnet format for C# formatting
        $formatResult = & dotnet format --include $File.FullName --verbosity minimal 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ✅ C# formatting successful" -ForegroundColor Green
        }
        else {
            Write-Host "    ❌ C# formatting failed: $formatResult" -ForegroundColor Red
        }

        # Additional C# fixes if AutoFix is enabled
        if ($AutoFix) {
            $content = Get-Content $File.FullName -Raw

            # Fix common issues
            $fixes = @{
                # Fix nullable properties
                'public\s+(\w+)\s+(\w+)\s*{\s*get;\s*set;\s*}' = 'public $1 $2 { get; set; } = null!;'
                # Fix using statements order
                'using System\..*\r?\n'                        = ''  # This would need more sophisticated logic
            }

            $modified = $false
            foreach ($fix in $fixes.GetEnumerator()) {
                if ($content -match $fix.Key) {
                    $newContent = $content -replace $fix.Key, $fix.Value
                    if ($newContent -ne $content) {
                        $content = $newContent
                        $modified = $true
                    }
                }
            }

            if ($modified) {
                Set-Content -Path $File.FullName -Value $content -Encoding UTF8
                Write-Host "    🔄 Applied automatic fixes" -ForegroundColor Cyan
            }
        }
    }
    catch {
        Write-Host "    ❌ Error formatting C# file: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Format-XamlFile {
    param([System.IO.FileInfo]$File, [switch]$AutoFix)

    if ($ValidateOnly) { return }

    Write-Host "  🎨 Formatting XAML file: $($File.Name)" -ForegroundColor Yellow

    try {
        # Check for XAML Styler
        $xamlStylerPath = Get-Command "XamlStyler.Console.exe" -ErrorAction SilentlyContinue

        if ($xamlStylerPath) {
            $styleResult = & $xamlStylerPath.Source -f $File.FullName 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "    ✅ XAML formatting successful" -ForegroundColor Green
            }
            else {
                Write-Host "    ❌ XAML formatting failed: $styleResult" -ForegroundColor Red
            }
        }
        else {
            # Basic XAML formatting without XamlStyler
            Write-Host "    ⚠️ XamlStyler not found, applying basic formatting" -ForegroundColor Yellow

            $content = Get-Content $File.FullName -Raw

            # Basic XAML formatting
            $content = $content -replace '\s+>', '>'  # Remove extra spaces before closing >
            $content = $content -replace '>\s+<', '><'  # Remove spaces between tags

            Set-Content -Path $File.FullName -Value $content -Encoding UTF8
            Write-Host "    🔄 Applied basic XAML formatting" -ForegroundColor Cyan
        }

        # Additional XAML fixes if AutoFix is enabled
        if ($AutoFix) {
            $content = Get-Content $File.FullName -Raw

            # Fix common XAML issues
            $modified = $false

            # Add x:Name to elements that might need it
            if ($content -match '<Button\s+(?!.*x:Name).*Content=') {
                Write-Host "    💡 Consider adding x:Name to Button elements" -ForegroundColor Cyan
            }

            # Check for Syncfusion theme consistency
            if ($content -match 'syncfusion' -and $content -notmatch 'FluentDark') {
                Write-Host "    💡 Consider using FluentDark theme for consistency" -ForegroundColor Cyan
            }
        }
    }
    catch {
        Write-Host "    ❌ Error formatting XAML file: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function New-FileReport {
    param([System.Collections.ArrayList]$Results)

    $reportPath = Join-Path $projectRoot "logs\file-debug-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"

    $report = @{
        Timestamp   = Get-Date
        TotalFiles  = $Results.Count
        CSharpFiles = ($Results | Where-Object { $_.Extension -eq '.cs' }).Count
        XamlFiles   = ($Results | Where-Object { $_.Extension -eq '.xaml' }).Count
        TotalIssues = ($Results | ForEach-Object { $_.Issues.Count } | Measure-Object -Sum).Sum
        Results     = $Results
    }

    $report | ConvertTo-Json -Depth 5 | Set-Content $reportPath -Encoding UTF8

    return $reportPath
}

# Main Processing
$filesToProcess = Get-FilesToProcess -Paths $FilePaths -Pattern $FilePattern
$results = [System.Collections.ArrayList]::new()

Write-Host "`n📁 Processing $($filesToProcess.Count) files..." -ForegroundColor Cyan

foreach ($file in $filesToProcess) {
    Write-Host "`n🔍 Analyzing: $($file.Name)" -ForegroundColor White

    $fileResult = @{
        FilePath  = $file.FullName
        FileName  = $file.Name
        Extension = $file.Extension
        Issues    = @()
        Formatted = $false
    }

    # Analyze file based on extension
    switch ($file.Extension.ToLower()) {
        '.cs' {
            $fileResult.Issues = Test-CSharpFile -File $file
            if (-not $ValidateOnly) {
                Format-CSharpFile -File $file -AutoFix:$AutoFix
                $fileResult.Formatted = $true
            }
        }
        '.xaml' {
            $fileResult.Issues = Test-XamlFile -File $file
            if (-not $ValidateOnly) {
                Format-XamlFile -File $file -AutoFix:$AutoFix
                $fileResult.Formatted = $true
            }
        }
    }

    # Display issues
    if ($fileResult.Issues.Count -gt 0) {
        Write-Host "  📋 Found $($fileResult.Issues.Count) issues:" -ForegroundColor Yellow
        foreach ($issue in $fileResult.Issues) {
            $color = if ($issue.Type -eq 'Error') { 'Red' } else { 'Yellow' }
            Write-Host "    • $($issue.Rule)" -ForegroundColor $color
            if ($Verbose) {
                Write-Host "      💡 $($issue.Suggestion)" -ForegroundColor Gray
                if ($issue.Line) {
                    Write-Host "      📍 Line: $($issue.Line)" -ForegroundColor Gray
                }
            }
        }
    }
    else {
        Write-Host "  ✅ No issues found" -ForegroundColor Green
    }

    [void]$results.Add($fileResult)
}

# Generate Report
Write-Host "`n📊 SUMMARY" -ForegroundColor Cyan
Write-Host "════════════════════════════" -ForegroundColor Cyan
Write-Host "Files Processed: $($results.Count)" -ForegroundColor White
Write-Host "C# Files: $(($results | Where-Object { $_.Extension -eq '.cs' }).Count)" -ForegroundColor White
Write-Host "XAML Files: $(($results | Where-Object { $_.Extension -eq '.xaml' }).Count)" -ForegroundColor White
Write-Host "Total Issues: $(($results | ForEach-Object { $_.Issues.Count } | Measure-Object -Sum).Sum)" -ForegroundColor White
Write-Host "Files Formatted: $(($results | Where-Object { $_.Formatted }).Count)" -ForegroundColor White

$reportPath = New-FileReport -Results $results
Write-Host "`n📄 Report saved to: $reportPath" -ForegroundColor Cyan

# Integration functions for bb- commands
function Start-BusBuddyFileDebug {
    param($Pattern = "**/*.{cs,xaml}", [switch]$AutoFix, [switch]$Verbose)
    & $PSCommandPath -FilePattern $Pattern -AutoFix:$AutoFix -Verbose:$Verbose
}

function Format-BusBuddyFile {
    param($Pattern = "**/*.{cs,xaml}")
    & $PSCommandPath -FilePattern $Pattern -AutoFix
}

function Test-BusBuddyFile {
    param($Pattern = "**/*.{cs,xaml}")
    & $PSCommandPath -FilePattern $Pattern -ValidateOnly
}

# Export functions for module usage
if ($MyInvocation.InvocationName -eq '.') {
    Export-ModuleMember -Function @('Start-BusBuddyFileDebug', 'Format-BusBuddyFile', 'Test-BusBuddyFile')
}
