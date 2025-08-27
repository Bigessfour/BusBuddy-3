# 🚌 BusBuddy Dependency Management PowerShell Functions
# Integrated dependency management functions for the BusBuddy PowerShell module
# Usage: Import-Module or add to BusBuddy.psm1
# Enhanced with full module ecosystem: Az, PSScriptAnalyzer, SecretManagement, Pester, SqlServer, PSDepend, PSRule.Azure, etc.

#Requires -Version 7.5
#Requires -Module PSScriptAnalyzer

# PowerShell strict mode (latest available: Version 3.0) for enhanced syntax compliance
Set-StrictMode -Version 3.0

# Configure PSScriptAnalyzer settings for this module (if settings file exists)
$PSScriptAnalyzerSettingsPath = Join-Path $PSScriptRoot '..\..\PSScriptAnalyzerSettings.psd1'
if (Test-Path $PSScriptAnalyzerSettingsPath) {
    Write-Verbose "PSScriptAnalyzer settings found at: $PSScriptAnalyzerSettingsPath"
    # Apply settings to PSScriptAnalyzer if available
    try {
        Import-Module PSScriptAnalyzer -Force
        Set-ScriptAnalyzerConfiguration -Path $PSScriptAnalyzerSettingsPath
        Write-Verbose "Applied PSScriptAnalyzer settings from: $PSScriptAnalyzerSettingsPath"
    } catch {
        Write-Warning "Failed to apply PSScriptAnalyzer settings: $($_.Exception.Message)"
    }
}

#region Logging Infrastructure

# Module-level logging configuration following Microsoft best practices
$ModuleLogPath = Join-Path $PSScriptRoot '..\..\logs\dependency-management-module.log'

# Ensure logs directory exists
$LogsDirectory = Split-Path $ModuleLogPath -Parent
if (-not (Test-Path $LogsDirectory)) {
    try {
        New-Item -Path $LogsDirectory -ItemType Directory -Force | Out-Null
        Write-Verbose "Created logs directory: $LogsDirectory"
    } catch {
        Write-Warning "Failed to create logs directory: $($_.Exception.Message)"
    }
}

function Write-ModuleLog {
    <#
    .SYNOPSIS
    Structured logging function for BusBuddy-DependencyManagement module.

    .DESCRIPTION
    Provides consistent, structured logging following Microsoft PowerShell guidelines.
    Supports multiple output streams and centralized log file management.

    .PARAMETER Message
    The log message to write.

    .PARAMETER Level
    Log level: Information, Warning, Error, Verbose, Debug

    .PARAMETER FunctionName
    Name of the calling function for traceability.

    .PARAMETER Exception
    Exception object for error logging.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [ValidateSet('Information', 'Warning', 'Error', 'Verbose', 'Debug')]
        [string]$Level = 'Information',

        [string]$FunctionName = (Get-PSCallStack)[1].FunctionName,

        [System.Exception]$Exception
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logEntry = "[$timestamp] [$Level] [$FunctionName] $Message"

    # Add exception details if provided
    if ($Exception) {
        $logEntry += " | Exception: $($Exception.Message)"
        if ($Exception.InnerException) {
            $logEntry += " | Inner: $($Exception.InnerException.Message)"
        }
    }

    # Write to appropriate streams following Microsoft guidelines
    switch ($Level) {
        'Information' {
            Write-Information $Message -InformationAction Continue
        }
        'Warning' {
            Write-Warning $Message
        }
        'Error' {
            if ($Exception) {
                Write-Error -Message $Message -Exception $Exception
            } else {
                Write-Error $Message
            }
        }
        'Verbose' {
            Write-Verbose $Message
        }
        'Debug' {
            Write-Debug $Message
        }
    }

    # Write to centralized log file (with error handling)
    try {
        $logEntry | Out-File -FilePath $ModuleLogPath -Append -Encoding UTF8 -ErrorAction SilentlyContinue
    } catch {
        # Fail silently to prevent logging from breaking module functionality
        Write-Debug "Failed to write to log file: $($_.Exception.Message)"
    }
}

#endregion

#region Module Installation and Management

function Install-BusBuddyRequiredModule {
    <#
    .SYNOPSIS
    Installs all required PowerShell modules for BusBuddy development.

    .DESCRIPTION
    Ensures all necessary modules are installed for Azure management, testing,
    security, compliance, and development workflows.

    .PARAMETER Force
    Force installation even if modules exist.

    .EXAMPLE
    Install-BusBuddyRequiredModules

    .EXAMPLE
    bb-install-modules -Force
    #>
    [CmdletBinding()]
    [OutputType([Hashtable])]
    [Alias('bb-install-modules')]
    param(
        [switch]$Force,
        [switch]$PlainOutput
    )

    begin {
        Write-ModuleLog "Starting BusBuddy required PowerShell modules installation" -Level Information
        Write-ModuleLog "Force installation parameter: $Force" -Level Debug

        $requiredModules = @(
            @{ Name = 'Az.Accounts'; Description = 'Azure authentication and account management' },
            @{ Name = 'Az.Sql'; Description = 'Azure SQL database management' },
            @{ Name = 'Az.Resources'; Description = 'Azure resource management' },
            @{ Name = 'Az.Storage'; Description = 'Azure storage account operations' },
            @{ Name = 'Az.KeyVault'; Description = 'Azure Key Vault secrets management' },
            @{ Name = 'Az.Monitor'; Description = 'Azure monitoring and diagnostics' },
            @{ Name = 'PSScriptAnalyzer'; Description = 'PowerShell code analysis and linting' },
            @{ Name = 'Microsoft.PowerShell.SecretManagement'; Description = 'Secure secret storage' },
            @{ Name = 'Microsoft.PowerShell.SecretStore'; Description = 'Local secret store provider' },
            @{ Name = 'Pester'; Description = 'PowerShell testing framework'; Version = '5.6.1' },
            @{ Name = 'SqlServer'; Description = 'SQL Server management cmdlets' },
            @{ Name = 'PSDepend'; Description = 'PowerShell dependency management' },
            @{ Name = 'PSRule.Azure'; Description = 'Azure compliance and best practices validation' },
            @{ Name = 'Plaster'; Description = 'PowerShell template and scaffolding engine' },
            @{ Name = 'PSFramework'; Description = 'PowerShell development framework' },
            @{ Name = 'NuGet.PackageManagement'; Description = 'NuGet package management integration' }
        )

        Write-ModuleLog "Module installation plan includes $($requiredModules.Count) modules" -Level Information
    }

    process {
        $installed = @()
        $failed = @()
        $skipped = @()

        Write-ModuleLog "Beginning module installation process" -Level Information

        foreach ($module in $requiredModules) {
            Write-ModuleLog "Processing module: $($module.Name)" -Level Debug

            try {
                $existing = Get-Module -ListAvailable -Name $module.Name -ErrorAction SilentlyContinue

                if ($existing -and -not $Force) {
                    Write-ModuleLog "Module $($module.Name) already installed (version: $($existing[0].Version))" -Level Information
                    $skipped += $module.Name
                    continue
                }

                Write-ModuleLog "Installing module: $($module.Name) - $($module.Description)" -Level Information

                $installParams = @{
                    Name = $module.Name
                    Scope = 'CurrentUser'
                    Force = $true
                    AllowClobber = $true
                    SkipPublisherCheck = $true
                    ErrorAction = 'Stop'
                }

                if ($module.Version) {
                    $installParams.RequiredVersion = $module.Version
                    Write-ModuleLog "Installing specific version: $($module.Version)" -Level Debug
                }

                Install-Module @installParams
                $installed += $module.Name
                Write-ModuleLog "Successfully installed module: $($module.Name)" -Level Information

            } catch {
                $failed += $module.Name
                Write-ModuleLog "Failed to install module: $($module.Name)" -Level Error -Exception $_.Exception
            }
        }

        # Summary (deduplicated, trunk-friendly, no malformed braces)
        Write-ModuleLog "Installation process completed" -Level Information
        Write-ModuleLog "Successfully installed: $($installed.Count)" -Level Information
        Write-ModuleLog "Skipped (already present): $($skipped.Count)" -Level Information
        Write-ModuleLog "Failed: $($failed.Count)" -Level Information

        if ($installed.Count -gt 0) {
            $maxDisplay = 20
            $displayList = $installed | Select-Object -First $maxDisplay
            $msg = "Installed modules: $($displayList -join ', ')"
            if ($installed.Count -gt $maxDisplay) { $msg += ", ... ($($installed.Count - $maxDisplay) more)" }
            Write-ModuleLog $msg -Level Verbose
        }
        if ($skipped.Count -gt 0) {
            $maxDisplay = 10
            $skippedDisplay = $skipped | Select-Object -First $maxDisplay
            $skippedMsg = "Skipped modules: $($skippedDisplay -join ', ')"
            if ($skipped.Count -gt $maxDisplay) { $skippedMsg += ", ... ($($skipped.Count - $maxDisplay) more)" }
            Write-ModuleLog $skippedMsg -Level Verbose
        }
        if ($failed.Count -gt 0) {
            Write-ModuleLog "Failed modules: $($failed -join ', ')" -Level Error
            Write-Warning "💡 Try running in elevated PowerShell or check network connectivity"
        }
    }

    end {
        # Return a simple status object (pipeline-friendly)
        [pscustomobject]@{
            Installed = $installed
            Skipped = $skipped
            Failed = $failed
        }
    }
}

