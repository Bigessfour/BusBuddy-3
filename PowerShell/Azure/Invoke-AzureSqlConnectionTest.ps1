# Minimal, robust Azure SQL connection test using System.Data.SqlClient
[CmdletBinding()] param(
    [Parameter()] [string] $Server = "busbuddy-server-sm2.database.windows.net",
    [Parameter()] [string] $Database = "BusBuddyDB",
    [Parameter()] [int] $TimeoutSeconds = 15,
    [Parameter()] [switch] $UseAzureAD,
    [Parameter()] [string] $TenantId
)

Write-Information "Testing Azure SQL Connection..." -InformationAction Continue

Add-Type -AssemblyName "System.Data.SqlClient"

if ($UseAzureAD -or ($env:AZURE_SQL_USE_AAD -eq 'true')) {
    # Acquire AAD token via Az.Accounts or Azure CLI
    $token = $null
    try {
        $azModule = Get-Module -ListAvailable -Name Az.Accounts
        if ($azModule) {
            Import-Module Az.Accounts -ErrorAction SilentlyContinue | Out-Null
            if (-not (Get-AzContext -ErrorAction SilentlyContinue)) {
                if ($TenantId) { Connect-AzAccount -Tenant $TenantId -UseDeviceAuthentication | Out-Null }
                else { Connect-AzAccount -UseDeviceAuthentication | Out-Null }
            }
            $token = (Get-AzAccessToken -ResourceUrl "https://database.windows.net/").Token
        }
    }
    catch { }

    if (-not $token) {
        try {
            $azCli = Get-Command az -ErrorAction SilentlyContinue
            if ($azCli) {
                $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null
            }
        }
        catch { }
    }

    if (-not $token) {
        Write-Output ([pscustomobject]@{ Connected = $false; Error = "No Azure AD token found. Run Connect-AzAccount or az login, then retry with -UseAzureAD." })
        exit 1
    }

    $connStr = "Server=tcp:$Server,1433;Initial Catalog=$Database;Encrypt=True;TrustServerCertificate=False;Connection Timeout=$TimeoutSeconds;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $connection.AccessToken = $token
    try {
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT @@VERSION"
        $version = $cmd.ExecuteScalar()
        $connection.Close()
        Write-Output ([pscustomobject]@{ Connected = $true; Server = $Server; Database = $Database; Version = $version; Auth = 'AzureAD' })
    }
    catch {
        Write-Output ([pscustomobject]@{ Connected = $false; Error = $_.Exception.Message; Auth = 'AzureAD' })
        exit 1
    }
}
else {
    $azureUser = $env:AZURE_SQL_USER
    $azurePassword = $env:AZURE_SQL_PASSWORD
    if (-not $azureUser) { Write-Error "AZURE_SQL_USER not set"; exit 1 }
    if (-not $azurePassword) { Write-Error "AZURE_SQL_PASSWORD not set"; exit 1 }

    $connectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;Persist Security Info=False;User ID=$azureUser;Password=$azurePassword;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=$TimeoutSeconds;"
    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT @@VERSION"
        $version = $cmd.ExecuteScalar()
        $connection.Close()
        Write-Output ([pscustomobject]@{ Connected = $true; Server = $Server; Database = $Database; Version = $version; Auth = 'SqlLogin' })
    }
    catch {
        Write-Output ([pscustomobject]@{ Connected = $false; Error = $_.Exception.Message; Auth = 'SqlLogin' })
        exit 1
    }
}
