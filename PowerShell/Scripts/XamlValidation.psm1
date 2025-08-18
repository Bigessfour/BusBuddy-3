#Requires -Version 7.5

<#
.SYNOPSIS
    Enhanced XAML Validation Module - PowerShell 7.5.2 Compliant
.DESCRIPTION
    Professional XAML validation module for BusBuddy project files.
    Validates Syncfusion control usage, provides detailed error reports,
    and supports CI/CD integration with JSON output.
.NOTES
    File Name      : XamlValidation.psm1
    Author         : BusBuddy Development Team
    Prerequisite   : PowerShell 7.5.2+, Serilog
    Copyright      : (c) 2025 BusBuddy Project
    Version        : 2.0 (Enhanced for Greenfield Reset)
#>

function Invoke-ComprehensiveXamlValidation {
    <#
    .SYNOPSIS
        Comprehensive XAML validation for BusBuddy project files
    .DESCRIPTION
        Validates XAML files in BusBuddy.WPF, ensuring Syncfusion control usage,
        proper namespaces, and structural integrity. Outputs detailed reports
        and JSON for CI/CD pipelines.
    .PARAMETER ProjectPath
        Path to the BusBuddy project directory. Defaults to current directory.
    .PARAMETER OutputJson
        If specified, outputs validation results as JSON to the specified file.
    .EXAMPLE
        Invoke-ComprehensiveXamlValidation
    .EXAMPLE
        Invoke-ComprehensiveXamlValidation -ProjectPath "C:\Projects\BusBuddy" -OutputJson "validation.json"
    .NOTES
        Enhanced for Syncfusion compliance, parallel processing, and CI/CD integration.
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateScript({ Test-Path $_ -PathType Container })]
        [string]$ProjectPath = (Get-Location),
        [Parameter()]
        [string]$OutputJson
    )

    begin {
        Import-Module Serilog
        $log = [Serilog.Log]::ForContext('SourceContext', 'Invoke-ComprehensiveXamlValidation')
        $log.Information("🔍 Starting Comprehensive XAML Validation for {ProjectPath}", $ProjectPath)
        $syncfusionNamespaces = @(
            "clr-namespace:Syncfusion.UI.Xaml.Controls.Input;assembly=Syncfusion.SfInput.Wpf",
            "clr-namespace:Syncfusion.UI.Xaml.Controls.DataGrid;assembly=Syncfusion.SfGrid.Wpf",
            "clr-namespace:Syncfusion.UI.Xaml.Controls.Charts;assembly=Syncfusion.SfChart.Wpf"
        )
    }

    process {
        try {
            $xamlPath = Join-Path $ProjectPath "BusBuddy.WPF"
            if (-not (Test-Path $xamlPath)) {
                $log.Warning("BusBuddy.WPF directory not found at: {Path}", $xamlPath)
                Write-Warning "BusBuddy.WPF directory not found at: $xamlPath"
                return $null
            }
            $xamlFiles = Get-ChildItem -Path $xamlPath -Filter "*.xaml" -Recurse
            $log.Information("Found {Count} XAML files", $xamlFiles.Count)
            $validFiles = @()
            $invalidFiles = @()
            $resourceDictionaries = @()
            $syncfusionViolations = @()
            $results = $xamlFiles | ForEach-Object -Parallel {
                $log = [Serilog.Log]::ForContext('SourceContext', 'Invoke-ComprehensiveXamlValidation')
                $file = $_
                $result = [PSCustomObject]@{
                    File                 = $file
                    IsValid              = $true
                    Errors               = @()
                    IsResourceDictionary = $false
                    SyncfusionIssues     = @()
                }
                $content = Get-Content $file.FullName -Raw
                if ($content -match '<ResourceDictionary') {
                    $result.IsResourceDictionary = $true
                    $log.Information("📄 {File}: Resource Dictionary (no code-behind needed)", $file.Name)
                    return $result
                }
                try {
                    $xamlDoc = [System.Xml.Linq.XDocument]::Parse($content)
                    $result.IsValid = $true
                }
                catch {
                    $result.IsValid = $false
                    $result.Errors += [PSCustomObject]@{
                        Line       = $_.Exception.LineNumber
                        Message    = $_.Exception.Message
                        Suggestion = "Check XML syntax; ensure well-formed XAML."
                    }
                    $log.Error("📄 {File}: Invalid XAML - {Message}", $file.Name, $_.Exception.Message)
                }
                $wpfControls = @("Button", "TextBox", "ListView", "ComboBox")
                foreach ($control in $wpfControls) {
                    if ($content -match "<$control\s") {
                        $line = ($content -split '\n' | Select-String "<$control\s").LineNumber
                        $result.SyncfusionIssues += [PSCustomObject]@{
                            Line       = $line
                            Message    = "Standard WPF $control detected; use Syncfusion equivalent (e.g., Sf$control)."
                            Suggestion = "Replace with Syncfusion.$control (e.g., SfButton, SfTextBox)."
                        }
                        $log.Warning("📄 {File}:{Line}: WPF {Control} found", $file.Name, $line, $control)
                    }
                }
                $hasSyncfusion = $false
                foreach ($ns in $using:syncfusionNamespaces) {
                    if ($content -match $ns) {
                        $hasSyncfusion = $true
                        break
                    }
                }
                if (-not $hasSyncfusion -and -not $result.IsResourceDictionary) {
                    $result.SyncfusionIssues += [PSCustomObject]@{
                        Line       = 1
                        Message    = "Missing Syncfusion namespace."
                        Suggestion = "Add xmlns:syncfusion='$($using:syncfusionNamespaces[0])' to root element."
                    }
                    $log.Warning("📄 {File}: Missing Syncfusion namespace", $file.Name)
                }
                return $result
            } -ThrottleLimit 4
            foreach ($result in $results) {
                if ($result.IsResourceDictionary) {
                    $resourceDictionaries += $result.File
                    continue
                }
                if ($result.IsValid -and $result.SyncfusionIssues.Count -eq 0) {
                    $validFiles += $result.File
                }
                else {
                    $invalidFiles += [PSCustomObject]@{
                        File             = $result.File
                        Errors           = $result.Errors
                        SyncfusionIssues = $result.SyncfusionIssues
                    }
                }
                $syncfusionViolations += $result.SyncfusionIssues
            }
            $summary = [PSCustomObject]@{
                TotalFiles           = $xamlFiles.Count
                ValidFiles           = $validFiles.Count
                InvalidFiles         = $invalidFiles.Count
                ResourceDictionaries = $resourceDictionaries.Count
                SyncfusionViolations = $syncfusionViolations.Count
                InvalidFileDetails   = $invalidFiles
                ProjectPath          = $ProjectPath
                ValidationDate       = Get-Date
            }
            $log.Information("📊 XAML Validation Summary: {Summary}", $summary)
            Write-Information "📊 XAML Validation Summary" -InformationAction Continue
            Write-Information ("=" * 60) -InformationAction Continue
            Write-Information "Total XAML files: $($summary.TotalFiles)" -InformationAction Continue
            Write-Information "Resource dictionaries: $($summary.ResourceDictionaries)" -InformationAction Continue
            Write-Information "Valid XAML files: $($summary.ValidFiles)" -InformationAction Continue
            Write-Information "Invalid XAML files: $($summary.InvalidFiles)" -InformationAction Continue
            Write-Information "Syncfusion violations: $($summary.SyncfusionViolations)" -InformationAction Continue
            if ($invalidFiles.Count -gt 0) {
                Write-Information "" -InformationAction Continue
                Write-Warning "🔧 Files requiring fixes:"
                foreach ($invalid in $invalidFiles) {
                    Write-Warning "   • $($invalid.File.Name)"
                    foreach ($error in $invalid.Errors) {
                        Write-Error "      Line $($error.Line): $($error.Message) - $($error.Suggestion)" -ErrorAction Continue
                    }
                    foreach ($issue in $invalid.SyncfusionIssues) {
                        Write-Warning "      Line $($issue.Line): $($issue.Message) - $($issue.Suggestion)"
                    }
                }
            }
            if ($OutputJson) {
                $summary | ConvertTo-Json -Depth 5 | Out-File -FilePath $OutputJson -Encoding utf8
                $log.Information("📄 JSON report written to {Path}", $OutputJson)
            }
            Write-Output $summary
            Write-Output "\nXAML File Validation Results:"
            $results | ForEach-Object {
                $status = if ($_.IsValid -and $_.SyncfusionIssues.Count -eq 0) { 'Valid' } else { 'Invalid' }
                $errors = if ($_.Errors.Count -gt 0) { ($_.Errors | ForEach-Object { $_.Message }) -join '; ' } else { '' }
                $syncfusion = if ($_.SyncfusionIssues.Count -gt 0) { ($_.SyncfusionIssues | ForEach-Object { $_.Message }) -join '; ' } else { '' }
                Write-Output ("{0,-40} {1,-8} {2,-30} {3}" -f $_.File, $status, $errors, $syncfusion)
            }
            $results | ConvertTo-Json -Depth 5 | Out-File -FilePath "XamlValidationResults.json" -Encoding utf8
            Write-Output "\nDetailed results exported to XamlValidationResults.json"
        }
        catch {
            $log.Error("XAML validation failed: {Message}", $_.Exception.Message)
            Write-Error "XAML validation failed: $($_.Exception.Message)" -ErrorAction Stop
        }
    }
    end {
        $log.Information("XAML validation completed")
    }
}

function Test-BusBuddyXml {
    <#
    .SYNOPSIS
        Validates a single XAML file's XML structure
    .PARAMETER FilePath
        Path to the XAML file to validate
    .EXAMPLE
        Test-BusBuddyXml -FilePath "C:\Projects\BusBuddy\BusBuddy.WPF\StudentForm.xaml"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ -PathType Leaf })]
        [string]$FilePath
    )
    try {
        $content = Get-Content $FilePath -Raw
        $xamlDoc = [System.Xml.Linq.XDocument]::Parse($content)
        return [PSCustomObject]@{
            IsValid = $true
            Errors  = @()
        }
    }
    catch {
        return [PSCustomObject]@{
            IsValid = $false
            Errors  = @([PSCustomObject]@{
                    Line       = $_.Exception.LineNumber
                    Message    = $_.Exception.Message
                    Suggestion = "Check XML syntax; ensure well-formed XAML."
                })
        }
    }
}

Export-ModuleMember -Function Invoke-ComprehensiveXamlValidation, Test-BusBuddyXml
