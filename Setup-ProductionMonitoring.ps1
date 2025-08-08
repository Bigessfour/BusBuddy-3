# BusBuddy Production Monitoring Setup
# Creates Application Insights dashboards and alerts for production monitoring

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "BusBuddy-RG",

    [Parameter(Mandatory=$true)]
    [string]$ApplicationInsightsName = "busbuddy-insights",

    [Parameter(Mandatory=$false)]
    [string]$Location = "centralus"
)

Write-Information "📊 Setting up BusBuddy Production Monitoring" -InformationAction Continue

try {
    # Step 1: Get Application Insights resource details
    Write-Information "🔍 Step 1: Retrieving Application Insights details" -InformationAction Continue

    $appInsightsId = az monitor app-insights component show `
        --app $ApplicationInsightsName `
        --resource-group $ResourceGroupName `
        --query "id" `
        --output tsv

    if ($LASTEXITCODE -ne 0) {
        throw "Application Insights resource not found: $ApplicationInsightsName"
    }

    Write-Information "✅ Application Insights found: $ApplicationInsightsName" -InformationAction Continue

    # Step 2: Create critical alerts
    Write-Information "🚨 Step 2: Creating critical monitoring alerts" -InformationAction Continue

    # High error rate alert
    $errorRateAlert = az monitor metrics alert create `
        --name "BusBuddy-HighErrorRate" `
        --resource-group $ResourceGroupName `
        --scopes $appInsightsId `
        --condition "avg exceptions/server > 10" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --severity 1 `
        --description "Alert when error rate exceeds 10 errors in 5 minutes"

    # High response time alert
    $responseTimeAlert = az monitor metrics alert create `
        --name "BusBuddy-HighResponseTime" `
        --resource-group $ResourceGroupName `
        --scopes $appInsightsId `
        --condition "avg requests/duration > 3000" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --severity 2 `
        --description "Alert when average response time exceeds 3 seconds"

    # Database connection failures
    $dbConnectionAlert = az monitor metrics alert create `
        --name "BusBuddy-DatabaseConnectionFailures" `
        --resource-group $ResourceGroupName `
        --scopes $appInsightsId `
        --condition "count dependencies/failed > 5" `
        --window-size 5m `
        --evaluation-frequency 1m `
        --severity 1 `
        --description "Alert when database connection failures exceed 5 in 5 minutes"

    Write-Information "✅ Critical alerts created successfully" -InformationAction Continue

    # Step 3: Create custom dashboard configuration
    Write-Information "📊 Step 3: Creating monitoring dashboard configuration" -InformationAction Continue

    $dashboardConfig = @{
        "name" = "BusBuddy Production Monitoring"
        "description" = "Comprehensive monitoring dashboard for BusBuddy MVP"
        "widgets" = @(
            @{
                "type" = "ApplicationInsights"
                "title" = "Application Health Overview"
                "metrics" = @("requests/count", "requests/duration", "exceptions/count", "dependencies/duration")
                "timeRange" = "PT1H"
            },
            @{
                "type" = "ApplicationInsights"
                "title" = "Student Management Performance"
                "query" = "requests | where name contains 'Student' | summarize avg(duration), count() by bin(timestamp, 5m)"
                "timeRange" = "PT4H"
            },
            @{
                "type" = "ApplicationInsights"
                "title" = "Route Design Performance"
                "query" = "requests | where name contains 'Route' | summarize avg(duration), count() by bin(timestamp, 5m)"
                "timeRange" = "PT4H"
            },
            @{
                "type" = "ApplicationInsights"
                "title" = "Database Performance"
                "query" = "dependencies | where type == 'SQL' | summarize avg(duration), count() by bin(timestamp, 5m)"
                "timeRange" = "PT4H"
            },
            @{
                "type" = "ApplicationInsights"
                "title" = "Error Analysis"
                "query" = "exceptions | summarize count() by type, bin(timestamp, 10m) | order by timestamp desc"
                "timeRange" = "PT24H"
            },
            @{
                "type" = "ApplicationInsights"
                "title" = "User Activity"
                "query" = "pageViews | summarize count() by name, bin(timestamp, 1h) | order by timestamp desc"
                "timeRange" = "PT24H"
            }
        ),
        "alerts" = @(
            @{
                "name" = "BusBuddy-HighErrorRate"
                "description" = "Monitor for high error rates"
                "severity" = "Critical"
            },
            @{
                "name" = "BusBuddy-HighResponseTime"
                "description" = "Monitor for slow response times"
                "severity" = "Warning"
            },
            @{
                "name" = "BusBuddy-DatabaseConnectionFailures"
                "description" = "Monitor for database connectivity issues"
                "severity" = "Critical"
            }
        ),
        "thresholds" = @{
            "maxResponseTime" = 3000
            "maxErrorRate" = 10
            "maxDbConnectionFailures" = 5
            "minAvailability" = 99.0
        }
    }

    # Save dashboard configuration
    $dashboardFileName = "BusBuddy-Monitoring-Dashboard-Config.json"
    $dashboardConfig | ConvertTo-Json -Depth 5 | Out-File $dashboardFileName -Encoding UTF8

    Write-Information "✅ Dashboard configuration saved: $dashboardFileName" -InformationAction Continue

    # Step 4: Create health check queries
    Write-Information "🔍 Step 4: Creating health check query templates" -InformationAction Continue

    $healthQueries = @{
        "applicationHealth" = @{
            "query" = "requests | where timestamp > ago(5m) | summarize successRate = 100.0 * countif(success == true) / count()"
            "description" = "Overall application success rate in last 5 minutes"
            "threshold" = 95.0
        },
        "performanceHealth" = @{
            "query" = "requests | where timestamp > ago(10m) | summarize avgDuration = avg(duration)"
            "description" = "Average response time in last 10 minutes"
            "threshold" = 3000
        },
        "databaseHealth" = @{
            "query" = "dependencies | where type == 'SQL' and timestamp > ago(5m) | summarize successRate = 100.0 * countif(success == true) / count()"
            "description" = "Database connection success rate in last 5 minutes"
            "threshold" = 98.0
        },
        "errorHealth" = @{
            "query" = "exceptions | where timestamp > ago(10m) | count"
            "description" = "Exception count in last 10 minutes"
            "threshold" = 5
        }
    }

    $healthQueriesFileName = "BusBuddy-Health-Check-Queries.json"
    $healthQueries | ConvertTo-Json -Depth 3 | Out-File $healthQueriesFileName -Encoding UTF8

    Write-Information "✅ Health check queries saved: $healthQueriesFileName" -InformationAction Continue

    # Step 5: Summary and next steps
    Write-Information "📊 Step 5: Production Monitoring Summary" -InformationAction Continue
    Write-Information "═══════════════════════════════════════" -InformationAction Continue
    Write-Information "🎯 Application Insights: $ApplicationInsightsName" -InformationAction Continue
    Write-Information "🚨 Critical Alerts: 3 alerts created" -InformationAction Continue
    Write-Information "📊 Dashboard Config: $dashboardFileName" -InformationAction Continue
    Write-Information "🔍 Health Queries: $healthQueriesFileName" -InformationAction Continue
    Write-Information "📍 Resource Group: $ResourceGroupName" -InformationAction Continue
    Write-Information "═══════════════════════════════════════" -InformationAction Continue

    Write-Information "🎯 Next Steps:" -InformationAction Continue
    Write-Information "1. Import dashboard configuration to Azure portal" -InformationAction Continue
    Write-Information "2. Configure alert notification channels (email, Teams, etc.)" -InformationAction Continue
    Write-Information "3. Set up automated health check monitoring" -InformationAction Continue
    Write-Information "4. Create runbooks for common alert scenarios" -InformationAction Continue
    Write-Information "5. Schedule regular monitoring reviews" -InformationAction Continue

    Write-Information "📋 Monitoring Resources Created:" -InformationAction Continue
    Write-Information "   🚨 BusBuddy-HighErrorRate alert" -InformationAction Continue
    Write-Information "   ⏱️ BusBuddy-HighResponseTime alert" -InformationAction Continue
    Write-Information "   🗄️ BusBuddy-DatabaseConnectionFailures alert" -InformationAction Continue
    Write-Information "   📊 Dashboard configuration template" -InformationAction Continue
    Write-Information "   🔍 Health check query templates" -InformationAction Continue

    Write-Information "🎉 Production monitoring setup completed successfully!" -InformationAction Continue

} catch {
    Write-Error "❌ Monitoring setup failed: $($_.Exception.Message)"
    Write-Error "📋 Check Azure permissions and Application Insights configuration"
    exit 1
}
