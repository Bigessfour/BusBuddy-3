# Load-BusBuddyValidation.ps1 - Loader for validation and testing functions
# Ensures all validation capabilities are available in the environment

#Requires -Version 7.5

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$Quiet
)

function Import-BusBuddyValidation {
    <#
    .SYNOPSIS
        Loads BusBuddy validation and testing functions into the current session
    .DESCRIPTION
        Ensures all database validation, CRUD testing, and diagnostic functions are available
    .PARAMETER Force
        Force reload even if functions are already available
    .PARAMETER Quiet
        Suppress informational output
    #>
    [CmdletBinding()]
    param(
        [switch]$Force,
        [switch]$Quiet
    )

    $scriptsLoaded = @()
    $scriptsSkipped = @()
    $scriptsFailed = @()

    # Define scripts to load
    $validationScripts = @(
        @{
            Name = "Database Validation"
            Path = "$PSScriptRoot\PowerShell\Modules\BusBuddy\bb-validate-database.ps1"
            TestFunction = "Test-BusBuddyDatabase"
        },
        @{
            Name = "End-to-End CRUD Testing"
            Path = "$PSScriptRoot\Test-EndToEndCRUD.ps1"
            TestFunction = "Invoke-EndToEndTests"
        }
    )

    if (-not $Quiet) {
        Write-Host "🔧 Loading BusBuddy Validation Framework..." -ForegroundColor Cyan
    }

    foreach ($script in $validationScripts) {
        try {
            # Check if function already exists (unless Force is specified)
            if (-not $Force -and (Get-Command $script.TestFunction -ErrorAction SilentlyContinue)) {
                $scriptsSkipped += $script.Name
                if (-not $Quiet) {
                    Write-Host "⏭️  $($script.Name) - Already loaded" -ForegroundColor Yellow
                }
                continue
            }

            # Check if script file exists
            if (-not (Test-Path $script.Path)) {
                $scriptsFailed += "$($script.Name) - File not found: $($script.Path)"
                if (-not $Quiet) {
                    Write-Host "❌ $($script.Name) - File not found" -ForegroundColor Red
                }
                continue
            }

            # Load the script
            . $script.Path
            $scriptsLoaded += $script.Name

            if (-not $Quiet) {
                Write-Host "✅ $($script.Name) - Loaded successfully" -ForegroundColor Green
            }

        } catch {
            $scriptsFailed += "$($script.Name) - $($_.Exception.Message)"
            if (-not $Quiet) {
                Write-Host "❌ $($script.Name) - Failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }

    # Summary
    if (-not $Quiet) {
        Write-Host ""
        Write-Host "📊 Loading Summary:" -ForegroundColor Cyan
        Write-Host "   ✅ Loaded: $($scriptsLoaded.Count)" -ForegroundColor Green
        Write-Host "   ⏭️  Skipped: $($scriptsSkipped.Count)" -ForegroundColor Yellow
        Write-Host "   ❌ Failed: $($scriptsFailed.Count)" -ForegroundColor Red

        if ($scriptsLoaded.Count -gt 0) {
            Write-Host ""
            Write-Host "🎯 Available Commands:" -ForegroundColor Magenta
            Write-Host "   • Test-BusBuddyDatabase -IncludeCRUD -Detailed" -ForegroundColor White
            Write-Host "   • bb-validate-database (alias)" -ForegroundColor White
            Write-Host "   • .\Test-EndToEndCRUD.ps1 -IncludeForeignKeyTests -GenerateReport" -ForegroundColor White
        }

        if ($scriptsFailed.Count -gt 0) {
            Write-Host ""
            Write-Host "⚠️  Failed to load:" -ForegroundColor Yellow
            foreach ($failure in $scriptsFailed) {
                Write-Host "   • $failure" -ForegroundColor Red
            }
        }
    }

    return @{
        Loaded = $scriptsLoaded
        Skipped = $scriptsSkipped
        Failed = $scriptsFailed
        Success = $scriptsFailed.Count -eq 0
    }
}

# Auto-load if script is run directly
if ($MyInvocation.InvocationName -ne '.') {
    Import-BusBuddyValidation -Force:$Force -Quiet:$Quiet
}

# Export the function for manual use
Export-ModuleMember -Function Import-BusBuddyValidation
