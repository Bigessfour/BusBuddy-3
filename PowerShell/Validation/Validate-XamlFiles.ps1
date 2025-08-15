#Requires -Version 7.5

<#
.SYNOPSIS
    Enhanced XAML Validation Module - PowerShell 7.5.2 Compliant with Serilog

.DESCRIPTION
    Professional XAML validation module for BusBuddy project files.
    Validates Syncfusion control usage, provides detailed error reports,
    and supports CI/CD integration with JSON output. Uses Serilog for
    structured logging following BusBuddy project standards.

.NOTES
    File Name      : Validate-XamlFiles.ps1
    Author         : BusBuddy Development Team
    Prerequisite   : PowerShell 7.5.2+, Serilog.dll
    Copyright      : (c) 2025 BusBuddy Project
    Version        : 2.1 (Enhanced Serilog Integration)
#>

# Initialize Serilog for PowerShell module following BusBuddy patterns
if (-not $script:SerilogInitialized) {
    try {
        # Try to load Serilog from common locations
        $serilogFound = $false
        $serilogPaths = @(
            (Join-Path $PSScriptRoot "..\..\BusBuddy.WPF\bin\Debug\net9.0-windows\Serilog.dll"),
            (Join-Path $env:USERPROFILE ".nuget\packages\serilog\3.1.1\lib\net7.0\Serilog.dll"),
            (Join-Path $env:USERPROFILE ".nuget\packages\serilog\4.3.0\lib\net9.0\Serilog.dll")
        )

        foreach ($path in $serilogPaths) {
            if (Test-Path $path) {
                Add-Type -Path $path -ErrorAction SilentlyContinue
                $serilogFound = $true
                break
            }
        }

        if ($serilogFound) {
            # Try to configure Serilog logger
            try {
                # Use simplified Serilog configuration that works with PowerShell
                $script:SerilogInitialized = $true
                Write-Output "✅ Serilog assembly loaded for XAML validation module"
            }
            catch {
                Write-Warning "Serilog assembly loaded but configuration failed: $($_.Exception.Message)"
                $script:SerilogInitialized = $false
            }
        } else {
            Write-Warning "Serilog assembly not found. Using Write-Output fallback for logging."
            $script:SerilogInitialized = $false
        }
    }
    catch {
        Write-Warning "Failed to initialize Serilog: $($_.Exception.Message). Using Write-Output fallback."
        $script:SerilogInitialized = $false
    }
}

