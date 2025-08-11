# DEPRECATED — Use Scripts\Query-Students-Azure.cmd instead
# Query students from BusBuddyDB using SQL authentication
# Docs: SQLCMD Utility — https://learn.microsoft.com/sql/tools/sqlcmd-utility
# PowerShell Output Streams — https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

[CmdletBinding()]
param(
    [string]$Search = "",
    [int]$Top = 25
)

function Invoke-StudentQuery {
    [CmdletBinding()]
    param(
        [string]$Search,
        [int]$Top
    )

    $server = "tcp:busbuddy-server-sm2.database.windows.net,1433"
    $database = "BusBuddyDB"
    $user = $env:AZURE_SQL_USER
    $pass = $env:AZURE_SQL_PASSWORD

    if (-not $user -or -not $pass) {
        Write-Error "AZURE_SQL_USER/AZURE_SQL_PASSWORD environment variables are not set."
        return
    }

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

    & sqlcmd -S $server -d $database -U $user -P $pass -Q $query -W -s "," | Write-Output
}

Invoke-StudentQuery -Search $Search -Top $Top
