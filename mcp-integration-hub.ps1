# Combined Azure & GitHub MCP Integration for BusBuddy-3 CI/CD
# This script provides comprehensive MCP integration for automated workflows

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("status", "setup", "test", "deploy", "monitor", "azure", "github", "combined")]
    [string]$Action = "status",

    [Parameter(Mandatory = $false)]
    [ValidateSet("staging", "production")]
    [string]$Environment = "staging",

    [Parameter(Mandatory = $false)]
    [string]$WorkflowName = "",

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

# Configuration
$config = @{
    Azure = @{
        ClientId = "860af3d3-df7a-4c76-915a-a6f980bd86ed"
        TenantId = "3ee44d11-b5ae-43a0-9c02-004b04858d9e"
        SubscriptionId = "57b297a5-44cf-4abc-9ac4-91a5ed147de1"
        ResourceGroup = "BusBuddy-RG"
        SqlServer = "busbuddy-server-sm2"
    }
    GitHub = @{
        Owner = "BigE0rns"  # Update with your GitHub username
        Repository = "BusBuddy"
        DefaultBranch = "main"
        StagingBranch = "develop"
    }
    Databases = @{
        staging = "BusBuddyDB-Staging"
        production = "BusBuddyDB"
    }
    Workflows = @{
        CI = "azure-sql-ci-cd.yml"
        Deploy = "azure-deployment.yml"
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Message
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Write-StepHeader {
    param([string]$Message)
    Write-Information "`n🔧 $Message" -InformationAction Continue
    Write-Information ( -InformationAction Continue"=" * ($Message.Length + 3)) -ForegroundColor DarkCyan
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Message
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Write-Success {
    param([string]$Message)
    Write-Information "✅ $Message" -InformationAction Continue
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Message
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Write-Error {
    param([string]$Message)
    Write-Information "❌ $Message" -InformationAction Continue
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Message
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Write-Warning {
    param([string]$Message)
    Write-Information "⚠️ $Message" -InformationAction Continue
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Message
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Write-Info {
    param([string]$Message)
    Write-Information "ℹ️ $Message" -InformationAction Continue
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
function Test-AzureMCPStatus {
    Write-StepHeader "Testing Azure MCP Integration"

    $results = @{}

    try {
        # Test Azure CLI authentication
        $account = az account show --output json 2>$null | ConvertFrom-Json
        if ($account.id -eq $config.Azure.SubscriptionId) {
            Write-Success "Azure CLI authenticated"
            $results.AzureAuth = $true
        } else {
            Write-Error "Wrong Azure subscription or not authenticated"
            $results.AzureAuth = $false
        }
    }
    catch {
        Write-Error "Azure CLI not available or not authenticated"
        $results.AzureAuth = $false
    }

    try {
        # Test resource group access
        $rg = az group show --name $config.Azure.ResourceGroup --output json 2>$null | ConvertFrom-Json
        if ($rg.name -eq $config.Azure.ResourceGroup) {
            Write-Success "Resource group access confirmed"
            $results.ResourceGroup = $true
        } else {
            Write-Error "Cannot access resource group"
            $results.ResourceGroup = $false
        }
    }
    catch {
        Write-Error "Resource group access failed"
        $results.ResourceGroup = $false
    }

    try {
        # Test SQL server access
        $sqlServer = az sql server show --name $config.Azure.SqlServer --resource-group $config.Azure.ResourceGroup --output json 2>$null | ConvertFrom-Json
        if ($sqlServer.name -eq $config.Azure.SqlServer) {
            Write-Success "SQL Server access confirmed"
            $results.SqlServer = $true
        } else {
            Write-Error "Cannot access SQL Server"
            $results.SqlServer = $false
        }
    }
    catch {
        Write-Error "SQL Server access failed"
        $results.SqlServer = $false
    }

    try {
        # Test database access
        $database = $config.Databases[$Environment]
        $db = az sql db show --server $config.Azure.SqlServer --name $database --resource-group $config.Azure.ResourceGroup --output json 2>$null | ConvertFrom-Json
        if ($db.name -eq $database) {
            Write-Success "Database '$database' access confirmed"
            $results.Database = $true
        } else {
            Write-Error "Cannot access database '$database'"
            $results.Database = $false
        }
    }
    catch {
        Write-Error "Database access failed"
        $results.Database = $false
    }

    return $results
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
function Test-GitHubMCPStatus {
    Write-StepHeader "Testing GitHub MCP Integration"

    $results = @{}

    try {
        # Test GitHub CLI authentication
        $authStatus = gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            $user = gh api user --jq '.login' 2>$null
            Write-Success "GitHub CLI authenticated as: $user"
            $results.GitHubAuth = $true
        } else {
            Write-Error "GitHub CLI not authenticated"
            $results.GitHubAuth = $false
        }
    }
    catch {
        Write-Error "GitHub CLI not available"
        $results.GitHubAuth = $false
    }

    try {
        # Test repository access
        $repo = gh repo view "$($config.GitHub.Owner)/$($config.GitHub.Repository)" --json name, owner 2>$null | ConvertFrom-Json
        if ($repo.name -eq $config.GitHub.Repository) {
            Write-Success "Repository access confirmed"
            $results.Repository = $true
        } else {
            Write-Error "Cannot access repository"
            $results.Repository = $false
        }
    }
    catch {
        Write-Error "Repository access failed"
        $results.Repository = $false
    }

    try {
        # Test workflows
        $workflows = gh workflow list --repo "$($config.GitHub.Owner)/$($config.GitHub.Repository)" --json name, state 2>$null | ConvertFrom-Json
        $activeWorkflows = ($workflows | Where-Object { $_.state -eq "active" }).Count
        Write-Success "Found $($workflows.Count) workflows ($activeWorkflows active)"
        $results.Workflows = $workflows.Count -gt 0
    }
    catch {
        Write-Error "Cannot retrieve workflows"
        $results.Workflows = $false
    }

    try {
        # Test secrets
        $secrets = gh secret list --repo "$($config.GitHub.Owner)/$($config.GitHub.Repository)" 2>$null
        $azureSecrets = $secrets | Where-Object { $_ -match "AZURE_" }
        Write-Success "Found $($azureSecrets.Count) Azure secrets"
        $results.Secrets = $azureSecrets.Count -ge 3
    }
    catch {
        Write-Error "Cannot retrieve secrets"
        $results.Secrets = $false
    }

    return $results
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
function Setup-GitHubSecret {
    Write-StepHeader "Setting up GitHub Secrets"

    $secrets = @{
        "AZURE_CLIENT_ID" = $config.Azure.ClientId
        "AZURE_TENANT_ID" = $config.Azure.TenantId
        "AZURE_SUBSCRIPTION_ID" = $config.Azure.SubscriptionId
    }

    $secretsAdded = 0

    foreach ($secret in $secrets.GetEnumerator()) {
        try {
            echo $secret.Value | gh secret set $secret.Key --repo "$($config.GitHub.Owner)/$($config.GitHub.Repository)"
            Write-Success "Added secret: $($secret.Key)"
            $secretsAdded++
        }
        catch {
            Write-Error "Failed to add secret: $($secret.Key)"
        }
    }

    Write-Warning "AZURE_CLIENT_SECRET must be added manually for security"
    Write-Info "Use: gh secret set AZURE_CLIENT_SECRET --repo $($config.GitHub.Owner)/$($config.GitHub.Repository)"

    return $secretsAdded
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER WorkflowFile
${3:Parameter description}

.PARAMETER Ref
${4:Parameter description}

.EXAMPLE
${5:An example}

.NOTES
${6:General notes}
#>
function Trigger-Workflow {
    param([string]$WorkflowFile, [string]$Ref = "develop")

    Write-StepHeader "Triggering Workflow: $WorkflowFile"

    try {
        gh workflow run $WorkflowFile --repo "$($config.GitHub.Owner)/$($config.GitHub.Repository)" --ref $Ref
        Write-Success "Workflow triggered on branch: $Ref"

        # Wait and get status
        Start-Sleep -Seconds 5
        $runs = gh run list --workflow=$WorkflowFile --repo "$($config.GitHub.Owner)/$($config.GitHub.Repository)" --limit 1 --json status, url, conclusion 2>$null | ConvertFrom-Json

        if ($runs.Count -gt 0) {
            $run = $runs[0]
            Write-Info "Status: $($run.status)"
            Write-Info "URL: $($run.url)"
        }

        return $true
    }
    catch {
        Write-Error "Failed to trigger workflow: $($_.Exception.Message)"
        return $false
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER Limit
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Monitor-Workflow {
    param([int]$Limit = 5)

    Write-StepHeader "Monitoring Recent Workflow Runs"

    try {
        $runs = gh run list --repo "$($config.GitHub.Owner)/$($config.GitHub.Repository)" --limit $Limit --json status, conclusion, workflowName, createdAt, url 2>$null | ConvertFrom-Json

        foreach ($run in $runs) {
            $statusIcon = switch ($run.status) {
                "completed" { if ($run.conclusion -eq "success") { "✅" } else { "❌" } }
                "in_progress" { "🔄" }
                "queued" { "⏳" }
                default { "❓" }
            }

            $date = [DateTime]::Parse($run.createdAt).ToString("MM-dd HH:mm")
            Write-Information "$statusIcon [$date] $($run.workflowName) - $($run.status)" -InformationAction Continue
        }

        return $true
    }
    catch {
        Write-Error "Failed to retrieve workflow runs"
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
function Test-MCPEnvironment {
    Write-StepHeader "Testing MCP Environment"

    # Check MCP configuration
    $mcpConfigPath = ".vscode/mcp.json"
    if (Test-Path $mcpConfigPath) {
        Write-Success "MCP configuration file found"

        try {
            $mcpConfig = Get-Content $mcpConfigPath | ConvertFrom-Json
            $azureMCP = $mcpConfig.mcpServers.azure
            $githubMCP = $mcpConfig.mcpServers.github

            if ($azureMCP) {
                Write-Success "Azure MCP server configured"
            } else {
                Write-Warning "Azure MCP server not configured"
            }

            if ($githubMCP) {
                Write-Success "GitHub MCP server configured"
            } else {
                Write-Warning "GitHub MCP server not configured"
            }
        }
        catch {
            Write-Error "Invalid MCP configuration format"
        }
    } else {
        Write-Warning "MCP configuration file not found"
    }

    # Check environment variables
    $envVars = @("AZURE_CLIENT_ID", "AZURE_TENANT_ID", "AZURE_SUBSCRIPTION_ID")
    foreach ($var in $envVars) {
        if ((Get-Item "env:$var" -ErrorAction SilentlyContinue).Value) {
            Write-Success "Environment variable $var is set"
        } else {
            Write-Warning "Environment variable $var is not set"
        }
    }
}

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER TargetEnvironment
${3:Parameter description}

.EXAMPLE
${4:An example}

.NOTES
${5:General notes}
#>
function Deploy-Environment {
    param([string]$TargetEnvironment)

    Write-StepHeader "Deploying to $TargetEnvironment Environment"

    $branch = if ($TargetEnvironment -eq "staging") { $config.GitHub.StagingBranch } else { $config.GitHub.DefaultBranch }

    # Trigger the appropriate workflow
    $workflowTriggered = Trigger-Workflow -WorkflowFile $config.Workflows.CI -Ref $branch

    if ($workflowTriggered) {
        Write-Success "Deployment initiated for $TargetEnvironment"
        Write-Info "Monitor progress with: -Action monitor"
        return $true
    } else {
        Write-Error "Failed to initiate deployment"
        return $false
    }
}

# Main execution logic
Write-Information "=== BusBuddy-3 MCP Integration Hub ===" -InformationAction Continue
Write-Information "Action: $Action | Environment: $Environment" -InformationAction Continue
Write-Information "Azure MCP + GitHub MCP Unified Management" -InformationAction Continue
Write-Host ""

$overallSuccess = $true

switch ($Action.ToLower()) {
    "status" {
        $azureResults = Test-AzureMCPStatus
        $githubResults = Test-GitHubMCPStatus
        Test-MCPEnvironment

        $azureHealthy = ($azureResults.Values | Where-Object { $_ -eq $true }).Count -eq $azureResults.Count
        $githubHealthy = ($githubResults.Values | Where-Object { $_ -eq $true }).Count -eq $githubResults.Count

        Write-Information "`n📊 Integration Status Summary:" -InformationAction Continue
        Write-Information "Azure MCP: $(if ($azureHealthy) { '✅ Healthy' } else { '❌ Issues' })" -InformationAction Continue -ForegroundColor $(if ($azureHealthy) { 'Green' } else { 'Red' })
        Write-Information "GitHub MCP: $(if ($githubHealthy) { '✅ Healthy' } else { '❌ Issues' })" -InformationAction Continue -ForegroundColor $(if ($githubHealthy) { 'Green' } else { 'Red' })

        $overallSuccess = $azureHealthy -and $githubHealthy
    }

    "setup" {
        Write-StepHeader "Complete MCP Setup"

        # Test prerequisites
        $azureResults = Test-AzureMCPStatus
        $githubResults = Test-GitHubMCPStatus

        if ($githubResults.GitHubAuth -and $githubResults.Repository) {
            $secretsAdded = Setup-GitHubSecrets
            Write-Success "Setup completed - $secretsAdded secrets configured"
        } else {
            Write-Error "GitHub prerequisites not met"
            $overallSuccess = $false
        }
    }

    "test" {
        Write-StepHeader "Running Integration Tests"

        # Test both systems
        $azureResults = Test-AzureMCPStatus
        $githubResults = Test-GitHubMCPStatus
        Test-MCPEnvironment

        # Trigger a test workflow if requested
        if ($Force -and $githubResults.GitHubAuth) {
            Trigger-Workflow -WorkflowFile $config.Workflows.CI -Ref $config.GitHub.StagingBranch
        }
    }

    "deploy" {
        $deploySuccess = Deploy-Environment -TargetEnvironment $Environment
        $overallSuccess = $deploySuccess
    }

    "monitor" {
        Monitor-Workflows -Limit 10
    }

    "azure" {
        Test-AzureMCPStatus | Out-Null
    }

    "github" {
        Test-GitHubMCPStatus | Out-Null
    }

    "combined" {
        Write-StepHeader "Full Integration Test"

        $azureResults = Test-AzureMCPStatus
        $githubResults = Test-GitHubMCPStatus
        Test-MCPEnvironment

        if ($Force) {
            Setup-GitHubSecrets | Out-Null
            Trigger-Workflow -WorkflowFile $config.Workflows.CI -Ref $config.GitHub.StagingBranch | Out-Null
        }

        Monitor-Workflows -Limit 3
    }

    default {
        Write-Error "Unknown action: $Action"
        Write-Info "Available actions: status, setup, test, deploy, monitor, azure, github, combined"
        $overallSuccess = $false
    }
}

# Final status
Write-Host ""
if ($overallSuccess) {
    Write-Information "🎉 MCP Integration Hub - Operation completed successfully!" -InformationAction Continue

    if ($Action -eq "setup") {
        Write-Information "`nNext steps:" -InformationAction Continue
        Write-Information "1. Add AZURE_CLIENT_SECRET manually: gh secret set AZURE_CLIENT_SECRET" -InformationAction Continue
        Write-Information "2. Test deployment: .\mcp-integration-hub.ps1 -Action deploy -Environment staging" -InformationAction Continue
        Write-Information "3. Monitor workflows: .\mcp-integration-hub.ps1 -Action monitor" -InformationAction Continue
    }
} else {
    Write-Information "❌ MCP Integration Hub - Issues detected" -InformationAction Continue
    Write-Information "Run with -Action status to diagnose problems" -InformationAction Continue
    exit 1
}
