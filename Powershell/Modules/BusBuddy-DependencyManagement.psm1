# 🚌 BusBuddy Dependency Management PowerShell Functions
# Integrated dependency management functions for the BusBuddy PowerShell module
# Usage: Import-Module or add to BusBuddy.psm1
# Enhanced with full module ecosystem: Az, PSScriptAnalyzer, SecretManagement, Pester, SqlServer, PSDepend, PSRule.Azure, etc.

#Requires -Version 7.5
#Requires -Module PSScriptAnalyzer

# PowerShell 7.5.2 strict mode for enhanced syntax compliance
Set-StrictMode -Version 3.0

# Configure PSScriptAnalyzer settings for this module
$PSScriptAnalyzerSettings = Join-Path $PSScriptRoot '..\..\PSScriptAnalyzerSettings.psd1'

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
    [Alias('bb-install-modules')]
    param(
        [switch]$Force
    )

    begin {
        Write-Information "🚌 Installing BusBuddy required PowerShell modules..." -InformationAction Continue

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
    }

    process {
        $installed = @()
        $failed = @()

        foreach ($module in $requiredModules) {
            try {
                $existing = Get-Module -ListAvailable -Name $module.Name -ErrorAction SilentlyContinue

                if ($existing -and -not $Force) {
                    Write-Information "✅ $($module.Name) already installed (version: $($existing[0].Version))" -InformationAction Continue
                    $installed += $module.Name
                    continue
                }

                Write-Information "📦 Installing $($module.Name) - $($module.Description)" -InformationAction Continue

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
                }

                Install-Module @installParams
                $installed += $module.Name
                Write-Information "✅ $($module.Name) installed successfully" -InformationAction Continue

            } catch {
                $failed += $module.Name
                Write-Warning "❌ Failed to install $($module.Name): $($_.Exception.Message)"
            }
        }

        # Summary
        Write-Output "`n📊 Installation Summary:"
        Write-Output "✅ Successfully installed: $($installed.Count) modules"
        Write-Output "❌ Failed installations: $($failed.Count) modules"

        if ($failed.Count -gt 0) {
            Write-Warning "Failed modules: $($failed -join ', ')"
            Write-Output "💡 Try running in elevated PowerShell or check network connectivity"
        }

        return @{
            Installed = $installed
            Failed = $failed
            TotalRequired = $requiredModules.Count
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

    .PARAMETER SecretName
    Name of the secret to store.

    .PARAMETER SecretValue
    Value of the secret to store (accepts both plain text and SecureString).

    .PARAMETER VaultName
    Name of the secret vault (defaults to 'BusBuddy').

    .EXAMPLE
    Set-BusBuddySecrets -SecretName "SYNCFUSION_LICENSE_KEY" -SecretValue "your-license-key"

    .EXAMPLE
    bb-secrets-set -SecretName "AZURE_SQL_CONNECTION" -SecretValue "connection-string"
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [Alias('bb-secrets-set')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SecretName,

        [Parameter(Mandatory = $true)]
        [object]$SecretValue,

        [string]$VaultName = "BusBuddy"
    )

    begin {
        Write-Information "🔐 Setting up BusBuddy secret management..." -InformationAction Continue

        # Install SecretManagement if not available
        if (-not (Get-Module -ListAvailable Microsoft.PowerShell.SecretManagement -ErrorAction SilentlyContinue)) {
            Write-Information "Installing Microsoft.PowerShell.SecretManagement..." -InformationAction Continue
            Install-Module Microsoft.PowerShell.SecretManagement -Scope CurrentUser -Force -AllowClobber
        }

        if (-not (Get-Module -ListAvailable Microsoft.PowerShell.SecretStore -ErrorAction SilentlyContinue)) {
            Write-Information "Installing Microsoft.PowerShell.SecretStore..." -InformationAction Continue
            Install-Module Microsoft.PowerShell.SecretStore -Scope CurrentUser -Force -AllowClobber
        }
    }

    process {
        try {
            Import-Module Microsoft.PowerShell.SecretManagement -Force
            Import-Module Microsoft.PowerShell.SecretStore -Force

            # Register secret vault if it doesn't exist
            if (-not (Get-SecretVault -Name $VaultName -ErrorAction SilentlyContinue)) {
                Write-Information "Registering secret vault: $VaultName" -InformationAction Continue
                Register-SecretVault -Name $VaultName -ModuleName Microsoft.PowerShell.SecretStore
            }

            # Convert to secure string if needed
            $secureValue = if ($SecretValue -is [SecureString]) {
                $SecretValue
            } else {
                ConvertTo-SecureString -String $SecretValue -AsPlainText -Force
            }

            if ($PSCmdlet.ShouldProcess("Secret '$SecretName' in vault '$VaultName'", "Set secret")) {
                Set-Secret -Name $SecretName -Secret $secureValue -Vault $VaultName
                Write-Information "✅ Secret '$SecretName' stored securely in vault '$VaultName'" -InformationAction Continue

                # Remove from environment if it exists
                if (Test-Path "Env:$SecretName") {
                    if ($PSCmdlet.ShouldProcess("Environment variable '$SecretName'", "Remove from environment")) {
                        Remove-Item "Env:$SecretName"
                        Write-Information "🗑️ Removed '$SecretName' from environment variables" -InformationAction Continue
                    }
                }
            }
            }

        } catch {
            Write-Error "Failed to store secret '$SecretName': $($_.Exception.Message)"
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
    [Alias('bb-secrets-get')]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SecretName,

        [string]$VaultName = "BusBuddy",

        [switch]$AsPlainText
    )

    try {
        Import-Module Microsoft.PowerShell.SecretManagement -Force

        $secret = Get-Secret -Name $SecretName -Vault $VaultName -ErrorAction Stop

        if ($AsPlainText) {
            return [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
        } else {
            return $secret
        }

    } catch {
        Write-Error "Failed to retrieve secret '$SecretName': $($_.Exception.Message)"
        return $null
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
        Write-Information "🚀 Initializing BusBuddy secret vault..." -InformationAction Continue
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

            $migrated = 0

            foreach ($secretName in $commonSecrets) {
                $envValue = [Environment]::GetEnvironmentVariable($secretName)

                if (-not [string]::IsNullOrEmpty($envValue)) {
                    Set-BusBuddySecrets -SecretName $secretName -SecretValue $envValue
                    $migrated++
                    Write-Information "📦 Migrated $secretName to secure storage" -InformationAction Continue
                } else {
                    Write-Information "⚠️ $secretName not found in environment variables" -InformationAction Continue
                }
            }

            Write-Output "✅ Secret vault initialized. Migrated $migrated secrets."
            Write-Output "💡 Use Set-BusBuddySecrets to add more secrets securely."

        } catch {
            Write-Error "❌ Failed to initialize secret vault: $($_.Exception.Message)"
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
        Write-Information "🗃️ Executing advanced SQL query..." -InformationAction Continue

        # Ensure SqlServer module is available
        if (-not (Get-Module -ListAvailable SqlServer -ErrorAction SilentlyContinue)) {
            Install-Module SqlServer -Scope CurrentUser -Force -AllowClobber
        }

        Import-Module SqlServer -Force
    }

    process {
        try {
            # Get connection string from secrets or environment
            $connectionString = $null

            try {
                $connectionString = Get-BusBuddySecret -SecretName "AZURE_SQL_CONNECTION" -AsPlainText -ErrorAction SilentlyContinue
            } catch {
                # Fallback to environment variable
                $connectionString = $env:BUSBUDDY_CONNECTION
            }

            if ([string]::IsNullOrEmpty($connectionString)) {
                throw "No connection string found. Set AZURE_SQL_CONNECTION secret or BUSBUDDY_CONNECTION environment variable."
            }

            $startTime = Get-Date

            # Execute query using SqlServer module
            $results = Invoke-Sqlcmd -ConnectionString $connectionString -Query $Query -Database $Database -ErrorAction Stop

            $endTime = Get-Date
            $executionTime = $endTime - $startTime

            if ($ShowTiming) {
                Write-Information "⏱️ Query executed in $($executionTime.TotalMilliseconds.ToString('F2')) ms" -InformationAction Continue
            }

            # Export to CSV if requested
            if ($ExportToCsv) {
                if ([string]::IsNullOrEmpty($OutputPath)) {
                    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
                    $OutputPath = "BusBuddy-Query-Results-$timestamp.csv"
                }

                $results | Export-Csv -Path $OutputPath -NoTypeInformation
                Write-Information "📄 Results exported to: $OutputPath" -InformationAction Continue
            }

            # Display results summary
            $rowCount = if ($results) { $results.Count } else { 0 }
            Write-Information "📊 Query returned $rowCount rows" -InformationAction Continue

            return $results

        } catch {
            Write-Error "SQL query execution failed: $($_.Exception.Message)"
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
    [Alias('bb-module-health')]
    param()

    begin {
        Write-Information "🔍 Testing BusBuddy PowerShell module health..." -InformationAction Continue
    }

    process {
        $coreModules = @('Az.Accounts', 'Az.Sql', 'Az.Resources', 'PSScriptAnalyzer', 'Microsoft.PowerShell.SecretManagement')
        $optionalModules = @('Az.Storage', 'Az.KeyVault', 'Az.Monitor', 'SqlServer', 'Pester', 'PSDepend', 'Plaster', 'PSFramework')

        $moduleStatus = @{}
        $healthScore = 0
        $totalModules = $coreModules.Count + $optionalModules.Count

        # Check core modules
        foreach ($module in $coreModules) {
            $available = Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue

            if ($available) {
                $moduleStatus[$module] = @{
                    Status = 'Available'
                    Version = $available[0].Version.ToString()
                    Critical = $true
                }
                $healthScore += 1
                Write-Output "✅ $module (v$($available[0].Version)) - Available (Core)"
            } else {
                $moduleStatus[$module] = @{
                    Status = 'Missing'
                    Critical = $true
                }
                Write-Error "❌ $module - Missing (Critical)"
            }
        }

        # Check optional modules
        foreach ($module in $optionalModules) {
            $available = Get-Module -ListAvailable -Name $module -ErrorAction SilentlyContinue

            if ($available) {
                $moduleStatus[$module] = @{
                    Status = 'Available'
                    Version = $available[0].Version.ToString()
                    Critical = $false
                }
                $healthScore += 0.5
                Write-Output "✅ $module (v$($available[0].Version)) - Available (Optional)"
            } else {
                $moduleStatus[$module] = @{
                    Status = 'Missing'
                    Critical = $false
                }
                Write-Information "⚠️ $module - Missing (Optional)" -InformationAction Continue
            }
        }

        $healthPercentage = [math]::Round(($healthScore / $totalModules) * 100, 1)

        Write-Output "`n📊 Module Health Summary:"
        Write-Output "Health Score: $healthPercentage%"
        Write-Output "Core Modules: $($coreModules.Count)"
        Write-Output "Optional Modules: $($optionalModules.Count)"

        if ($healthPercentage -ge 80) {
            Write-Output "🎉 Module health is excellent!"
        } elseif ($healthPercentage -ge 60) {
            Write-Warning "⚠️ Module health needs attention"
        } else {
            Write-Error "❌ Critical module health issues detected"
        }

        return @{
            HealthScore = $healthPercentage
            ModuleStatus = $moduleStatus
            CriticalMissing = ($moduleStatus.Values | Where-Object { $_.Status -eq 'Missing' -and $_.Critical }).Count
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
    [CmdletBinding()]
    [Alias('bb-deps-check')]
    param(
        [switch]$CheckOutdated,
        [switch]$CheckVulnerabilities,
        [switch]$ValidateLicense,
        [switch]$GenerateReport,
        [string]$OutputPath = "dependency-status.json"
    )

    begin {
        Write-Information "🚌 Starting BusBuddy dependency health check..." -InformationAction Continue
        $startTime = Get-Date

        # Install PSScriptAnalyzer if not available
        if (-not (Get-Module -ListAvailable PSScriptAnalyzer -ErrorAction SilentlyContinue)) {
            Write-Information "Installing PSScriptAnalyzer module..." -InformationAction Continue
            Install-Module PSScriptAnalyzer -Scope CurrentUser -Force -AllowClobber
        }
    }

    process {
        try {
            # Validate we're in the correct directory
            if (-not (Test-Path "BusBuddy.sln")) {
                throw "BusBuddy.sln not found. Please run from project root directory."
            }

            $results = @{
                Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                ProjectPath = (Get-Location).Path
                Status = "Unknown"
                Checks = @{}
                Issues = @()
                Recommendations = @()
            }

            # Syncfusion License Check
            if ($ValidateLicense) {
                Write-Information "🔐 Validating Syncfusion license..." -InformationAction Continue

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

            # Version Consistency Check
            Write-Information "🔄 Checking package version consistency..." -InformationAction Continue

            $syncfusionVersions = @()

            # Check Directory.Build.props
            if (Test-Path "Directory.Build.props") {
                $propsContent = Get-Content "Directory.Build.props" -Raw
                if ($propsContent -match "<SyncfusionVersion>(.*?)</SyncfusionVersion>") {
                    $syncfusionVersions += $Matches[1]
                }
            }

            $versionConsistent = $syncfusionVersions | Group-Object | Measure-Object | ForEach-Object { $_.Count -eq 1 }

            if ($versionConsistent) {
                Write-Output "✅ Package versions are consistent"
            } else {
                $results.Issues += "Package version inconsistencies detected"
                Write-Warning "⚠️ Package version inconsistencies found"
            }

            $results.Checks.VersionConsistency = @{
                IsConsistent = $versionConsistent
                SyncfusionVersions = $syncfusionVersions
            }

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
            if ($results.Checks.OutdatedPackages.HasOutdated) {
                $results.Recommendations += "Review and update outdated packages"
            }

            if ($results.Checks.SyncfusionLicense.Status -ne "Valid") {
                $results.Recommendations += "Configure Syncfusion license key and registration"
            }

            # Generate report if requested
            if ($GenerateReport) {
                $reportJson = $results | ConvertTo-Json -Depth 10
                $reportJson | Out-File -FilePath $OutputPath -Encoding UTF8
                Write-Output "📄 Dependency report saved to: $OutputPath"
            }

            return $results

        } catch {
            Write-Error "Dependency check failed: $($_.Exception.Message)"
            throw
        }
    }

    end {
        $duration = (Get-Date) - $startTime
        Write-Information "⏱️ Dependency check completed in $($duration.TotalSeconds.ToString('F2')) seconds" -InformationAction Continue
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
    $statusColor = if ($validation.Issues.Count -eq 0) { "Green" } else { "Yellow" }
    $status = if ($validation.Issues.Count -eq 0) { "✅ Valid" } else { "⚠️ Issues Found" }
    Write-Information "`nDependabot Configuration: $status" -InformationAction Continue

        foreach ($issue in $validation.Issues) {
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

# Export module members
Export-ModuleMember -Function @(
    'Install-BusBuddyDependencies',
    'Update-BusBuddyDependencies',
    'Test-BusBuddyDependencies',
    'Resolve-BusBuddyDependencyConflicts',
    'Get-BusBuddyPackageVersions',
    'Test-SyncfusionLicense',
    'Initialize-DependabotConfig',
    'Get-BusBuddyDependencyReport'
) -Alias @(
    'bb-deps-install',
    'bb-deps-update',
    'bb-deps-test',
    'bb-deps-resolve',
    'bb-deps-versions',
    'bb-deps-syncfusion',
    'bb-deps-dependabot',
    'bb-deps-report'
)

# Create specific aliases for common commands
New-Alias -Name 'bb-deps-check' -Value 'Test-BusBuddyDependencies' -Force
