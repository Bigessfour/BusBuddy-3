#Requires -Version 7.0
<#
.SYNOPSIS
    Updates BusBuddy WPF appsettings to use LocalDB connection
.DESCRIPTION
    Modifies the WPF project's appsettings.json to set DatabaseProvider to LocalDB
    and updates the DefaultConnection to use LocalDB instance.
.PARAMETER WpfProjectPath
    Path to the BusBuddy.WPF project directory (defaults to relative path)
.PARAMETER BackupOriginal
    Create a backup of the original appsettings.json file
.EXAMPLE
    .\Update-AppSettingsForLocalDB.ps1
.EXAMPLE
    .\Update-AppSettingsForLocalDB.ps1 -BackupOriginal
.NOTES
    This script updates appsettings.json in the WPF project to use LocalDB for development.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter()]
    [string]$WpfProjectPath = "../../BusBuddy.WPF",

    [Parameter()]
    [switch]$BackupOriginal
)

# Auto-detect repository root and correct the path if needed
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$autoDetectedWpfPath = Join-Path $repoRoot "BusBuddy.WPF"

# Use auto-detected path if the default doesn't exist but auto-detected does
if (-not (Test-Path $WpfProjectPath) -and (Test-Path $autoDetectedWpfPath)) {
    $WpfProjectPath = $autoDetectedWpfPath
    Write-Information "Auto-detected WPF project path: $WpfProjectPath" -InformationAction Continue
}

function Update-AppSettingsForLocalDB {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppSettingsPath
    )

    # Read current appsettings.json
    if (-not (Test-Path $AppSettingsPath)) {
        Write-Warning "appsettings.json not found at: $AppSettingsPath"
        return
    }

    try {
        $jsonContent = Get-Content -Path $AppSettingsPath -Raw | ConvertFrom-Json

        # Update database provider to LocalDB
        if (-not $jsonContent.DatabaseProvider) {
            $jsonContent | Add-Member -Type NoteProperty -Name "DatabaseProvider" -Value "LocalDB"
        } else {
            $jsonContent.DatabaseProvider = "LocalDB"
        }

        # Ensure LocalDB connection string exists
        if (-not $jsonContent.ConnectionStrings) {
            $jsonContent | Add-Member -Type NoteProperty -Name "ConnectionStrings" -Value @{}
        }

        $localDbConnectionString = "Server=(localdb)\mssqllocaldb;Database=BusBuddy;Trusted_Connection=true;MultipleActiveResultSets=true"
        $jsonContent.ConnectionStrings.DefaultConnection = $localDbConnectionString

        $updatedJson = $jsonContent | ConvertTo-Json -Depth 10

        if ($PSCmdlet.ShouldProcess($AppSettingsPath, "Update appsettings for LocalDB")) {
            Set-Content -Path $AppSettingsPath -Value $updatedJson -Encoding utf8
            Write-Information "✅ Updated appsettings.json for LocalDB" -InformationAction Continue
            return $true
        }
        return $true  # WhatIf scenarios should also return success
    }
    catch {
        Write-Error "Failed to update appsettings.json: $($_.Exception.Message)"
        return $false
    }
}

# Main execution
try {
    # Resolve the WPF project path
    $resolvedPath = Resolve-Path $WpfProjectPath -ErrorAction Stop
    $appSettingsPath = Join-Path $resolvedPath "appsettings.json"

    Write-Information "Target appsettings.json: $appSettingsPath" -InformationAction Continue

    if (-not (Test-Path $appSettingsPath)) {
        throw "appsettings.json not found in WPF project at: $appSettingsPath"
    }

    # Create backup if requested
    if ($BackupOriginal) {
        $backupPath = "$appSettingsPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Write-Information "Creating backup: $backupPath" -InformationAction Continue
        Copy-Item $appSettingsPath $backupPath
    }

    # Update the settings
    if (Update-AppSettingsForLocalDB -AppSettingsPath $appSettingsPath) {
        Write-Information "Configuration updated successfully!" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "Next steps:" -InformationAction Continue
        Write-Information "1. Ensure LocalDB is installed and running (use Install-LocalDB.ps1)" -InformationAction Continue
        Write-Information "2. Run Entity Framework migrations to create the database" -InformationAction Continue
        Write-Information "3. Test your BusBuddy application with LocalDB" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "Connection String: Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;..." -InformationAction Continue
    } else {
        Write-Error "Failed to update configuration"
        exit 1
    }
}
catch {
    Write-Error "Script failed: $($_.Exception.Message)"
    exit 1
}
