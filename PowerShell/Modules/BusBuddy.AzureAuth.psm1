# BusBuddy.AzureAuth.psm1
# Default Azure Subscription ID for BusBuddy            Write-Warning "Existing context cannot access target subscription. Forcing fresh login..."
            Write-Host "You will receive a device code to enter at https://microsoft.com/devicelogin" -ForegroundColor Cyan
            Disconnect-AzAccount -Scope Process -ErrorAction SilentlyContinue | Out-Null
            Clear-AzContext -Scope Process -Force -ErrorAction SilentlyContinue | Out-Null
            try {
                $result = Connect-AzAccount -DeviceCode -ErrorAction Stop
                if ($result) {
                    Write-Host "Re-authentication successful!" -ForegroundColor Green
                }
            } catch {
                Write-Warning "Device code authentication failed. Attempting standard interactive login..."
                Connect-AzAccount -ErrorAction Stop | Out-Null
            }yDefaultSubscriptionId = '57b297a5-44cf-4abc-9ac4-91a5ed147de1'
# Minimal Azure authentication module (Microsoft-compliant)
# Reference: https://learn.microsoft.com/en-us/powershell/azure/authenticate-azureps?view=azps-14.3.0

function Connect-BusBuddyAzure {
    <#
    .SYNOPSIS
        Authenticates to Azure using the Az PowerShell module.
    .DESCRIPTION
        Supports interactive, service principal, and managed identity authentication.
        If no credentials are provided and no context exists, always prompts for interactive login.
    .PARAMETER TenantId
        Azure AD tenant ID (for service principal).
    .PARAMETER AppId
        Application (client) ID (for service principal).
    .PARAMETER Secret
        Application secret (for service principal).
    .PARAMETER UseManagedIdentity
        Switch to use managed identity authentication.
    .EXAMPLE
        Connect-BusBuddyAzure            # Interactive login
        Connect-BusBuddyAzure -TenantId '...' -AppId '...' -Secret '...'   # Service principal
        Connect-BusBuddyAzure -UseManagedIdentity   # Managed identity
    #>
    [CmdletBinding()]
    param(
        [string]$TenantId,
        [string]$AppId,
        [string]$Secret,
        [switch]$UseManagedIdentity
    )
    # Use environment variables if parameters are not provided
    if (-not $TenantId) { $TenantId = $env:AZURE_TENANT_ID }
    if (-not $AppId)   { $AppId   = $env:AZURE_CLIENT_ID }
    if (-not $Secret)  { $Secret  = $env:AZURE_CLIENT_SECRET }

    $context = $null
    try {
        $context = Get-AzContext -ErrorAction SilentlyContinue
    } catch {}

    if ($UseManagedIdentity) {
        Connect-AzAccount -Identity | Out-Null
    } elseif ($TenantId -and $AppId -and $Secret) {
        $secureSecret = ConvertTo-SecureString $Secret -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential($AppId, $secureSecret)
        Connect-AzAccount -ServicePrincipal -Tenant $TenantId -Credential $credential | Out-Null
    } elseif (-not $context) {
        Write-Host "No Azure context found. Starting device code authentication..." -ForegroundColor Yellow
        Write-Host "You will receive a device code to enter at https://microsoft.com/devicelogin" -ForegroundColor Cyan
        try {
            $result = Connect-AzAccount -DeviceCode -ErrorAction Stop
            if ($result) {
                Write-Host "Authentication successful!" -ForegroundColor Green
            }
        } catch {
            Write-Warning "Device code authentication failed. Attempting standard interactive login..."
            try {
                Connect-AzAccount -ErrorAction Stop | Out-Null
            } catch {
                Write-Error "Both device code and browser authentication failed. Please check your MFA/conditional access settings or contact your Azure admin. Error: $_"
                return
            }
        }
    } else {
        Write-Host "Azure context already exists: $($context.Account) ($($context.Subscription.Name))" -ForegroundColor Green
        # Check if the existing context has access to subscription, if not force re-login
        try {
            Get-AzSubscription -SubscriptionId $BusBuddyDefaultSubscriptionId -ErrorAction Stop | Out-Null
        } catch {
            Write-Warning "Existing context cannot access target subscription. Forcing fresh login..."
            Disconnect-AzAccount -Scope Process -ErrorAction SilentlyContinue | Out-Null
            Clear-AzContext -Scope Process -Force -ErrorAction SilentlyContinue | Out-Null
            try {
                Connect-AzAccount -DeviceCode -ErrorAction Stop | Out-Null
            } catch {
                Write-Warning "Device code authentication failed. Attempting standard interactive login..."
                Connect-AzAccount -ErrorAction Stop | Out-Null
            }
        }
    }
    # Set default subscription context after login
    try {
        Set-AzContext -Subscription $BusBuddyDefaultSubscriptionId -ErrorAction Stop | Out-Null
        Write-Output "Azure context set to subscription $BusBuddyDefaultSubscriptionId."
    } catch {
        Write-Warning "Could not set default subscription context: $_"
    }
}

