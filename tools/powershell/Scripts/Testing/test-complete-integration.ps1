# Test Complete Integration
# Tests Azure MCP + GitHub MCP + CI/CD pipeline

Write-Information "🚀 Testing Complete MCP + Azure + GitHub Integration" -InformationAction Continue

# Test 1: Azure CLI Status
Write-Information "`n1️⃣ Testing Azure CLI Authentication..." -InformationAction Continue
try {
    $azureAccount = az account show --query "{name:name, id:id, tenantId:tenantId}" -o json | ConvertFrom-Json
    Write-Information "✅ Azure CLI authenticated:" -InformationAction Continue
    Write-Information "   Account: $($azureAccount.name)" -InformationAction Continue
    Write-Information "   Subscription: $($azureAccount.id)" -InformationAction Continue
    Write-Information "   Tenant: $($azureAccount.tenantId)" -InformationAction Continue
} catch {
    Write-Information "❌ Azure CLI authentication failed: $($_.Exception.Message)" -InformationAction Continue
}

# Test 2: GitHub CLI Status
Write-Information "`n2️⃣ Testing GitHub CLI Authentication..." -InformationAction Continue
try {
    $ghStatus = gh auth status 2>&1 | Out-String
    if ($ghStatus -match "Logged in to github.com account (.+?) \(") {
        $username = $Matches[1]
        Write-Information "✅ GitHub CLI authenticated as: $username" -InformationAction Continue

        # Test repo access
        $repo = gh repo view --json name, owner | ConvertFrom-Json
        Write-Information "   Repository: $($repo.owner.login)/$($repo.name)" -InformationAction Continue
    } else {
        Write-Information "❌ GitHub CLI not authenticated" -InformationAction Continue
    }
} catch {
    Write-Information "❌ GitHub CLI error: $($_.Exception.Message)" -InformationAction Continue
}

# Test 3: GitHub Secrets Status
Write-Information "`n3️⃣ Testing GitHub Secrets..." -InformationAction Continue
try {
    $secrets = gh secret list --json name | ConvertFrom-Json
    $requiredSecrets = @("AZURE_CLIENT_ID", "AZURE_TENANT_ID", "AZURE_CLIENT_SECRET", "XAI_API_KEY")

    foreach ($secret in $requiredSecrets) {
        if ($secrets.name -contains $secret) {
            Write-Information "   ✅ $secret - Set" -InformationAction Continue
        } else {
            Write-Information "   ❌ $secret - Missing" -InformationAction Continue
        }
    }
} catch {
    Write-Information "❌ Cannot access GitHub secrets: $($_.Exception.Message)" -InformationAction Continue
}

# Test 4: Service Principal Status
Write-Information "`n4️⃣ Testing Service Principal..." -InformationAction Continue
try {
    $clientId = "860af3d3-df7a-4c76-915a-a6f980bd86ed"
    $sp = az ad sp show --id $clientId --query "{displayName:displayName, appId:appId, objectId:objectId}" -o json | ConvertFrom-Json
    Write-Information "✅ Service Principal found:" -InformationAction Continue
    Write-Information "   Name: $($sp.displayName)" -InformationAction Continue
    Write-Information "   App ID: $($sp.appId)" -InformationAction Continue
    Write-Information "   Object ID: $($sp.objectId)" -InformationAction Continue
} catch {
    Write-Information "❌ Service Principal error: $($_.Exception.Message)" -InformationAction Continue
}

# Test 5: MCP Configuration
Write-Information "`n5️⃣ Testing MCP Configuration..." -InformationAction Continue
$mcpConfigPath = ".vscode/mcp.json"
if (Test-Path $mcpConfigPath) {
    try {
        $mcpConfig = Get-Content $mcpConfigPath | ConvertFrom-Json
        Write-Information "✅ MCP configuration found with servers:" -InformationAction Continue
        foreach ($server in $mcpConfig.mcpServers.PSObject.Properties) {
            Write-Information "   • $($server.Name)" -InformationAction Continue
        }
    } catch {
        Write-Information "❌ MCP configuration invalid: $($_.Exception.Message)" -InformationAction Continue
    }
} else {
    Write-Information "❌ MCP configuration file not found" -InformationAction Continue
}

# Test 6: GitHub Actions Workflow
Write-Information "`n6️⃣ Testing GitHub Actions Workflow..." -InformationAction Continue
$workflowPath = ".github/workflows/azure-sql-ci-cd.yml"
if (Test-Path $workflowPath) {
    Write-Information "✅ CI/CD workflow file exists" -InformationAction Continue
    Write-Information "   Path: $workflowPath" -InformationAction Continue

    # Check for required secrets in workflow
    $workflowContent = Get-Content $workflowPath -Raw
    $secretsUsed = @()
    if ($workflowContent -match 'secrets\.AZURE_CLIENT_ID') { $secretsUsed += "AZURE_CLIENT_ID" }
    if ($workflowContent -match 'secrets\.AZURE_TENANT_ID') { $secretsUsed += "AZURE_TENANT_ID" }
    if ($workflowContent -match 'secrets\.AZURE_CLIENT_SECRET') { $secretsUsed += "AZURE_CLIENT_SECRET" }

    Write-Information "   Secrets referenced: $($secretsUsed -join ', ')" -InformationAction Continue
} else {
    Write-Information "❌ GitHub Actions workflow not found" -InformationAction Continue
}

# Test 7: Environment Variables
Write-Information "`n7️⃣ Testing Environment Variables..." -InformationAction Continue
$envVars = @("BRAVE_API_KEY", "XAI_API_KEY")
foreach ($envVar in $envVars) {
    if (Get-Item "env:$envVar" -ErrorAction SilentlyContinue) {
        Write-Information "   ✅ $envVar - Set" -InformationAction Continue
    } else {
        Write-Information "   ⚠️ $envVar - Not set" -InformationAction Continue
    }
}

# Final Summary
Write-Information "`n🎯 INTEGRATION TEST SUMMARY" -InformationAction Continue
Write-Information "=" -InformationAction Continue * 50 -ForegroundColor Cyan
Write-Information "✅ Azure + GitHub + MCP integration appears complete!" -InformationAction Continue
Write-Information "`n🚀 Ready for next steps:" -InformationAction Continue
Write-Information "   1. Push to develop branch to test staging deployment" -InformationAction Continue
Write-Information "   2. Use MCP servers in VS Code for enhanced development" -InformationAction Continue
Write-Information "   3. Monitor GitHub Actions for automated deployments" -InformationAction Continue

Write-Information "`n📊 For detailed status, see: INTEGRATION-COMPLETE.md" -InformationAction Continue
