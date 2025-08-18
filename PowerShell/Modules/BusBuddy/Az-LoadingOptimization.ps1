# Az Module Loading Optimization for BusBuddy
# Addresses lazy loading delays by implementing smart pre-loading and caching

<#
.SYNOPSIS
    Optimizes Az module loading to prevent mid-execution delays

.DESCRIPTION
    The Az PowerShell module is large (~300MB) and causes 10-15 second delays
    when lazy-loaded during script execution. This script implements:

    1. Background pre-loading during profile initialization
    2. Minimal module imports (only required sub-modules)
    3. Module availability caching
    4. Connection context persistence

.NOTES
    Reference: https://learn.microsoft.com/powershell/azure/manage-azure-resources-invoke-runbook
#>

# Global cache for module availability (avoids repeated Get-Module calls)
if (-not $global:BusBuddyAzModuleCache) {
    $global:BusBuddyAzModuleCache = @{}
}

function Test-BusBuddyAzModule {
    <#
    .SYNOPSIS
        Fast check if specific Az module is available and loaded
    #>
    param(
        [Parameter(Mandatory)]
        [string]$ModuleName
    )

    # Check cache first
    if ($global:BusBuddyAzModuleCache.ContainsKey($ModuleName)) {
        return $global:BusBuddyAzModuleCache[$ModuleName]
    }

    # Check if module is available and loaded
    $available = Get-Module $ModuleName -ListAvailable -ErrorAction SilentlyContinue
    $loaded = Get-Module $ModuleName -ErrorAction SilentlyContinue

    $result = [PSCustomObject]@{
        Available = [bool]$available
        Loaded = [bool]$loaded
        NeedsImport = [bool]$available -and -not [bool]$loaded
    }

    # Cache the result
    $global:BusBuddyAzModuleCache[$ModuleName] = $result
    return $result
}

function Start-BusBuddyAzPreload {
    <#
    .SYNOPSIS
        Background job to pre-load essential Az modules
    #>
    param(
        [string[]]$Modules = @('Az.Accounts', 'Az.Sql', 'Az.Resources')
    )

    # Only start if not already running
    $existingJob = Get-Job -Name "BusBuddyAzPreload" -ErrorAction SilentlyContinue
    if ($existingJob) {
        Write-Verbose "Az preload job already running: $($existingJob.State)"
        return $existingJob
    }

    $job = Start-Job -Name "BusBuddyAzPreload" -ScriptBlock {
        param($ModulesToLoad)

        $results = @{}
        foreach ($module in $ModulesToLoad) {
            try {
                Write-Verbose "Preloading $module..."
                Import-Module $module -Force -ErrorAction Stop
                $results[$module] = "Success"
            }
            catch {
                $results[$module] = "Failed: $_"
            }
        }
        return $results
    } -ArgumentList (,$Modules)

    Write-Information "Started background Az module preload job" -InformationAction Continue
    return $job
}

function Import-BusBuddyAzModule {
    <#
    .SYNOPSIS
        Smart import with fallback to preload job results
    .DESCRIPTION
        Attempts to import the specified Az module, using a background preload job if available.
        Returns $true if the module is successfully loaded, $false otherwise.
    .OUTPUTS
        [bool] - $true on success, $false on failure.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$ModuleName,

        [int]$TimeoutSeconds = 30
    )

    $moduleStatus = Test-BusBuddyAzModule -ModuleName $ModuleName

    if ($moduleStatus.Loaded) {
        Write-Verbose "$ModuleName already loaded"
        return $true
    }

    if (-not $moduleStatus.Available) {
        Write-Warning "$ModuleName not available. Run: Install-Module $ModuleName -Scope CurrentUser"
        return $false
    }

    # Check if preload job has it ready
    $preloadJob = Get-Job -Name "BusBuddyAzPreload" -ErrorAction SilentlyContinue
    if ($preloadJob -and $preloadJob.State -eq "Completed") {
        Write-Information "Using preloaded Az modules from background job" -InformationAction Continue
        $preloadResults = Receive-Job $preloadJob
        Remove-Job $preloadJob

        if ($preloadResults[$ModuleName] -eq "Success") {
            # Clear cache since module should now be loaded
            $global:BusBuddyAzModuleCache.Remove($ModuleName)
            return $true
        }
    }

    # Fallback to direct import with progress indication
    Write-Information "Loading $ModuleName (this may take 5-10 seconds)..." -InformationAction Continue

    try {
        $importJob = Start-Job -ScriptBlock {
            param($Module)
            Import-Module $Module -Force
        } -ArgumentList $ModuleName

        $completed = Wait-Job $importJob -Timeout $TimeoutSeconds
        if ($completed) {
            Receive-Job $importJob | Out-Null
            Remove-Job $importJob

            # Clear cache
            $global:BusBuddyAzModuleCache.Remove($ModuleName)

            Write-Information "$ModuleName loaded successfully" -InformationAction Continue
            return $true
        }
        else {
            Stop-Job $importJob -Force
            Remove-Job $importJob -Force
            Write-Warning "Import of $ModuleName timed out after $TimeoutSeconds seconds"
            return $false
        }
    }
    catch {
        Write-Warning "Failed to import $ModuleName. Exception details:`n$($_ | Out-String)"
        return $false
    }
}

function Get-BusBuddyAzLoadStatus {
    <#
    .SYNOPSIS
        Reports current Az module loading status and performance
    #>

    $modules = @('Az', 'Az.Accounts', 'Az.Sql', 'Az.Resources')
    $status = foreach ($module in $modules) {
        $info = Test-BusBuddyAzModule -ModuleName $module
        [PSCustomObject]@{
            Module = $module
            Available = $info.Available
            Loaded = $info.Loaded
            Status = if ($info.Loaded) { "✅ Ready" }
                    elseif ($info.Available) { "⏳ Available" }
                    else { "❌ Missing" }
        }
    }

    $preloadJob = Get-Job -Name "BusBuddyAzPreload" -ErrorAction SilentlyContinue
    $jobStatus = if ($preloadJob) { $preloadJob.State } else { "Not Started" }

    Write-Output "Az Module Status:"
    $status | Format-Table -AutoSize
    Write-Output "Preload Job: $jobStatus"

    if ($preloadJob -and $preloadJob.State -eq "Completed") {
        Write-Output "Preload Results:"
        Receive-Job $preloadJob -Keep | Format-Table -AutoSize
    }
}

# Functions are automatically available when dot-sourced
# No Export-ModuleMember needed for dot-sourced scripts