function Show-BusBuddyAzAccount {
    <#
    .SYNOPSIS
        Shows the current Azure CLI account context.
    .DESCRIPTION
        Runs 'az account show' and displays the current authenticated account, subscription, and tenant info.
    .EXAMPLE
        Show-BusBuddyAzAccount
    #>
    [CmdletBinding()]
    param()
    az account show
}


function Get-BusBuddyAzContext {
    <#
        .SYNOPSIS
        Gets the current Azure context (subscription, account, tenant).
        .EXAMPLE
        Get-BusBuddyAzContext
        .LINK
        https://learn.microsoft.com/en-us/powershell/module/az.accounts/get-azcontext
    #>
    try {
        Import-Module Az.Accounts -MinimumVersion 5.2.0 -ErrorAction Stop
        $ctx = Get-AzContext
        if ($null -eq $ctx) {
            Write-Output "No Azure context is set. Use Connect-BusBuddyAzure to sign in."
        } else {
            Write-Output $ctx
        }
    } catch {
        Write-Error "Failed to get Azure context: $_"
    }
}

function Set-BusBuddyAzContext {
    <#
        .SYNOPSIS
        Sets the current Azure context to a specified subscription or context name.
        .PARAMETER Name
        The name or ID of the context or subscription.
        .EXAMPLE
        Set-BusBuddyAzContext -Name "MySubscription"
        .LINK
        https://learn.microsoft.com/en-us/powershell/module/az.accounts/set-azcontext
    #>
    param(
        [Parameter(Position=0)]
        [string]$Name
    )
    $sub = $Name
    if (-not $sub) { $sub = $BusBuddyDefaultSubscriptionId }
    try {
        Import-Module Az.Accounts -MinimumVersion 5.2.0 -ErrorAction Stop
        $result = Set-AzContext -Subscription $sub -ErrorAction Stop
        Write-Output "Context set to: $($result.Subscription.Name) ($($result.Account))"
    } catch {
        Write-Error "Failed to set Azure context: $_"
    }
}

function Disconnect-BusBuddyAzure {
    <#
        .SYNOPSIS
        Disconnects all Azure accounts and clears context.
        .EXAMPLE
        Disconnect-BusBuddyAzure
        .LINK
        https://learn.microsoft.com/en-us/powershell/module/az.accounts/disconnect-azaccount
    #>
    try {
        Import-Module Az.Accounts -MinimumVersion 5.2.0 -ErrorAction Stop
        Disconnect-AzAccount -Scope Process -ErrorAction SilentlyContinue | Out-Null
        Clear-AzContext -Scope Process -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Output "Disconnected all Azure accounts and cleared context."
    } catch {
        Write-Error "Failed to disconnect Azure accounts: $_"
    }
}

