# DEPRECATED — Use Scripts\Query-Students-Azure.cmd instead
# Query students from busbuddy-db using Entra ID (Azure AD) token
# Docs: SQLCMD Utility — https://learn.microsoft.com/sql/tools/sqlcmd-utility
# Azure AD sign-in in sqlcmd — https://learn.microsoft.com/sql/azure-data-studio/enable-azure-authentication?view=sql-server-ver16

[CmdletBinding()]
param(
    [string]$Search = "",
    [int]$Top = 25,
    [ValidateSet('ActiveDirectoryDefault', 'ActiveDirectoryInteractive', 'ActiveDirectoryDeviceCode')]
    [string]$Auth = 'ActiveDirectoryDefault'
)

function Invoke-StudentQueryAAD {
    [CmdletBinding()]
    param(
        [string]$Search,
        [int]$Top,
        [string]$Auth
    )

    $server = "tcp:busbuddy-server-sm2.database.windows.net,1433"
    $database = "busbuddy-db"

    $filter = ""
    if ($Search) { $filter = "WHERE StudentName LIKE '%$Search%' OR ParentGuardian LIKE '%$Search%'" }

    $query = @"
SET NOCOUNT ON;
SELECT TOP ($Top)
    StudentId, StudentName, Grade, City, State
FROM dbo.Students
$filter
ORDER BY StudentId DESC;
"@

    $sqlArgs = @(
        "--authentication-method", $Auth,
        "-S", $server,
        "-d", $database,
        "-Q", $query,
        "-W",
        "-s", ","
    )

    & sqlcmd @sqlArgs | Write-Output
}

Invoke-StudentQueryAAD -Search $Search -Top $Top -Auth $Auth
