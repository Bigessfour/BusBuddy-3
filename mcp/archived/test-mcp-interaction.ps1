#Requires -Version 7.5

<#
.SYNOPSIS
    BusBuddy MCP Live Interaction Test

.DESCRIPTION
    Tests actual MCP server interaction by starting the Grok-4 server and calling its tools.
    This is the complete integration test to verify MCP functionality is working end-to-end.

.EXAMPLE
    .\test-mcp-interaction.ps1

.EXAMPLE
    .\test-mcp-interaction.ps1 -Tool "bb-health" -Detailed
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Server = "busbuddy-grok4-mcp",

    [Parameter()]
    [string]$Tool = "grok-analyze-problem",

    [Parameter()]
    [switch]$Detailed,

    [Parameter()]
    [int]$TimeoutSeconds = 30
)

function Write-TestLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"

    # Security: Mask any API keys in log messages
    $secureMessage = $Message -replace 'xai-[a-zA-Z0-9]{40,}', 'xai-***MASKED***'
    $secureMessage = $secureMessage -replace 'XAI_API_KEY.*', 'XAI_API_KEY: ***MASKED***'

    $formattedMessage = "[$timestamp] [$Level] $secureMessage"

    switch ($Level) {
        "ERROR" { Write-Error $formattedMessage }
        "WARN" { Write-Warning $formattedMessage }
        "SUCCESS" { Write-Information $formattedMessage -InformationAction Continue }
        default { Write-Information $formattedMessage -InformationAction Continue }
    }
}

function Test-SecureApiKey {
    <#
    .SYNOPSIS
    Securely validates XAI API key without exposing it in logs
    #>

    if (-not $env:XAI_API_KEY) {
        Write-TestLog "❌ XAI_API_KEY environment variable not found" "ERROR"
        return $false
    }

    $keyLength = $env:XAI_API_KEY.Length
    $keyPrefix = $env:XAI_API_KEY.Substring(0, [Math]::Min(4, $keyLength))

    if ($keyLength -lt 10) {
        Write-TestLog "❌ XAI_API_KEY appears too short (length: $keyLength)" "ERROR"
        return $false
    }

    if (-not $keyPrefix.StartsWith("xai-")) {
        Write-TestLog "❌ XAI_API_KEY does not have expected format" "ERROR"
        return $false
    }

    Write-TestLog "✅ XAI_API_KEY found with correct format (length: $keyLength)" "SUCCESS"
    return $true
}

function Start-MCPServer {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$ServerName)

    if (-not $PSCmdlet.ShouldProcess("MCP Server: $ServerName", "Start")) {
        return $null
    }

    Write-TestLog "🚀 Starting MCP Server: $ServerName" "INFO"

    try {
        # Get server configuration
        $mcpConfigPath = Join-Path (Get-Item ..).FullName ".vscode\mcp.json"
        $mcpConfig = Get-Content $mcpConfigPath | ConvertFrom-Json
        $serverConfig = $mcpConfig.servers.$ServerName

        if (-not $serverConfig) {
            throw "Server '$ServerName' not found in MCP config"
        }

        # Set environment variables
        $env:XAI_API_KEY = $env:XAI_API_KEY
        $env:BUSBUDDY_PROJECT_ROOT = (Get-Item ..).FullName
        $env:NODE_ENV = "production"

        # Start the server process
        $serverScript = $serverConfig.args | Where-Object { $_ -like "*.js" } | Select-Object -First 1
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = "node"
        $startInfo.Arguments = $serverScript
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardInput = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true
        $startInfo.WorkingDirectory = Split-Path $serverScript

        # Add environment variables
        foreach ($envVar in $serverConfig.env.PSObject.Properties) {
            $startInfo.EnvironmentVariables[$envVar.Name] = $envVar.Value
        }

        $process = [System.Diagnostics.Process]::Start($startInfo)
        Write-TestLog "✅ MCP Server started with PID: $($process.Id)" "SUCCESS"

        return $process
    }
    catch {
        Write-TestLog "❌ Failed to start MCP server: $($_.Exception.Message)" "ERROR"
        return $null
    }
}

