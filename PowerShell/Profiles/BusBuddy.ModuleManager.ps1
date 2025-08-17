#requires -Version 7.5 -PSEdition Core
<#
.SYNOPSIS
Hardened BusBuddy Module Manager for persistent command availability
.DESCRIPTION
Robust module loading system that ensures bb* commands are always available,
even under constant terminal refreshes and varied loading conditions.
Environment validation will check for PowerShell version and edition; if `$PSVersionTable.PSEdition` is missing or not 'Core', the validation will fail and document the issue in the output.

.NOTES
Author: BusBuddy Development Team
Standards: PowerShell 7.5+, StrictMode 3.0, Microsoft compliance
References:
- PowerShell Modules: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module
- Module Loading: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
- Error Handling: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_try_catch_finally
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$Force,
    [switch]$Quiet,
    [switch]$ValidateOnly,
    [int]$RetryAttempts = 3,
    [int]$TimeoutSeconds = 30
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# =============================================================================
# HARDENED MODULE LOADING SYSTEM
# =============================================================================

class BusBuddyModuleManager {
    [string]$RepoRoot
    [hashtable]$ModuleStatus
    [string[]]$RequiredCommands
    [string[]]$CriticalModules
    [bool]$IsInitialized
    [datetime]$LastValidation
    [int]$ValidationIntervalMinutes = 5

    BusBuddyModuleManager() {
        $this.ModuleStatus = @{}
        $this.RequiredCommands = @(
            'bbHealth', 'bbBuild', 'bbTest', 'bbRun', 'bbClean', 'bbRestore',
            'bbAntiRegression', 'bbXamlValidate', 'bbMvpCheck', 'bbCommands'
        )
        $this.CriticalModules = @('BusBuddy', 'BusBuddy.Testing')
        $this.IsInitialized = $false
        $this.RepoRoot = $this.FindRepoRoot()
    }

    [string] FindRepoRoot() {
        Write-Information "🔍 Locating BusBuddy repository root..." -InformationAction Continue

        $probe = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
        $maxDepth = 10
        $currentDepth = 0

        while ($probe -and $currentDepth -lt $maxDepth) {
            $solutionPath = Join-Path $probe 'BusBuddy.sln'
            if (Test-Path $solutionPath) {
                Write-Information "✅ Repository root found: $probe" -InformationAction Continue
                return $probe
            }

            $parent = Split-Path $probe -Parent
            if (-not $parent -or $parent -eq $probe) { break }
            $probe = $parent
            $currentDepth++
        }

        throw "BusBuddy repository root not found. Ensure BusBuddy.sln exists in repo root."
    }

    [hashtable] ValidateEnvironment() {
        Write-Information "🔧 Validating PowerShell environment..." -InformationAction Continue

        $result = @{
            IsValid = $true
            Issues = @()
            Details = @()
        }

        # Check PowerShell version with null safety
        try {
            $psVersion = $global:PSVersionTable
            if ($null -eq $psVersion -or $null -eq $psVersion.PSVersion) {
                $result.Issues += "PSVersionTable or PSVersion not available"
                $result.IsValid = $false
            } elseif ($psVersion.PSVersion -lt [Version]'7.5.0') {
                $result.Issues += "PowerShell 7.5+ required. Current: $($psVersion.PSVersion)"
                $result.IsValid = $false
            } else {
                $result.Details += "PowerShell version OK: $($psVersion.PSVersion)"
            }
        }
        catch {
            $result.Issues += "Failed to check PowerShell version: $($_.Exception.Message)"
            $result.IsValid = $false
        }

        # Check PSEdition with enhanced null safety
        try {
            $psVersion = $global:PSVersionTable
            if ($null -eq $psVersion) {
                $result.Issues += "PSVersionTable not available"
                $result.IsValid = $false
            } elseif (-not $psVersion.ContainsKey('PSEdition')) {
                $result.Issues += "PSEdition property not found in PSVersionTable"
                $result.IsValid = $false
            } elseif ([string]::IsNullOrEmpty($psVersion.PSEdition)) {
                $result.Issues += "PSEdition is null or empty"
                $result.IsValid = $false
            } elseif ($psVersion.PSEdition -ne 'Core') {
                $result.Issues += "PowerShell Core required. Current: $($psVersion.PSEdition)"
                $result.IsValid = $false
            } else {
                $result.Details += "PowerShell edition OK: $($psVersion.PSEdition)"
            }
        }
        catch {
            $result.Issues += "Failed to check PSEdition: $($_.Exception.Message)"
            $result.IsValid = $false
        }

        # Check repository structure
        $criticalPaths = @(
            'BusBuddy.sln',
            (Join-Path 'PowerShell' (Join-Path 'Modules' (Join-Path 'BusBuddy' 'BusBuddy.psd1'))),
            (Join-Path 'PowerShell' (Join-Path 'Modules' (Join-Path 'BusBuddy.Testing' 'BusBuddy.Testing.psd1')))
        )

        foreach ($path in $criticalPaths) {
            $fullPath = Join-Path $this.RepoRoot $path
            if (-not (Test-Path $fullPath)) {
                $result.Issues += "Critical file missing: $fullPath"
                $result.IsValid = $false
            } else {
                $result.Details += "Critical file found: $fullPath"
            }
        }

        if ($result.IsValid) {
            Write-Information "✅ Environment validation passed" -InformationAction Continue
        } else {
            Write-Warning "❌ Environment validation failed:"
            $result.Issues | ForEach-Object { Write-Warning "  • $_" }
        }

        return $result
    }

