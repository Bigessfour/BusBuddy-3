# BusBuddy-3 Database User Setup Script
# This script creates the Azure AD service principal user in both databases

# Database connection parameters
$serverName = "busbuddy-server-sm2.database.windows.net"
$databases = @("BusBuddyDB", "BusBuddyDB-Staging")
$objectId = "0a93d214-37e7-4147-beaf-8ca8036c614e"

# SQL commands to execute
$sqlCommands = @(
    "CREATE USER [$objectId] FROM EXTERNAL PROVIDER;",
    "ALTER ROLE db_datareader ADD MEMBER [$objectId];",
    "ALTER ROLE db_datawriter ADD MEMBER [$objectId];"
)

# Function to execute SQL using Azure AD authentication
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Server
${3:Parameter description}

.PARAMETER Database
${4:Parameter description}

.PARAMETER Commands
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
function Execute-AzureSql {
    param(
        [string]$Server,
        [string]$Database,
        [string[]]$Commands
    )

    Write-Information "Processing database: $Database" -InformationAction Continue

    foreach ($command in $Commands) {
        Write-Information "Executing: $command" -InformationAction Continue
        try {
            # Using sqlcmd with Azure AD authentication
            $result = sqlcmd -S $Server -d $Database -G -Q $command
            if ($LASTEXITCODE -eq 0) {
                Write-Information "✅ Command executed successfully" -InformationAction Continue
            } else {
                Write-Information "❌ Command failed with exit code: $LASTEXITCODE" -InformationAction Continue
                Write-Information "Output: $result" -InformationAction Continue
            }
        }
        catch {
            Write-Information "❌ Error executing command: $($_.Exception.Message)" -InformationAction Continue
        }
    }
    Write-Information "" -InformationAction Continue
}

# Main execution
Write-Information "=== BusBuddy-3 Database User Setup ===" -InformationAction Continue
Write-Information "Server: $serverName" -InformationAction Continue
Write-Information "Databases: $($databases -join ', ')" -InformationAction Continue
Write-Information "Service Principal Object ID: $objectId" -InformationAction Continue
Write-Information "" -InformationAction Continue

# Check if sqlcmd is available
if (!(Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Information "❌ sqlcmd is not available. Please install SQL Server Command Line Utilities." -InformationAction Continue
    Write-Information "Download from: https://docs.microsoft.com/en-us/sql/tools/sqlcmd-utility" -InformationAction Continue
    exit 1
}

# Execute for each database
foreach ($db in $databases) {
    Execute-AzureSql -Server $serverName -Database $db -Commands $sqlCommands
}

Write-Information "=== Setup Complete ===" -InformationAction Continue
Write-Information "Next steps:" -InformationAction Continue
Write-Information "1. Add GitHub secrets (see AZURE-SQL-SETUP.md)" -InformationAction Continue
Write-Information "2. Test the GitHub workflow" -InformationAction Continue
Write-Information "3. Verify database connectivity from your application" -InformationAction Continue