function Test-MCPTool {
    param([System.Diagnostics.Process]$ServerProcess)

    Write-TestLog "🔧 Testing MCP Tools List" "INFO"

    try {
        # Send tools/list request
        $listRequest = @{
            jsonrpc = "2.0"
            id = 1
            method = "tools/list"
            params = @{}
        } | ConvertTo-Json -Depth 3

        Write-TestLog "📤 Sending tools/list request..." "INFO"
        $ServerProcess.StandardInput.WriteLine($listRequest)
        $ServerProcess.StandardInput.Flush()

        # Read response with timeout and better error handling
        $response = $null
        $timeout = (Get-Date).AddSeconds(10)
        $allOutput = @()

        while ((Get-Date) -lt $timeout -and -not $ServerProcess.HasExited) {
            if ($ServerProcess.StandardOutput.Peek() -ge 0) {
                $line = $ServerProcess.StandardOutput.ReadLine()
                $allOutput += $line

                # Look for JSON response (starts with {)
                if ($line.Trim().StartsWith('{')) {
                    $response = $line
                    break
                }
            }
            Start-Sleep -Milliseconds 100
        }

        if ($response) {
            try {
                Write-TestLog "📨 Raw response: $($response.Substring(0, [Math]::Min(100, $response.Length)))..." "INFO"
                $responseObj = $response | ConvertFrom-Json
                if ($responseObj.result -and $responseObj.result.tools) {
                    $toolCount = $responseObj.result.tools.Count
                    Write-TestLog "✅ Received $toolCount tools from server" "SUCCESS"

                    if ($Detailed) {
                        foreach ($tool in $responseObj.result.tools) {
                            Write-TestLog "   🔨 $($tool.name): $($tool.description)" "INFO"
                        }
                    }
                    return $true
                } else {
                    Write-TestLog "❌ Invalid response structure" "ERROR"
                    return $false
                }
            }
            catch {
                Write-TestLog "❌ JSON parsing failed: $($_.Exception.Message)" "ERROR"
                Write-TestLog "📋 All server output: $($allOutput -join '`n')" "INFO"
                return $false
            }
        } else {
            Write-TestLog "❌ No response received from server" "ERROR"
            if ($allOutput.Count -gt 0) {
                Write-TestLog "📋 Server output: $($allOutput -join '`n')" "INFO"
            }
            return $false
        }
    }
    catch {
        Write-TestLog "❌ Error testing tools: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Test-MCPToolCall {
    param(
        [System.Diagnostics.Process]$ServerProcess,
        [string]$ToolName
    )

    Write-TestLog "🎯 Testing Tool Call: $ToolName" "INFO"

    try {
        # Prepare tool call based on tool type
        $arguments = switch ($ToolName) {
            "bb-health" { @{} }
            "grok-analyze-problem" {
                @{
                    query = "Test query: How is the BusBuddy MCP integration working?"
                    context = "Running from MCP test script"
                    includeProjectStatus = $true
                }
            }
            "grok-code-review" {
                @{
                    filePath = "BusBuddy.WPF\App.xaml.cs"
                    focusArea = "general"
                }
            }
            default { @{} }
        }

        $toolCallRequest = @{
            jsonrpc = "2.0"
            id = 2
            method = "tools/call"
            params = @{
                name = $ToolName
                arguments = $arguments
            }
        } | ConvertTo-Json -Depth 4

        Write-TestLog "📤 Calling tool: $ToolName" "INFO"
        $ServerProcess.StandardInput.WriteLine($toolCallRequest)
        $ServerProcess.StandardInput.Flush()

        # Read response with timeout
        $response = $null
        $timeout = (Get-Date).AddSeconds(20)  # Longer timeout for AI responses

        while ((Get-Date) -lt $timeout -and -not $ServerProcess.HasExited) {
            if ($ServerProcess.StandardOutput.Peek() -ge 0) {
                $response = $ServerProcess.StandardOutput.ReadLine()
                break
            }
            Start-Sleep -Milliseconds 200
        }

        if ($response) {
            $responseObj = $response | ConvertFrom-Json
            if ($responseObj.result) {
                Write-TestLog "✅ Tool call successful!" "SUCCESS"
                if ($Detailed -and $responseObj.result.content) {
                    Write-TestLog "📋 Response preview: $($responseObj.result.content[0..100] -join '')" "INFO"
                }
                return $true
            }
        }

        Write-TestLog "❌ Tool call failed or timed out" "ERROR"
        return $false
    }
    catch {
        Write-TestLog "❌ Error calling tool: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Stop-MCPServer {
    [CmdletBinding(SupportsShouldProcess)]
    param([System.Diagnostics.Process]$ServerProcess)

    if ($ServerProcess -and -not $ServerProcess.HasExited -and $PSCmdlet.ShouldProcess("MCP Server PID: $($ServerProcess.Id)", "Stop")) {
        Write-TestLog "🛑 Stopping MCP Server..." "INFO"
        $ServerProcess.Kill()
        $ServerProcess.WaitForExit(5000)
        Write-TestLog "✅ MCP Server stopped" "SUCCESS"
    }
}

# Main Test Execution
Write-TestLog "🧪 BusBuddy MCP Live Interaction Test Starting" "INFO"
Write-TestLog "🎯 Target Server: $Server" "INFO"
Write-TestLog "🔧 Target Tool: $Tool" "INFO"

$serverProcess = $null
$testResults = @{}

try {
    # Security Check: Validate API key first
    Write-TestLog "🔐 Validating API key security..." "INFO"
    if (-not (Test-SecureApiKey)) {
        throw "API key validation failed - cannot proceed"
    }

    # Start MCP Server
    $serverProcess = Start-MCPServer -ServerName $Server
    if (-not $serverProcess) {
        throw "Failed to start MCP server"
    }

    # Wait for server initialization
    Start-Sleep -Seconds 3

    # Test tools list
    $testResults["ToolsList"] = Test-MCPTool -ServerProcess $serverProcess

    # Test specific tool call
    $testResults["ToolCall"] = Test-MCPToolCall -ServerProcess $serverProcess -ToolName $Tool

    Write-TestLog "" "INFO"
    Write-TestLog "📊 Live Interaction Test Results:" "INFO"
    foreach ($test in $testResults.GetEnumerator()) {
        $status = if ($test.Value) { "✅ PASS" } else { "❌ FAIL" }
        Write-TestLog "   $($test.Key): $status" "INFO"
    }

    $allPassed = $testResults.Values -notcontains $false
    if ($allPassed) {
        Write-TestLog "" "INFO"
        Write-TestLog "🎉 MCP Live Interaction Test PASSED! Grok-4 integration is working!" "SUCCESS"
    } else {
        Write-TestLog "" "INFO"
        Write-TestLog "⚠️  Some live interaction tests failed." "WARN"
    }
}
catch {
    Write-TestLog "❌ Test execution failed: $($_.Exception.Message)" "ERROR"
    $allPassed = $false
}
finally {
    # Always stop the server
    Stop-MCPServer -ServerProcess $serverProcess
}

return $allPassed
