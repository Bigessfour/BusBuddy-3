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

Export-ModuleMember -Function Test-BusBuddyAzureSql,Get-BusBuddySqlStatus
