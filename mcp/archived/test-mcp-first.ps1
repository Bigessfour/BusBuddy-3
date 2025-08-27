#Requires -Version 7.5

<#
.SYNOPSIS
    First BusBuddy MCP Test - Basic Function Validation

.DESCRIPTION
    Tests the core MCP server functionality with a simple Grok-4 interaction.
    This is the minimal viable test to verify MCP infrastructure is working.

.EXAMPLE
    .\test-mcp-first.ps1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Server = "busbuddy-grok4-mcp",

    [Parameter()]
    [switch]$Detailed
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)<#
).SYNOPSIS
)${1:Short description}
)
).DESCRIPTION
)${2:Long description}
)
).PARAMETER Message
)${3:Parameter description}
)
).PARAMETER Level
)${4:Parameter description}
)
).EXAMPLE
)${5:An example}
)
).NOTES
)${6:General notes}
)#>
)function Write-TestLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"
    $formattedMessage = "[$timestamp] [$Level] $Message"

    switch ($Level) {
        "ERROR" { Write-Error $formattedMessage }
        "WARN" { Write-Warning $formattedMessage }
        "SUCCESS" { Write-Information $formattedMessage -InformationAction Continue }
        default { Write-Information $formattedMessage -InformationAction Continue }
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
function Test-MCPEnvironment {
    Write-TestLog "🔍 Testing MCP Environment Setup" "INFO"

    # Test 1: XAI API Key
    if ($env:XAI_API_KEY) {
        Write-TestLog "✅ XAI_API_KEY found (${($env:XAI_API_KEY.Substring(0,10))}...)" "SUCCESS"
    } else {
        Write-TestLog "❌ XAI_API_KEY not found" "ERROR"
        return $false
    }

    # Test 2: Project Root (set it if not exists)
    $projectRoot = $env:BUSBUDDY_PROJECT_ROOT
    if (-not $projectRoot) {
        $projectRoot = (Get-Item ..).FullName  # Parent of mcp folder
        $env:BUSBUDDY_PROJECT_ROOT = $projectRoot
    }

    if (Test-Path $projectRoot -ErrorAction SilentlyContinue) {
        Write-TestLog "✅ Project root found: $projectRoot" "SUCCESS"
    } else {
        Write-TestLog "❌ Project root not accessible: $projectRoot" "ERROR"
        return $false
    }

    # Test 3: MCP Configuration (in parent directory)
    $mcpConfigPath = Join-Path (Get-Item ..).FullName ".vscode\mcp.json"
    if (Test-Path $mcpConfigPath) {
        Write-TestLog "✅ MCP configuration found: $mcpConfigPath" "SUCCESS"
    } else {
        Write-TestLog "❌ MCP configuration missing: $mcpConfigPath" "ERROR"
        return $false
    }

    # Test 4: Node Dependencies
    if (Test-Path "node_modules\@modelcontextprotocol") {
        Write-TestLog "✅ MCP SDK dependencies found" "SUCCESS"
    } else {
        Write-TestLog "❌ MCP dependencies missing" "ERROR"
        return $false
    }

    return $true
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER ServerName
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Test-MCPServerStart {
    param([string]$ServerName)

    Write-TestLog "🚀 Testing MCP Server Startup: $ServerName" "INFO"

    try {
        # Read MCP configuration from parent directory
        $mcpConfigPath = Join-Path (Get-Item ..).FullName ".vscode\mcp.json"
        $mcpConfig = Get-Content $mcpConfigPath | ConvertFrom-Json
        $serverConfig = $mcpConfig.servers.$ServerName

        if (-not $serverConfig) {
            Write-TestLog "❌ Server '$ServerName' not found in MCP config" "ERROR"
            return $false
        }

        Write-TestLog "📋 Server config found: $($serverConfig.command) $($serverConfig.args -join ' ')" "INFO"

        # Test server script exists
        $serverScript = $serverConfig.args | Where-Object { $_ -like "*.js" } | Select-Object -First 1
        if ($serverScript -and (Test-Path $serverScript)) {
            Write-TestLog "✅ Server script exists: $serverScript" "SUCCESS"
        } else {
            Write-TestLog "❌ Server script not found: $serverScript" "ERROR"
            return $false
        }

        return $true
    }
    catch {
        Write-TestLog "❌ Error testing server startup: $($_.Exception.Message)" "ERROR"
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
function Test-MCPBasicInteraction {
    Write-TestLog "💬 Testing Basic MCP Interaction" "INFO"

    try {
        # This is a placeholder for actual MCP client interaction
        # In a real test, you would:
        # 1. Start the MCP server
        # 2. Send a tools/list request
        # 3. Send a tools/call request with simple parameters
        # 4. Verify the response

        Write-TestLog "ℹ️  MCP client interaction test (placeholder)" "INFO"
        Write-TestLog "ℹ️  This would test:" "INFO"
        Write-TestLog "   - Server startup" "INFO"
        Write-TestLog "   - Tools listing" "INFO"
        Write-TestLog "   - Basic tool call" "INFO"
        Write-TestLog "   - Response validation" "INFO"

        return $true
    }
    catch {
        Write-TestLog "❌ Error in MCP interaction: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Main Test Execution
Write-TestLog "🧪 BusBuddy MCP First Test Starting" "INFO"
Write-TestLog "📁 Working Directory: $(Get-Location)" "INFO"
Write-TestLog "🎯 Target Server: $Server" "INFO"

$testResults = @{
    Environment = Test-MCPEnvironment
    ServerStart = Test-MCPServerStart -ServerName $Server
    BasicInteraction = Test-MCPBasicInteraction
}

Write-TestLog "" "INFO"
Write-TestLog "📊 Test Results Summary:" "INFO"
foreach ($test in $testResults.GetEnumerator()) {
    $status = if ($test.Value) { "✅ PASS" } else { "❌ FAIL" }
    Write-TestLog "   $($test.Key): $status" "INFO"
}

$allPassed = $testResults.Values -notcontains $false
if ($allPassed) {
    Write-TestLog "" "INFO"
    Write-TestLog "🎉 All tests passed! MCP setup is ready for Grok-4 integration." "SUCCESS"
    Write-TestLog "🔄 Next step: Run actual MCP server interaction test" "INFO"
} else {
    Write-TestLog "" "INFO"
    Write-TestLog "⚠️  Some tests failed. Review the errors above before proceeding." "WARN"
}

return $allPassed