    [bool] LoadModuleWithRetry([string]$ModulePath, [string]$ModuleName, [int]$Attempts = 3) {
        for ($i = 1; $i -le $Attempts; $i++) {
            try {
                Write-Information "📦 Loading module $ModuleName (attempt $i/$Attempts)..." -InformationAction Continue

                # Check if module path exists
                if (-not (Test-Path $ModulePath)) {
                    Write-Warning "Module path not found: $ModulePath"
                    return $false
                }

                # Remove existing module if Force is specified or if reload needed
                $existingModule = Get-Module $ModuleName -ErrorAction SilentlyContinue
                if ($existingModule) {
                    Write-Information "🔄 Removing existing module $ModuleName..." -InformationAction Continue
                    Remove-Module $ModuleName -Force -ErrorAction SilentlyContinue
                }

                # Import module with comprehensive parameters
                Import-Module $ModulePath -Force -DisableNameChecking -Global -ErrorAction Stop

                # Validate module loaded
                $loadedModule = Get-Module $ModuleName -ErrorAction SilentlyContinue
                if ($loadedModule) {
                    Write-Information "✅ Module $ModuleName loaded successfully" -InformationAction Continue
                    $this.ModuleStatus[$ModuleName] = @{
                        Status = 'Loaded'
                        LoadTime = Get-Date
                        Version = $loadedModule.Version
                        Path = $ModulePath
                    }
                    return $true
                } else {
                    Write-Warning "Module $ModuleName not found after import attempt"
                }
            }
            catch {
                Write-Warning "Failed to load module $ModuleName (attempt $i/$Attempts): $($_.Exception.Message)"
                if ($i -lt $Attempts) {
                    Start-Sleep -Seconds 2
                }
            }
        }

        $this.ModuleStatus[$ModuleName] = @{
            Status = 'Failed'
            LoadTime = Get-Date
            Error = "Failed after $Attempts attempts"
        }
        return $false
    }

    [bool] LoadAllModules() {
        Write-Information "🚀 Loading BusBuddy modules..." -InformationAction Continue

        $moduleDefinitions = @(
            @{
                Name = 'BusBuddy'
                Path = Join-Path $this.RepoRoot 'PowerShell\Modules\BusBuddy'
                Critical = $true
            },
            @{
                Name = 'BusBuddy.Testing'
                Path = Join-Path $this.RepoRoot 'PowerShell\Modules\BusBuddy.Testing'
                Critical = $true
            },
            @{
                Name = 'BusBuddy.CLI'
                Path = Join-Path $this.RepoRoot 'PowerShell\Modules\BusBuddy.CLI'
                Critical = $false
            }
        )

        $allSuccess = $true
        $loadedCount = 0

        foreach ($module in $moduleDefinitions) {
            $success = $this.LoadModuleWithRetry($module.Path, $module.Name, $script:RetryAttempts)

            if ($success) {
                $loadedCount++
            } elseif ($module.Critical) {
                Write-Error "Critical module $($module.Name) failed to load"
                $allSuccess = $false
            } else {
                Write-Warning "Optional module $($module.Name) failed to load"
            }
        }

        Write-Information "📊 Module loading summary: $loadedCount/$($moduleDefinitions.Count) modules loaded" -InformationAction Continue
        return $allSuccess
    }

