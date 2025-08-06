#Requires -Version 7.5

<#
.SYNOPSIS
    XAML Validation Module - PowerShell 7.5.2 Compliant

.DESCRIPTION
    Professional XAML validation module for BusBuddy project files.
    Provides comprehensive XAML validation and reporting capabilities.
    Follows Microsoft PowerShell 7.5.2 guidelines and best practices.

.NOTES
    File Name      : XamlValidation.psm1
    Author         : BusBuddy Development Team
    Prerequisite   : PowerShell 7.5+ (Microsoft Standard)
    Copyright      : (c) 2025 BusBuddy Project
    PS Version     : PowerShell 7.5.2 Compliant
#>

function Invoke-ComprehensiveXamlValidation {
    <#
    .SYNOPSIS
        Comprehensive XAML validation for BusBuddy project files

    .DESCRIPTION
        Validates all XAML files in the BusBuddy project and provides detailed reports
        of issues that need to be fixed for proper XAML compilation.

    .PARAMETER ProjectPath
        Path to the BusBuddy project directory. Defaults to current directory.

    .EXAMPLE
        Invoke-ComprehensiveXamlValidation

    .EXAMPLE
        Invoke-ComprehensiveXamlValidation -ProjectPath "C:\Projects\BusBuddy"

    .NOTES
        Uses Microsoft PowerShell 7.5.2 standards for output and error handling
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateScript({ Test-Path $_ -PathType Container })]
        [string]$ProjectPath = (Get-Location)
    )

    begin {
        Write-Verbose "Starting XAML validation for project: $ProjectPath"
        Write-Information "🔍 Starting Comprehensive XAML Validation..." -InformationAction Continue
        Write-Information ("=" * 60) -InformationAction Continue
    }

    process {
        try {
            # Get all XAML files
            $xamlPath = Join-Path $ProjectPath "BusBuddy.WPF"
            if (-not (Test-Path $xamlPath)) {
                Write-Warning "BusBuddy.WPF directory not found at: $xamlPath"
                return $null
            }

            $xamlFiles = Get-ChildItem -Path $xamlPath -Filter "*.xaml" -Recurse

            $validFiles = @()
            $invalidFiles = @()
            $resourceDictionaries = @()

            foreach ($file in $xamlFiles) {
                Write-Information "📄 Validating: $($file.Name)" -InformationAction Continue

                # Check if it's a resource dictionary
                $content = Get-Content $file.FullName -Raw
                if ($content -match '<ResourceDictionary') {
                    $resourceDictionaries += $file
                    Write-Information "   ✅ Resource Dictionary (no code-behind needed)" -InformationAction Continue
                    continue
                }

                # Validate XAML
                try {
                    $result = Test-BusBuddyXml -FilePath $file.FullName
                    if ($result.IsValid) {
                        $validFiles += $file
                        Write-Information "   ✅ Valid XAML" -InformationAction Continue
                    } else {
                        $invalidFiles += [PSCustomObject]@{
                            File = $file
                            Errors = $result.Errors
                        }
                        Write-Warning "   ❌ Invalid XAML: $($file.Name)"
                        foreach ($validationError in $result.Errors) {
                            Write-Error "      Error: $($validationError.Exception.Message)" -ErrorAction Continue
                        }
                    }
                } catch {
                    $invalidFiles += [PSCustomObject]@{
                        File = $file
                        Errors = @($_)
                    }
                    Write-Error "   ❌ Validation failed for $($file.Name): $($_.Exception.Message)" -ErrorAction Continue
                }
            }

            # Summary report
            Write-Information "" -InformationAction Continue
            Write-Information "📊 XAML Validation Summary" -InformationAction Continue
            Write-Information ("=" * 60) -InformationAction Continue
            Write-Information "Total XAML files: $($xamlFiles.Count)" -InformationAction Continue
            Write-Information "Resource dictionaries: $($resourceDictionaries.Count)" -InformationAction Continue
            Write-Information "Valid XAML files: $($validFiles.Count)" -InformationAction Continue
            Write-Information "Invalid XAML files: $($invalidFiles.Count)" -InformationAction Continue

            if ($invalidFiles.Count -gt 0) {
                Write-Information "" -InformationAction Continue
                Write-Warning "🔧 Files requiring fixes:"
                foreach ($invalid in $invalidFiles) {
                    Write-Warning "   • $($invalid.File.Name)"
                }
            }

            $result = [PSCustomObject]@{
                TotalFiles = $xamlFiles.Count
                ValidFiles = $validFiles.Count
                InvalidFiles = $invalidFiles.Count
                ResourceDictionaries = $resourceDictionaries.Count
                InvalidFileDetails = $invalidFiles
                ProjectPath = $ProjectPath
                ValidationDate = Get-Date
            }

            Write-Output $result
        }
        catch {
            Write-Error "XAML validation failed: $($_.Exception.Message)" -ErrorAction Stop
        }
    }

    end {
        Write-Verbose "XAML validation completed"
    }
}

#region Export Module Members (Microsoft PowerShell 7.5.2 Standard)

# Create alias
Set-Alias -Name 'bb-xaml-validate' -Value 'Invoke-ComprehensiveXamlValidation' -Description 'Comprehensive XAML validation'

Export-ModuleMember -Function @(
    'Invoke-ComprehensiveXamlValidation'
) -Alias @(
    'bb-xaml-validate'
)

#endregion