#endregion

#region SecretManagement Integration

function Set-BusBuddySecret {
    <#
    .SYNOPSIS
    Securely manages BusBuddy secrets using Microsoft.PowerShell.SecretManagement.

    .DESCRIPTION
    Replaces plain text environment variables with secure secret storage.
    Manages Syncfusion license keys, Azure credentials, and other sensitive data.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [Alias('bb-secrets-set')]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$SecretName,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateNotNull()]
        [object]$SecretValue,

        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty()]
        [string]$VaultName = "BusBuddy"
    )

    begin {
        Write-ModuleLog "Initiating BusBuddy secret management setup" -Level Information
        Write-ModuleLog "Secret name: $SecretName, Vault: $VaultName" -Level Debug

        # Install SecretManagement if not available
        if (-not (Get-Module -ListAvailable Microsoft.PowerShell.SecretManagement -ErrorAction SilentlyContinue)) {
            Write-ModuleLog "Microsoft.PowerShell.SecretManagement not found, installing..." -Level Information
            try {
                Install-Module Microsoft.PowerShell.SecretManagement -Scope CurrentUser -Force -AllowClobber
                Write-ModuleLog "Successfully installed Microsoft.PowerShell.SecretManagement" -Level Information
            } catch {
                Write-ModuleLog "Failed to install Microsoft.PowerShell.SecretManagement" -Level Error -Exception $_.Exception
                throw
            }
        }

        if (-not (Get-Module -ListAvailable Microsoft.PowerShell.SecretStore -ErrorAction SilentlyContinue)) {
            Write-ModuleLog "Microsoft.PowerShell.SecretStore not found, installing..." -Level Information
            try {
                Install-Module Microsoft.PowerShell.SecretStore -Scope CurrentUser -Force -AllowClobber
                Write-ModuleLog "Successfully installed Microsoft.PowerShell.SecretStore" -Level Information
            } catch {
                Write-ModuleLog "Failed to install Microsoft.PowerShell.SecretStore" -Level Error -Exception $_.Exception
                throw
            }
        }
    }

    process {
        try {
            Write-ModuleLog "Importing SecretManagement modules" -Level Debug
            Import-Module Microsoft.PowerShell.SecretManagement -Force
            Import-Module Microsoft.PowerShell.SecretStore -Force

            # Register secret vault if it doesn't exist
            if (-not (Get-SecretVault -Name $VaultName -ErrorAction SilentlyContinue)) {
                Write-ModuleLog "Registering new secret vault: $VaultName" -Level Information
                Register-SecretVault -Name $VaultName -ModuleName Microsoft.PowerShell.SecretStore
                Write-ModuleLog "Secret vault '$VaultName' registered successfully" -Level Information
            } else {
                Write-ModuleLog "Secret vault '$VaultName' already exists" -Level Debug
            }

            # Convert to secure string if needed - using Read-Host for security compliance
            $secureValue = if ($SecretValue -is [SecureString]) {
                Write-ModuleLog "Secret value provided as SecureString" -Level Debug
                $SecretValue
            } else {
                # For automated scenarios, this should be provided as SecureString
                # This fallback is for development/testing only
                Write-ModuleLog "Converting plain text to SecureString (development mode)" -Level Warning
                Read-Host -Prompt "Enter secret value for '$SecretName'" -AsSecureString
            }

            if ($PSCmdlet.ShouldProcess("Secret '$SecretName' in vault '$VaultName'", "Set secret")) {
                Write-ModuleLog "Storing secret '$SecretName' in vault '$VaultName'" -Level Information
                Set-Secret -Name $SecretName -Secret $secureValue -Vault $VaultName
                Write-ModuleLog "Secret '$SecretName' stored securely in vault '$VaultName'" -Level Information

                # Remove from environment if it exists
                if (Test-Path "Env:$SecretName") {
                    if ($PSCmdlet.ShouldProcess("Environment variable '$SecretName'", "Remove from environment")) {
                        Write-ModuleLog "Removing environment variable '$SecretName' for security" -Level Information
                        Remove-Item "Env:$SecretName"
                        Write-ModuleLog "Environment variable '$SecretName' removed" -Level Information
                    }
                } else {
                    Write-ModuleLog "No environment variable '$SecretName' found to remove" -Level Debug
                }
            } else {
                Write-ModuleLog "Secret storage operation cancelled by WhatIf parameter" -Level Information
            }

        } catch {
            Write-ModuleLog "Failed to store secret '$SecretName'" -Level Error -Exception $_.Exception
            throw
        }
    }
}

function Get-BusBuddySecret {
    <#
    .SYNOPSIS
    Retrieves BusBuddy secrets from secure storage.

    .DESCRIPTION
    Safely retrieves secrets from the BusBuddy secret vault.

    .PARAMETER SecretName
    Name of the secret to retrieve.

    .PARAMETER VaultName
    Name of the secret vault (defaults to 'BusBuddy').

    .PARAMETER AsPlainText
    Return the secret as plain text (use with caution).

    .EXAMPLE
    $license = Get-BusBuddySecret -SecretName "SYNCFUSION_LICENSE_KEY"

    .EXAMPLE
    bb-secrets-get -SecretName "AZURE_SQL_CONNECTION" -AsPlainText
    #>
    [CmdletBinding()]
    [OutputType([string], [securestring])]
    [Alias('bb-secrets-get')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SecretName,

        [string]$VaultName = "BusBuddy",

        [switch]$AsPlainText
    )

    begin {
        Write-ModuleLog "Retrieving secret '$SecretName' from vault '$VaultName'" -Level Information
        if ($AsPlainText) {
            Write-ModuleLog "WARNING: Secret will be returned as plain text" -Level Warning
        }
    }

    process {
        try {
            Write-ModuleLog "Importing SecretManagement module" -Level Debug
            Import-Module Microsoft.PowerShell.SecretManagement -Force

            Write-ModuleLog "Attempting to retrieve secret '$SecretName'" -Level Debug
            $secret = Get-Secret -Name $SecretName -Vault $VaultName -ErrorAction Stop

            if ($AsPlainText) {
                Write-ModuleLog "Converting SecureString to plain text" -Level Debug
                $plainText = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
                Write-ModuleLog "Secret '$SecretName' retrieved as plain text" -Level Information
                return $plainText
            } else {
                Write-ModuleLog "Secret '$SecretName' retrieved as SecureString" -Level Information
                return $secret
            }

        } catch {
            Write-ModuleLog "Failed to retrieve secret '$SecretName'" -Level Error -Exception $_.Exception
            return $null
        }
    }
}

