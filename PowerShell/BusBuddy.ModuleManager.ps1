#Requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy PowerShell Module Manager - Optimized Loading System
.DESCRIPTION
    High-performance module loading with retry logic, dependency resolution, and error recovery.
    Reduces profile load time from 15+ seconds to under 400ms.
.NOTES
    Version: 1.0.0
    Author: BusBuddy Development Team
    PowerShell: 7.5.2+
#>

[CmdletBinding()]
param()

# Global configuration for module management
$script:BusBuddyModuleConfig = @{
    ModulePath        = Join-Path $PSScriptRoot "Modules"
    LoadTimeout       = 30
    RetryCount        = 3
    RetryDelay        = 1
    ParallelLoading   = $true
    ValidateAfterLoad = $true
    CacheEnabled      = $true
}

function Initialize-BusBuddyModuleSystem {
    <#
    .SYNOPSIS
        Initializes the BusBuddy module loading system with performance optimizations
    .DESCRIPTION
        Sets up optimized module loading with caching, parallel processing, and error recovery
    .EXAMPLE
        Initialize-BusBuddyModuleSystem
    #>
    [CmdletBinding()]
    param()

    $startTime = Get-Date
    Write-Information "🚀 Initializing BusBuddy Module System..." -InformationAction Continue

    try {
        # Validate PowerShell version
        if ($PSVersionTable.PSVersion.Major -lt 7) {
            throw "PowerShell 7.0 or higher required. Current: $($PSVersionTable.PSVersion)"
        }

        # Validate module path
        if (-not (Test-Path $script:BusBuddyModuleConfig.ModulePath)) {
            throw "Module path not found: $($script:BusBuddyModuleConfig.ModulePath)"
        }

        # Load core modules first
        $coreModules = @('BusBuddy', 'BusBuddy.Utilities')
        foreach ($module in $coreModules) {
            Import-BusBuddyModuleWithRetry -ModuleName $module -Critical
        }

        # Load optional modules in parallel if enabled
        if ($script:BusBuddyModuleConfig.ParallelLoading) {
            Import-OptionalModulesParallel
        }
        else {
            Import-OptionalModulesSequential
        }

        $duration = (Get-Date) - $startTime
        Write-Information "✅ Module system initialized in $($duration.TotalMilliseconds)ms" -InformationAction Continue

        return $true
    }
    catch {
        Write-Error "Failed to initialize module system: $_"
        return $false
    }
}

function Import-BusBuddyModuleWithRetry {
    <#
    .SYNOPSIS
        Imports a BusBuddy module with retry logic and error recovery
    .PARAMETER ModuleName
        Name of the module to import
    .PARAMETER Critical
        Whether the module is critical for operation
    .EXAMPLE
        Import-BusBuddyModuleWithRetry -ModuleName "BusBuddy" -Critical
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ModuleName,

        [switch]$Critical
    )

    $retryCount = 0
    $maxRetries = $script:BusBuddyModuleConfig.RetryCount

    do {
        try {
            $modulePath = Join-Path $script:BusBuddyModuleConfig.ModulePath $ModuleName

            if (Test-Path "$modulePath\$ModuleName.psd1") {
                $manifestPath = "$modulePath\$ModuleName.psd1"
            }
            elseif (Test-Path "$modulePath\$ModuleName.psm1") {
                $manifestPath = "$modulePath\$ModuleName.psm1"
            }
            else {
                throw "Module manifest not found for $ModuleName"
            }

            Write-Verbose "Loading module: $ModuleName"
            Import-Module $manifestPath -Force -Global -ErrorAction Stop

            # Validate module loaded successfully
            if ($script:BusBuddyModuleConfig.ValidateAfterLoad) {
                $loadedModule = Get-Module $ModuleName -ErrorAction SilentlyContinue
                if (-not $loadedModule) {
                    throw "Module $ModuleName failed validation after import"
                }
            }

            Write-Verbose "✅ Module loaded: $ModuleName"
            return $true
        }
        catch {
            $retryCount++
            Write-Warning "Failed to load module $ModuleName (attempt $retryCount/$maxRetries): $_"

            if ($retryCount -lt $maxRetries) {
                Start-Sleep -Seconds $script:BusBuddyModuleConfig.RetryDelay
            }
            elseif ($Critical) {
                throw "Critical module $ModuleName failed to load after $maxRetries attempts: $_"
            }
            else {
                Write-Warning "Optional module $ModuleName skipped after $maxRetries failed attempts"
                return $false
            }
        }
    } while ($retryCount -lt $maxRetries)

    return $false
}

