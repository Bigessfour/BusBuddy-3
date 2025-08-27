#Requires -Version 7.5

<#
.SYNOPSIS
    Simple MCP Server Test - Direct Node.js execution

.DESCRIPTION
    Tests the Grok-4 MCP server by running it directly and checking for basic startup.
#>

[CmdletBinding()]
param()

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Message
${3:Parameter description}

.PARAMETER Level
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Write-TestLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Information "[$timestamp] [$Level] $Message" -InformationAction Continue
}

Write-TestLog "🧪 Simple MCP Server Test Starting" "INFO"

# Set environment variables
$env:XAI_API_KEY = $env:XAI_API_KEY
$env:BUSBUDDY_PROJECT_ROOT = (Get-Item ..).FullName
$env:NODE_ENV = "production"

Write-TestLog "📁 Project Root: $env:BUSBUDDY_PROJECT_ROOT" "INFO"
Write-TestLog "🔑 XAI API Key: $($env:XAI_API_KEY.Substring(0,10))..." "INFO"

# Test server script exists
$serverScript = ".\servers\grok4-mcp-server.js"
if (Test-Path $serverScript) {
    Write-TestLog "✅ Server script found: $serverScript" "SUCCESS"
} else {
    Write-TestLog "❌ Server script not found: $serverScript" "ERROR"
    exit 1
}

Write-TestLog "🚀 Starting MCP server directly..." "INFO"

try {
    # Run server with timeout and capture output
    $output = & Node $serverScript --timeout 10 2>&1
    Write-TestLog "📤 Server output:" "INFO"
    $output | ForEach-Object { Write-TestLog "   $_" "INFO" }
}
catch {
    Write-TestLog "❌ Error running server: $($_.Exception.Message)" "ERROR"
}

Write-TestLog "✅ Test completed" "SUCCESS"
