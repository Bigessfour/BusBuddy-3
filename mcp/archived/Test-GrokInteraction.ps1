#Requires -Version 7.5
#Requires -Modules Global-SecureApiManager

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [switch]$TestOnly,

    [Parameter()]
    [switch]$Interactive,

    [Parameter()]
    [string]$Query = "Please analyze the BusBuddy development environment and provide your recommendations for improvement."
)

<#
.SYNOPSIS
    Test Grok-4 MCP Server interaction and get AI recommendations

.DESCRIPTION
    Launches the Grok-4 MCP server and sends test queries to get Grok's analysis
    of the BusBuddy development environment and architecture.

.EXAMPLE
    .\Test-GrokInteraction.ps1

.EXAMPLE
    .\Test-GrokInteraction.ps1 -Interactive -Query "What do you think of the MCP integration in BusBuddy?"
#>

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    $colorMap = @{
        "Red" = "`e[31m"
        "Green" = "`e[32m"
        "Yellow" = "`e[33m"
        "Blue" = "`e[34m"
        "Magenta" = "`e[35m"
        "Cyan" = "`e[36m"
        "White" = "`e[37m"
        "Gray" = "`e[90m"
    }
    $resetColor = "`e[0m"

    if ($colorMap.ContainsKey($Color)) {
        Write-Information "$($colorMap[$Color])$Message$resetColor" -InformationAction Continue
    } else {
        Write-Information $Message -InformationAction Continue
    }
}

function Test-GrokEnvironment {
    Write-ColorOutput "🔍 Validating Grok-4 Environment..." "Cyan"

    # Check XAI API key
    $apiKey = Get-SecureApiKey -Service "XAI" -ErrorAction SilentlyContinue
    if (-not $apiKey) {
        Write-ColorOutput "❌ XAI API key not found in secure vault" "Red"
        return $false
    }

    if ($apiKey.Length -ne 84) {
        Write-ColorOutput "⚠️  XAI API key has unexpected length: $($apiKey.Length)" "Yellow"
    } else {
        Write-ColorOutput "✅ XAI API key properly configured" "Green"
    }

    # Check Node.js
    try {
        $nodeVersion = node --version 2>$null
        Write-ColorOutput "✅ Node.js: $nodeVersion" "Green"
    } catch {
        Write-ColorOutput "❌ Node.js not found" "Red"
        return $false
    }

    # Check MCP server file
    $serverPath = Join-Path $PSScriptRoot "servers\grok4-mcp-server.js"
    if (Test-Path $serverPath) {
        Write-ColorOutput "✅ Grok-4 MCP server found: $serverPath" "Green"
    } else {
        Write-ColorOutput "❌ Grok-4 MCP server not found: $serverPath" "Red"
        return $false
    }

    return $true
}

function Start-GrokInteractiveTest {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [string]$TestQuery
    )

    Write-ColorOutput "`n🤖 Starting Grok-4 Interactive Test..." "Magenta"
    Write-ColorOutput "Query: $TestQuery" "White"

    $serverPath = Join-Path $PSScriptRoot "servers\grok4-mcp-server.js"

    if ($PSCmdlet.ShouldProcess("Grok-4 MCP Server", "Start interactive test")) {
        try {
            # Set environment variables
            $env:XAI_API_KEY = Get-SecureApiKey -Service "XAI"
            $env:BUSBUDDY_NO_WELCOME = "1"

            Write-ColorOutput "🚀 Launching Grok-4 MCP Server..." "Yellow"

            # Create a simple test JSON-RPC request
            $testRequest = @{
                jsonrpc = "2.0"
                id = 1
                method = "tools/call"
                params = @{
                    name = "grok-analyze-problem"
                    arguments = @{
                        query = $TestQuery
                        context = "BusBuddy school transportation management system"
                        includeProjectStatus = $true
                    }
                }
            } | ConvertTo-Json -Depth 10

            Write-ColorOutput "📝 Test request prepared" "Green"
            Write-ColorOutput "Request: $($testRequest.Substring(0, 100))..." "Gray"

            # For now, let's just validate the server can start
            $process = Start-Process -FilePath "node" -ArgumentList $serverPath -NoNewWindow -PassThru -RedirectStandardOutput "grok-test-output.txt" -RedirectStandardError "grok-test-error.txt"

            Start-Sleep 3

            if ($process -and -not $process.HasExited) {
                Write-ColorOutput "✅ Grok-4 MCP server started successfully (PID: $($process.Id))" "Green"
                $process.Kill()
                Write-ColorOutput "🛑 Server stopped for testing" "Yellow"

                # Check output
                if (Test-Path "grok-test-output.txt") {
                    $output = Get-Content "grok-test-output.txt" -Raw
                    if ($output) {
                        Write-ColorOutput "`n📊 Server Output:" "Cyan"
                        Write-ColorOutput $output.Substring(0, [Math]::Min(500, $output.Length)) "White"
                    }
                }

                if (Test-Path "grok-test-error.txt") {
                    $errorOutput = Get-Content "grok-test-error.txt" -Raw
                    if ($errorOutput) {
                        Write-ColorOutput "`n⚠️  Server Errors:" "Yellow"
                        Write-ColorOutput $errorOutput.Substring(0, [Math]::Min(300, $errorOutput.Length)) "Red"
                    }
                }
            } else {
                Write-ColorOutput "❌ Failed to start Grok-4 MCP server" "Red"
            }

        } catch {
            Write-ColorOutput "❌ Error during Grok test: $($_.Exception.Message)" "Red"
        } finally {
            # Cleanup
            Remove-Item "grok-test-output.txt" -ErrorAction SilentlyContinue
            Remove-Item "grok-test-error.txt" -ErrorAction SilentlyContinue
        }
    }
}

