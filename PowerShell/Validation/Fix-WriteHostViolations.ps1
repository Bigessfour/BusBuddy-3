#requires -Version 7.0
<#
.SYNOPSIS
    Automatically refactor Write-Host violations in BusBuddy PowerShell modules
.DESCRIPTION
    This script systematically replaces Write-Host calls with appropriate PowerShell output streams
    following Microsoft PowerShell development guidelines for enterprise-grade modules.
.PARAMETER ModulePath
    Path to the PowerShell module to refactor
.PARAMETER BackupOriginal
    Create backup of original file before modifications
.PARAMETER TestAfterChanges
    Run bbAntiRegression after changes to validate compliance
.EXAMPLE
    .\Fix-WriteHostViolations.ps1 -ModulePath "PowerShell\Modules\BusBuddy\BusBuddy.psm1" -BackupOriginal -TestAfterChanges
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path $_ })]
    [string]$ModulePath,

    [switch]$BackupOriginal,

    [switch]$TestAfterChanges,

    [switch]$DryRun
)

function Write-RefactorStatus {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Type = 'Info'
    )

    $prefix = switch ($Type) {
        'Info'    { "🔄" }
        'Success' { "✅" }
        'Warning' { "⚠️" }
        'Error'   { "❌" }
    }

    Write-Information "$prefix $Message" -InformationAction Continue
}

function Get-WriteHostReplacements {
    return @{
        # Success messages (Green)
        'Write-Host\s+"([^"]*(?:✅|Success|successful|completed|passed|ready|built|finished)[^"]*)"[^-]*-ForegroundColor\s+Green' = 'Write-Output "$1"'
        'Write-Host\s+\$([^-\s]+)[^-]*-ForegroundColor\s+Green' = 'Write-Output $$1'

        # Error messages (Red)
        'Write-Host\s+"([^"]*(?:❌|Error|Failed|failed|error|cannot|unable)[^"]*)"[^-]*-ForegroundColor\s+Red' = 'Write-Error "$1"'
        'Write-Host\s+\$([^-\s]+)[^-]*-ForegroundColor\s+Red' = 'Write-Error $$1'

        # Warning messages (Yellow)
        'Write-Host\s+"([^"]*(?:⚠️|Warning|warning|deprecated|caution)[^"]*)"[^-]*-ForegroundColor\s+Yellow' = 'Write-Warning "$1"'
        'Write-Host\s+\$([^-\s]+)[^-]*-ForegroundColor\s+Yellow' = 'Write-Warning $$1'

        # Debug/Verbose messages (Gray, DarkGray, Cyan for debugging)
        'Write-Host\s+"([^"]*(?:Debug|verbose|trace|📝|🔍)[^"]*)"[^-]*-ForegroundColor\s+(?:Gray|DarkGray|Cyan)' = 'Write-Verbose "$1"'
        'Write-Host\s+\$([^-\s]+)[^-]*-ForegroundColor\s+(?:Gray|DarkGray|Cyan)' = 'Write-Verbose $$1'

        # Informational messages (any other colors or no color)
        'Write-Host\s+"([^"]*)"(?:\s+-ForegroundColor\s+\w+)?' = 'Write-Information "$1" -InformationAction Continue'
        'Write-Host\s+\$([^-\s]+)(?:\s+-ForegroundColor\s+\w+)?' = 'Write-Information $$1 -InformationAction Continue'

        # Pattern separators and decorative lines
        'Write-Host\s+"(=+|#+|-+|\*+)"[^-]*-ForegroundColor\s+\w+' = 'Write-Information "$1" -InformationAction Continue'

        # Newline patterns
        'Write-Host\s+"`n([^"]*)"' = 'Write-Information "`n$1" -InformationAction Continue'
    }
}

function Test-ModuleCompliance {
    param([string]$ModulePath)

    Write-RefactorStatus "Testing module compliance..." -Type Info

    try {
        $result = & bbAntiRegression 2>&1
        $writeHostCount = ($result | Select-String "Write-Host violations" | ForEach-Object {
            if ($_ -match "violations:\s*(\d+)") { [int]$matches[1] }
        }) | Measure-Object -Sum | Select-Object -ExpandProperty Sum

        return @{
            WriteHostViolations = $writeHostCount
            Passed = $writeHostCount -eq 0
        }
    }
    catch {
        Write-RefactorStatus "Error testing compliance: $($_.Exception.Message)" -Type Error
        return @{ WriteHostViolations = -1; Passed = $false }
    }
}

# Main refactoring logic
try {
    Write-RefactorStatus "Starting Write-Host refactoring for: $ModulePath" -Type Info

    # Backup original if requested
    if ($BackupOriginal) {
        $backupPath = "$ModulePath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Copy-Item $ModulePath $backupPath -Force
        Write-RefactorStatus "Original backed up to: $backupPath" -Type Success
    }

    # Test initial compliance
    $initialCompliance = Test-ModuleCompliance -ModulePath $ModulePath
    Write-RefactorStatus "Initial Write-Host violations: $($initialCompliance.WriteHostViolations)" -Type Warning

    # Read module content
    $content = Get-Content $ModulePath -Raw
    $originalContent = $content

    # Apply replacements
    $replacements = Get-WriteHostReplacements
    $replacementCount = 0

    foreach ($pattern in $replacements.Keys) {
        $replacement = $replacements[$pattern]
        $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

        if ($matches.Count -gt 0) {
            Write-RefactorStatus "Applying replacement pattern: $($pattern.Substring(0, [Math]::Min(50, $pattern.Length)))..." -Type Info
            $content = [regex]::Replace($content, $pattern, $replacement, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            $replacementCount += $matches.Count
            Write-RefactorStatus "Replaced $($matches.Count) instances" -Type Success
        }
    }

    if ($replacementCount -eq 0) {
        Write-RefactorStatus "No Write-Host violations found to replace" -Type Info
        return
    }

    if ($DryRun) {
        Write-RefactorStatus "DRY RUN: Would apply $replacementCount replacements" -Type Warning
        return
    }

    # Save refactored content
    if ($PSCmdlet.ShouldProcess($ModulePath, "Apply $replacementCount Write-Host replacements")) {
        Set-Content -Path $ModulePath -Value $content -Encoding UTF8
        Write-RefactorStatus "Applied $replacementCount replacements to $ModulePath" -Type Success

        # Test compliance after changes
        if ($TestAfterChanges) {
            Start-Sleep -Seconds 2  # Allow file system to sync
            $finalCompliance = Test-ModuleCompliance -ModulePath $ModulePath

            if ($finalCompliance.Passed) {
                Write-RefactorStatus "✅ Module now passes compliance checks!" -Type Success
            } else {
                Write-RefactorStatus "⚠️ Module still has $($finalCompliance.WriteHostViolations) violations" -Type Warning
                Write-RefactorStatus "Additional manual refactoring may be needed" -Type Info
            }
        }
    }
}
catch {
    Write-RefactorStatus "Refactoring failed: $($_.Exception.Message)" -Type Error
    throw
}
