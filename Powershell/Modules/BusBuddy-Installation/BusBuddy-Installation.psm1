#Requires -Version 7.0

<#
.SYNOPSIS
    BusBuddy PowerShell Modules Installation Module
.DESCRIPTION
    Provides comprehensive installation and management capabilities for PowerShell modules
    required by the BusBuddy development environment
.NOTES
    - PowerShell 7.0+ required
    - Manages Azure modules, testing frameworks, and development tools
    - Implements structured logging and comprehensive error handling
    - Follows Microsoft PowerShell best practices
#>

# Module-level logging configuration
$script:LogPath = Join-Path $PSScriptRoot "../../logs/installation.log"
$script:ModuleName = "BusBuddy-Installation"

<#
.SYNOPSIS
    Writes structured log messages for the module
.DESCRIPTION
    Provides centralized logging with multiple output streams and file logging
.PARAMETER Message
    The message to log
.PARAMETER Level
    The logging level (Information, Warning, Error, Verbose, Debug)
.PARAMETER FunctionName
    Override the function name in the log entry
.EXAMPLE
    Write-ModuleLog "Starting installation" -Level Information
.NOTES
    Follows Microsoft PowerShell logging best practices
#>
function Write-ModuleLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Information', 'Warning', 'Error', 'Verbose', 'Debug')]
        [string]$Level = 'Information',

        [Parameter()]
        [string]$FunctionName = $null
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $caller = if ($FunctionName) { $FunctionName } else { (Get-PSCallStack)[1].FunctionName }
    $logEntry = "[$timestamp] [$Level] [$script:ModuleName.$caller] $Message"

    # Write to appropriate stream
    switch ($Level) {
        'Information' { Write-Information $logEntry -InformationAction Continue }
        'Warning' { Write-Warning $logEntry }
        'Error' { Write-Error $logEntry }
        'Verbose' { Write-Verbose $logEntry }
        'Debug' { Write-Debug $logEntry }
    }

    # Append to log file
    try {
        $logDir = Split-Path $script:LogPath -Parent
        if (-not (Test-Path $logDir)) {
            New-Item -Path $logDir -ItemType Directory -Force | Out-Null
        }
        Add-Content -Path $script:LogPath -Value $logEntry -Encoding UTF8
    } catch {
        Write-Warning "Failed to write to log file: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
    Gets the list of required PowerShell modules for BusBuddy development
.DESCRIPTION
    Returns a structured list of modules with installation priorities and descriptions
.PARAMETER SkipAzure
    Skip Azure-related modules
.EXAMPLE
    Get-RequiredModules -SkipAzure
.NOTES
    Modules are organized by priority for proper dependency management
#>
function Get-RequiredModule {
    [CmdletBinding()]
    [OutputType([System.Object[]])]
    param(
        [Parameter()]
        [switch]$SkipAzure
    )

    Write-ModuleLog "Getting required modules list" -Level Information

    $allModules = @(
        # Phase 1: Security and Foundation
        @{ Name = 'PSScriptAnalyzer'; Description = 'PowerShell code analysis and linting'; Priority = 1 },
        @{ Name = 'Microsoft.PowerShell.SecretManagement'; Description = 'Secure secret storage framework'; Priority = 1 },
        @{ Name = 'Microsoft.PowerShell.SecretStore'; Description = 'Local secret store provider'; Priority = 1 },

        # Phase 2: Azure Integration
        @{ Name = 'Az.Storage'; Description = 'Azure Storage account operations'; Priority = 2; SkipIf = 'SkipAzure' },
        @{ Name = 'Az.KeyVault'; Description = 'Azure Key Vault secrets management'; Priority = 2; SkipIf = 'SkipAzure' },
        @{ Name = 'Az.Monitor'; Description = 'Azure monitoring and diagnostics'; Priority = 2; SkipIf = 'SkipAzure' },

        # Phase 3: Database and Testing
        @{ Name = 'SqlServer'; Description = 'SQL Server management cmdlets'; Priority = 3 },
        @{ Name = 'Pester'; Description = 'PowerShell testing framework'; Priority = 3; Version = '5.6.1' },

        # Phase 4: Development Workflow
        @{ Name = 'PSDepend'; Description = 'PowerShell dependency management'; Priority = 4 },
        @{ Name = 'Plaster'; Description = 'PowerShell template and scaffolding engine'; Priority = 4 },
        @{ Name = 'PSFramework'; Description = 'PowerShell development framework'; Priority = 4 },

        # Phase 5: Advanced Features
        @{ Name = 'NuGet.PackageManagement'; Description = 'NuGet package management integration'; Priority = 5 }
    )

    # Filter modules based on parameters
    $modulesToReturn = $allModules | Where-Object {
        if ($SkipAzure -and $_.SkipIf -eq 'SkipAzure') {
            return $false
        }
        return $true
    }

    Write-ModuleLog "Returning $($modulesToReturn.Count) modules" -Level Information
    return $modulesToReturn
}

<#
.SYNOPSIS
    Tests if a PowerShell module is installed and optionally checks version
.DESCRIPTION
    Validates module installation and version compatibility
.PARAMETER ModuleName
    Name of the module to test
.PARAMETER RequiredVersion
    Specific version to check for (optional)
.EXAMPLE
    Test-ModuleInstallation -ModuleName "Pester" -RequiredVersion "5.6.1"
.NOTES
    Returns structured hashtable with installation status and version information
#>
function Test-ModuleInstallation {
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleName,

        [Parameter()]
        [string]$RequiredVersion
    )

    try {
        $installedModule = Get-Module -ListAvailable -Name $ModuleName -ErrorAction SilentlyContinue

        if (-not $installedModule) {
            Write-ModuleLog "Module $ModuleName not found" -Level Information
            return @{
                Installed = $false
                Available = $false
                ModuleName = $ModuleName
                Message = "Module not installed"
            }
        }

        # Ensure $installedModule is always treated as an array
        if ($installedModule -isnot [System.Collections.IEnumerable] -or $installedModule -is [string]) {
            $installedModule = @($installedModule)
        }

        if ($RequiredVersion) {
            $versionMatch = $installedModule | Where-Object { $_.Version -eq $RequiredVersion }
            if (-not $versionMatch) {
                return @{
                    Installed = $false
                    Available = $true
                    ModuleName = $ModuleName
                    InstalledVersion = $installedModule[0].Version
                    RequiredVersion = $RequiredVersion
                    VersionMatch = $false
                    Message = "Version mismatch"
                }
            }
        }

        return @{
            Installed = $true
            Available = $true
            ModuleName = $ModuleName
            InstalledVersion = $installedModule[0].Version
            RequiredVersion = $RequiredVersion
            VersionMatch = $true
            Message = "Module available"
        }
    } catch {
        Write-ModuleLog "Error testing module $ModuleName`: $($_.Exception.Message)" -Level Error
        return @{
            Installed = $false
            Available = $false
            ModuleName = $ModuleName
            Message = "Error checking module: $($_.Exception.Message)"
        }
    }
}

