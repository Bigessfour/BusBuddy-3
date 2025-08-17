<#
    BusBuddy.ProfileTools - Helper functions used by Copilot agents and the repo profile

    Exports:
      - Ensure-BusBuddyProfileLoaded
      - Get-BusBuddyPwshProcesses
      - Stop-BusBuddyPwshProcesses

    Design goals:
      - Safe: never spawn new interactive terminals during import
      - Idempotent: safe to call repeatedly
      - Guarded: avoid unguarded calls to ManagementDateTimeConverter
      - Interactive safety: preview and explicit confirmation before stopping processes
#>

function Get-RepoRootFromPath {
    param(
        [Parameter(Mandatory=$true)][string]$StartPath
    )
    $probe = (Resolve-Path -Path $StartPath).ProviderPath
    while ($probe) {
        if (Test-Path (Join-Path $probe 'BusBuddy.sln')) { return $probe }
        $next = Split-Path $probe -Parent
        if (-not $next -or $next -eq $probe) { return $null }
        $probe = $next
    }
    return $null
}

function initializeBusBuddyProfileLoaded {
    <#
    .SYNOPSIS
    Initialize the BusBuddy repo profile for the current session (camelCase, approved verb).

    .DESCRIPTION
    Idempotent helper that locates the repository root (looks for BusBuddy.sln) and
    sets environment variables useful for other tools. It will not open external terminals
    or perform interactive UI operations. Safe for use by automated agents.
    #>
    [CmdletBinding()]
    param()

    if ($env:BUSBUDDY_PROFILE_LOADED -eq '1') {
        return $true
    }

    try {
        $modulePath = $PSScriptRoot
        $repoRoot = Get-RepoRootFromPath -StartPath $modulePath
        if (-not $repoRoot) {
            # fallback: attempt to use current location
            $repoRoot = Get-RepoRootFromPath -StartPath (Get-Location).Path
        }

        if (-not $repoRoot) {
            Write-Verbose "BusBuddy repo root not found from module path or current location."
            return $false
        }

        $env:BUSBUDDY_REPO_ROOT = $repoRoot
        $env:BUSBUDDY_PROFILE_LOADED = '1'

        # Make module path available in PSModulePath (de-duplicate)
        $busBuddyModules = Join-Path $repoRoot 'PowerShell\Modules'
        if (Test-Path $busBuddyModules) {
            $paths = ($env:PSModulePath -split ';') | ForEach-Object { $_.TrimEnd('\') } | Where-Object { $_ }
            if ($busBuddyModules -notin $paths) {
                $env:PSModulePath = ($busBuddyModules + ';' + ($paths -join ';')).TrimEnd(';')
            }
        }

        return $true
    }
    catch {
        Write-Verbose "initializeBusBuddyProfileLoaded failed: $($_.Exception.Message)"
        return $false
    }
}

function getBusBuddyPwshProcesses {
    <#
    .SYNOPSIS
    List pwsh.exe processes with guarded CreationDate conversion (camelCase naming).

    .PARAMETER Minutes
    Only include processes started within the last N minutes. If 0 or not specified,
    include all found processes.
    #>
    [CmdletBinding()]
    param(
        [int]$Minutes = 30
    )

    $cutoff = if ($Minutes -gt 0) { (Get-Date).AddMinutes(-$Minutes) } else { $null }

    Get-CimInstance Win32_Process -Filter "Name='pwsh.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object {
            $cmd = $_.CommandLine
            $started = $null
            try { if ($_.CreationDate) { $started = [System.Management.ManagementDateTimeConverter]::ToDateTime($_.CreationDate) } } catch {}
            [PSCustomObject]@{
                PID = $_.ProcessId
                CommandLine = $cmd
                Started = $started
            }
        } |
        Where-Object { if ($null -eq $cutoff) { $true } else { -not $_.Started -or $_.Started -gt $cutoff } } |
        Sort-Object @{Expression={$_.Started};Descending=$false}
}

function stopBusBuddyPwshProcesses {
    <#
    .SYNOPSIS
    Safely stop pwsh.exe processes that match criteria (camelCase naming). Requires explicit confirmation.

    .PARAMETER Pids
    Array of process IDs to stop. If not provided, candidates are chosen by CommandLine matching '-NoExit'.

    .PARAMETER Minutes
    Window in minutes to consider recent processes (default 30).

    .PARAMETER Preview
    If specified, only show the candidates without stopping.

    .EXAMPLE
    stopBusBuddyPwshProcesses -Preview
    #>
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [int[]]$Pids,
        [int]$Minutes = 30,
        [switch]$Preview
    )

    $currentPid = $PID

    if (-not $Pids) {
        $candidates = getBusBuddyPwshProcesses -Minutes $Minutes | Where-Object { $_.PID -ne $currentPid -and ($_.CommandLine -match '-NoExit' -or $_.CommandLine -match '--%') }
    } else {
        $candidates = getBusBuddyPwshProcesses -Minutes 0 | Where-Object { $Pids -contains $_.PID -and $_.PID -ne $currentPid }
    }

    if (-not $candidates -or $candidates.Count -eq 0) {
        Write-Output "No candidate pwsh processes found to stop."
        return
    }

    $candidates | Format-Table PID, Started, @{Name='Command';Expression={$_.CommandLine}} -AutoSize

    if ($Preview) { Write-Output 'Preview mode - no processes stopped.'; return }

    $confirm = Read-Host "Type 'yes' to stop ALL listed processes (or blank to cancel)"
    if ($confirm -ne 'yes') { Write-Output 'Canceled by user.'; return }

    foreach ($c in $candidates) {
        try {
            if ($PSCmdlet.ShouldProcess("pwsh PID $($c.PID)", "Stop")) {
                Stop-Process -Id $c.PID -Force -ErrorAction Stop
                Write-Output "Stopped PID $($c.PID)"
            }
        } catch {
            Write-Warning "Failed to stop PID $($c.PID): $($_.Exception.Message)"
        }
    }
}

# Export only camelCase functions - no unapproved verbs
Export-ModuleMember -Function initializeBusBuddyProfileLoaded, getBusBuddyPwshProcesses, stopBusBuddyPwshProcesses