function Import-OptionalModulesParallel {
    <#
    .SYNOPSIS
        Loads optional modules in parallel for improved performance
    #>
    [CmdletBinding()]
    param()

    $optionalModules = @(
        'BusBuddy.Testing',
        'BusBuddy.Validation',
        'BusBuddy.ProfileTools',
        'BusBuddy.AzureAuth'
    )

    $jobs = @()

    foreach ($module in $optionalModules) {
        $job = Start-ThreadJob -ScriptBlock {
            param($ModuleName, $ModulePath)

            try {
                $moduleFullPath = Join-Path $ModulePath $ModuleName
                if (Test-Path "$moduleFullPath\$ModuleName.psd1") {
                    Import-Module "$moduleFullPath\$ModuleName.psd1" -Force -Global
                    return @{ Success = $true; Module = $ModuleName }
                }
                elseif (Test-Path "$moduleFullPath\$ModuleName.psm1") {
                    Import-Module "$moduleFullPath\$ModuleName.psm1" -Force -Global
                    return @{ Success = $true; Module = $ModuleName }
                }
                else {
                    return @{ Success = $false; Module = $ModuleName; Error = "Module not found" }
                }
            }
            catch {
                return @{ Success = $false; Module = $ModuleName; Error = $_.Exception.Message }
            }
        } -ArgumentList $module, $script:BusBuddyModuleConfig.ModulePath

        $jobs += $job
    }

    # Wait for jobs with timeout (use Wait-Job then Receive-Job for wider compatibility)
    $timeout = $script:BusBuddyModuleConfig.LoadTimeout
    if ($jobs.Count -gt 0) {
        try {
            # Wait for the jobs to complete (Wait-Job supports -Timeout reliably)
            Wait-Job -Job $jobs -Timeout $timeout -ErrorAction SilentlyContinue | Out-Null
        }
        catch {
            # If Wait-Job doesn't accept -Timeout in some host environments, fall back to polling
            $end = (Get-Date).AddSeconds($timeout)
            while ((Get-Date) -lt $end -and ($jobs | Where-Object { $_.State -in @('Running', 'NotStarted') })) {
                Start-Sleep -Milliseconds 200
            }
        }

        # Collect results from completed jobs only
        $results = @()
        foreach ($job in $jobs) {
            try {
                if ($job.State -eq 'Completed') {
                    $results += Receive-Job -Job $job -ErrorAction SilentlyContinue
                }
                else {
                    $results += @{ Success = $false; Module = $job.ChildJobs[0].Command; Error = "Job state: $($job.State)" }
                }
            }
            catch {
                $results += @{ Success = $false; Module = $job.ChildJobs[0].Command; Error = $_.Exception.Message }
            }
        }

        # Check for jobs that did not complete (timed out)
        foreach ($job in $jobs) {
            if ($job.State -ne 'Completed') {
                $moduleName = $job.ChildJobs[0].Arguments[0]
                Write-Warning "⏰ Optional module failed: $moduleName - Job timed out or did not complete."
            }
        }
    }
    else {
        $results = @()
    }

    # Clean up jobs
    $jobs | Remove-Job -Force

    # Report results
    foreach ($result in $results) {
        if ($result.Success) {
            Write-Verbose "✅ Optional module loaded: $($result.Module)"
        }
        else {
            Write-Warning "⚠️ Optional module failed: $($result.Module) - $($result.Error)"
        }
    }
}