<#
.SYNOPSIS
    Installs a specific PowerShell module with error handling
.DESCRIPTION
    Installs a module with proper error handling and logging
.PARAMETER ModuleName
    Name of the module to install
.PARAMETER RequiredVersion
    Specific version to install (optional)
.PARAMETER Force
    Force installation even if module exists
.EXAMPLE
    Install-BusBuddyModule -ModuleName "Pester" -RequiredVersion "5.6.1" -Force
.NOTES
    Uses Microsoft PowerShell standards for module installation
#>
function Install-BusBuddyModule {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([System.Collections.Hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleName,

        [Parameter()]
        [string]$RequiredVersion,

        [Parameter()]
        [switch]$Force
    )

    Write-ModuleLog "Installing module: $ModuleName" -Level Information

    if ($PSCmdlet.ShouldProcess($ModuleName, "Install PowerShell Module")) {
        try {
            $installParams = @{
                Name = $ModuleName
                Scope = 'CurrentUser'
                Force = [bool]$Force
                AllowClobber = $true
            }

            if ($RequiredVersion) {
                $installParams.RequiredVersion = $RequiredVersion
            }

            Install-Module @installParams -ErrorAction Stop

            Write-ModuleLog "Successfully installed module: $ModuleName" -Level Information
            return @{
                Success = $true
                ModuleName = $ModuleName
                Message = "Module installed successfully"
            }
        }
        catch {
            Write-ModuleLog "Failed to install module $ModuleName`: $($_.Exception.Message)" -Level Error
            return @{
                Success = $false
                ModuleName = $ModuleName
                Message = $_.Exception.Message
            }
        }
    }
    else {
        Write-ModuleLog "Installation of module $ModuleName was cancelled" -Level Information
        return @{
            Success = $false
            ModuleName = $ModuleName
            Message = "Installation cancelled"
        }
    }
}

<#
.SYNOPSIS
    Installs all required BusBuddy PowerShell modules
.DESCRIPTION
    Installs modules in phases with proper error handling and reporting
.PARAMETER SkipAzure
    Skip Azure-related modules
.PARAMETER Force
    Force installation of all modules
.EXAMPLE
    Install-AllBusBuddyModules -SkipAzure -Force
.NOTES
    Installs modules in phases for proper dependency management
#>
function Install-AllBusBuddyModule {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([System.Collections.Hashtable])]
    param(
        [Parameter()]
        [switch]$SkipAzure,

        [Parameter()]
        [switch]$Force
    )

    Write-ModuleLog "Starting installation of all BusBuddy modules" -Level Information

    $modules = Get-RequiredModules -SkipAzure:$SkipAzure
    $results = @{
        Installed = @()
        Updated = @()
        Failed = @()
        Skipped = @()
    }

    for ($phase = 1; $phase -le 5; $phase++) {
        $phaseModules = $modules | Where-Object Priority -EQ $phase
        if ($phaseModules) {
            Write-ModuleLog "Installing Phase $phase modules" -Level Information

            foreach ($moduleInfo in $phaseModules) {
                $testResult = Test-ModuleInstallation -ModuleName $moduleInfo.Name -RequiredVersion $moduleInfo.Version

                if (-not $testResult.Installed -or $Force) {
                    $installResult = Install-BusBuddyModule -ModuleName $moduleInfo.Name -RequiredVersion $moduleInfo.Version -Force:$Force

                    if ($installResult.Success) {
                        $results.Installed += $moduleInfo.Name
                    } else {
                        $results.Failed += @{
                            Name = $moduleInfo.Name
                            Error = $installResult.Message
                        }
                    }
                } else {
                    $results.Skipped += $moduleInfo.Name
                    Write-ModuleLog "Module $($moduleInfo.Name) already installed" -Level Information
                }
            }
        }
    }

    Write-ModuleLog "Module installation completed: $($results.Installed.Count) installed, $($results.Failed.Count) failed" -Level Information
    return $results
}

