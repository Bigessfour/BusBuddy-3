#!/usr/bin/env pwsh
<#
.SYNOPSIS
Complete setup validation for BusBuddy-3 Azure and GitHub integration
.DESCRIPTION
Validates all components of the Azure SQL, GitHub, and MCP integration setup
.NOTES
Author: AI Assistant
Date: $(Get-Date -Format 'yyyy-MM-dd')
#>

param(
    [switch]$Detailed,
    [switch]$QuickCheck
)

Write-Information "🚌 BusBuddy-3 Complete Setup Validation" -InformationAction Continue
Write-Information "=======================================" -InformationAction Continue

$results = @{
    AzureCLI = $false
    GitHubCLI = $false
    AzureAuth = $false
    GitHubAuth = $false
    ServicePrincipal = $false
    Databases = $false
    MCPConfig = $false
    VSCodeExtensions = $false
}

# Check Azure CLI
Write-Information "`n1. Azure CLI Status" -InformationAction Continue
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Information "   ✅ Azure CLI installed: $($azVersion.'azure-cli')" -InformationAction Continue
    $results.AzureCLI = $true
} catch {
    Write-Information "   ❌ Azure CLI not found or not authenticated" -InformationAction Continue
}

# Check GitHub CLI
Write-Information "`n2. GitHub CLI Status" -InformationAction Continue
try {
    $ghVersion = gh version --json version 2>$null | ConvertFrom-Json
    Write-Information "   ✅ GitHub CLI installed: $($ghVersion.version)" -InformationAction Continue
    $results.GitHubCLI = $true
} catch {
    Write-Information "   ❌ GitHub CLI not found" -InformationAction Continue
}

# Check Azure Authentication
Write-Information "`n3. Azure Authentication" -InformationAction Continue
try {
    $azAccount = az account show --output json 2>$null | ConvertFrom-Json
    if ($azAccount) {
        Write-Information "   ✅ Authenticated as: $($azAccount.user.name)" -InformationAction Continue
        Write-Information "   ✅ Subscription: $($azAccount.name)" -InformationAction Continue
        Write-Information "   ✅ Tenant: $($azAccount.tenantId)" -InformationAction Continue
        $results.AzureAuth = $true
    }
} catch {
    Write-Information "   ❌ Not authenticated to Azure. Run 'az login'" -InformationAction Continue
}

# Check GitHub Authentication
Write-Information "`n4. GitHub Authentication" -InformationAction Continue
try {
    $ghAuth = gh auth status 2>&1
    if ($ghAuth -match "Logged in") {
        Write-Information "   ✅ GitHub CLI authenticated" -InformationAction Continue
        $results.GitHubAuth = $true
    } else {
        Write-Information "   ❌ GitHub CLI not authenticated" -InformationAction Continue
    }
} catch {
    Write-Information "   ❌ GitHub authentication check failed" -InformationAction Continue
}

# Check Service Principal
Write-Information "`n5. Service Principal Status" -InformationAction Continue
try {
    $sp = az ad app show --id "860af3d3-df7a-4c76-915a-a6f980bd86ed" --output json 2>$null | ConvertFrom-Json
    if ($sp) {
        Write-Information "   ✅ Service Principal exists: $($sp.displayName)" -InformationAction Continue
        $results.ServicePrincipal = $true
    }
} catch {
    Write-Information "   ❌ Service Principal not found or no access" -InformationAction Continue
}

# Check Database Connectivity
Write-Information "`n6. Database Connectivity" -InformationAction Continue
if ($results.AzureAuth) {
    try {
        $servers = az sql server list --output json 2>$null | ConvertFrom-Json
        $busBuddyServers = $servers | Where-Object { $_.name -like "*busbuddy*" }

        if ($busBuddyServers) {
            Write-Information "   ✅ Found BusBuddy SQL servers:" -InformationAction Continue
            foreach ($server in $busBuddyServers) {
                Write-Information "      - $($server.name)" -InformationAction Continue
            }
            $results.Databases = $true
        } else {
            Write-Information "   ⚠️  No BusBuddy SQL servers found" -InformationAction Continue
        }
    } catch {
        Write-Information "   ❌ Database check failed" -InformationAction Continue
    }
} else {
    Write-Information "   ⏭️  Skipped - Azure not authenticated" -InformationAction Continue
}

