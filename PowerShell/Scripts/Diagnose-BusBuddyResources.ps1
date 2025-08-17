#Requires -Version 7.0
#Requires -Modules Logging

<#
.SYNOPSIS
    Diagnose BusBuddy WPF resource and XAML errors using PowerShell Logging
.DESCRIPTION
    Uses the Logging module to systematically analyze XAML files, resource dictionaries,
    and Syncfusion control usage to identify startup exception causes.
#>

param(
    [string]$LogLevel = 'INFO',
    [string]$LogPath = "logs/busbuddy-diagnostics-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
)

# Import the Logging module
Import-Module Logging -Force

# Configure logging with multiple targets
$logConfig = @{
    Level = $LogLevel
    Targets = @(
        @{
            Type = 'Console'
            Format = '[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level:-7}] {message}'
            ColorMapping = @{
                DEBUG = 'Gray'
                INFO = 'White'
                WARNING = 'Yellow'
                ERROR = 'Red'
            }
        }
        @{
            Type = 'File'
            Path = $LogPath
            Format = '[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level:-7}] [{caller}] {message}'
        }
    )
}

Set-LoggingDefaultLevel -Level $LogLevel
Add-LoggingTarget -Name Console -Configuration $logConfig.Targets[0]
Add-LoggingTarget -Name File -Configuration $logConfig.Targets[1]

Write-Log -Level INFO -Message "🔍 Starting BusBuddy WPF Resource Diagnostics"
Write-Log -Level INFO -Message "Log file: $LogPath"

