# BusBuddy MCP Server Setup and Diagnostics
# This script ensures MCP servers start properly

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("test", "start", "restart", "diagnose")]
    [string]$Action = "diagnose",

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

Write-Information "🔍 BusBuddy MCP Server Diagnostics" -InformationAction Continue

# Function to test MCP server availability
<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
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

.PARAMETER ServerName
${3:Parameter description}

.PARAMETER Command
${4:Parameter description}

.PARAMETER Args
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
function Test-MCPServer {
    param(
        [string]$ServerName,
        [string]$Command,
        [array]$Args
    )

    Write-Information "Testing $ServerName..." -InformationAction Continue
    try {
        $testResult = & $Command @Args --help 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ $ServerName is available" -InformationAction Continue
            return $true
        } else {
            Write-Warning "❌ $ServerName failed: $testResult"
            return $false
        }
    } catch {
        Write-Warning "❌ $ServerName error: $($_.Exception.Message)"
        return $false
    }
}

# Check environment variables
Write-Information "`n🔧 Environment Variables:" -InformationAction Continue
$envVars = @{
    "BRAVE_API_KEY" = $env:BRAVE_API_KEY
    "GITHUB_PERSONAL_ACCESS_TOKEN" = $env:GITHUB_PERSONAL_ACCESS_TOKEN
    "AZURE_CLIENT_ID" = $env:AZURE_CLIENT_ID
    "AZURE_TENANT_ID" = $env:AZURE_TENANT_ID
    "AZURE_SUBSCRIPTION_ID" = $env:AZURE_SUBSCRIPTION_ID
}

foreach ($var in $envVars.GetEnumerator()) {
    if ($var.Value) {
        $maskedValue = if ($var.Key -like "*TOKEN*" -or $var.Key -like "*KEY*") {
            $var.Value.Substring(0, [Math]::Min(10, $var.Value.Length)) + "..."
        } else {
            $var.Value
        }
        Write-Information "✅ $($var.Key): $maskedValue" -InformationAction Continue
    } else {
        Write-Warning "❌ $($var.Key): Not set"
    }
}

# Test MCP servers
Write-Information "`n🧪 Testing MCP Servers:" -InformationAction Continue

$servers = @(
    @{ Name = "Filesystem"; Command = "npx"; Args = @("-y", "@modelcontextprotocol/server-filesystem") },
    @{ Name = "Brave Search"; Command = "npx"; Args = @("-y", "@modelcontextprotocol/server-brave-search") },
    @{ Name = "Microsoft Docs"; Command = "npx"; Args = @("-y", "@microsoft/mcp-server-docs") },
    @{ Name = "Azure MCP"; Command = "npx"; Args = @("-y", "@azure/mcp-server-azure") }
)

$results = @{}
foreach ($server in $servers) {
    $results[$server.Name] = Test-MCPServer -ServerName $server.Name -Command $server.Command -Args $server.Args
}

# Check VS Code MCP configuration
Write-Information "`n📋 VS Code MCP Configuration:" -InformationAction Continue
$mcpConfigPath = ".vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    Write-Information "✅ MCP config exists at $mcpConfigPath" -InformationAction Continue
    $mcpConfig = Get-Content $mcpConfigPath | ConvertFrom-Json
    Write-Information "📊 Configured servers: $($mcpConfig.servers.PSObject.Properties.Name -join ', ')" -InformationAction Continue
} else {
    Write-Warning "❌ No MCP configuration found at $mcpConfigPath"
}

# Summary and recommendations
Write-Information "`n📊 Summary:" -InformationAction Continue
$successCount = ($results.Values | Where-Object { $_ -eq $true }).Count
$totalCount = $results.Count

Write-Information "MCP Servers: $successCount/$totalCount working" -InformationAction Continue

if ($successCount -lt $totalCount) {
    Write-Information "`n🔧 Recommendations:" -InformationAction Continue
    Write-Information "1. Ensure all environment variables are set" -InformationAction Continue
    Write-Information "2. Run 'npm install -g @modelcontextprotocol/server-filesystem @modelcontextprotocol/server-brave-search @microsoft/mcp-server-docs'" -InformationAction Continue
    Write-Information "3. Restart VS Code after fixing environment variables" -InformationAction Continue
    Write-Information "4. Check VS Code Developer Tools Console for MCP server errors" -InformationAction Continue
}

if ($Action -eq "restart") {
    Write-Information "`n🔄 Restarting VS Code..." -InformationAction Continue
    & code --command "workbench.action.reloadWindow"
}
