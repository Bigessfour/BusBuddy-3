#requires -Version 7.0
<#
    Query-Students-Azure.ps1 — Queries Students from Azure SQL using Entra ID (-G) auth

    References:
    - Microsoft sqlcmd docs (Windows): https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility
    - PowerShell output streams best practices: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

    Usage:
      .\PowerShell\Scripts\Query-Students-Azure.ps1 -Server "busbuddy-server-sm2.database.windows.net" -Database "BusBuddyDB" -Top 10

    Notes:
    - Requires Azure login with an account that has access (use: az login)
    - Requires sqlcmd on PATH
#>
param(
    [Parameter(Mandatory=$false)]
    [string]$Server = "busbuddy-server-sm2.database.windows.net",

    [Parameter(Mandatory=$false)]
    [string]$Database = "BusBuddyDB",

    [Parameter(Mandatory=$false)]
    [int]$Top = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Define $base for any downstream downloads in this session (avoid undefined variable errors)
$script:base = "https://raw.githubusercontent.com/Bigessfour/BusBuddy-3/master"

function Test-Tool([string]$Name, [string]$Check) {
    if (-not (Get-Command $Check -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' not found. Please install and ensure it is on PATH."
    }
}

try {
    Test-Tool -Name 'sqlcmd' -Check 'sqlcmd'

    $query = @"
SELECT TOP ($Top)
    StudentId,
    StudentName,
    HomeAddress,
    Grade,
    City
FROM Students
ORDER BY StudentId DESC;
"@

    Write-Information "Querying Azure SQL [$Database@$Server] for TOP $Top students..." -InformationAction Continue

    # -G uses Azure AD/Entra ID authentication (docs: sqlcmd -G)
    $sqlCmdParams = @('-S', $Server, '-d', $Database, '-G', '-Q', $query)
    $output = & sqlcmd @sqlCmdParams 2>&1
    $sqlcmdExitCode = $LASTEXITCODE

    if ($sqlcmdExitCode -ne 0) {
        Write-Error ("sqlcmd failed (exit {0}): {1}" -f $sqlcmdExitCode, ($output -join ' '))
        exit $sqlcmdExitCode
    }

    # Emit raw output so callers can pipe/inspect
    $output | ForEach-Object { Write-Output $_ }

    # Quick count check (robust parse)
    $countQuery = 'SET NOCOUNT ON; SELECT COUNT(1) FROM Students;'
    $countLines = & sqlcmd -S $Server -d $Database -G -Q $countQuery -W -h -1 2>&1
    if ($LASTEXITCODE -eq 0 -and $countLines) {
        $countText = $countLines | Where-Object { $_ -match '^[0-9]+$' } | Select-Object -First 1
        if ($countText) {
            [int]$total = [int]$countText
            Write-Information ("Total students: {0}" -f $total) -InformationAction Continue
        }
    }
}
catch {
    Write-Error ("{0}" -f $_)
    exit 1
}