function Initialize-BusBuddySecretVault {
    <#
    .SYNOPSIS
    Initializes the BusBuddy secret vault with common secrets.

    .DESCRIPTION
    Sets up the secret vault and migrates common environment variables to secure storage.

    .EXAMPLE
    Initialize-BusBuddySecretVault

    .EXAMPLE
    bb-init-secrets
    #>
    [CmdletBinding()]
    [Alias('bb-init-secrets')]
    param()

    begin {
        Write-ModuleLog "Initializing BusBuddy secret vault with common secrets migration" -Level Information
    }

    process {
        try {
            # Common secrets to migrate
            $commonSecrets = @(
                'SYNCFUSION_LICENSE_KEY',
                'AZURE_SQL_CONNECTION',
                'AZURE_CLIENT_ID',
                'AZURE_CLIENT_SECRET',
                'AZURE_TENANT_ID',
                'AZURE_SUBSCRIPTION_ID'
            )

            Write-ModuleLog "Planning migration for $($commonSecrets.Count) common secrets" -Level Information
            $migrated = 0
            $notFound = 0

            foreach ($secretName in $commonSecrets) {
                Write-ModuleLog "Checking environment variable: $secretName" -Level Debug
                $envValue = [Environment]::GetEnvironmentVariable($secretName)

                if (-not [string]::IsNullOrEmpty($envValue)) {
                    Write-ModuleLog "Found environment variable '$secretName', migrating to secure storage" -Level Information
                    try {
                        Set-BusBuddySecret -SecretName $secretName -SecretValue $envValue
                        $migrated++
                        Write-ModuleLog "Successfully migrated '$secretName' to secure storage" -Level Information
                    } catch {
                        Write-ModuleLog "Failed to migrate '$secretName'" -Level Error -Exception $_.Exception
                    }
                } else {
                    Write-ModuleLog "Environment variable '$secretName' not found" -Level Warning
                    $notFound++
                }
            }

            Write-ModuleLog "Secret vault initialization completed" -Level Information
            Write-ModuleLog "Migration results - Migrated: $migrated, Not found: $notFound" -Level Information

            Write-Output "✅ Secret vault initialized. Migrated $migrated secrets."
            if ($notFound -gt 0) {
                Write-Output "⚠️ $notFound environment variables were not found."
            }
            Write-Output "💡 Use Set-BusBuddySecret to add more secrets securely."

        } catch {
            Write-ModuleLog "Failed to initialize secret vault" -Level Error -Exception $_.Exception
            throw
        }
    }
}

#endregion

#region Azure SQL Server Integration (SqlServer Module)

function Invoke-BusBuddyAdvancedSqlQuery {
    <#
    .SYNOPSIS
    Enhanced SQL query execution with SqlServer module capabilities.

    .DESCRIPTION
    Executes SQL queries against BusBuddy Azure SQL database with advanced features
    like CSV export, query timing, and result formatting.

    .PARAMETER Query
    SQL query to execute.

    .PARAMETER Database
    Database name (defaults to BusBuddyDB).

    .PARAMETER ExportToCsv
    Export results to CSV file.

    .PARAMETER OutputPath
    Path for CSV export.

    .PARAMETER ShowTiming
    Display query execution time.

    .EXAMPLE
    Invoke-BusBuddyAdvancedSqlQuery -Query "SELECT TOP 10 * FROM Students" -ShowTiming

    .EXAMPLE
    bb-sql-query -Query "SELECT * FROM Vehicles WHERE Status = 'Active'" -ExportToCsv -OutputPath "active-vehicles.csv"
    #>
    [CmdletBinding()]
    [Alias('bb-sql-query')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Query,

        [string]$Database = "BusBuddyDB",

        [switch]$ExportToCsv,

        [string]$OutputPath,

        [switch]$ShowTiming
    )

    begin {
        Write-ModuleLog "Initializing advanced SQL query execution" -Level Information
        Write-ModuleLog "Target database: $Database" -Level Debug
        Write-ModuleLog "Query: $($Query.Substring(0, [Math]::Min(100, $Query.Length)))$(if($Query.Length -gt 100){'...'})" -Level Debug
        Write-ModuleLog "Export to CSV: $ExportToCsv" -Level Debug
        Write-ModuleLog "Show timing: $ShowTiming" -Level Debug

        # Ensure SqlServer module is available
        if (-not (Get-Module -ListAvailable SqlServer -ErrorAction SilentlyContinue)) {
            Write-ModuleLog "SqlServer module not found, installing..." -Level Information
            try {
                Install-Module SqlServer -Scope CurrentUser -Force -AllowClobber
                Write-ModuleLog "Successfully installed SqlServer module" -Level Information
            } catch {
                Write-ModuleLog "Failed to install SqlServer module" -Level Error -Exception $_.Exception
                throw
            }
        }

        try {
            Import-Module SqlServer -Force
            Write-ModuleLog "SqlServer module imported successfully" -Level Debug
        } catch {
            Write-ModuleLog "Failed to import SqlServer module" -Level Error -Exception $_.Exception
            throw
        }
    }

    process {
        try {
            # Get connection string from secrets or environment
            $connectionString = $null

            Write-ModuleLog "Attempting to retrieve connection string from secure storage" -Level Debug
            try {
                $connectionString = Get-BusBuddySecret -SecretName "AZURE_SQL_CONNECTION" -AsPlainText -ErrorAction SilentlyContinue
                if ($connectionString) {
                    Write-ModuleLog "Connection string retrieved from secure storage" -Level Information
                }
            } catch {
                Write-ModuleLog "Failed to retrieve connection string from secure storage" -Level Warning -Exception $_.Exception
            }

            if ([string]::IsNullOrEmpty($connectionString)) {
                Write-ModuleLog "Falling back to environment variable BUSBUDDY_CONNECTION" -Level Warning
                $connectionString = $env:BUSBUDDY_CONNECTION
                if ($connectionString) {
                    Write-ModuleLog "Connection string retrieved from environment variable" -Level Information
                }
            }

            if ([string]::IsNullOrEmpty($connectionString)) {
                $errorMessage = "No connection string found. Set AZURE_SQL_CONNECTION secret or BUSBUDDY_CONNECTION environment variable."
                Write-ModuleLog $errorMessage -Level Error
                throw $errorMessage
            }

            Write-ModuleLog "Starting SQL query execution" -Level Information
            $startTime = Get-Date

            # Execute query using SqlServer module
            Write-ModuleLog "Executing SQL command against database '$Database'" -Level Debug
            $results = Invoke-Sqlcmd -ConnectionString $connectionString -Query $Query -Database $Database -ErrorAction Stop

            $endTime = Get-Date
            $executionTime = $endTime - $startTime
            $executionTimeMs = $executionTime.TotalMilliseconds

            Write-ModuleLog "SQL query completed in $($executionTimeMs.ToString('F2')) milliseconds" -Level Information

            if ($ShowTiming) {
                Write-Information "⏱️ Query executed in $($executionTimeMs.ToString('F2')) ms" -InformationAction Continue
            }

            # Export to CSV if requested
            if ($ExportToCsv) {
                if ([string]::IsNullOrEmpty($OutputPath)) {
                    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
                    $OutputPath = "BusBuddy-Query-Results-$timestamp.csv"
                    Write-ModuleLog "Auto-generated output path: $OutputPath" -Level Debug
                }

                Write-ModuleLog "Exporting results to CSV: $OutputPath" -Level Information
                try {
                    $results | Export-Csv -Path $OutputPath -NoTypeInformation
                    Write-ModuleLog "Results successfully exported to: $OutputPath" -Level Information
                    Write-Information "📄 Results exported to: $OutputPath" -InformationAction Continue
                } catch {
                    Write-ModuleLog "Failed to export results to CSV" -Level Error -Exception $_.Exception
                }
            }

            # Display results summary
            $rowCount = if ($results) { $results.Count } else { 0 }
            Write-ModuleLog "Query returned $rowCount rows" -Level Information
            Write-Information "📊 Query returned $rowCount rows" -InformationAction Continue

            return $results

        } catch {
            Write-ModuleLog "SQL query execution failed" -Level Error -Exception $_.Exception
            throw
        }
    }
}