    [bool] ValidateCommands() {
        Write-Information "🔍 Validating required commands..." -InformationAction Continue

        $missingCommands = @()
        $availableCommands = @()

        foreach ($command in $this.RequiredCommands) {
            if (Get-Command $command -ErrorAction SilentlyContinue) {
                $availableCommands += $command
            } else {
                $missingCommands += $command
            }
        }

        if ($missingCommands.Count -eq 0) {
            Write-Information "✅ All required commands available ($($availableCommands.Count)/$($this.RequiredCommands.Count))" -InformationAction Continue
            return $true
        } else {
            Write-Warning "Missing commands: $($missingCommands -join ', ')"
            Write-Information "Available commands: $($availableCommands -join ', ')" -InformationAction Continue
            return $false
        }
    }

    [void] CreatePersistentAliases() {
        Write-Information "🔧 Creating persistent aliases..." -InformationAction Continue

        # Enhanced alias creation with error handling
        $aliasDefinitions = @(
            @{ Name = 'bbRefresh'; Value = 'Invoke-BusBuddyModuleRefresh'; Description = 'Refresh BusBuddy modules' },
            @{ Name = 'bbStatus'; Value = 'Get-BusBuddyModuleStatus'; Description = 'Check module status' },
            @{ Name = 'bbValidate'; Value = 'Test-BusBuddyEnvironment'; Description = 'Validate environment' },
            @{ Name = 'bbRepair'; Value = 'Repair-BusBuddyEnvironment'; Description = 'Repair module issues' }
        )

        foreach ($aliasDef in $aliasDefinitions) {
            try {
                Set-Alias -Name $aliasDef.Name -Value $aliasDef.Value -Scope Global -Force -ErrorAction Stop
                Write-Information "✅ Alias created: $($aliasDef.Name) → $($aliasDef.Value)" -InformationAction Continue
            }
            catch {
                Write-Warning "Failed to create alias $($aliasDef.Name): $($_.Exception.Message)"
            }
        }
    }

    [hashtable] GetStatus() {
        return @{
            RepoRoot = $this.RepoRoot
            IsInitialized = $this.IsInitialized
            LastValidation = $this.LastValidation
            ModuleStatus = $this.ModuleStatus
            RequiredCommands = $this.RequiredCommands
            CriticalModules = $this.CriticalModules
        }
    }

    [bool] Initialize([bool]$Force = $false) {
        if ($this.IsInitialized -and -not $Force) {
            $timeSinceValidation = (Get-Date) - $this.LastValidation
            if ($timeSinceValidation.TotalMinutes -lt $this.ValidationIntervalMinutes) {
                Write-Information "✅ Modules already initialized and validated recently" -InformationAction Continue
                return $true
            }
        }

        Write-Information "🚀 Initializing BusBuddy module system..." -InformationAction Continue

        try {
            # Step 1: Validate environment
            $envResult = $this.ValidateEnvironment()
            if (-not $envResult.IsValid) {
                foreach ($issue in $envResult.Issues) {
                    Write-Warning $issue
                }
                throw "Environment validation failed"
            } else {
                foreach ($detail in $envResult.Details) {
                    Write-Information $detail -InformationAction Continue
                }
            }

            # Step 2: Set environment variables
            $env:BUSBUDDY_REPO_ROOT = $this.RepoRoot
            $env:DOTNET_VERSION = "9.0.108"
            $env:BUILD_CONFIGURATION = "Release"
            $env:SOLUTION_FILE = "BusBuddy.sln"

            # Step 3: Load modules
            if (-not $this.LoadAllModules()) {
                throw "Critical modules failed to load"
            }

            # Step 4: Validate commands
            if (-not $this.ValidateCommands()) {
                throw "Required commands not available"
            }

            # Step 5: Create persistent aliases
            $this.CreatePersistentAliases()

            # Step 6: Mark as initialized
            $this.IsInitialized = $true
            $this.LastValidation = Get-Date
            $env:BUSBUDDY_MODULES_LOADED = '1'

            Write-Information "✅ BusBuddy module system initialized successfully" -InformationAction Continue
            return $true
        }
        catch {
            Write-Error "Failed to initialize BusBuddy module system: $($_.Exception.Message)"
            $this.IsInitialized = $false
            return $false
        }
    }
}