function Install-BusBuddyAzureMigrationModules {
    <#
        .SYNOPSIS
        Installs Azure migration and database modules for BusBuddy.
        .DESCRIPTION
        Installs Az.DataMigration, Az.Sql, and AzSqlGateway modules for database migration operations.
        .EXAMPLE
        Install-BusBuddyAzureMigrationModules
    #>
    [CmdletBinding()]
    param()

    $modules = @(
        @{Name="Az.Sql"; MinVersion="6.0.5"; Description="Azure SQL service cmdlets"},
        @{Name="Az.DataMigration"; MinVersion="0.15.0"; Description="Database Migration Service cmdlets"},
        @{Name="AzSqlGateway"; MinVersion="0.0.1"; Description="Azure SQL Gateway IP retrieval"}
    )

    foreach ($module in $modules) {
        try {
            Write-Host "Installing $($module.Name) - $($module.Description)..." -ForegroundColor Yellow
            Install-Module -Name $module.Name -MinimumVersion $module.MinVersion -Force -AllowClobber -Scope CurrentUser
            Write-Host "✓ $($module.Name) installed successfully" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to install $($module.Name): $_"
        }
    }
}

function Get-BusBuddyAzureSqlDatabases {
    <#
        .SYNOPSIS
        Lists all Azure SQL databases in the current subscription.
        .EXAMPLE
        Get-BusBuddyAzureSqlDatabases
    #>
    [CmdletBinding()]
    param()

    try {
        Import-Module Az.Sql -ErrorAction Stop
        $databases = Get-AzSqlDatabase
        Write-Output $databases | Format-Table ResourceGroupName, ServerName, DatabaseName, Edition, ServiceObjectiveName -AutoSize
    } catch {
        Write-Error "Failed to retrieve Azure SQL databases: $_"
        Write-Host "Run 'Install-BusBuddyAzureMigrationModules' first if Az.Sql is not installed." -ForegroundColor Yellow
    }
}

function Start-BusBuddyDatabaseMigration {
    <#
        .SYNOPSIS
        Starts a database migration to Azure SQL for BusBuddy.
        .PARAMETER SourceConnectionString
        Source database connection string.
        .PARAMETER TargetServer
        Target Azure SQL server name.
        .PARAMETER TargetDatabase
        Target Azure SQL database name.
        .EXAMPLE
        Start-BusBuddyDatabaseMigration -SourceConnectionString "..." -TargetServer "busbuddy-server" -TargetDatabase "BusBuddyDB"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$SourceConnectionString,
        [Parameter(Mandatory=$true)]
        [string]$TargetServer,
        [Parameter(Mandatory=$true)]
        [string]$TargetDatabase
    )

    try {
        Import-Module Az.DataMigration -ErrorAction Stop
        Write-Host "Starting BusBuddy database migration to Azure SQL..." -ForegroundColor Yellow
        Write-Host "Source: LocalDB/SQL Server" -ForegroundColor Cyan
        Write-Host "Target: $TargetServer/$TargetDatabase" -ForegroundColor Cyan

        # Note: Actual migration would use New-AzDataMigrationTask
        # This is a framework for future implementation
        Write-Warning "Migration framework ready. Implement specific migration logic based on BusBuddy schema requirements."

    } catch {
        Write-Error "Failed to start database migration: $_"
        Write-Host "Run 'Install-BusBuddyAzureMigrationModules' first if Az.DataMigration is not installed." -ForegroundColor Yellow
    }
}

function Get-BusBuddyAzureSqlGatewayIPs {
    <#
        .SYNOPSIS
        Gets Azure SQL Gateway IP addresses for firewall configuration.
        .EXAMPLE
        Get-BusBuddyAzureSqlGatewayIPs
    #>
    [CmdletBinding()]
    param()

    try {
        Import-Module AzSqlGateway -ErrorAction Stop
        $gatewayIPs = Get-AzSqlGatewayIpAddress
        Write-Output "Azure SQL Gateway IP Addresses for firewall configuration:"
        Write-Output $gatewayIPs | Format-Table -AutoSize
    } catch {
        Write-Error "Failed to retrieve Azure SQL Gateway IPs: $_"
        Write-Host "Run 'Install-BusBuddyAzureMigrationModules' first if AzSqlGateway is not installed." -ForegroundColor Yellow
    }
}

Export-ModuleMember -Function Connect-BusBuddyAzure, Show-BusBuddyAzAccount, Get-BusBuddyAzContext, Set-BusBuddyAzContext, Disconnect-BusBuddyAzure, Install-BusBuddyAzureMigrationModules, Get-BusBuddyAzureSqlDatabases, Start-BusBuddyDatabaseMigration, Get-BusBuddyAzureSqlGatewayIPs
