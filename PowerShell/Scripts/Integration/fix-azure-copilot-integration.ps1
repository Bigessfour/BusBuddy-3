# Azure GitHub Copilot Integration Fix Script
# This script resolves authentication issues and sets up proper integration

Write-Information "=== Azure GitHub Copilot Integration Setup ===" -InformationAction Continue

# Step 1: Verify Azure CLI Authentication
Write-Information "`n1. Checking Azure CLI Authentication..." -InformationAction Continue
try {
    $azAccount = az account show --query "{name:name, user:user.name, tenantId:tenantId}" | ConvertFrom-Json
    Write-Information "✅ Azure CLI authenticated as: $($azAccount.user)" -InformationAction Continue
    Write-Information "   Subscription: $($azAccount.name)" -InformationAction Continue
    Write-Information "   Tenant: $($azAccount.tenantId)" -InformationAction Continue
} catch {
    Write-Information "❌ Azure CLI not authenticated. Run: az login" -InformationAction Continue
    exit 1
}

# Step 2: Verify GitHub CLI Authentication
Write-Information "`n2. Checking GitHub CLI Authentication..." -InformationAction Continue
try {
    $ghStatus = gh auth status 2>&1 | Out-String
    if ($ghStatus -match "Logged in to github.com") {
        Write-Information "✅ GitHub CLI authenticated" -InformationAction Continue
    } else {
        Write-Information "❌ GitHub CLI not authenticated. Run: gh auth login" -InformationAction Continue
        exit 1
    }
} catch {
    Write-Information "❌ GitHub CLI authentication failed" -InformationAction Continue
    exit 1
}

# Step 3: Check VS Code Extensions
Write-Information "`n3. Checking VS Code Azure Extensions..." -InformationAction Continue
$azureExtensions = @(
    "ms-azuretools.vscode-azure-github-copilot",
    "ms-vscode.azure-account",
    "ms-azuretools.vscode-azureresourcegroups"
)

foreach ($ext in $azureExtensions) {
    $installed = code --list-extensions | Where-Object { $_ -eq $ext }
    if ($installed) {
        Write-Information "✅ Extension installed: $ext" -InformationAction Continue
    } else {
        Write-Information "❌ Extension missing: $ext" -InformationAction Continue
        Write-Information "   Installing..." -InformationAction Continue
        code --install-extension $ext
    }
}

# Step 4: Update MCP Configuration for Azure Copilot
Write-Information "`n4. Updating MCP Configuration..." -InformationAction Continue
$mcpConfigPath = ".vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    Write-Information "✅ MCP configuration exists" -InformationAction Continue
} else {
    Write-Information "❌ MCP configuration missing" -InformationAction Continue
}

# Step 5: Create Azure Copilot Settings
Write-Information "`n5. Creating Azure Copilot Settings..." -InformationAction Continue
$vsCodeSettings = @{
    "azure.cloudShell.defaultShell" = "PowerShell"
    "azure.tenant" = $azAccount.tenantId
    "azure.copilot.enabled" = $true
    "github.copilot.enable" = $true
}

$settingsPath = ".vscode\settings.json"
if (Test-Path $settingsPath) {
    $currentSettings = Get-Content $settingsPath | ConvertFrom-Json -AsHashtable
    foreach ($key in $vsCodeSettings.Keys) {
        $currentSettings[$key] = $vsCodeSettings[$key]
    }
    $currentSettings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath
    Write-Information "✅ VS Code settings updated" -InformationAction Continue
} else {
    $vsCodeSettings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath
    Write-Information "✅ VS Code settings created" -InformationAction Continue
}

# Step 6: Test Azure Resource Graph Access
Write-Information "`n6. Testing Azure Resource Graph Access..." -InformationAction Continue
try {
    $resourceGroups = az group list --query "[].name" --output tsv
    Write-Information "✅ Azure Resource Graph accessible" -InformationAction Continue
    Write-Information "   Found $($resourceGroups.Count) resource groups" -InformationAction Continue
} catch {
    Write-Information "❌ Azure Resource Graph access failed" -InformationAction Continue
}

Write-Information "`n=== Setup Complete ===" -InformationAction Continue
Write-Information "Actions needed in VS Code:" -InformationAction Continue
Write-Information "1. Restart VS Code to reload extensions" -InformationAction Continue
Write-Information "2. Sign in to Azure account when prompted" -InformationAction Continue
Write-Information "3. Grant permissions when Azure Copilot requests access" -InformationAction Continue
Write-Information "4. Test MCP tools: azure_resources-query_azure_resource_graph" -InformationAction Continue
