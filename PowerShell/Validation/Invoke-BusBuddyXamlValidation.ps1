#Requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy XAML Validation Workflow Integration

.DESCRIPTION
    Wrapper function for Test-XmlSyntax.ps1 that provides workflow-friendly
    XAML validation with GitHub Actions integration and VS Code problem reporting.

.PARAMETER Path
    Path to XAML files to validate (default: BusBuddy.WPF)

.PARAMETER FailOnWarnings
    Exit with error code if warnings are found

.PARAMETER OutputFormat
    Output format: 'console', 'github', 'vscode', 'json'

.PARAMETER GenerateVSCodeProblems
    Generate VS Code problems.json for integration

.EXAMPLE
    .\Invoke-BusBuddyXamlValidation.ps1 -Path "BusBuddy.WPF" -OutputFormat "github"

.EXAMPLE
    .\Invoke-BusBuddyXamlValidation.ps1 -GenerateVSCodeProblems

.NOTES
    Author: BusBuddy Development Team
    Date: 2025-08-01
    Purpose: Replace VS Code XAML error detection with PowerShell validation
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = "BusBuddy.WPF",

    [Parameter()]
    [switch]$FailOnWarnings,

    [Parameter()]
    [ValidateSet('console', 'github', 'vscode', 'json')]
    [string]$OutputFormat = 'console',

    [Parameter()]
    [switch]$GenerateVSCodeProblems,

    [Parameter()]
    [switch]$Quiet
)

# Import required functions
$ErrorActionPreference = 'Stop'

function Write-BusBuddyStatus {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$Message,
        [ValidateSet("Success", "Error", "Warning", "Info")]
        [string]$Status = "Info"
    )

    if ($Quiet) { return }

    $colors = @{
        "Success" = "Green"
        "Error" = "Red"
        "Warning" = "Yellow"
        "Info" = "Cyan"
    }

    switch ($Status) {
        "Success" { Write-Information $Message -InformationAction Continue }
        "Error" { Write-Error $Message }
        "Warning" { Write-Warning $Message }
        "Info" { Write-Information $Message -InformationAction Continue }
        default { Write-Information $Message -InformationAction Continue }
    }
}

function Invoke-BusBuddyXamlValidation {
    [CmdletBinding()]
    param(
        [string]$ValidationPath,
        [string]$Format,
        [bool]$FailOnWarnings,
        [bool]$GenerateProblems
    )

    # Locate the Test-XmlSyntax script
    $scriptPaths = @(
        "PowerShell/Scripts/Utilities/Test-XmlSyntax.ps1",
        "Scripts/Utilities/Test-XmlSyntax.ps1",
        "Test-XmlSyntax.ps1"
    )

    $validatorScript = $null
    foreach ($scriptPath in $scriptPaths) {
        if (Test-Path $scriptPath) {
            $validatorScript = Resolve-Path $scriptPath
            break
        }
    }

    if (-not $validatorScript) {
        Write-BusBuddyStatus "❌ Could not find Test-XmlSyntax.ps1 validator script" -Status Error
        Write-BusBuddyStatus "Searched paths: $($scriptPaths -join ', ')" -Status Error
        return $false
    }

    Write-BusBuddyStatus "🎨 Running XAML validation with: $validatorScript" -Status Info

    try {
        # Run the comprehensive XAML validation
        $validationResults = & $validatorScript -Path $ValidationPath -Recurse -IncludeXaml -GenerateReport

        if (-not $validationResults) {
            Write-BusBuddyStatus "✅ No XAML issues found!" -Status Success
            return $true
        }

        # Process results based on output format
        switch ($Format) {
            'github' {
                Write-GitHubActionsProblemFormat -Results $validationResults
            }
            'vscode' {
                Write-VSCodeProblemFormat -Results $validationResults
            }
            'json' {
                $validationResults | ConvertTo-Json -Depth 3
            }
            default {
                Write-ConsoleProblemFormat -Results $validationResults
            }
        }

        # Generate VS Code problems file if requested
        if ($GenerateProblems) {
            Write-VSCodeProblemsFile -Results $validationResults
        }

        # Check for critical issues
        $errors = $validationResults | Where-Object { $_.Severity -eq "Error" }
        $warnings = $validationResults | Where-Object { $_.Severity -eq "Warning" }

        if ($errors) {
            Write-BusBuddyStatus "❌ Found $($errors.Count) critical XAML errors" -Status Error
            return $false
        }

        if ($warnings -and $FailOnWarnings) {
            Write-BusBuddyStatus "⚠️ Found $($warnings.Count) XAML warnings (fail-on-warnings enabled)" -Status Warning
            return $false
        }

        Write-BusBuddyStatus "✅ XAML validation completed: $($validationResults.Count) issues found" -Status Success
        return $true

    } catch {
        Write-BusBuddyStatus "❌ XAML validation failed: $($_.Exception.Message)" -Status Error
        return $false
    }
}

function Write-GitHubActionsProblemFormat {
    param($Results)

    foreach ($result in $Results) {
        $level = if ($result.Severity -eq "Error") { "error" } else { "warning" }
        $file = $result.File -replace '\\', '/'
        $line = if ($result.Line) { $result.Line } else { 1 }

        Write-Output "::$level file=$file,line=$line::$($result.Message)"
    }
}

function Write-VSCodeProblemFormat {
    param($Results)

    foreach ($result in $Results) {
        $severity = if ($result.Severity -eq "Error") { "Error" } else { "Warning" }
        Write-Output "[$severity] $($result.File):$($result.Line): $($result.Message)"
    }
}

function Write-ConsoleProblemFormat {
    param($Results)

    Write-BusBuddyStatus "📊 XAML Validation Results:" -Status Info
    $Results | Format-Table File, Line, Severity, Message -AutoSize
}

function Write-VSCodeProblemsFile {
    param($Results)

    $problems = $Results | ForEach-Object {
        @{
            file = $_.File
            line = [int]($_.Line ?? 1)
            column = [int]($_.Column ?? 1)
            severity = if ($_.Severity -eq "Error") { 1 } else { 2 }
            message = $_.Message
            source = "BusBuddy XAML Validator"
        }
    }

    $problemsJson = @{
        version = "1.0.0"
        problems = $problems
    } | ConvertTo-Json -Depth 3

    $problemsFile = ".vscode/xaml-problems.json"
    $problemsJson | Out-File -FilePath $problemsFile -Encoding UTF8
    Write-BusBuddyStatus "📁 Generated VS Code problems file: $problemsFile" -Status Info
}

# Main execution
try {
    Write-BusBuddyStatus "🚌 BusBuddy XAML Validation Workflow Integration" -Status Info

    $success = Invoke-BusBuddyXamlValidation -ValidationPath $Path -Format $OutputFormat -FailOnWarnings:$FailOnWarnings -GenerateProblems:$GenerateVSCodeProblems

    if (-not $success) {
        Write-BusBuddyStatus "❌ XAML validation failed!" -Status Error
        exit 1
    }

    Write-BusBuddyStatus "✅ XAML validation completed successfully!" -Status Success
    exit 0

} catch {
    Write-BusBuddyStatus "💥 Unexpected error: $($_.Exception.Message)" -Status Error
    exit 1
}