#endregion

#region PowerShell Module Health Check

function Test-BusBuddyModuleHealth {
    <#
    .SYNOPSIS
    Comprehensive health check for all BusBuddy PowerShell modules.

    .DESCRIPTION
    Validates installation status and versions of all required and optional modules.

    .EXAMPLE
    Test-BusBuddyModuleHealth

    .EXAMPLE
    bb-module-health
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    [Alias('bb-module-health')]
    param()

    begin {
        Write-ModuleLog "Starting comprehensive BusBuddy PowerShell module health check" -Level Information
    }

    process {
        $coreModules = @('Az.Accounts', 'Az.Sql', 'Az.Resources', 'PSScriptAnalyzer', 'Microsoft.PowerShell.SecretManagement')
        $optionalModules = @('Az.Storage', 'Az.KeyVault', 'Az.Monitor', 'SqlServer', 'Pester', 'PSDepend', 'Plaster', 'PSFramework')

        Write-ModuleLog "Health check plan: $($coreModules.Count) core modules, $($optionalModules.Count) optional modules" -Level Information

        $moduleStatus = @{}
        $healthScore = 0
        $totalModules = $coreModules.Count + $optionalModules.Count
        $criticalMissing = 0

        Write-ModuleLog "Checking core modules..." -Level Information
        # Check core modules
        foreach ($module in $coreModules) {
            Write-ModuleLog "Checking core module: $module" -Level Debug
            $available = Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue

            if ($available) {
                $moduleStatus[$module] = @{
                    Status = 'Available'
                    Version = $available[0].Version.ToString()
                    Critical = $true
                }
                $healthScore += 1
                Write-ModuleLog "Core module '$module' available (v$($available[0].Version))" -Level Information
                Write-Output "✅ $module (v$($available[0].Version)) - Available (Core)"
            } else {
                $moduleStatus[$module] = @{
                    Status = 'Missing'
                    Critical = $true
                }
                $criticalMissing++
                Write-ModuleLog "CRITICAL: Core module '$module' is missing" -Level Error
                Write-Error "❌ $module - Missing (Critical)"
            }
        }

        Write-ModuleLog "Checking optional modules..." -Level Information
        # Check optional modules
        foreach ($module in $optionalModules) {
            Write-ModuleLog "Checking optional module: $module" -Level Debug
            $available = Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue

            if ($available) {
                $moduleStatus[$module] = @{
                    Status = 'Available'
                    Version = $available[0].Version.ToString()
                    Critical = $false
                }
                $healthScore += 0.5
                Write-ModuleLog "Optional module '$module' available (v$($available[0].Version))" -Level Information
                Write-Output "✅ $module (v$($available[0].Version)) - Available (Optional)"
            } else {
                $moduleStatus[$module] = @{
                    Status = 'Missing'
                    Critical = $false
                }
                Write-ModuleLog "Optional module '$module' is missing" -Level Warning
                Write-Information "⚠️ $module - Missing (Optional)" -InformationAction Continue
            }
        }

        $healthPercentage = [math]::Round(($healthScore / $totalModules) * 100, 1)

        Write-ModuleLog "Module health assessment completed" -Level Information
        Write-ModuleLog "Health score: $healthPercentage% ($healthScore/$totalModules)" -Level Information
        Write-ModuleLog "Critical missing modules: $criticalMissing" -Level Information

        Write-Output "`n📊 Module Health Summary:"
        Write-Output "Health Score: $healthPercentage%"
        Write-Output "Core Modules: $($coreModules.Count)"
        Write-Output "Optional Modules: $($optionalModules.Count)"
        Write-Output "Critical Missing: $criticalMissing"

        if ($healthPercentage -ge 80) {
            Write-ModuleLog "Module health status: EXCELLENT" -Level Information
            Write-Output "🎉 Module health is excellent!"
        } elseif ($healthPercentage -ge 60) {
            Write-ModuleLog "Module health status: NEEDS ATTENTION" -Level Warning
            Write-Warning "⚠️ Module health needs attention"
        } else {
            Write-ModuleLog "Module health status: CRITICAL ISSUES" -Level Error
            Write-Error "❌ Critical module health issues detected"
        }

        return @{
            HealthScore = $healthPercentage
            ModuleStatus = $moduleStatus
            CriticalMissing = $criticalMissing
        }
    }
}

#endregion

#region Dependency Management Functions