# =============================================================================
# MODULE MANAGEMENT FUNCTIONS
# =============================================================================

function Invoke-BusBuddyModuleRefresh {
    <#
    .SYNOPSIS
    Refresh BusBuddy modules and commands
    .DESCRIPTION
    Forces a reload of all BusBuddy modules and validates command availability.
    Use this when commands are missing or modules need updating.
    .EXAMPLE
    bbRefresh
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([switch]$Force)

    if ($PSCmdlet.ShouldProcess("BusBuddy Modules", "Refresh")) {
        Write-Information "🔄 Refreshing BusBuddy module system..." -InformationAction Continue

        $manager = [BusBuddyModuleManager]::new()
        $success = $manager.Initialize($Force.IsPresent)

        if ($success) {
            Write-Information "✅ Module refresh completed successfully" -InformationAction Continue
            Get-BusBuddyModuleStatus
        } else {
            Write-Error "Module refresh failed. Use bbRepair to attempt fixes."
        }

        return $success
    }
}

function Get-BusBuddyModuleStatus {
    <#
    .SYNOPSIS
    Get current status of BusBuddy modules and commands
    .DESCRIPTION
    Displays detailed status of loaded modules, available commands, and system health.
    .EXAMPLE
    bbStatus
    #>
    [CmdletBinding()]
    param()

    Write-Information "📊 BusBuddy Module Status" -InformationAction Continue
    Write-Information "=========================" -InformationAction Continue

    # Check environment variables
    $repoRoot = $env:BUSBUDDY_REPO_ROOT
    $modulesLoaded = $env:BUSBUDDY_MODULES_LOADED

    Write-Information "Repository Root: $repoRoot" -InformationAction Continue
    Write-Information "Modules Loaded Flag: $modulesLoaded" -InformationAction Continue

    # Check loaded modules
    $busBuddyModules = Get-Module BusBuddy* | Sort-Object Name
    if ($busBuddyModules) {
        Write-Information "`nLoaded Modules:" -InformationAction Continue
        $busBuddyModules | ForEach-Object {
            Write-Information "  ✅ $($_.Name) v$($_.Version) ($($_.ModuleType))" -InformationAction Continue
        }
    } else {
        Write-Warning "No BusBuddy modules currently loaded"
    }

    # Check available commands
    $bbCommands = Get-Command bb* -ErrorAction SilentlyContinue | Sort-Object Name
    if ($bbCommands) {
        Write-Information "`nAvailable Commands: ($($bbCommands.Count) total)" -InformationAction Continue
        $bbCommands | ForEach-Object {
            $source = if ($_.Source) { $_.Source } else { 'Function' }
            Write-Information "  🔧 $($_.Name) [$source]" -InformationAction Continue
        }
    } else {
        Write-Warning "No bb* commands currently available"
    }

    # Check critical functions
    $criticalCommands = @('bbHealth', 'bbBuild', 'bbTest', 'bbAntiRegression', 'bbXamlValidate')
    $missingCritical = @()

    foreach ($cmd in $criticalCommands) {
        if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
            $missingCritical += $cmd
        }
    }

    if ($missingCritical.Count -eq 0) {
        Write-Information "`n✅ All critical commands available" -InformationAction Continue
    } else {
        Write-Warning "`nMissing critical commands: $($missingCritical -join ', ')"
        Write-Information "Run bbRefresh to reload modules" -InformationAction Continue
    }
}