# Check MCP Configuration
Write-Information "`n7. MCP Configuration" -InformationAction Continue
$mcpPath = ".vscode/mcp.json"
if (Test-Path $mcpPath) {
    try {
        $mcpConfig = Get-Content $mcpPath | ConvertFrom-Json
        if ($mcpConfig.servers) {
            Write-Information "   ✅ MCP configuration found with servers:" -InformationAction Continue
            foreach ($server in $mcpConfig.servers.PSObject.Properties.Name) {
                Write-Information "      - $server" -InformationAction Continue
            }
            $results.MCPConfig = $true
        }
    } catch {
        Write-Information "   ❌ Invalid MCP configuration" -InformationAction Continue
    }
} else {
    Write-Information "   ❌ MCP configuration not found" -InformationAction Continue
}

# Check VS Code Extensions
Write-Information "`n8. VS Code Extensions" -InformationAction Continue
$extensionsPath = ".vscode/extensions.json"
if (Test-Path $extensionsPath) {
    try {
        $extensions = Get-Content $extensionsPath | ConvertFrom-Json
        $azureExtensions = $extensions.recommendations | Where-Object { $_ -like "*azure*" }
        if ($azureExtensions) {
            Write-Information "   ✅ Azure extensions configured:" -InformationAction Continue
            foreach ($ext in $azureExtensions) {
                Write-Information "      - $ext" -InformationAction Continue
            }
            $results.VSCodeExtensions = $true
        }
    } catch {
        Write-Information "   ❌ Extensions configuration error" -InformationAction Continue
    }
} else {
    Write-Information "   ❌ Extensions configuration not found" -InformationAction Continue
}

# Summary
Write-Information "`n📊 Setup Summary" -InformationAction Continue
Write-Information "=================" -InformationAction Continue

$totalChecks = $results.Values.Count
$passedChecks = ($results.Values | Where-Object { $_ -eq $true }).Count
$successRate = [math]::Round(($passedChecks / $totalChecks) * 100, 1)

Write-Information "Passed: $passedChecks/$totalChecks ($successRate%)" -InformationAction Continue -ForegroundColor $(if ($successRate -ge 80) { "Green" } elseif ($successRate -ge 60) { "Yellow" } else { "Red" })

if ($Detailed) {
    Write-Information "`n📋 Detailed Results:" -InformationAction Continue
    foreach ($check in $results.GetEnumerator()) {
        $status = if ($check.Value) { "✅ PASS" } else { "❌ FAIL" }
        $color = if ($check.Value) { "Green" } else { "Red" }
        Write-Information "   $($check.Key): $status" -InformationAction Continue -ForegroundColor $color
    }
}

# Next Steps
Write-Information "`n🎯 Next Steps" -InformationAction Continue
Write-Information "=============" -InformationAction Continue

if (-not $results.AzureAuth) {
    Write-Information "1. Run 'az login' to authenticate Azure CLI" -InformationAction Continue
}

if (-not $results.GitHubAuth) {
    Write-Information "2. Run 'gh auth login' to authenticate GitHub CLI" -InformationAction Continue
}

if ($results.AzureAuth -and $results.GitHubAuth -and $results.ServicePrincipal) {
    Write-Information "✅ Core authentication setup complete!" -InformationAction Continue
    Write-Information "📋 You can now:" -InformationAction Continue
    Write-Information "   - Use Azure MCP tools for Azure resource management" -InformationAction Continue
    Write-Information "   - Use GitHub MCP tools for repository operations" -InformationAction Continue
    Write-Information "   - Run CI/CD workflows with service principal authentication" -InformationAction Continue
}

if ($QuickCheck) {
    return $successRate -ge 80
}

Write-Information "`nValidation complete! 🚌" -InformationAction Continue