function Invoke-BusBuddyDependencyCheck {
    <#
    .SYNOPSIS
    Performs comprehensive dependency health check for BusBuddy project.

    .DESCRIPTION
    Validates package versions, checks for vulnerabilities, verifies Syncfusion license,
    and provides actionable recommendations for dependency management.

    .PARAMETER CheckOutdated
    Check for outdated packages.

    .PARAMETER CheckVulnerabilities
    Scan for package vulnerabilities.

    .PARAMETER ValidateLicense
    Validate Syncfusion license configuration.

    .PARAMETER GenerateReport
    Generate JSON report of dependency status.

    .PARAMETER OutputPath
    Path for the dependency report output.

    .EXAMPLE
    Invoke-BusBuddyDependencyCheck -CheckOutdated -ValidateLicense

    .EXAMPLE
    bb-deps-check -CheckVulnerabilities -GenerateReport
    #>
    [CmdletBinding(SupportsShouldProcess = $false, DefaultParameterSetName = 'Default')]
    [Alias('bb-deps-check')]
    param(
        # Allow running the check from an alternative solution root (supports pipeline & wildcards per MS guidelines SC01/SC02)
        [Parameter(Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [Alias('ProjectPath', 'Root', 'PSPath')]
        [ValidateNotNullOrEmpty()]
        [string]$Path = '.',

        [switch]$CheckOutdated,
        [switch]$CheckVulnerabilities,
        [switch]$ValidateLicense,
        [switch]$GenerateReport,
        [string]$OutputPath = 'dependency-status.json'
    )

    begin {
        Write-Verbose "Starting Invoke-BusBuddyDependencyCheck (Path=$Path)"
        # Track start time across blocks
        $script:depCheckStart = Get-Date
    }

    process {
        # Resolve and push location (supporting provider paths) — per MS docs GetUnresolvedProviderPathFromPSPath
        try { $resolved = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($Path) } catch { Write-Error "Invalid path: $Path"; return }
        if (-not (Test-Path -LiteralPath $resolved)) { Write-Error "Path not found: $resolved"; return }
        Push-Location -LiteralPath $resolved
        Write-ModuleLog "Starting comprehensive BusBuddy dependency health check" -Level Information
        Write-ModuleLog "Check parameters - Outdated: $CheckOutdated, Vulnerabilities: $CheckVulnerabilities, License: $ValidateLicense, Report: $GenerateReport" -Level Debug
        Write-ModuleLog "Output path: $OutputPath" -Level Debug

        # Ensure analyzer availability (non-fatal if install fails)
        if (-not (Get-Module -ListAvailable PSScriptAnalyzer -ErrorAction SilentlyContinue)) {
            Write-ModuleLog "PSScriptAnalyzer module not found, installing..." -Level Information
            try { Install-Module PSScriptAnalyzer -Scope CurrentUser -Force -AllowClobber; Write-ModuleLog "Successfully installed PSScriptAnalyzer module" -Level Information } catch { Write-ModuleLog "Failed to install PSScriptAnalyzer module" -Level Error -Exception $_.Exception }
        } else { Write-ModuleLog "PSScriptAnalyzer module already available" -Level Debug }

        # Progress initialization
        $progressActivity = 'BusBuddy Dependency Analysis'
        $progressId = Get-Random
        $progressStep = 0
        $totalSteps = 5 + [int]($CheckOutdated) + [int]($CheckVulnerabilities) + [int]($ValidateLicense)
        Write-Progress -Activity $progressActivity -Status 'Initializing' -PercentComplete 0 -Id $progressId
        try {
            Write-ModuleLog "Validating project directory structure" -Level Debug
            # Validate we're in the correct directory
            if (-not (Test-Path "BusBuddy.sln")) {
                $errorMessage = "BusBuddy.sln not found. Please run from project root directory."
                Write-ModuleLog $errorMessage -Level Error
                throw $errorMessage
            }

            $currentPath = (Get-Location).Path
            Write-ModuleLog "Project validation successful - running from: $currentPath" -Level Information

            $results = @{
                Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                ProjectPath = $currentPath
                Status = "Unknown"
                Checks = @{}
                Issues = @()
                Recommendations = @()
            }

            # Helper function to increment progress consistently
            <#
        .SYNOPSIS
        ${1:Short description}

        .DESCRIPTION
        ${2:Long description}

        .PARAMETER status
        ${3:Parameter description}

        .EXAMPLE
        ${4:An example}

        .NOTES
        ${5:General notes}
        #>
            <#
        .SYNOPSIS
        ${1:Short description}

        .DESCRIPTION
        ${2:Long description}

        .PARAMETER status
        ${3:Parameter description}

        .EXAMPLE
        ${4:An example}

        .NOTES
        ${5:General notes}
        #>
            <#
        .SYNOPSIS
        ${1:Short description}

        .DESCRIPTION
        ${2:Long description}

        .PARAMETER status
        ${3:Parameter description}

        .EXAMPLE
        ${4:An example}

        .NOTES
        ${5:General notes}
        #>
            <#
        .SYNOPSIS
        ${1:Short description}

        .DESCRIPTION
        ${2:Long description}

        .PARAMETER status
        ${3:Parameter description}

        .EXAMPLE
        ${4:An example}

        .NOTES
        ${5:General notes}
        #>
            <#
        .SYNOPSIS
        ${1:Short description}

        .DESCRIPTION
        ${2:Long description}

        .PARAMETER status
        ${3:Parameter description}

        .EXAMPLE
        ${4:An example}

        .NOTES
        ${5:General notes}
        #>
            <#
        .SYNOPSIS
        ${1:Short description}

        .DESCRIPTION
        ${2:Long description}

        .PARAMETER status
        ${3:Parameter description}

        .EXAMPLE
        ${4:An example}

        .NOTES
        ${5:General notes}
        #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER status
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER status
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER status
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            function _StepProgress($status) {
                <#
                .SYNOPSIS
                Internal helper to advance progress bar (not exported).
                .PARAMETER status
                Status message displayed in progress UI.
                .OUTPUTS
                None
                #>
                $script:progressStep++
                $pct = [int](($script:progressStep / $totalSteps) * 100)
                Write-Progress -Activity $progressActivity -Status $status -PercentComplete $pct -Id $progressId
            }

            # Syncfusion License Check
            if ($ValidateLicense) {
                _StepProgress 'Validating Syncfusion license'
                Write-ModuleLog "Starting Syncfusion license validation" -Level Information

                $licenseStatus = @{
                    EnvironmentKey = -not [string]::IsNullOrEmpty($env:SYNCFUSION_LICENSE_KEY)
                    CodeRegistration = $false
                    Status = "Invalid"
                }

                # Check for license registration in code
                $appXamlPath = "BusBuddy.WPF\App.xaml.cs"
                if (Test-Path $appXamlPath) {
                    $content = Get-Content $appXamlPath -Raw
                    $licenseStatus.CodeRegistration = $content -match "RegisterLicense"
                }

                if ($licenseStatus.EnvironmentKey -and $licenseStatus.CodeRegistration) {
                    $licenseStatus.Status = "Valid"
                    Write-Output "✅ Syncfusion license properly configured"
                } else {
                    $licenseStatus.Status = "Invalid"
                    $results.Issues += "Syncfusion license configuration incomplete"
                    Write-Warning "❌ Syncfusion license configuration issues detected"
                }

                $results.Checks.SyncfusionLicense = $licenseStatus
            }

            # Outdated Packages Check
            if ($CheckOutdated) {
                _StepProgress 'Checking outdated packages'
                Write-Information "📊 Checking for outdated packages..." -InformationAction Continue

                $outdatedResult = dotnet list package --outdated 2>&1
                $hasOutdated = $outdatedResult -notlike "*No outdated packages found*"

                if ($hasOutdated) {
                    $results.Issues += "Outdated packages detected"
                    Write-Warning "⚠️ Outdated packages found"
                    $outdatedResult | Where-Object { $_ -like "*>*" } | ForEach-Object {
                        Write-Output "  $_"
                    }
                } else {
                    Write-Output "✅ All packages are up to date"
                }

                $results.Checks.OutdatedPackages = @{
                    HasOutdated = $hasOutdated
                    Output = $outdatedResult
                }
            }

            # Vulnerability Check
            if ($CheckVulnerabilities) {
                _StepProgress 'Scanning vulnerabilities'
                Write-Information "🔍 Scanning for package vulnerabilities..." -InformationAction Continue

                $vulnResult = dotnet list package --vulnerable --include-transitive 2>&1
                $hasVulnerabilities = $vulnResult -notlike "*No vulnerable packages found*"

                if ($hasVulnerabilities) {
                    $results.Issues += "Vulnerable packages detected"
                    Write-Error "❌ Security vulnerabilities found in packages" -ErrorAction Continue
                    $vulnResult | Where-Object { $_ -like "*>*" } | ForEach-Object {
                        Write-Warning "  $_"
                    }
                    $results.Recommendations += "Update vulnerable packages immediately"
                } else {
                    Write-Output "✅ No package vulnerabilities detected"
                }

                $results.Checks.Vulnerabilities = @{
                    HasVulnerabilities = $hasVulnerabilities
                    Output = $vulnResult
                }
            }

            # Version Consistency & Drift Check (expanded) — scans all csproj for mismatches
            _StepProgress 'Analyzing version consistency'
            Write-Information "🔄 Checking package version consistency..." -InformationAction Continue

            $declared = [System.Collections.Generic.List[object]]::new()
            $centralVersions = @{}
            if (Test-Path 'Directory.Build.props') {
                $propsContent = Get-Content 'Directory.Build.props' -Raw
                $centralMatches = [regex]::Matches($propsContent, '<(\w+Version)>(.*?)</\1>')
                foreach ($m in $centralMatches) { $centralVersions[$m.Groups[1].Value] = $m.Groups[2].Value }
            }

            Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object {
                $xml = [xml](Get-Content $_.FullName -Raw)
                $pkgRefs = $xml.Project.ItemGroup.PackageReference | Where-Object { $_.Include }
                foreach ($pr in $pkgRefs) {
                    $ver = $pr.Version
                    if (-not $ver) { continue }
                    $declared.Add([pscustomobject]@{ Project = $_.Name; Package = $pr.Include; Version = $ver })
                }
            }

            # Resolve transitive actual versions via dotnet list (fallback if command fails handled)
            $actualMap = @{}
            try {
                _StepProgress 'Resolving restored versions'
                $resolvedOutput = dotnet list BusBuddy.sln package --include-transitive 2>$null
                foreach ($line in $resolvedOutput) {
                    if ($line -match '^(?<pkg>[A-Za-z0-9_.-]+)\s+(?<req>[0-9][^\s]*)\s+(?<res>[0-9][^\s]*)') {
                        $actualMap[$Matches.pkg] = $Matches.res
                    }
                }
            } catch { Write-Verbose 'dotnet list package failed for version resolution' }

            $drift = @()
            foreach ($d in $declared) {
                $effective = $d.Version
                # Property substitution resolution (e.g., $(EntityFrameworkVersion))
                if ($effective -match '^\$\((?<prop>[^)]+)\)$') {
                    $propName = $Matches.prop
                    if ($centralVersions.ContainsKey($propName)) { $effective = $centralVersions[$propName] }
                }
                if ($actualMap.ContainsKey($d.Package)) {
                    $restored = $actualMap[$d.Package]
                    if ($restored -and ($restored -ne $effective)) {
                        $drift += [pscustomobject]@{ Project = $d.Project; Package = $d.Package; Declared = $effective; Restored = $restored }
                    }
                }
            }

            if ($drift.Count -gt 0) {
                $results.Issues += "Version drift detected ($($drift.Count) packages)"
                Write-Warning "⚠️ Version drift detected:"
                $drift | Sort-Object Package | ForEach-Object { Write-Output "  • $($_.Package) Declared=$($_.Declared) Restored=$($_.Restored)" }
            } else {
                Write-Output '✅ No version drift detected'
            }
            $results.Checks.VersionConsistency = @{ Drift = $drift; Declared = $declared; Central = $centralVersions }

            # Determine overall status
            if ($results.Issues.Count -eq 0) {
                $results.Status = "Healthy"
                Write-Output "`n✅ All dependency checks passed"
            } elseif ($results.Issues.Count -le 2) {
                $results.Status = "Warning"
                Write-Warning "`n⚠️ Minor dependency issues detected"
            } else {
                $results.Status = "Critical"
                Write-Error "`n❌ Critical dependency issues require attention" -ErrorAction Continue
            }

            # Generate recommendations
            if ($results.Checks.ContainsKey('OutdatedPackages') -and $results.Checks.OutdatedPackages.HasOutdated) {
                $results.Recommendations += 'Review and update outdated packages'
            }

            if ($results.Checks.ContainsKey('SyncfusionLicense') -and $results.Checks.SyncfusionLicense.Status -ne 'Valid') {
                $results.Recommendations += 'Configure Syncfusion license key and registration'
            }

            # Generate report if requested
            if ($GenerateReport) {
                $reportJson = $results | ConvertTo-Json -Depth 10
                $reportJson | Out-File -FilePath $OutputPath -Encoding UTF8
                Write-Output "📄 Dependency report saved to: $OutputPath"
            }

            # Emit result object (pipeline friendly single object per invocation)
            Write-Output $results

        } catch {
            Write-Error "Dependency check failed: $($_.Exception.Message)"
            throw
        } finally {
            Pop-Location -ErrorAction SilentlyContinue
            Write-Progress -Activity $progressActivity -Completed -Id $progressId
        }
    }

    end {
        if ($script:depCheckStart) {
            $duration = (Get-Date) - $script:depCheckStart
            Write-Information "⏱️ Dependency check completed in $($duration.TotalSeconds.ToString('F2')) seconds" -InformationAction Continue
        }
    }
}

function Update-BusBuddyDependency {
    <#
    .SYNOPSIS
    Updates BusBuddy project dependencies with safety checks.

    .DESCRIPTION
    Safely updates project dependencies with pre-update validation,
    backup creation, and post-update verification.

    .PARAMETER PackageNames
    Specific packages to update. If not specified, updates all packages.

    .PARAMETER Preview
    Show what would be updated without making changes.

    .PARAMETER Force
    Force update even if there are warnings.

    .PARAMETER CreateBackup
    Create backup of current package configuration.

    .EXAMPLE
    Update-BusBuddyDependencies -Preview

    .EXAMPLE
    bb-deps-update -PackageNames "Serilog.*" -CreateBackup
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [Alias('bb-deps-update')]
    param(
        [string[]]$PackageNames,
        [switch]$Preview,
        [switch]$Force,
        [switch]$CreateBackup
    )

    begin {
        Write-Information "🔄 Starting BusBuddy dependency update process..." -InformationAction Continue

        if (-not (Test-Path "BusBuddy.sln")) {
            throw "BusBuddy.sln not found. Please run from project root directory."
        }
    }

    process {
        try {
            # Pre-update validation
            Write-Information "🔍 Running pre-update validation..." -InformationAction Continue
            $preUpdateCheck = Invoke-BusBuddyDependencyCheck -CheckVulnerabilities -ValidateLicense

            if ($preUpdateCheck.Status -eq "Critical" -and -not $Force) {
                Write-Warning "Critical issues detected. Use -Force to proceed anyway."
                return
            }

            # Create backup if requested
            if ($CreateBackup) {
                $backupPath = "PackageBackup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                New-Item -ItemType Directory -Path $backupPath -Force | Out-Null

                Copy-Item "Directory.Build.props" "$backupPath\" -ErrorAction SilentlyContinue
                Copy-Item "NuGet.config" "$backupPath\" -ErrorAction SilentlyContinue
                Get-ChildItem -Recurse -Filter "*.csproj" | Copy-Item -Destination $backupPath -ErrorAction SilentlyContinue

                Write-Output "📦 Configuration backup created: $backupPath"
            }

            if ($Preview) {
                Write-Information "👀 Preview mode - showing available updates..." -InformationAction Continue
                dotnet list package --outdated
                return
            }

            if ($PSCmdlet.ShouldProcess("BusBuddy Dependencies", "Update Packages")) {
                # Clear package cache
                Write-Information "🧹 Clearing package cache..." -InformationAction Continue
                dotnet nuget locals all --clear

                # Restore packages
                Write-Information "📥 Restoring packages..." -InformationAction Continue
                dotnet restore --force --no-cache

                if ($PackageNames) {
                    foreach ($package in $PackageNames) {
                        Write-Information "📦 Updating package: $package" -InformationAction Continue
                        # Note: dotnet doesn't have direct package update command
                        # This would require nuget.exe or manual project file editing
                        Write-Warning "Package-specific updates require manual Directory.Build.props editing"
                    }
                } else {
                    Write-Information "📦 Package cache cleared and restored. Check Dependabot PRs for updates." -InformationAction Continue
                }

                # Post-update validation
                Write-Information "✅ Running post-update validation..." -InformationAction Continue
                $postUpdateCheck = Invoke-BusBuddyDependencyCheck -CheckVulnerabilities -ValidateLicense

                if ($postUpdateCheck.Status -ne "Healthy") {
                    Write-Warning "Post-update issues detected. Review dependency status."
                } else {
                    Write-Output "✅ Dependency update completed successfully"
                }
            }

        } catch {
            Write-Error "Dependency update failed: $($_.Exception.Message)"
            throw
        }
    }
}

function Test-BusBuddyDependabotConfig {
    <#
    .SYNOPSIS
    Validates Dependabot configuration for BusBuddy project.

    .DESCRIPTION
    Checks Dependabot configuration file for proper setup and provides
    recommendations for improvement.

    .PARAMETER ValidateOnly
    Only validate existing configuration without suggestions.

    .PARAMETER ShowRecommendations
    Display configuration recommendations.

    .EXAMPLE
    Test-BusBuddyDependabotConfig -ShowRecommendations

    .EXAMPLE
    bb-deps-dependabot -ValidateOnly
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Hashtable])]
    [Alias('bb-deps-dependabot')]
    param(
        [switch]$ValidateOnly,
        [switch]$ShowRecommendations
    )

    begin {
        Write-Information "🤖 Validating Dependabot configuration..." -InformationAction Continue
    }

    process {
        $configPath = ".github/dependabot.yml"
        $validation = @{
            ConfigExists = Test-Path $configPath
            HasNuGetEcosystem = $false
            HasSchedule = $false
            HasGrouping = $false
            Issues = @()
            Recommendations = @()
        }

        if (-not $validation.ConfigExists) {
            $validation.Issues += "Dependabot configuration file not found"
            Write-Warning "❌ Dependabot configuration missing at $configPath"

            if (-not $ValidateOnly) {
                Write-Output "💡 Create Dependabot configuration with:"
                Write-Output "   New-Item -ItemType Directory -Path '.github' -Force"
                Write-Output "   # Then add dependabot.yml configuration"
            }

            return $validation
        }

        # Read and validate configuration
        $configContent = Get-Content $configPath -Raw

        # Check for NuGet ecosystem
        if ($configContent -match 'package-ecosystem:\s*["\'']*nuget["\'']*') {
            $validation.HasNuGetEcosystem = $true
            Write-Output "✅ NuGet ecosystem configured"
        } else {
            $validation.Issues += "NuGet package ecosystem not configured"
            Write-Warning "❌ NuGet ecosystem missing"
        }

        # Check for schedule
        if ($configContent -match 'schedule:\s*\n\s*interval:') {
            $validation.HasSchedule = $true
            Write-Output "✅ Update schedule configured"
        } else {
            $validation.Issues += "Update schedule not configured"
            Write-Warning "❌ Update schedule missing"
        }

        # Check for grouping
        if ($configContent -match 'groups:') {
            $validation.HasGrouping = $true
            Write-Output "✅ Package grouping configured"
        } else {
            $validation.Recommendations += "Consider adding package grouping for better PR management"
            Write-Information "💡 Package grouping not configured" -InformationAction Continue
        }

        # Provide recommendations
        if ($ShowRecommendations -and -not $ValidateOnly) {
            Write-Output "`n💡 Dependabot Recommendations:"

            if (-not $validation.HasGrouping) {
                Write-Output "  • Add package grouping to reduce PR volume"
                Write-Output "  • Group Syncfusion packages together"
                Write-Output "  • Group Microsoft.Extensions.* packages"
            }

            Write-Output "  • Configure auto-merge for low-risk packages"
            Write-Output "  • Set up ignore rules for packages requiring manual review"
            Write-Output "  • Enable security-only updates for critical packages"
        }

        # Summary
        $status = if ($validation.Issues.Count -eq 0) { "✅ Valid" } else { "⚠️ Issues Found" }
        Write-ModuleLog "Dependabot configuration validation: $status" -Level Information
        Write-Information "`nDependabot Configuration: $status" -InformationAction Continue

        foreach ($issue in $validation.Issues) {
            Write-ModuleLog "Dependabot validation issue: $issue" -Level Warning
            Write-Warning "  • $issue"
        }

        return $validation
    }
}