# Helper function to log with simplified approach
function Write-ValidationLog {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Information", "Warning", "Error", "Debug", "Verbose")]
        [string]$Level,

        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [hashtable]$Properties = @{},

        [Parameter()]
        [System.Exception]$Exception
    )

    # Simple message formatting with properties
    $formattedMessage = $Message
    if ($Properties.Count -gt 0) {
        foreach ($key in $Properties.Keys) {
            $formattedMessage = $formattedMessage -replace "{$key}", $Properties[$key]
        }
    }

    if ($Exception) {
        $formattedMessage += " Exception: $($Exception.Message)"
    }

    # Use PowerShell output streams (Microsoft-compliant approach)
    switch ($Level) {
        "Information" { Write-Output $formattedMessage }
        "Warning" { Write-Warning $formattedMessage }
        "Error" { Write-Error $formattedMessage -ErrorAction Continue }
        "Debug" { Write-Debug $formattedMessage }
        "Verbose" { Write-Verbose $formattedMessage }
    }
}

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
        Write-ValidationLog -Level "Information" -Message "🔍 Starting Comprehensive XAML Validation for {ProjectPath}" -Properties @{ ProjectPath = $ProjectPath }

        # Syncfusion namespace patterns for validation
        $syncfusionNamespaces = @(
            "http://schemas.syncfusion.com/wpf",
            "clr-namespace:Syncfusion.UI.Xaml.Controls.Input;assembly=Syncfusion.SfInput.Wpf",
            "clr-namespace:Syncfusion.UI.Xaml.Controls.DataGrid;assembly=Syncfusion.SfGrid.Wpf",
            "clr-namespace:Syncfusion.UI.Xaml.Controls.Charts;assembly=Syncfusion.SfChart.Wpf"
        )

        # Standard WPF controls that should be replaced with Syncfusion equivalents
        $wpfControlsToReplace = @{
            "Button" = "SfButton"
            "TextBox" = "SfTextBox"
            "ComboBox" = "SfComboBox"
            "DataGrid" = "SfDataGrid"
            "ListView" = "SfListView"
            "TreeView" = "SfTreeView"
            "DatePicker" = "SfDatePicker"
            "Slider" = "SfRangeSlider"
        }
    }

    process {
        try {
            $xamlPath = Join-Path $ProjectPath "BusBuddy.WPF"
            if (-not (Test-Path $xamlPath)) {
                Write-ValidationLog -Level "Warning" -Message "BusBuddy.WPF directory not found at: {Path}" -Properties @{ Path = $xamlPath }
                Write-Warning "BusBuddy.WPF directory not found at: $xamlPath"
                return $null
            }

            $startTime = Get-Date
            $xamlFiles = Get-ChildItem -Path $xamlPath -Filter "*.xaml" -Recurse
            Write-ValidationLog -Level "Information" -Message "Found {Count} XAML files for validation" -Properties @{ Count = $xamlFiles.Count; Path = $xamlPath }

            $validFiles = @()
            $invalidFiles = @()
            $resourceDictionaries = @()
            $syncfusionViolations = @()

            # Determine a hardware-aware ThrottleLimit for parallelism
            # Microsoft docs: ForEach-Object -Parallel supports -ThrottleLimit (PowerShell 7+)
            # Ref: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/foreach-object
            try {
                $logical = [int]([Environment]::ProcessorCount)
                $cores = $null
                try { $cores = (Get-CimInstance Win32_Processor | Select-Object -Expand NumberOfCores -First 1) } catch { Write-Warning ("Failed to read CPU cores — {0}" -f $_.Exception.Message) }
                $memGiB = $null
                try { $memGiB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1GB, 1) } catch { Write-Warning ("Failed to read memory — {0}" -f $_.Exception.Message) }

                # Base throttle: 75% of logical processors, min 2, max 12 (conservative default)
                $computed = if ($logical -gt 0) { [math]::Max([math]::Min([int][math]::Floor($logical * 0.75), 12), 2) } else { 4 }

                # Optional memory guard: if memory < 8 GiB, cap at 4 to avoid contention
                if ($memGiB -and $memGiB -lt 8 -and $computed -gt 4) { $computed = 4 }

                # Allow env override (e.g., $env:BB_ThrottleLimit)
                $throttleLimit = $computed
                if ($env:BB_ThrottleLimit -and $env:BB_ThrottleLimit -match '^[0-9]+$') {
                    $throttleLimit = [int]$env:BB_ThrottleLimit
                }

                Write-ValidationLog -Level "Information" -Message "Using ThrottleLimit {Value} (Logical={Logical}, Cores={Cores}, MemGiB={MemGiB})" -Properties @{
                    Value = $throttleLimit; Logical = $logical; Cores = ($cores ?? 'n/a'); MemGiB = ($memGiB ?? 'n/a')
                }
            }
            catch {
                Write-ValidationLog -Level "Warning" -Message "Failed to compute hardware-aware ThrottleLimit. Falling back to 4." -Properties @{}
                $throttleLimit = 4
            }

            # Process each XAML file with enhanced Serilog logging
            $results = $xamlFiles | ForEach-Object -Parallel {
                $file = $_
                $result = [PSCustomObject]@{
                    File = $file
                    IsValid = $true
                    Errors = @()
                    IsResourceDictionary = $false
                    SyncfusionIssues = @()
                    HasSyncfusionNamespace = $false
                    StandardWpfControls = @()
                }

                try {
                    $content = Get-Content $file.FullName -Raw -ErrorAction Stop

                    # Check if it's a resource dictionary
                    if ($content -match '<ResourceDictionary') {
                        $result.IsResourceDictionary = $true
                        return $result
                    }

                    # Validate XAML structure using System.Xml.Linq
                    try {
                        $xamlDoc = [System.Xml.Linq.XDocument]::Parse($content)
                        $result.IsValid = $true
                    }
                    catch {
                        $result.IsValid = $false
                        $result.Errors += [PSCustomObject]@{
                            Line = if ($_.Exception.LineNumber) { $_.Exception.LineNumber } else { 1 }
                            Message = $_.Exception.Message
                            Suggestion = "Check XML syntax; ensure well-formed XAML."
                            ErrorType = "XmlSyntax"
                        }
                    }

                    # Check for Syncfusion namespace presence
                    $syncfusionNamespaces = $using:syncfusionNamespaces
                    foreach ($ns in $syncfusionNamespaces) {
                        if ($content -match [regex]::Escape($ns)) {
                            $result.HasSyncfusionNamespace = $true
                            break
                        }
                    }

                    # Check for standard WPF controls that should be Syncfusion
                    $wpfControlsToReplace = $using:wpfControlsToReplace
                    foreach ($control in $wpfControlsToReplace.Keys) {
                        $pattern = "<$control[\s>]"
                        if ($content -match $pattern) {
                            $lines = $content -split '\n'
                            for ($i = 0; $i -lt $lines.Count; $i++) {
                                if ($lines[$i] -match $pattern) {
                                    $result.StandardWpfControls += $control
                                    $result.SyncfusionIssues += [PSCustomObject]@{
                                        Line = $i + 1
                                        Message = "Standard WPF $control detected; should use Syncfusion $($wpfControlsToReplace[$control])"
                                        Suggestion = "Replace <$control> with <syncfusion:$($wpfControlsToReplace[$control])>"
                                        ErrorType = "ControlUpgrade"
                                        ControlType = $control
                                        RecommendedControl = $wpfControlsToReplace[$control]
                                    }
                                }
                            }
                        }
                    }

                    # Check for missing Syncfusion namespace when controls are found
                    if ($result.StandardWpfControls.Count -gt 0 -and -not $result.HasSyncfusionNamespace) {
                        $result.SyncfusionIssues += [PSCustomObject]@{
                            Line = 1
                            Message = "Missing Syncfusion namespace declaration"
                            Suggestion = "Add xmlns:syncfusion='http://schemas.syncfusion.com/wpf' to root element"
                            ErrorType = "MissingNamespace"
                        }
                    }
                }
                catch {
                    $result.IsValid = $false
                    $result.Errors += [PSCustomObject]@{
                        Line = 1
                        Message = "Failed to read or process file: $($_.Exception.Message)"
                        Suggestion = "Check file permissions and encoding"
                        ErrorType = "FileAccess"
                    }
                }

                return $result
            } -ThrottleLimit $throttleLimit

            # Process results and categorize files with enhanced logging
            foreach ($result in $results) {
                $fileName = $result.File.Name

                if ($result.IsResourceDictionary) {
                    $resourceDictionaries += $result.File
                    Write-ValidationLog -Level "Debug" -Message "📄 {FileName}: Resource Dictionary (no validation needed)" -Properties @{ FileName = $fileName }
                    continue
                }

                if ($result.IsValid -and $result.SyncfusionIssues.Count -eq 0) {
                    $validFiles += $result.File
                    Write-ValidationLog -Level "Debug" -Message "✅ {FileName}: Valid XAML with proper Syncfusion usage" -Properties @{ FileName = $fileName }
                } else {
                    $invalidFiles += [PSCustomObject]@{
                        File = $result.File
                        Errors = $result.Errors
                        SyncfusionIssues = $result.SyncfusionIssues
                        HasSyncfusionNamespace = $result.HasSyncfusionNamespace
                        StandardWpfControls = $result.StandardWpfControls
                    }

                    # Log specific issues found
                    if ($result.Errors.Count -gt 0) {
                        Write-ValidationLog -Level "Error" -Message "❌ {FileName}: {ErrorCount} XAML syntax errors" -Properties @{
                            FileName = $fileName
                            ErrorCount = $result.Errors.Count
                            Errors = ($result.Errors | ForEach-Object { $_.Message }) -join "; "
                        }
                    }

                    if ($result.SyncfusionIssues.Count -gt 0) {
                        Write-ValidationLog -Level "Warning" -Message "⚠️ {FileName}: {IssueCount} Syncfusion compliance issues" -Properties @{
                            FileName = $fileName
                            IssueCount = $result.SyncfusionIssues.Count
                            Issues = ($result.SyncfusionIssues | ForEach-Object { $_.Message }) -join "; "
                        }
                    }
                }

                $syncfusionViolations += $result.SyncfusionIssues
            }

            # Generate comprehensive summary with structured logging
            $summary = [PSCustomObject]@{
                TotalFiles = $xamlFiles.Count
                ValidFiles = $validFiles.Count
                InvalidFiles = $invalidFiles.Count
                ResourceDictionaries = $resourceDictionaries.Count
                SyncfusionViolations = $syncfusionViolations.Count
                InvalidFileDetails = $invalidFiles
                ProjectPath = $ProjectPath
                ValidationDate = Get-Date
                ValidationDurationMs = (Get-Date).Subtract($startTime).TotalMilliseconds
            }

            # Log comprehensive summary
            Write-ValidationLog -Level "Information" -Message "📊 XAML Validation Summary - {TotalFiles} files processed" -Properties @{
                TotalFiles = $summary.TotalFiles
                ValidFiles = $summary.ValidFiles
                InvalidFiles = $summary.InvalidFiles
                ResourceDictionaries = $summary.ResourceDictionaries
                SyncfusionViolations = $summary.SyncfusionViolations
                ProjectPath = $summary.ProjectPath
                DurationMs = $summary.ValidationDurationMs
            }
            # Display results with proper output streams (following PowerShell 7.5.2 standards)
            Write-Output "📊 XAML Validation Summary"
            Write-Output ("=" * 60)
            Write-Output "Total XAML files: $($summary.TotalFiles)"
            Write-Output "Resource dictionaries: $($summary.ResourceDictionaries)"
            Write-Output "Valid XAML files: $($summary.ValidFiles)"
            Write-Output "Invalid XAML files: $($summary.InvalidFiles)"
            Write-Output "Syncfusion violations: $($summary.SyncfusionViolations)"
            Write-Output "Validation duration: $([math]::Round($summary.ValidationDurationMs))ms"

            if ($invalidFiles.Count -gt 0) {
                Write-Output ""
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

            # CI/CD JSON output with enhanced structure
            if ($OutputJson) {
                $jsonOutput = @{
                    Summary = $summary
                    DetailedResults = $results | ForEach-Object {
                        @{
                            FileName = $_.File.Name
                            FilePath = $_.File.FullName
                            IsValid = $_.IsValid
                            IsResourceDictionary = $_.IsResourceDictionary
                            HasSyncfusionNamespace = $_.HasSyncfusionNamespace
                            StandardWpfControls = $_.StandardWpfControls
                            Errors = $_.Errors
                            SyncfusionIssues = $_.SyncfusionIssues
                        }
                    }
                    ValidationMetadata = @{
                        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
                        ModuleVersion = "2.1"
            SerilogEnabled = $global:SerilogInitialized
                        ValidationDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                    }
        }

                $jsonOutput | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputJson -Encoding utf8
                Write-ValidationLog -Level "Information" -Message "📄 JSON report written to {Path}" -Properties @{ Path = $OutputJson }
                Write-Output "JSON report saved to: $OutputJson"
            }

            return $summary
        }
        catch {
            $errorMessage = "XAML validation failed: $($_.Exception.Message)"
            Write-ValidationLog -Level "Error" -Message $errorMessage -Exception $_.Exception
            Write-Error $errorMessage -ErrorAction Stop
        }
    }

    end {
        Write-ValidationLog -Level "Information" -Message "XAML validation module completed successfully"
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
        $content = Get-Content $FilePath -Raw -ErrorAction Stop
        $xamlDoc = [System.Xml.Linq.XDocument]::Parse($content)

        Write-ValidationLog -Level "Debug" -Message "Successfully validated XML structure for {FilePath}" -Properties @{ FilePath = $FilePath }

        return [PSCustomObject]@{
            IsValid = $true
            Errors = @()
            FilePath = $FilePath
            ValidationDate = Get-Date
        }
    } catch {
        $errorMessage = "XML validation failed for $FilePath`: $($_.Exception.Message)"
        Write-ValidationLog -Level "Error" -Message $errorMessage -Exception $_.Exception

        return [PSCustomObject]@{
            IsValid = $false
            Errors = @([PSCustomObject]@{
                Line = if ($_.Exception.LineNumber) { $_.Exception.LineNumber } else { 1 }
                Message = $_.Exception.Message
                Suggestion = "Check XML syntax; ensure well-formed XAML."
                ErrorType = "XmlSyntax"
            })
            FilePath = $FilePath
            ValidationDate = Get-Date
        }
    }
}

# Test the functions if script is run directly
if ($MyInvocation.InvocationName -ne '.') {
    # Script is being executed directly, not dot-sourced
    Write-Output "🔍 Starting BusBuddy XAML Validation..."
    $result = Invoke-ComprehensiveXamlValidation

    if ($result) {
        Write-Output ""
        Write-Output "📋 Validation completed. Summary:"
        Write-Output "   Total files: $($result.TotalFiles)"
        Write-Output "   Valid: $($result.ValidFiles)"
        Write-Output "   Invalid: $($result.InvalidFiles)"
        Write-Output "   Resource Dictionaries: $($result.ResourceDictionaries)"
        Write-Output "   Syncfusion Violations: $($result.SyncfusionViolations)"
    }
}


