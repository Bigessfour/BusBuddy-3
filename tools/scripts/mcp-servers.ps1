# BusBuddy MCP Server Management
# Use this script to manage MCP servers in VS Code

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("start", "restart", "check", "setup")]
    [string]$Action = "check"
)

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Start-MCPServer {
    Write-Information "🚀 Starting MCP servers for BusBuddy..." -InformationAction Continue

    # Ensure environment variables are set
    if (-not $env:GITHUB_PERSONAL_ACCESS_TOKEN) {
        Write-Warning "⚠️ GITHUB_PERSONAL_ACCESS_TOKEN not set"
        Write-Information "Please set your GitHub token first:" -InformationAction Continue
        Write-Information "1. Go to GitHub Settings > Developer settings > Personal access tokens" -InformationAction Continue
        Write-Information "2. Generate a new token with 'repo' scope" -InformationAction Continue
        Write-Information "3. Run: [System.Environment]::SetEnvironmentVariable('GITHUB_PERSONAL_ACCESS_TOKEN', 'your_token', 'User')" -InformationAction Continue
        return $false
    }

    # Reload VS Code window to restart MCP servers
    Write-Information "🔄 Reloading VS Code window to restart MCP servers..." -InformationAction Continue
    try {
        & code --command "workbench.action.reloadWindow"
        return $true
    } catch {
        Write-Warning "Failed to reload VS Code window: $($_.Exception.Message)"
        Write-Information "Please manually reload VS Code window (Ctrl+R)" -InformationAction Continue
        return $false
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Test-MCPConfiguration {
    Write-Information "🔍 Checking MCP configuration..." -InformationAction Continue

    $mcpConfigPath = ".vscode\mcp.json"
    if (-not (Test-Path $mcpConfigPath)) {
        Write-Error "MCP configuration not found at $mcpConfigPath"
        return $false
    }

    try {
        $config = Get-Content $mcpConfigPath | ConvertFrom-Json
        $serverCount = $config.servers.PSObject.Properties.Count
        Write-Information "✅ Found $serverCount MCP servers configured" -InformationAction Continue

        foreach ($server in $config.servers.PSObject.Properties) {
            Write-Information "  - $($server.Name): $($server.Value.command) $($server.Value.args -join ' ')" -InformationAction Continue
        }

        return $true
    } catch {
        Write-Error "Invalid MCP configuration: $($_.Exception.Message)"
        return $false
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
function Install-MCPServer {
    Write-Information "📦 Installing MCP server packages..." -InformationAction Continue

    $packages = @(
        "@modelcontextprotocol/server-filesystem",
        "@modelcontextprotocol/server-brave-search",
        "@microsoft/mcp-server-docs",
        "@azure/mcp-server-azure"
    )

    foreach ($package in $packages) {
        Write-Information "Installing $package..." -InformationAction Continue
        try {
            & npm install -g $package
            if ($LASTEXITCODE -eq 0) {
                Write-Information "✅ $package installed successfully" -InformationAction Continue
            } else {
                Write-Warning "❌ Failed to install $package"
            }
        } catch {
            Write-Warning "❌ Error installing $package`: $($_.Exception.Message)"
        }
    }
}

# Main execution
switch ($Action) {
    "start" {
        if (Test-MCPConfiguration) {
            Start-MCPServers
        }
    }
    "restart" {
        Write-Information "🔄 Restarting MCP servers..." -InformationAction Continue
        Start-MCPServers
    }
    "setup" {
        Install-MCPServers
        if (Test-MCPConfiguration) {
            Start-MCPServers
        }
    }
    "check" {
        Write-Information "🔍 BusBuddy MCP Server Status Check" -InformationAction Continue

        # Check environment variables
        $envStatus = @{
            "BRAVE_API_KEY" = [bool]$env:BRAVE_API_KEY
            "GITHUB_PERSONAL_ACCESS_TOKEN" = [bool]$env:GITHUB_PERSONAL_ACCESS_TOKEN
            "AZURE_CLIENT_ID" = [bool]$env:AZURE_CLIENT_ID
        }

        Write-Information "`nEnvironment Variables:" -InformationAction Continue
        foreach ($env in $envStatus.GetEnumerator()) {
            $status = if ($env.Value) { "✅" } else { "❌" }
            Write-Information "  $status $($env.Key)" -InformationAction Continue
        }

        # Check configuration
        Write-Information "`nConfiguration:" -InformationAction Continue
        if (Test-MCPConfiguration) {
            Write-Information "✅ MCP configuration is valid" -InformationAction Continue
        } else {
            Write-Information "❌ MCP configuration has issues" -InformationAction Continue
        }

        # Recommendations
        Write-Information "`n💡 Recommendations:" -InformationAction Continue
        if (-not $env:GITHUB_PERSONAL_ACCESS_TOKEN) {
            Write-Information "1. Set GitHub Personal Access Token" -InformationAction Continue
        }
        Write-Information "2. Run: .\Scripts\mcp-servers.ps1 -Action restart" -InformationAction Continue
        Write-Information "3. Check VS Code Developer Tools Console for any MCP errors" -InformationAction Continue
    }
}