<#
.SYNOPSIS
    Validates that the module can be imported and basic functions are available
.DESCRIPTION
    Tests module loading and function availability for validation
.EXAMPLE
    Test-ModuleImport
.NOTES
    Returns structured information about module status and exported functions
#>
function Test-ModuleImport {
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    param()

    Write-ModuleLog "Testing Module Import..." -Level Information

    try {
        $module = Get-Module -Name $script:ModuleName
        if ($module) {
            Write-ModuleLog "Module $script:ModuleName is loaded successfully" -Level Information
            return @{
                Success = $true
                ModuleName = $module.Name
                Version = $module.Version
                ExportedFunctions = if ($module.ExportedFunctions) { $module.ExportedFunctions.Keys } else { @() }
            }
        } else {
            Write-ModuleLog "Module $script:ModuleName is not loaded" -Level Warning
            return @{
                Success = $false
                Message = "Module not loaded"
            }
        }
    } catch {
        Write-ModuleLog "Error testing module import: $($_.Exception.Message)" -Level Error
        return @{
            Success = $false
            Message = "Error testing module: $($_.Exception.Message)"
        }
    }
}

# Export module members
Export-ModuleMember -Function @(
    'Get-RequiredModules'
    'Test-ModuleInstallation'
    'Install-BusBuddyModule'
    'Install-AllBusBuddyModules'
    'Test-ModuleImport'
)

# Module initialization
Write-ModuleLog "BusBuddy Installation module loaded successfully" -Level Information
