# BusBuddy Application Insights Setup Script
# Based on Microsoft Azure PowerShell documentation

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "BusBuddy-RG",

    [Parameter(Mandatory=$true)]
    [string]$Location = "centralus",

    [Parameter(Mandatory=$false)]
    [string]$AppInsightsName = "busbuddy-insights"
)

Write-Information "Setting up Application Insights for BusBuddy MVP monitoring" -InformationAction Continue

try {
    # Create Application Insights resource
    Write-Information "Creating Application Insights resource: $AppInsightsName" -InformationAction Continue

    $appInsights = az monitor app-insights component create `
        --app $AppInsightsName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --query "instrumentationKey" `
        --output tsv

    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Application Insights created successfully" -InformationAction Continue
        Write-Information "📋 Instrumentation Key: $appInsights" -InformationAction Continue

        # Set environment variable for local development
        [Environment]::SetEnvironmentVariable("APPLICATIONINSIGHTS_INSTRUMENTATION_KEY", $appInsights, "User")

        Write-Information "🔧 Next steps:" -InformationAction Continue
        Write-Information "1. Add Application Insights to appsettings.azure.json" -InformationAction Continue
        Write-Information "2. Install Microsoft.ApplicationInsights.AspNetCore NuGet package" -InformationAction Continue
        Write-Information "3. Configure Application Insights in Startup/Program.cs" -InformationAction Continue
    } else {
        Write-Error "Failed to create Application Insights resource"
    }
} catch {
    Write-Error "Error setting up Application Insights: $($_.Exception.Message)"
}
