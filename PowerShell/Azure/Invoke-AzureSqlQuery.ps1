# Runs a SQL query against Azure SQL using env-based credentials
[CmdletBinding()] param(
    [Parameter()] [string] $Server = "busbuddy-server-sm2.database.windows.net",
    [Parameter()] [string] $Database = "BusBuddyDB",
    [Parameter()] [string] $Query = "SELECT TOP 10 * FROM Students ORDER BY Id DESC",
    [Parameter()] [switch] $UseAzureAD,
    [Parameter()] [string] $TenantId
)

Add-Type -AssemblyName "System.Data.SqlClient"

function Invoke-QueryWithConnection {
    param([System.Data.SqlClient.SqlConnection] $Connection, [string] $Sql)
    $Connection.Open()
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $reader = $cmd.ExecuteReader()
    $table = New-Object System.Data.DataTable
    $table.Load($reader)
    $Connection.Close()
    $table | Format-Table -AutoSize
    Write-Output ([pscustomobject]@{ Rows = $table.Rows.Count; Columns = $table.Columns.Count })
}

$aadRequested = $UseAzureAD -or ($env:AZURE_SQL_USE_AAD -eq 'true')
if ($aadRequested) {
    # Try to acquire an access token using Az.Accounts or Azure CLI
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
    } catch { }

    if (-not $token) {
        try {
            $azCli = Get-Command az -ErrorAction SilentlyContinue
            if ($azCli) {
                $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null
            }
        } catch { }
    }

    if (-not $token) {
        Write-Error "Could not acquire Azure AD access token. Install Az.Accounts (or Azure CLI) and sign in, or run without -UseAzureAD to use SQL authentication."
        exit 1
    }

    $connStr = "Server=tcp:$Server,1433;Initial Catalog=$Database;Encrypt=True;TrustServerCertificate=False;Connection Timeout=15;"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    # Use token-based auth supported by SqlClient via AccessToken property
    $conn.AccessToken = $token
    try { Invoke-QueryWithConnection -Connection $conn -Sql $Query } catch { Write-Error $_.Exception.Message; exit 1 }
} else {
    $azureUser = $env:AZURE_SQL_USER
    $azurePassword = $env:AZURE_SQL_PASSWORD
    if (-not $azureUser -or -not $azurePassword) { Write-Error "AZURE_SQL_USER/AZURE_SQL_PASSWORD not set. Set them or pass -UseAzureAD."; exit 1 }
    $connStr = "Server=tcp:$Server,1433;Initial Catalog=$Database;Persist Security Info=False;User ID=$azureUser;Password=$azurePassword;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=15;"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    try { Invoke-QueryWithConnection -Connection $conn -Sql $Query } catch { Write-Error $_.Exception.Message; exit 1 }
}
