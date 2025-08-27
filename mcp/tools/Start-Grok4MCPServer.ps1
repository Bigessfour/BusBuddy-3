#requires -Version 7.5

<#
.SYNOPSIS
BusBuddy Grok-4 MCP Server Launcher with Secure API Key Management

.DESCRIPTION
Securely launches the Grok-4 MCP Server using the PowerShell profile's API key management.
This script ensures the XAI_API_KEY is available from the secure vault before starting the server.

.PARAMETER Port
Optional port number for testing (development only)

.PARAMETER Validate
Only validate environment without starting server

.EXAMPLE
.\Start-Grok4MCPServer.ps1

.EXAMPLE
.\Start-Grok4MCPServer.ps1 -Validate

.NOTES
Author: BusBuddy Development Team
Version: 1.0.0
Requires: Global-SecureApiManager PowerShell module
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int]$Port,

    [Parameter()]
    [switch]$Validate
)

# Import the global security module if available
$globalSecurityModule = "$PSScriptRoot\..\tools\powershell\Modules\Global-SecureApiManager\Global-SecureApiManager.psm1"
if (Test-Path $globalSecurityModule) {
    try {
        Import-Module $globalSecurityModule -Force -ErrorAction Stop
        Write-Information "✅ Global security module loaded" -InformationAction Continue
    }
    catch {
        Write-Warning "Could not load Global-SecureApiManager: $($_.Exception.Message)"
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
function Test-GrokEnvironment {
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param()

    Write-Information "🔍 Validating Grok-4 MCP Server environment..." -InformationAction Continue

    # Test 1: Check XAI_API_KEY availability
    $apiKeyAvailable = [System.Environment]::GetEnvironmentVariable("XAI_API_KEY") -ne $null
    if ($apiKeyAvailable) {
        Write-Information "✅ XAI_API_KEY environment variable detected" -InformationAction Continue
    }
    else {
        Write-Warning "❌ XAI_API_KEY not found in environment"
        Write-Information "💡 Attempting to retrieve from secure vault..." -InformationAction Continue

        # Try to get from secure vault if function is available
        if (Get-Command "Get-GlobalSecureApiKey" -ErrorAction SilentlyContinue) {
            try {
                $secureKey = Get-GlobalSecureApiKey -Provider "XAI" -AsPlainText -ErrorAction Stop
                if ($secureKey) {
                    $env:XAI_API_KEY = $secureKey
                    Write-Information "✅ Retrieved API key from secure vault" -InformationAction Continue
                    $apiKeyAvailable = $true
                }
            }
            catch {
                Write-Warning "Could not retrieve API key from vault: $($_.Exception.Message)"
            }
        }
        else {
            Write-Warning "Secure vault functions not available"
        }
    }

    # Test 2: Node.js availability
    try {
        $nodeVersion = Node --version 2>$null
        if ($nodeVersion) {
            Write-Information "✅ Node.js available: $nodeVersion" -InformationAction Continue
        }
        else {
            Write-Error "❌ Node.js not found in PATH"
            return $false
        }
    }
    catch {
        Write-Error "❌ Node.js not accessible: $($_.Exception.Message)"
        return $false
    }

    # Test 3: Server file exists
    $serverPath = Join-Path $PSScriptRoot "servers\grok4-mcp-server.js"
    if (Test-Path $serverPath) {
        Write-Information "✅ Grok-4 MCP Server file found" -InformationAction Continue
    }
    else {
        Write-Error "❌ Server file not found: $serverPath"
        return $false
    }

    return $apiKeyAvailable
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
function Start-Grok4Server {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    $serverPath = Join-Path $PSScriptRoot "servers\grok4-mcp-server.js"

    if (-not $PSCmdlet.ShouldProcess("Grok-4 MCP Server", "Start")) {
        return
    }

    try {
        Write-Information "🚀 Starting Grok-4 MCP Server..." -InformationAction Continue
        Write-Information "📁 Server path: $serverPath" -InformationAction Continue

        # Ensure we're in the correct directory
        Push-Location $PSScriptRoot

        # Start the server with proper environment
        $env:BUSBUDDY_NO_WELCOME = "1"  # Suppress welcome messages

        if ($Port) {
            Write-Information "🔧 Development mode: Port $Port" -InformationAction Continue
            & Node $serverPath --port $Port
        }
        else {
            Write-Information "🔗 MCP stdio mode (for VS Code integration)" -InformationAction Continue
            & Node $serverPath
        }
    }
    catch {
        Write-Error "❌ Failed to start Grok-4 MCP Server: $($_.Exception.Message)"
    }
    finally {
        Pop-Location
    }
}

# Main execution
try {
    if ($Validate) {
        Write-Information "🧪 Running validation only..." -InformationAction Continue
        $isValid = Test-GrokEnvironment
        if ($isValid) {
            Write-Information "✅ Environment validation successful!" -InformationAction Continue
            exit 0
        }
        else {
            Write-Error "❌ Environment validation failed"
            exit 1
        }
    }
    else {
        # Validate first, then start
        $isValid = Test-GrokEnvironment
        if ($isValid) {
            Start-Grok4Server
        }
        else {
            Write-Error "❌ Environment validation failed. Cannot start server."
            Write-Information "💡 Try running with -Validate to see specific issues" -InformationAction Continue
            exit 1
        }
    }
}
catch {
    Write-Error "❌ Grok-4 MCP Server launch failed: $($_.Exception.Message)"
    exit 1
}