function Test-BusBuddyEnvironment {
    <#
    .SYNOPSIS
    Comprehensive environment validation
    .DESCRIPTION
    Validates PowerShell version, module paths, command availability, and repository structure.
    .EXAMPLE
    bbValidate
    #>
    [CmdletBinding()]
    param()

    Write-Information "🔍 BusBuddy Environment Validation" -InformationAction Continue
    Write-Information "===================================" -InformationAction Continue

    $manager = [BusBuddyModuleManager]::new()
    $issues = @()

    # Test 1: PowerShell version
    if ($PSVersionTable.PSVersion -lt [Version]'7.5.0') {
        $issues += "PowerShell 7.5+ required. Current: $($PSVersionTable.PSVersion)"
    } else {
        Write-Information "✅ PowerShell version: $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))" -InformationAction Continue
    }

    # Test 2: Repository structure
    try {
        $repoRoot = $manager.FindRepoRoot()
        Write-Information "✅ Repository root: $repoRoot" -InformationAction Continue
    }
    catch {
        $issues += "Repository root not found: $($_.Exception.Message)"
    }

    # Test 3: Module availability
    $moduleTest = $manager.ValidateEnvironment()
    if ($moduleTest.IsValid) {
        Write-Information "✅ Repository structure valid" -InformationAction Continue
        foreach ($detail in $moduleTest.Details) {
            Write-Information "  $detail" -InformationAction Continue
        }
    } else {
        $issues += "Repository structure validation failed"
        foreach ($issue in $moduleTest.Issues) {
            $issues += "  $issue"
        }
    }

    # Test 4: Command availability
    $requiredCommands = @('bbHealth', 'bbBuild', 'bbTest', 'bbAntiRegression', 'bbXamlValidate')
    $missingCommands = @()

    foreach ($cmd in $requiredCommands) {
        if (Get-Command $cmd -ErrorAction SilentlyContinue) {
            Write-Information "✅ Command available: $cmd" -InformationAction Continue
        } else {
            $missingCommands += $cmd
        }
    }

    if ($missingCommands.Count -gt 0) {
        $issues += "Missing commands: $($missingCommands -join ', ')"
    }

    # Summary
    if ($issues.Count -eq 0) {
        Write-Information "`n✅ Environment validation passed" -InformationAction Continue
        return $true
    } else {
        Write-Warning "`n❌ Environment validation failed:"
        $issues | ForEach-Object { Write-Warning "  • $_" }
        Write-Information "`n💡 Run bbRepair to attempt automatic fixes" -InformationAction Continue
        return $false
    }
}

function Repair-BusBuddyEnvironment {
    <#
    .SYNOPSIS
    Attempt to repair BusBuddy environment issues
    .DESCRIPTION
    Automatically fixes common module loading and command availability issues.
    .EXAMPLE
    bbRepair
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess("BusBuddy Environment", "Repair")) {
        Write-Information "🔧 Repairing BusBuddy environment..." -InformationAction Continue

        try {
            # Step 1: Clear existing modules
            Write-Information "🧹 Removing existing modules..." -InformationAction Continue
            Get-Module BusBuddy* | Remove-Module -Force -ErrorAction SilentlyContinue

            # Step 2: Clear environment flags
            $env:BUSBUDDY_MODULES_LOADED = $null

            # Step 3: Force reload
            Write-Information "🔄 Force reloading modules..." -InformationAction Continue
            $manager = [BusBuddyModuleManager]::new()
            $success = $manager.Initialize($true)

            if ($success) {
                Write-Information "✅ Environment repair completed successfully" -InformationAction Continue
                Test-BusBuddyEnvironment
            } else {
                Write-Error "Environment repair failed. Manual intervention required."
            }

            return $success
        }
        catch {
            Write-Error "Repair failed: $($_.Exception.Message)"
            return $false
        }
    }
}

# =============================================================================
# INITIALIZATION
# =============================================================================

if (-not $ValidateOnly) {
    Write-Information "🚀 Starting BusBuddy Module Manager..." -InformationAction Continue

    try {
        $manager = [BusBuddyModuleManager]::new()
        $initSuccess = $manager.Initialize($Force.IsPresent)

        if ($initSuccess) {
            if (-not $Quiet) {
                Write-Information "✅ BusBuddy modules ready! Available commands:" -InformationAction Continue
                Get-Command bb* -ErrorAction SilentlyContinue | Sort-Object Name | ForEach-Object {
                    Write-Information "  • $($_.Name)" -InformationAction Continue
                }
                Write-Information "`n💡 Use bbStatus to check status, bbRefresh to reload, bbValidate to check health" -InformationAction Continue
            }
        } else {
            Write-Error "Failed to initialize BusBuddy modules. Run bbRepair to attempt fixes."
        }

        # Export status for calling scripts
        return $initSuccess
    }
    catch {
        Write-Error "Module manager initialization failed: $($_.Exception.Message)"
        return $false
    }
} else {
    Write-Information "✅ Module manager loaded (validation only mode)" -InformationAction Continue
    return $true
}