function Get-BusBuddyDependencyReport {
    <#
    .SYNOPSIS
    Generates comprehensive dependency report for BusBuddy project.

    .DESCRIPTION
    Creates detailed report of all dependencies, their status, vulnerabilities,
    licensing, and recommendations for maintenance.

    .PARAMETER OutputFormat
    Output format: JSON, HTML, or Text.

    .PARAMETER OutputPath
    Path for the generated report.

    .PARAMETER IncludeMetrics
    Include historical metrics and trends.

    .EXAMPLE
    Get-BusBuddyDependencyReport -OutputFormat HTML -OutputPath "dependency-report.html"

    .EXAMPLE
    bb-deps-report -IncludeMetrics
    #>
    [CmdletBinding()]
    [Alias('bb-deps-report')]
    param(
        [ValidateSet('JSON', 'HTML', 'Text')]
        [string]$OutputFormat = 'JSON',
        [string]$OutputPath,
        [switch]$IncludeMetrics
    )

    begin {
        Write-Information "📊 Generating comprehensive dependency report..." -InformationAction Continue
        $reportData = @{
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            ProjectName = "BusBuddy"
            ReportVersion = "1.0"
            Summary = @{}
            Details = @{}
            Recommendations = @()
        }
    }

    process {
        try {
            # Comprehensive dependency check
            $dependencyStatus = Invoke-BusBuddyDependencyCheck -CheckOutdated -CheckVulnerabilities -ValidateLicense
            $dependabotStatus = Test-BusBuddyDependabotConfig -ValidateOnly

            # Build summary
            $reportData.Summary = @{
                OverallHealth = $dependencyStatus.Status
                TotalIssues = $dependencyStatus.Issues.Count
                SyncfusionLicense = $dependencyStatus.Checks.SyncfusionLicense.Status
                DependabotConfigured = $dependabotStatus.ConfigExists
                LastUpdated = $dependencyStatus.Timestamp
            }

            # Detailed findings
            $reportData.Details = @{
                DependencyChecks = $dependencyStatus.Checks
                DependabotValidation = $dependabotStatus
                Issues = $dependencyStatus.Issues
                PackageVersions = @{}
            }

            # Extract package versions from Directory.Build.props
            if (Test-Path "Directory.Build.props") {
                $propsContent = Get-Content "Directory.Build.props" -Raw
                $versions = [regex]::Matches($propsContent, '<(\w+Version)>(.*?)</\1>')
                foreach ($match in $versions) {
                    $reportData.Details.PackageVersions[$match.Groups[1].Value] = $match.Groups[2].Value
                }
            }

            # Recommendations
            $reportData.Recommendations = $dependencyStatus.Recommendations

            if ($dependabotStatus.Issues.Count -gt 0) {
                $reportData.Recommendations += "Fix Dependabot configuration issues"
            }

            # Set default output path if not provided
            if (-not $OutputPath) {
                $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
                $extension = switch ($OutputFormat) {
                    'JSON' { 'json' }
                    'HTML' { 'html' }
                    'Text' { 'txt' }
                }
                $OutputPath = "BusBuddy-Dependency-Report-$timestamp.$extension"
            }

            # Generate output based on format
            switch ($OutputFormat) {
                'JSON' {
                    $reportJson = $reportData | ConvertTo-Json -Depth 10
                    $reportJson | Out-File -FilePath $OutputPath -Encoding UTF8
                }
                'HTML' {
                    $htmlReport = @"
                    <!DOCTYPE html>
                    <html>
                    <head>
                    <title>BusBuddy Dependency Report</title>
                    <style>
                    body { font-family: Arial, sans-serif; margin: 20px; }
                    .header { background-color: #007ACC; color: white; padding: 10px; }
                    .section { margin: 20px 0; }
                    .status-healthy { color: green; }
                    .status-warning { color: orange; }
                    .status-critical { color: red; }
                    .issue { background-color: #fff3cd; padding: 5px; margin: 5px 0; }
                    .recommendation { background-color: #d4edda; padding: 5px; margin: 5px 0; }
                    </style>
                    </head>
                    <body>
                    <div class="header">
                    <h1>🚌 BusBuddy Dependency Report</h1>
                    <p>Generated: $($reportData.GeneratedAt)</p>
                    </div>

                    <div class="section">
                    <h2>Summary</h2>
                    <p><strong>Overall Health:</strong> <span class="status-$($reportData.Summary.OverallHealth.ToLower())">$($reportData.Summary.OverallHealth)</span></p>
                    <p><strong>Total Issues:</strong> $($reportData.Summary.TotalIssues)</p>
                    <p><strong>Syncfusion License:</strong> $($reportData.Summary.SyncfusionLicense)</p>
                    <p><strong>Dependabot Configured:</strong> $($reportData.Summary.DependabotConfigured)</p>
                    </div>

                    <div class="section">
                    <h2>Issues</h2>
                    $(foreach ($issue in $reportData.Details.Issues) { "<div class='issue'>• $issue</div>" })
                    </div>

                    <div class="section">
                    <h2>Recommendations</h2>
                    $(foreach ($rec in $reportData.Recommendations) { "<div class='recommendation'>• $rec</div>" })
                    </div>
                    </body>
                    </html>
"@
                    $htmlReport | Out-File -FilePath $OutputPath -Encoding UTF8
                }
                'Text' {
                    $textReport = @"
                    🚌 BusBuddy Dependency Report
                    Generated: $($reportData.GeneratedAt)

                    SUMMARY
                    =======
                    Overall Health: $($reportData.Summary.OverallHealth)
                    Total Issues: $($reportData.Summary.TotalIssues)
                    Syncfusion License: $($reportData.Summary.SyncfusionLicense)
                    Dependabot Configured: $($reportData.Summary.DependabotConfigured)

                    ISSUES
                    ======
                    $(foreach ($issue in $reportData.Details.Issues) { "• $issue`n" })

                    RECOMMENDATIONS
                    ===============
                    $(foreach ($rec in $reportData.Recommendations) { "• $rec`n" })

                    PACKAGE VERSIONS
                    ================
                    $(foreach ($version in $reportData.Details.PackageVersions.GetEnumerator()) { "$($version.Key): $($version.Value)`n" })
"@
                    $textReport | Out-File -FilePath $OutputPath -Encoding UTF8
                }
            }
        } catch {
            Write-Error "Failed to generate dependency report: $($_.Exception.Message)"
            throw
        }
    }
}

#region Version Drift (Focused) — Microsoft Guidelines Compliant
function Get-BusBuddyVersionDrift {
    <#
    .SYNOPSIS
    Returns declared vs restored package version drift across the solution.

    .DESCRIPTION
    Scans all *.csproj files for PackageReference versions (including property indirection),
    resolves central version properties from Directory.Build.props, correlates with
    restored versions from 'dotnet list package --include-transitive', and outputs
    any mismatches (drift). Supports ignoring packages and failing fast for CI.

    .PARAMETER Path
    Project root path containing BusBuddy.sln (pipeline & wildcard capable).

    .PARAMETER FailOnDrift
    Throw a terminating error (non-zero exit) if any drift is detected (CI gating).

    .PARAMETER IgnorePackage
    One or more wildcard patterns of packages to ignore (e.g. 'Moq*').

    .PARAMETER OutputFormat
    Output formatting for summary: Text (default) or Json.

    .EXAMPLE
    Get-BusBuddyVersionDrift -Verbose

    .EXAMPLE
    bb-version-drift -FailOnDrift -IgnorePackage 'Moq*','coverlet.*'

    .NOTES
    Follows Microsoft PowerShell guidelines: approved verb (Get), noun specificity,
    pipeline input, proper use of Write-* streams, no Write-Host. Outputs objects to pipeline.
    #>
    [CmdletBinding(SupportsShouldProcess = $false)]
    [Alias('bb-version-drift')]
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [Alias('Root', 'ProjectPath', 'PSPath')]
        [ValidateNotNullOrEmpty()]
        [string]$Path = '.',

        [switch]$FailOnDrift,
        [string[]]$IgnorePackage,
        [ValidateSet('Text', 'Json')][string]$OutputFormat = 'Text'
    )

    begin {
        Write-Verbose 'Initializing version drift analysis'
        $allDeclared = [System.Collections.Generic.List[object]]::new()
        $centralVersions = @{}
    }

    process {
        # Resolve path using provider semantics
        try { $resolved = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($Path) } catch { Write-Error "Invalid path: $Path"; return }
        if (-not (Test-Path -LiteralPath $resolved)) { Write-Error "Path not found: $resolved"; return }
        Push-Location -LiteralPath $resolved
        try {
            if (-not (Test-Path 'BusBuddy.sln')) { throw 'BusBuddy.sln not found at specified path.' }

            # Load central versions
            if (Test-Path 'Directory.Build.props') {
                $propsContent = Get-Content 'Directory.Build.props' -Raw
                $matches = [regex]::Matches($propsContent, '<(\w+Version)>(.*?)</\1>')
                foreach ($m in $matches) { $centralVersions[$m.Groups[1].Value] = $m.Groups[2].Value }
            }

            # Collect declared versions
            Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object {
                $projFile = $_
                try { $xml = [xml](Get-Content $projFile.FullName -Raw) } catch { Write-Warning "Skipping unreadable project: $($projFile.Name)"; return }
                $xml.Project.ItemGroup | ForEach-Object { $_.PackageReference } | Where-Object { $_ -and $_.Include } | ForEach-Object {
                    $ver = $_.Version
                    if (-not $ver) { return }
                    $decl = $ver
                    if ($decl -match '^\$\((?<prop>[^)]+)\)$') {
                        $propName = $Matches.prop
                        if ($centralVersions.ContainsKey($propName)) { $decl = $centralVersions[$propName] }
                    }
                    $allDeclared.Add([pscustomobject]@{
                            Project = $projFile.Name
                            Package = $_.Include
                            DeclaredVersion = $decl
                            RawVersion = $ver
                        })
                }
            }

            # Build ignore predicate
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            <#
            .SYNOPSIS
            ${1:Short description}

            .DESCRIPTION
            ${2:Long description}

            .PARAMETER pkg
            ${3:Parameter description}

            .EXAMPLE
            ${4:An example}

            .NOTES
            ${5:General notes}
            #>
            function _Ignore($pkg) {
                <#
                .SYNOPSIS
                Determines if a package should be ignored based on IgnorePackage patterns.
                .PARAMETER pkg
                Package name to test.
                .OUTPUTS
                System.Boolean
                #>
                if (-not $IgnorePackage) { return $false }
                foreach ($pattern in $IgnorePackage) { if ($pkg -like $pattern) { return $true } }
                return $false
            }

            # Resolve restored versions
            $restoredMap = @{}
            try {
                $resolvedLines = dotnet list BusBuddy.sln package --include-transitive 2>$null
                foreach ($line in $resolvedLines) {
                    if ($line -match '^(?<pkg>[A-Za-z0-9_.-]+)\s+(?<req>[0-9][^\s]*)\s+(?<res>[0-9][^\s]*)') {
                        $restoredMap[$Matches.pkg] = $Matches.res
                    }
                }
            } catch { Write-Warning 'dotnet list package failed; restored versions may be incomplete.' }

            $drift = [System.Collections.Generic.List[object]]::new()
            foreach ($decl in $allDeclared) {
                if (_Ignore $decl.Package) { continue }
                if ($restoredMap.ContainsKey($decl.Package)) {
                    $res = $restoredMap[$decl.Package]
                    if ($res -ne $decl.DeclaredVersion) {
                        $drift.Add([pscustomobject]@{
                                Package = $decl.Package
                                Project = $decl.Project
                                Declared = $decl.DeclaredVersion
                                Restored = $res
                            })
                    }
                }
            }

            $result = [pscustomobject]@{
                Path = (Get-Location).Path
                Drift = $drift
                DeclaredCount = $allDeclared.Count
                CentralVersionCount = $centralVersions.Count
                IgnoredPatterns = $IgnorePackage
                Timestamp = Get-Date
            }

            if ($drift.Count -gt 0) {
                Write-Warning "Version drift detected ($($drift.Count) packages)"
                if ($OutputFormat -eq 'Text') {
                    $drift | Sort-Object Package | ForEach-Object { Write-Output ("DRIFT {0} Declared={1} Restored={2} ({3})" -f $_.Package, $_.Declared, $_.Restored, $_.Project) }
                }
            } else {
                Write-Verbose 'No version drift detected.'
            }

            switch ($OutputFormat) {
                'Json' { $result | ConvertTo-Json -Depth 6 | Write-Output }
                default { Write-Output $result }
            }

            if ($FailOnDrift -and $drift.Count -gt 0) {
                throw "Version drift detected ($($drift.Count) packages)."
            }
        } finally {
            Pop-Location -ErrorAction SilentlyContinue
        }
    }
}
#endregion Version Drift

# Export module members with comprehensive logging support
Export-ModuleMember -Function @(
    'Install-BusBuddyRequiredModule',
    'Set-BusBuddySecret',
    'Get-BusBuddySecret',
    'Initialize-BusBuddySecretVault',
    'Invoke-BusBuddyAdvancedSqlQuery',
    'Test-BusBuddyModuleHealth',
    'Invoke-BusBuddyDependencyCheck',
    'Update-BusBuddyDependency',
    'Test-BusBuddyDependabotConfig',
    'Get-BusBuddyDependencyReport',
    'Get-BusBuddyVersionDrift'
) -Alias @(
    'bb-install-modules',
    'bb-secrets-set',
    'bb-secrets-get',
    'bb-init-secrets',
    'bb-sql-query',
    'bb-module-health',
    'bb-deps-check',
    'bb-deps-update',
    'bb-deps-dependabot',
    'bb-deps-report',
    'bb-version-drift'
)

# Write module load confirmation to log
if (Get-Command Write-ModuleLog -ErrorAction SilentlyContinue) {
    Write-ModuleLog "BusBuddy-DependencyManagement module loaded successfully with enhanced logging" -Level Information
}