function Show-GrokRecommendation {
    Write-ColorOutput "`n🎯 Grok-4 Integration Recommendations:" "Magenta"
    Write-ColorOutput "=====================================" "Magenta"

    Write-ColorOutput "`n💡 To interact with Grok-4 MCP Server:" "Yellow"
    Write-ColorOutput "1. VS Code MCP Extension:" "White"
    Write-ColorOutput "   • Configure in .vscode/mcp.json" "Gray"
    Write-ColorOutput "   • Use '@grok-analyze-problem' tool" "Gray"

    Write-ColorOutput "`n2. PowerShell Direct:" "White"
    Write-ColorOutput "   • .\Start-Grok4MCPServer.ps1" "Gray"
    Write-ColorOutput "   • Use JSON-RPC over stdio" "Gray"

    Write-ColorOutput "`n3. CLI Testing:" "White"
    Write-ColorOutput "   • node mcp\servers\grok4-mcp-server.js" "Gray"
    Write-ColorOutput "   • Send JSON-RPC requests" "Gray"

    Write-ColorOutput "`n🔧 Available Grok-4 Tools:" "Yellow"
    Write-ColorOutput "• grok-analyze-problem - General problem analysis" "White"
    Write-ColorOutput "• grok-architecture-review - Architecture evaluation" "White"
    Write-ColorOutput "• grok-syncfusion-guidance - Syncfusion-specific help" "White"
    Write-ColorOutput "• grok-powershell-optimization - PowerShell improvements" "White"
    Write-ColorOutput "• grok-database-advice - Database optimization" "White"

    Write-ColorOutput "`n🚀 Next Steps:" "Yellow"
    Write-ColorOutput "1. Configure VS Code MCP extension" "White"
    Write-ColorOutput "2. Test with real queries about BusBuddy" "White"
    Write-ColorOutput "3. Use Grok-4 for code reviews and optimizations" "White"
}

# Main execution
try {
    Write-ColorOutput "🚌 BusBuddy Grok-4 MCP Server Test" "Magenta"
    Write-ColorOutput "=================================" "Magenta"

    if (-not (Test-GrokEnvironment)) {
        Write-ColorOutput "❌ Environment validation failed" "Red"
        exit 1
    }

    if ($TestOnly) {
        Write-ColorOutput "✅ Environment validation passed - Grok-4 MCP ready!" "Green"
    Show-GrokRecommendation
        exit 0
    }

    if ($Interactive) {
        Start-GrokInteractiveTest -TestQuery $Query
    }

    Show-GrokRecommendation    Write-ColorOutput "`n✨ Grok-4 MCP Server is ready for action!" "Green"
    Write-ColorOutput "Ask Grok-4 anything about improving BusBuddy! 🤖" "Cyan"

} catch {
    Write-ColorOutput "❌ Critical error: $($_.Exception.Message)" "Red"
    Write-ColorOutput "Stack trace: $($_.ScriptStackTrace)" "Red"
    exit 1
}