try {
    # Step 1: Check Syncfusion references and resources
    Write-Log -Level INFO -Message "📦 Analyzing Syncfusion References"

    $projectFile = "BusBuddy.WPF/BusBuddy.WPF.csproj"
    if (Test-Path $projectFile) {
        $content = Get-Content $projectFile -Raw
        $syncfusionRefs = Select-String -InputObject $content -Pattern 'Syncfusion\.' -AllMatches
        Write-Log -Level INFO -Message "Found $($syncfusionRefs.Matches.Count) Syncfusion package references"

        foreach ($match in $syncfusionRefs.Matches) {
            Write-Log -Level DEBUG -Message "  - $($match.Value)"
        }
    } else {
        Write-Log -Level ERROR -Message "Project file not found: $projectFile"
    }

    # Step 2: Analyze App.xaml resource dictionaries
    Write-Log -Level INFO -Message "📋 Analyzing App.xaml Resource Dictionaries"

    $appXaml = "BusBuddy.WPF/App.xaml"
    if (Test-Path $appXaml) {
        $xamlContent = Get-Content $appXaml -Raw

        # Check for ResourceDictionary sources
        $resourceDicts = Select-String -InputObject $xamlContent -Pattern '<ResourceDictionary\s+Source="([^"]+)"' -AllMatches
        Write-Log -Level INFO -Message "Found $($resourceDicts.Matches.Count) ResourceDictionary references"

        foreach ($match in $resourceDicts.Matches) {
            $sourcePath = $match.Groups[1].Value
            $fullPath = "BusBuddy.WPF/$sourcePath"

            if (Test-Path $fullPath) {
                Write-Log -Level INFO -Message "✅ Resource dictionary exists: $sourcePath"

                # Check for specific problematic resources
                $dictContent = Get-Content $fullPath -Raw
                $buttonStyles = Select-String -InputObject $dictContent -Pattern 'x:Key="[^"]*Button[^"]*"' -AllMatches
                Write-Log -Level DEBUG -Message "  Button styles found: $($buttonStyles.Matches.Count)"

                foreach ($buttonMatch in $buttonStyles.Matches) {
                    Write-Log -Level DEBUG -Message "    - $($buttonMatch.Value)"
                }
            } else {
                Write-Log -Level ERROR -Message "❌ Missing resource dictionary: $sourcePath"
            }
        }
    }

    # Step 3: Check for resource key conflicts
    Write-Log -Level INFO -Message "🔑 Checking for Resource Key Conflicts"

    $resourceFiles = Get-ChildItem "BusBuddy.WPF/Resources" -Filter "*.xaml" -Recurse -ErrorAction SilentlyContinue
    $allKeys = @{}

    foreach ($file in $resourceFiles) {
        Write-Log -Level DEBUG -Message "Scanning: $($file.Name)"
        $content = Get-Content $file.FullName -Raw
        $keys = Select-String -InputObject $content -Pattern 'x:Key="([^"]+)"' -AllMatches

        foreach ($match in $keys.Matches) {
            $key = $match.Groups[1].Value
            if ($allKeys.ContainsKey($key)) {
                Write-Log -Level WARNING -Message "⚠️  Duplicate resource key '$key' found in:"
                Write-Log -Level WARNING -Message "    - $($allKeys[$key])"
                Write-Log -Level WARNING -Message "    - $($file.FullName)"
            } else {
                $allKeys[$key] = $file.FullName
            }
        }
    }

    # Step 4: Check specific problem resources
    Write-Log -Level INFO -Message "🎯 Checking Specific Problem Resources"

    $problemResources = @(
        'BusBuddyButtonAdv.Primary',
        'BusBuddy.ButtonAdv.Primary',
        'BusBuddy.ButtonAdv.Base',
        'SyncfusionAccentBrush'
    )

    foreach ($resourceKey in $problemResources) {
        $found = $false
        foreach ($file in $resourceFiles) {
            $content = Get-Content $file.FullName -Raw
            if ($content -match "x:Key=`"$resourceKey`"") {
                Write-Log -Level INFO -Message "✅ Found '$resourceKey' in $($file.Name)"
                $found = $true
            }
        }
        if (-not $found) {
            Write-Log -Level ERROR -Message "❌ Missing resource key: '$resourceKey'"
        }
    }

    # Step 5: Check MainWindow.xaml for problematic references
    Write-Log -Level INFO -Message "🏠 Analyzing MainWindow.xaml"

    $mainWindow = "BusBuddy.WPF/Views/Main/MainWindow.xaml"
    if (Test-Path $mainWindow) {
        $mainContent = Get-Content $mainWindow -Raw

        # Check for StaticResource references
        $staticRefs = Select-String -InputObject $mainContent -Pattern '\{StaticResource\s+([^}]+)\}' -AllMatches
        Write-Log -Level INFO -Message "Found $($staticRefs.Matches.Count) StaticResource references in MainWindow"

        foreach ($match in $staticRefs.Matches) {
            $resourceRef = $match.Groups[1].Value.Trim()
            if ($resourceRef -in $problemResources) {
                Write-Log -Level WARNING -Message "⚠️  MainWindow references problematic resource: '$resourceRef'"
            }
        }
    }

    # Step 6: Check Syncfusion namespace declarations
    Write-Log -Level INFO -Message "🏷️  Checking Syncfusion Namespace Declarations"

    $xamlFiles = Get-ChildItem "BusBuddy.WPF" -Filter "*.xaml" -Recurse
    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw
        $syncfusionNamespaces = Select-String -InputObject $content -Pattern 'xmlns:[^=]+=["''][^"'']*syncfusion[^"'']*["'']' -AllMatches

        if ($syncfusionNamespaces.Matches.Count -gt 0) {
            Write-Log -Level DEBUG -Message "$($file.Name) has $($syncfusionNamespaces.Matches.Count) Syncfusion namespace(s)"
            foreach ($ns in $syncfusionNamespaces.Matches) {
                Write-Log -Level DEBUG -Message "  - $($ns.Value)"
            }
        }
    }

    # Step 7: Generate recommendations
    Write-Log -Level INFO -Message "💡 Generating Recommendations"

    Write-Log -Level INFO -Message ""
    Write-Log -Level INFO -Message "🔧 RECOMMENDED FIXES:"
    Write-Log -Level INFO -Message "1. Remove duplicate resource keys (especially Button styles)"
    Write-Log -Level INFO -Message "2. Ensure all ResourceDictionary files exist and are valid XAML"
    Write-Log -Level INFO -Message "3. Verify Syncfusion license registration in App.xaml.cs"
    Write-Log -Level INFO -Message "4. Check that all StaticResource references have corresponding definitions"
    Write-Log -Level INFO -Message "5. Validate Syncfusion namespace declarations match package versions"

    Write-Log -Level INFO -Message ""
    Write-Log -Level INFO -Message "📁 Log file saved to: $LogPath"
    Write-Log -Level INFO -Message "✅ Diagnostic complete!"

} catch {
    Write-Log -Level ERROR -Message "❌ Diagnostic failed: $($_.Exception.Message)"
    Write-Log -Level ERROR -Message "Stack trace: $($_.ScriptStackTrace)"
}

# Display summary
Write-Log -Level INFO -Message ""
Write-Log -Level INFO -Message "📊 SUMMARY:"
Write-Log -Level INFO -Message "- Check the log file for detailed analysis"
Write-Log -Level INFO -Message "- Focus on resource key conflicts and missing files"
Write-Log -Level INFO -Message "- Verify Syncfusion setup and licensing"