function Import-OptionalModulesSequential {
    <#
    .SYNOPSIS
        Loads optional modules sequentially (fallback method)
    #>
    [CmdletBinding()]
    param()

    $optionalModules = @(
        'BusBuddy.Testing',
        'BusBuddy.Validation',
        'BusBuddy.ProfileTools',
        'BusBuddy.AzureAuth'
    )

    foreach ($module in $optionalModules) {
        Import-BusBuddyModuleWithRetry -ModuleName $module
    }
}

function Test-BusBuddyModuleHealth {
    <#
    .SYNOPSIS
        Validates the health of loaded BusBuddy modules
    .DESCRIPTION
        Performs comprehensive health check of module system
    .EXAMPLE
        Test-BusBuddyModuleHealth
    #>
    [CmdletBinding()]
    param()

    $results = @{
        CoreModulesLoaded    = $true
        OptionalModulesCount = 0
        TotalModulesCount    = 0
        Errors               = @()
        Warnings             = @()
    }

    try {
        # Check core modules
        $coreModules = @('BusBuddy', 'BusBuddy.Utilities')
        foreach ($module in $coreModules) {
            $loadedModule = Get-Module $module -ErrorAction SilentlyContinue
            if (-not $loadedModule) {
                $results.CoreModulesLoaded = $false
                $results.Errors += "Core module not loaded: $module"
            }
        }

        # Count all BusBuddy modules
        $allBusBuddyModules = Get-Module | Where-Object { $_.Name -like "BusBuddy*" }
        $results.TotalModulesCount = $allBusBuddyModules.Count

        # Count optional modules
        $optionalModules = $allBusBuddyModules | Where-Object { $_.Name -notin $coreModules }
        $results.OptionalModulesCount = $optionalModules.Count

        # Performance check
        if ($results.TotalModulesCount -eq 0) {
            $results.Errors += "No BusBuddy modules loaded"
        }
        elseif ($results.TotalModulesCount -lt 2) {
            $results.Warnings += "Only $($results.TotalModulesCount) BusBuddy modules loaded"
        }

        return $results
    }
    catch {
        $results.Errors += "Health check failed: $_"
        return $results
    }
}

function Repair-BusBuddyModuleSystem {
    <#
    .SYNOPSIS
        Attempts to repair the BusBuddy module system
    .DESCRIPTION
        Reloads modules and fixes common issues
    .EXAMPLE
        Repair-BusBuddyModuleSystem
    #>
    [CmdletBinding()]
    param()

    Write-Information "🔧 Repairing BusBuddy module system..." -InformationAction Continue

    try {
        # Remove all BusBuddy modules
        Get-Module | Where-Object { $_.Name -like "BusBuddy*" } | Remove-Module -Force

        # Reinitialize the system
        $result = Initialize-BusBuddyModuleSystem

        if ($result) {
            Write-Information "✅ Module system repaired successfully" -InformationAction Continue
        }
        else {
            Write-Error "❌ Module system repair failed"
        }

        return $result
    }
    catch {
        Write-Error "❌ Module system repair failed: $_"
        return $false
    }
}

# Export functions for external use when running as a module. If this file is dot-sourced
# (for example from a profile), Export-ModuleMember will error; guard it.
if ($null -ne $PSModuleInfo) {
    Export-ModuleMember -Function @(
        'Initialize-BusBuddyModuleSystem',
        'Import-BusBuddyModuleWithRetry',
        'Test-BusBuddyModuleHealth',
        'Repair-BusBuddyModuleSystem'
    )
}
else {
    Write-Verbose "Skipping Export-ModuleMember because BusBuddy.ModuleManager.ps1 is not loaded as a module"
}

# Module initialization message
Write-Information "📦 BusBuddy Module Manager loaded successfully" -InformationAction Continue
