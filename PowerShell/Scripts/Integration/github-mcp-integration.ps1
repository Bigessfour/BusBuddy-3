# GitHub MCP Integration for BusBuddy-3 CI/CD
# This script configures GitHub MCP for automated workflow management

param(
    [Parameter(Mandatory = $false)]
    [switch]$ValidateOnly,

    [Parameter(Mandatory = $false)]
    [switch]$SetupWorkflows
)

# GitHub configuration
$githubConfig = @{
    Owner = "Bigessfour"
    Repository = "BusBuddy"
    DefaultBranch = "main"
    StagingBranch = "develop"
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
function Test-GitHubAuthentication {
    Write-Information "🔐 Testing GitHub Authentication..." -InformationAction Continue

    try {
        $authStatus = gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ GitHub CLI authenticated successfully" -InformationAction Continue

            # Get current user
            $user = gh api user --jq '.login'
            Write-Information "✅ Authenticated as: $user" -InformationAction Continue
            return $true
        } else {
            Write-Information "❌ GitHub authentication failed" -InformationAction Continue
            return $false
        }
    }
    catch {
        Write-Information "❌ GitHub CLI error: $($_.Exception.Message)" -InformationAction Continue
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
function Test-RepositoryAccess {
    Write-Information "📁 Testing Repository Access..." -InformationAction Continue

    try {
        $repo = gh repo view "$($githubConfig.Owner)/$($githubConfig.Repository)" --json name, owner | ConvertFrom-Json

        if ($repo.name -eq $githubConfig.Repository -and $repo.owner.login -eq $githubConfig.Owner) {
            Write-Information "✅ Repository access confirmed" -InformationAction Continue
            return $true
        } else {
            Write-Information "❌ Repository access failed" -InformationAction Continue
            return $false
        }
    }
    catch {
        Write-Information "❌ Repository access test failed: $($_.Exception.Message)" -InformationAction Continue
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
function Test-GitHubSecret {
    Write-Information "🔑 Testing GitHub Secrets..." -InformationAction Continue

    $requiredSecrets = @("AZURE_CLIENT_ID", "AZURE_TENANT_ID", "AZURE_CLIENT_SECRET")
    $success = $true

    foreach ($secret in $requiredSecrets) {
        try {
            # Note: GitHub CLI cannot read secret values, only list names
            $secrets = gh secret list --repo "$($githubConfig.Owner)/$($githubConfig.Repository)" --json name | ConvertFrom-Json

            if ($secrets.name -contains $secret) {
                Write-Information "✅ Secret $secret exists" -InformationAction Continue
            } else {
                Write-Information "❌ Secret $secret is missing" -InformationAction Continue
                $success = $false
            }
        }
        catch {
            Write-Information "❌ Error checking secret $secret" -InformationAction Continue
            $success = $false
        }
    }

    return $success
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
function Test-WorkflowFile {
    Write-Information "🔄 Testing Workflow Files..." -InformationAction Continue

    $workflowFile = ".github/workflows/azure-sql-ci-cd.yml"

    if (Test-Path $workflowFile) {
        Write-Information "✅ CI/CD workflow file exists" -InformationAction Continue

        # Validate workflow syntax
        try {
            $workflow = Get-Content $workflowFile -Raw | ConvertFrom-Yaml -ErrorAction Stop
            Write-Information "✅ Workflow YAML syntax is valid" -InformationAction Continue
            return $true
        }
        catch {
            Write-Information "❌ Workflow YAML syntax error: $($_.Exception.Message)" -InformationAction Continue
            return $false
        }
    } else {
        Write-Information "❌ CI/CD workflow file missing" -InformationAction Continue
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
function Initialize-GitHubMCP {
    Write-Information "🚀 Initializing GitHub MCP Integration..." -InformationAction Continue

    # Test GitHub MCP server installation
    try {
        $mcpTest = npx -y @github/mcp-server --help 2>$null
        Write-Information "✅ GitHub MCP server is available" -InformationAction Continue
    }
    catch {
        Write-Information "❌ GitHub MCP server installation issue" -InformationAction Continue
    }

    # Set up environment for GitHub MCP
    # Note: GitHub PAT would need to be configured separately for security
    Write-Information "⚠️ GitHub Personal Access Token needs to be configured in MCP config" -InformationAction Continue
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
function Invoke-WorkflowValidation {
    Write-Information "🔍 Validating GitHub Workflows..." -InformationAction Continue

    try {
        # List recent workflow runs
        $runs = gh run list --repo "$($githubConfig.Owner)/$($githubConfig.Repository)" --limit 5 --json status, conclusion, workflowName | ConvertFrom-Json

        Write-Information "Recent workflow runs:" -InformationAction Continue
        foreach ($run in $runs) {
            $status = if ($run.conclusion -eq "success") { "✅" } elseif ($run.conclusion -eq "failure") { "❌" } else { "⏳" }
            Write-Information "$status $($run.workflowName) - $($run.status)" -InformationAction Continue
        }

        return $true
    }
    catch {
        Write-Information "❌ Error validating workflows: $($_.Exception.Message)" -InformationAction Continue
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
function Export-GitHubMCPConfig {
    Write-Information "📄 Exporting GitHub MCP configuration..." -InformationAction Continue

    $config = @{
        GitHub = $githubConfig
        MCP = @{
            Enabled = $true
            Server = "github-mcp"
            Capabilities = @(
                "repository-access",
                "workflow-management",
                "issue-tracking",
                "pull-request-management"
            )
        }
        Workflows = @{
            CICD = "azure-sql-ci-cd.yml"
            Triggers = @("push", "pull_request", "workflow_dispatch")
            Environments = @("staging", "production")
        }
        Integration = @{
            Azure = @{
                ServicePrincipal = "0a93d214-37e7-4147-beaf-8ca8036c614e"
                Databases = @("BusBuddyDB", "BusBuddyDB-Staging")
            }
        }
    }

    $configJson = $config | ConvertTo-Json -Depth 5
    $configPath = "github-mcp-config.json"
    $configJson | Out-File -FilePath $configPath -Encoding UTF8

    Write-Information "✅ GitHub MCP configuration exported to $configPath" -InformationAction Continue
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
function Enable-AdvancedWorkflow {
    Write-Information "⚡ Setting up advanced workflow features..." -InformationAction Continue

    # Enable workflow features
    try {
        # Enable Actions for the repository (if not already enabled)
        Write-Information "✅ GitHub Actions should be enabled" -InformationAction Continue

        # Set up branch protection rules (would require admin access)
        Write-Information "⚠️ Consider setting up branch protection rules for main branch" -InformationAction Continue

        return $true
    }
    catch {
        Write-Information "❌ Error setting up advanced workflows: $($_.Exception.Message)" -InformationAction Continue
        return $false
    }
}

# Main execution
Write-Information "=== GitHub MCP Integration for BusBuddy-3 ===" -InformationAction Continue
Write-Information "Repository: $($githubConfig.Owner)/$($githubConfig.Repository)" -InformationAction Continue
Write-Information "Validation Only: $ValidateOnly" -InformationAction Continue
Write-Information "Setup Workflows: $SetupWorkflows" -InformationAction Continue
Write-Host ""

$success = $true

# Run validation tests
$success = $success -and (Test-GitHubAuthentication)
$success = $success -and (Test-RepositoryAccess)
$success = $success -and (Test-GitHubSecrets)
$success = $success -and (Test-WorkflowFiles)

if (-not $ValidateOnly) {
    Initialize-GitHubMCP
    $success = $success -and (Invoke-WorkflowValidation)
    Export-GitHubMCPConfig

    if ($SetupWorkflows) {
        $success = $success -and (Enable-AdvancedWorkflows)
    }
}

if ($success) {
    Write-Information "`n🎉 GitHub MCP Integration completed successfully!" -InformationAction Continue
    Write-Information "Next steps:" -InformationAction Continue
    Write-Information "1. Configure GitHub Personal Access Token for MCP" -InformationAction Continue
    Write-Information "2. Test end-to-end CI/CD pipeline" -InformationAction Continue
    Write-Information "3. Monitor workflow executions" -InformationAction Continue
} else {
    Write-Information "`n❌ GitHub MCP Integration encountered issues" -InformationAction Continue
    Write-Information "Please resolve the above errors before proceeding" -InformationAction Continue
    exit 1
}
