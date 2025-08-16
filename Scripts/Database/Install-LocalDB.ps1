#Requires -Version 7.0
<#
.SYNOPSIS
    Installs and configures SQL Server LocalDB for BusBuddy development
.DESCRIPTION
    Checks for LocalDB installation, installs if missing (via Chocolatey or manual guidance),
    creates and starts the default instance, and optionally tests the connection.
.PARAMETER SkipInstall
    Skip automatic installation attempt if LocalDB is not found
.PARAMETER TestConnection
    Test LocalDB connection after setup
.EXAMPLE
    .\Install-LocalDB.ps1
.EXAMPLE
    .\Install-LocalDB.ps1 -TestConnection
.NOTES
    Docs: https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter()]
    [switch]$SkipInstall,

    [Parameter()]
    [switch]$TestConnection
)

function Test-LocalDBInstalled {
    # Return true if sqllocaldb command is available
    $cmd = Get-Command sqllocaldb -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $true } else { return $false }
}
function Test-ChocolateyInstalled {
    # Return true if choco command is available
    $cmd = Get-Command choco -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $true } else { return $false }
}
# Enable ShouldProcess for this function (see: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process)
function Install-LocalDBViaChocolatey {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not (Test-ChocolateyInstalled)) {
        Write-Warning "Chocolatey not found. Please install LocalDB manually."
        Write-Information "Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -InformationAction Continue
        return $false
    }

    try {
        Write-Information "Installing SQL Server LocalDB via Chocolatey..." -InformationAction Continue
        if ($PSCmdlet.ShouldProcess("System", "Install SQL Server LocalDB via Chocolatey")) {
            Start-Process -FilePath "choco" -ArgumentList "install", "sqllocaldb", "-y" -Wait -NoNewWindow
            return $?
        }
    }
    catch {
        Write-Warning "Chocolatey installation failed: $($_.Exception.Message)"
        return $false
    }

    return $false
}

function Initialize-LocalDBInstance {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([string]$InstanceName = "MSSQLLocalDB")

    try {
        Write-Information "Checking LocalDB instances..." -InformationAction Continue
        $instances = sqllocaldb info

        if ($instances -contains $InstanceName) {
            Write-Information "Instance '$InstanceName' already exists" -InformationAction Continue

            # Check if running
            $instanceInfo = sqllocaldb info $InstanceName
            if ($instanceInfo -match "State:\s+Running") {
                Write-Information "Instance '$InstanceName' is already running" -InformationAction Continue
                return $true
            } else {
                Write-Information "Starting instance '$InstanceName'..." -InformationAction Continue
                sqllocaldb start $InstanceName
                return $?
            }
        } else {
            if ($PSCmdlet.ShouldProcess("LocalDB", "Create instance $InstanceName")) {
                Write-Information "Creating LocalDB instance '$InstanceName'..." -InformationAction Continue
                sqllocaldb create $InstanceName

                if ($?) {
                    Write-Information "Starting instance '$InstanceName'..." -InformationAction Continue
                    sqllocaldb start $InstanceName
                    return $?
                }
            }
        }
    }
    catch {
        Write-Error "Failed to initialize LocalDB instance: $($_.Exception.Message)"
        return $false
    }

    return $false
}

function Test-LocalDBConnection {
    param([string]$InstanceName = "MSSQLLocalDB")

    $connectionString = "Data Source=(localdb)\$InstanceName;Integrated Security=True;Connect Timeout=5;"

    try {
        Write-Information "Testing LocalDB connection..." -InformationAction Continue

        # Use .NET SqlConnection for testing
        Add-Type -AssemblyName System.Data.SqlClient
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT @@VERSION"
        $version = $command.ExecuteScalar()

        $connection.Close()

        Write-Information "✅ LocalDB connection successful!" -InformationAction Continue
        Write-Information "Version: $($version.Split([Environment]::NewLine)[0])" -InformationAction Continue
        Write-Information "Connection String: $connectionString" -InformationAction Continue

        return $true
    }
    catch {
        Write-Error "LocalDB connection test failed: $($_.Exception.Message)"
        return $false
    }
}

# Main execution
try {
    Write-Information "Checking LocalDB installation..." -InformationAction Continue

    if (Test-LocalDBInstalled) {
        Write-Information "✅ LocalDB is already installed" -InformationAction Continue
    } else {
        Write-Warning "LocalDB not found"

        if ($SkipInstall) {
            Write-Information "Skipping installation as requested" -InformationAction Continue
            Write-Information "Manual installation options:" -InformationAction Continue
            Write-Information "1. Download SQL Server Express LocalDB: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -InformationAction Continue
            Write-Information "2. Or install via Chocolatey: choco install sqllocaldb" -InformationAction Continue
            Write-Information "3. Or install via Visual Studio Installer (Data storage and processing workload)" -InformationAction Continue
            exit 1
        }

        Write-Information "Attempting to install LocalDB..." -InformationAction Continue

        if (-not (Install-LocalDBViaChocolatey)) {
            Write-Error "Automatic installation failed. Please install LocalDB manually:"
            Write-Information "1. Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -InformationAction Continue
            Write-Information "2. Look for 'Express' edition and download the LocalDB installer" -InformationAction Continue
            Write-Information "3. Or install via Visual Studio Installer (Data storage and processing)" -InformationAction Continue
            exit 1
        }

        # Refresh PATH to pick up sqllocaldb
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

        if (-not (Test-LocalDBInstalled)) {
            Write-Error "LocalDB installation completed but sqllocaldb command not found. You may need to restart your shell."
            exit 1
        }
    }

    # Initialize the default instance
    if (Initialize-LocalDBInstance) {
        Write-Information "✅ LocalDB instance ready" -InformationAction Continue

        if ($TestConnection) {
            Test-LocalDBConnection
        } else {
            Write-Information "Connection string for your app: Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;" -InformationAction Continue
        }
    } else {
        Write-Error "Failed to initialize LocalDB instance"
        exit 1
    }
}
catch {
    Write-Error "Setup failed: $($_.Exception.Message)"
    exit 1
}
