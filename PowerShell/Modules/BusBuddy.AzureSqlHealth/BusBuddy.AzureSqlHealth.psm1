# BusBuddy.AzureSqlHealth Module
# References: https://learn.microsoft.com/azure/azure-sql/database/connectivity-architecture

function Test-BusBuddyAzureSql {
    <#
    .SYNOPSIS
        Test connectivity to Azure SQL (fallback to LocalDB on failure).
    .OUTPUTS
        Bool indicating Azure SQL connectivity success.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [string]$SettingsPath = 'appsettings.azure.json'
    )
    if (-not (Test-Path $SettingsPath)) {
        Write-Warning "Azure settings file not found: $SettingsPath"
        return $false
    }
    $attempts = 0
    $maxAttempts = 3
    $delaySeconds = 2
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $lastError = $null
    while ($attempts -lt $maxAttempts) {
        $attempts++
        try {
            $json = Get-Content $SettingsPath -Raw | ConvertFrom-Json -ErrorAction Stop
            $connString = $json.ConnectionStrings.DefaultConnection
            if (-not $connString) { throw 'Connection string not found in JSON (ConnectionStrings.DefaultConnection)' }
            $query = 'SELECT TOP 1 name FROM sys.databases'
            $result = Invoke-Sqlcmd -ConnectionString $connString -Query $query -ErrorAction Stop
            $stopwatch.Stop()
            Write-Information "✅ Azure SQL reachable (attempt $attempts) latency=$([math]::Round($stopwatch.Elapsed.TotalMilliseconds,0))ms Databases: $($result.name -join ',')" -InformationAction Continue
            return $true
        } catch {
            $lastError = $_.Exception.Message
            Write-Warning "Attempt $attempts failed: $lastError"
            if ($attempts -lt $maxAttempts) { Start-Sleep -Seconds $delaySeconds; $delaySeconds *= 2 }
        }
    }
    $stopwatch.Stop()
    Write-Warning "⚠️ Azure SQL test failed after $attempts attempts: $lastError"
    return $false
}

function Test-BusBuddyAzureSqlNative {
    <#
    .SYNOPSIS
        Test Azure SQL connectivity using System.Data.SqlClient directly (no sqlcmd dependency).
    .DESCRIPTION
        Reads connection string from appsettings.azure.json (ConnectionStrings.DefaultConnection) and attempts to open a connection
        and run a lightweight query. Implements simple retry with exponential backoff.
        Docs: https://learn.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnection.open
    .OUTPUTS
        Bool indicating success.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [string]$SettingsPath = 'appsettings.azure.json'
    )
    if (-not (Test-Path $SettingsPath)) { Write-Warning "Azure settings file not found: $SettingsPath"; return $false }
    try {
        $json = Get-Content $SettingsPath -Raw | ConvertFrom-Json -ErrorAction Stop
        $connString = $json.ConnectionStrings.DefaultConnection
        if (-not $connString) { throw 'Connection string not found in JSON (ConnectionStrings.DefaultConnection)' }
    } catch {
        Write-Warning "Failed to parse ${SettingsPath}: $($_.Exception.Message)"; return $false
    }

    # Expand ${ENV_VAR} placeholders if present in connection string
    # Pattern sourced from standard PowerShell regex usage — see Microsoft Docs: about_Regular_Expressions
    $expandEnv = {
        param([string]$s)
        if ([string]::IsNullOrWhiteSpace($s)) { return $s }
        return [regex]::Replace($s, '\$\{([A-Za-z0-9_]+)\}', {
            param($m)
            $name = $m.Groups[1].Value
            $val  = [Environment]::GetEnvironmentVariable($name)
            if (-not $val) { $m.Value } else { $val }
        })
    }
    $original = $connString
    $connString = & $expandEnv $connString
    if ($connString -match '\$\{') {
        Write-Information "ℹ️ One or more environment placeholders are unresolved in connection string (file=${SettingsPath}). Set required variables in the environment before running tests." -InformationAction Continue
    }

    $attempts = 0
    $maxAttempts = 3
    $delaySeconds = 1
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($attempts -lt $maxAttempts) {
        $attempts++
        try {
            $conn = [System.Data.SqlClient.SqlConnection]::new($connString)
            $conn.Open()
            try {
                $cmd = $conn.CreateCommand()
                $cmd.CommandText = 'SELECT DB_NAME()'
                $db = $cmd.ExecuteScalar()
                $sw.Stop()
                Write-Information ("✅ Azure SQL (native) reachable on attempt {0} — DB: {1}, latency={2}ms" -f $attempts, $db, [math]::Round($sw.Elapsed.TotalMilliseconds,0)) -InformationAction Continue
                return $true
            } finally {
                $conn.Dispose()
            }
        } catch {
            Write-Warning ("Native attempt {0} failed: {1}" -f $attempts, $_.Exception.Message)
            if ($attempts -lt $maxAttempts) { Start-Sleep -Seconds $delaySeconds; $delaySeconds = [Math]::Min($delaySeconds * 2, 8) }
        }
    }
    $sw.Stop()
    Write-Warning "⚠️ Azure SQL (native) test failed after $attempts attempts."
    return $false
}

function Get-BusBuddySqlStatus {
    <#
    .SYNOPSIS
        Provide detailed Azure SQL / LocalDB status summary.
    .OUTPUTS
        Hashtable with status fields.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string]$AzureSettingsPath = 'appsettings.azure.json',
        [string]$LocalSettingsPath = 'appsettings.json'
    )
    $status = [ordered]@{}
    $status.Timestamp = Get-Date
    $status.AzureSettingsPresent = Test-Path $AzureSettingsPath
    $status.LocalSettingsPresent = Test-Path $LocalSettingsPath
    $status.AzureReachable = $false
    $status.LocalDbReachable = $false

    if ($status.AzureSettingsPresent) {
        $status.AzureReachable = Test-BusBuddyAzureSql -SettingsPath $AzureSettingsPath
    }
    # LocalDB quick probe via sqlcmd if installed
    try {
        $localJson = if (Test-Path $LocalSettingsPath) { Get-Content $LocalSettingsPath -Raw | ConvertFrom-Json } else { $null }
        $localConn = $localJson.ConnectionStrings.DefaultConnection
        if ($localConn -and $localConn -match 'localdb') {
            try {
                Invoke-Sqlcmd -ConnectionString $localConn -Query 'SELECT 1' -ErrorAction Stop | Out-Null
                $status.LocalDbReachable = $true
            } catch {
                $status.LocalDbReachable = $false
            }
        }
    } catch {
        $status.LocalDbReachable = $false
    }

    $status.PrimaryDataSource = if ($status.AzureReachable) { 'Azure' } elseif ($status.LocalDbReachable) { 'LocalDB' } else { 'Unknown' }
    return $status
}

Export-ModuleMember -Function Test-BusBuddyAzureSql,Test-BusBuddyAzureSqlNative,Get-BusBuddySqlStatus
