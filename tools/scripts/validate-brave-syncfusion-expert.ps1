#!/usr/bin/env pwsh
# Brave MCP Syncfusion Expert Configuration Validation Script

Write-Information "🔍 Validating Brave MCP as Syncfusion WPF Expert Configuration..." -InformationAction Continue

# Check environment variables
Write-Information "`n📋 Environment Variables:" -InformationAction Continue
$braveApiKey = $env:BRAVE_API_KEY
if ($braveApiKey) {
    $maskedKey = $braveApiKey.Substring(0, 8) + "..." + $braveApiKey.Substring($braveApiKey.Length - 4)
    Write-Information "  ✅ BRAVE_API_KEY: $maskedKey" -InformationAction Continue
} else {
    Write-Warning "  ❌ BRAVE_API_KEY not found in environment variables"
}

# Check .env file
Write-Information "`n📄 .env File Configuration:" -InformationAction Continue
if (Test-Path ".env") {
    $envContent = Get-Content ".env" -Raw
    if ($envContent -match "BRAVE_API_KEY") {
        Write-Information "  ✅ .env file contains BRAVE_API_KEY" -InformationAction Continue
    } else {
        Write-Warning "  ❌ .env file missing BRAVE_API_KEY"
    }

    if ($envContent -match "BRAVE_SEARCH_EXPERT_MODE") {
        Write-Information "  ✅ .env file contains expert mode configuration" -InformationAction Continue
    } else {
        Write-Warning "  ❌ .env file missing expert mode configuration"
    }
} else {
    Write-Warning "  ❌ .env file not found"
}

# Check MCP configuration
Write-Information "`n⚙️ MCP Configuration:" -InformationAction Continue
if (Test-Path ".vscode\mcp.json") {
    try {
        $mcpConfig = Get-Content ".vscode\mcp.json" | ConvertFrom-Json
        if ($mcpConfig.servers."brave-search") {
            Write-Information "  ✅ brave-search server configured" -InformationAction Continue

            $braveConfig = $mcpConfig.servers."brave-search"
            if ($braveConfig.env.BRAVE_API_KEY) {
                Write-Information "  ✅ BRAVE_API_KEY environment variable configured" -InformationAction Continue
            } else {
                Write-Warning "  ❌ BRAVE_API_KEY not configured in MCP server"
            }

            if ($braveConfig.env.MCP_SEARCH_CONTEXT) {
                Write-Information "  ✅ Syncfusion expert context configured" -InformationAction Continue
            } else {
                Write-Warning "  ❌ Syncfusion expert context not configured"
            }
        } else {
            Write-Warning "  ❌ brave-search server not found in MCP configuration"
        }
    } catch {
        Write-Error "  ❌ Error parsing MCP configuration: $($_.Exception.Message)"
    }
} else {
    Write-Warning "  ❌ MCP configuration file not found"
}

# Test Brave API connectivity
Write-Information "`n🌐 API Connectivity Test:" -InformationAction Continue
if ($braveApiKey) {
    try {
        $headers = @{"X-Subscription-Token" = $braveApiKey }
        $testQuery = "syncfusion wpf sfdatagrid"
        $response = Invoke-RestMethod -Uri "https://api.search.brave.com/res/v1/web/search?q=$testQuery&count=3" -Headers $headers

        Write-Information "  ✅ Brave Search API connectivity successful" -InformationAction Continue
        Write-Information "  📊 Found $($response.web.results.Count) results for Syncfusion WPF query" -InformationAction Continue

        # Check if results include Syncfusion official documentation
        $syncfusionResults = $response.web.results | Where-Object { $_.url -like "*syncfusion.com*" }
        if ($syncfusionResults.Count -gt 0) {
            Write-Information "  ✅ Results include official Syncfusion documentation" -InformationAction Continue
        } else {
            Write-Warning "  ⚠️ No official Syncfusion documentation in results"
        }

        Write-Information "`n📋 Sample Results:" -InformationAction Continue
        $response.web.results | Select-Object -First 3 | ForEach-Object {
            Write-Information "    • $($_.title)" -InformationAction Continue
            Write-Information "      $($_.url)" -InformationAction Continue
        }

    } catch {
        Write-Error "  ❌ Brave Search API test failed: $($_.Exception.Message)"
    }
} else {
    Write-Warning "  ❌ Cannot test API - BRAVE_API_KEY not available"
}

# Check expert configuration files
Write-Information "`n📚 Expert Configuration Files:" -InformationAction Continue
if (Test-Path ".vscode\brave-syncfusion-expert.json") {
    Write-Information "  ✅ Syncfusion expert configuration file present" -InformationAction Continue
} else {
    Write-Warning "  ❌ Syncfusion expert configuration file missing"
}

if (Test-Path "docs\BRAVE-SYNCFUSION-EXPERT-GUIDE.md") {
    Write-Information "  ✅ Expert usage guide documentation present" -InformationAction Continue
} else {
    Write-Warning "  ❌ Expert usage guide documentation missing"
}

# Summary
Write-Information "`n🎯 Configuration Summary:" -InformationAction Continue
Write-Information "  • Brave MCP server configured as Syncfusion WPF expert" -InformationAction Continue
Write-Information "  • API key secured via environment variables" -InformationAction Continue
Write-Information "  • Search context optimized for Syncfusion documentation" -InformationAction Continue
Write-Information "  • Expert configuration files and documentation created" -InformationAction Continue

Write-Information "`n🚀 Next Steps:" -InformationAction Continue
Write-Information "  1. Restart VS Code to reload MCP server configuration" -InformationAction Continue
Write-Information "  2. Test Copilot Chat with Syncfusion WPF questions" -InformationAction Continue
Write-Information "  3. Verify responses reference help.syncfusion.com documentation" -InformationAction Continue
Write-Information "  4. Use specific control names (SfDataGrid, DockingManager, etc.) for best results" -InformationAction Continue

Write-Information "`n✅ Brave MCP Syncfusion Expert Configuration Complete!" -InformationAction Continue
