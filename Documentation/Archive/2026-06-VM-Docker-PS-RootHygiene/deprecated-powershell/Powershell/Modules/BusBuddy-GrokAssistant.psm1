# BusBuddy-GrokAssistant.psm1
# PowerShell Module for xAI Grok Integration in BusBuddy Project
# References:
# - GitHub Repository: https://github.com/Bigessfour/BusBuddy-3
# - Azure SQL Documentation: https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql
# - Syncfusion WPF Help: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf

#requires -Version 7.5

<#
.SYNOPSIS
    BusBuddy Grok Assistant Module
.DESCRIPTION
    Provides AI-powered analysis for CI/CD, route optimization, maintenance prediction, and more using xAI's Grok model.
.NOTES
    Requires XAI_API_KEY environment variable or secure API key setup.
    Integrates with BusBuddy WPF application using MVVM, Syncfusion, and Entity Framework Core.
    Version: 1.0.0
    Author: BusBuddy Development Team
#>

# Import configuration
$moduleRoot = $PSScriptRoot
Import-Module (Join-Path $moduleRoot 'grok-config.psm1') -Force -ErrorAction Stop

# Global variables for module state
$global:GrokModuleState = @{
    ApiCalls = @()
    ResponseCache = @{}
    LastApiCall = $null
    SessionStartTime = Get-Date
}

#region Helper Functions

# Helper function for API calls
function Invoke-GrokApi {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$true)]
        [string]$Prompt,

        [string]$Model = $Global:GrokConfig.DefaultModel,

        [string]$SystemPrompt = "You are Grok, a helpful AI assistant built by xAI, integrated into the BusBuddy school bus management system."
    )

    if (-not $Global:GrokConfig.ApiKey) {
        throw "XAI_API_KEY not set. Please set the environment variable or use Set-SecureApiKey from BusBuddy-SecureConfig module."
    }

    # Check cache first
    $cacheKey = Get-HashFromString -InputString "$SystemPrompt$Prompt$Model"
    if ($Global:GrokConfig.UseLiveAPI -and $global:GrokModuleState.ResponseCache.ContainsKey($cacheKey)) {
        $cachedItem = $global:GrokModuleState.ResponseCache[$cacheKey]
        if (((Get-Date) - $cachedItem.Timestamp).TotalMinutes -lt 30) {
            Write-Verbose "Returning cached response for prompt (age: $([Math]::Round(((Get-Date) - $cachedItem.Timestamp).TotalMinutes, 1)) minutes)"
            return $cachedItem.Response
        }
    }

    # Prepare API request
    $headers = @{
        "Authorization" = "Bearer $($Global:GrokConfig.ApiKey)"
        "Content-Type" = "application/json"
        "User-Agent" = "BusBuddy-PowerShell/1.0"
    }

    $body = @{
        model = $Model
        messages = @(
            @{role = "system"; content = $SystemPrompt}
            @{role = "user"; content = $Prompt}
        )
        max_tokens = $Global:GrokConfig.MaxTokens
        temperature = $Global:GrokConfig.Temperature
        stream = $false
    } | ConvertTo-Json -Depth 5

    $response = $null
    $attempt = 0
    $lastError = $null

    # Retry logic
    while ($attempt -lt $Global:GrokConfig.RetryAttempts) {
        try {
            Write-Verbose "API attempt $($attempt + 1)/$($Global:GrokConfig.RetryAttempts)"
            $response = Invoke-RestMethod -Uri "$($Global:GrokConfig.BaseUrl)/chat/completions" -Method Post -Headers $headers -Body $body -TimeoutSec $Global:GrokConfig.TimeoutSeconds
            break
        } catch {
            $lastError = $_

            # Enhanced error handling for specific HTTP status codes
            if ($_.Exception.Response.StatusCode -eq 400) {
                Write-Error "Bad Request (400): Check model name (use 'grok-4-0709') or payload format. Current model: $Model"
                throw "Invalid request: Verify model name is 'grok-4-0709' and payload is properly formatted."
            }
            elseif ($_.Exception.Response.StatusCode -eq 401) {
                Write-Error "Unauthorized (401): Check API key validity and authentication."
                throw "Authentication failed: Verify XAI API key is valid and properly configured."
            }
            elseif ($_.Exception.Response.StatusCode -eq 429) {
                Write-Warning "Rate limited (429): Too many requests. Extending retry delay..."
                Start-Sleep -Seconds ($Global:GrokConfig.RetryDelaySeconds * 2)
            }

            $attempt++
            if ($attempt -eq $Global:GrokConfig.RetryAttempts) {
                throw "API call failed after $attempt attempts: $($_.Exception.Message)"
            }
            Write-Warning "API attempt $attempt failed, retrying in $($Global:GrokConfig.RetryDelaySeconds) seconds..."
            Start-Sleep -Seconds $Global:GrokConfig.RetryDelaySeconds
        }
    }

    if (-not $response) {
        throw "Failed to get response from API: $($lastError.Exception.Message)"
    }

    $content = $response.choices[0].message.content

    # Log the successful call
    $promptPreview = $Prompt.Substring(0, [Math]::Min(100, $Prompt.Length))
    if ($Prompt.Length -gt 100) { $promptPreview += "..." }

    $apiCall = [PSCustomObject]@{
        Timestamp = Get-Date
        Prompt = $promptPreview
        Model = $Model
        TokensUsed = $response.usage.total_tokens
        PromptTokens = $response.usage.prompt_tokens
        CompletionTokens = $response.usage.completion_tokens
        Success = $true
    }

    $global:GrokModuleState.ApiCalls += $apiCall
    $global:GrokModuleState.LastApiCall = Get-Date

    # Cache the response
    $global:GrokModuleState.ResponseCache[$cacheKey] = @{
        Timestamp = Get-Date
        Response = $content
        TokensUsed = $response.usage.total_tokens
    }

    # Clean old cache entries (keep only last 100)
    if ($global:GrokModuleState.ResponseCache.Count -gt 100) {
        $oldestKeys = $global:GrokModuleState.ResponseCache.GetEnumerator() |
            Sort-Object { $_.Value.Timestamp } |
            Select-Object -First ($global:GrokModuleState.ResponseCache.Count - 80) |
            ForEach-Object { $_.Key }

        foreach ($key in $oldestKeys) {
            $global:GrokModuleState.ResponseCache.Remove($key)
        }
    }

    return $content
}

# Helper for hashing (for cache keys)
function Get-HashFromString {
    [CmdletBinding()]
    param([string]$InputString)

    try {
        $hasher = [System.Security.Cryptography.SHA256]::Create()
        $hashBytes = $hasher.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($InputString))
        return [BitConverter]::ToString($hashBytes) -replace '-',''
    }
    finally {
        if ($hasher) { $hasher.Dispose() }
    }
}

#endregion

#region Core Commands

# Command: Test-GrokConnection
function Test-GrokConnection {
    <#
    .SYNOPSIS
        Tests the connection to xAI Grok API
    .DESCRIPTION
        Performs a simple API call to verify connectivity and authentication
    .EXAMPLE
        Test-GrokConnection
        grok-test
    #>
    [CmdletBinding()]
    [Alias("grok-test")]
    param()

    Write-Information "Testing Grok API connection..." -InformationAction Continue

    try {
        $testPrompt = "Test connection: Respond with exactly 'Connection successful' and nothing else."
        $systemPrompt = "You are testing API connection. Respond only with the exact text requested."

        $response = Invoke-GrokApi -Prompt $testPrompt -SystemPrompt $systemPrompt

        if ($response -like "*successful*") {
            Write-Output "✅ Grok API connection successful."
            Write-Output "   Model: $($Global:GrokConfig.DefaultModel)"
            Write-Output "   Base URL: $($Global:GrokConfig.BaseUrl)"
            Write-Output "   Response: $($response.Trim())"
            return $true
        } else {
            Write-Warning "⚠️ Unexpected response: $response"
            return $false
        }
    } catch {
        Write-Error "❌ Connection failed: $($_.Exception.Message)"
        return $false
    }
}

# Command: Get-GrokConfig
function Get-GrokConfig {
    <#
    .SYNOPSIS
        Displays current Grok configuration
    .DESCRIPTION
        Shows configuration settings with API key masked for security
    .EXAMPLE
        Get-GrokConfig
        grok-config
    #>
    [CmdletBinding()]
    [Alias("grok-config")]
    param()

    $configCopy = $Global:GrokConfig.Clone()
    if ($configCopy.ApiKey) {
        $configCopy.ApiKey = "***MASKED*** (Length: $($Global:GrokConfig.ApiKey.Length))"
    } else {
        $configCopy.ApiKey = "Not Set"
    }

    return $configCopy
}

# Command: Get-GrokAPILog
function Get-GrokAPILog {
    <#
    .SYNOPSIS
        Shows recent API call history and statistics
    .DESCRIPTION
        Displays API usage logs with token consumption and timing information
    .PARAMETER LastN
        Number of recent calls to display (default: 10)
    .EXAMPLE
        Get-GrokAPILog
        Get-GrokAPILog -LastN 20
        grok-log
    #>
    [CmdletBinding()]
    [Alias("grok-log")]
    param(
        [int]$LastN = 10
    )

    $logs = $global:GrokModuleState.ApiCalls | Select-Object -Last $LastN
    $totalTokens = ($logs | Measure-Object -Property TokensUsed -Sum).Sum
    $totalCalls = $global:GrokModuleState.ApiCalls.Count

    Write-Output "🤖 Grok API Usage Summary"
    Write-Output "========================="
    Write-Output "Session Start: $($global:GrokModuleState.SessionStartTime)"
    Write-Output "Total Calls: $totalCalls"
    Write-Output "Cache Entries: $($global:GrokModuleState.ResponseCache.Count)"

    if ($logs.Count -gt 0) {
        Write-Output "`nRecent API Calls (Last $($logs.Count)):"
        $logs | Format-Table -Property Timestamp, Model, TokensUsed, Prompt -AutoSize

        Write-Output "Token Usage (last $($logs.Count) calls): $totalTokens"
    } else {
        Write-Output "`nNo API calls recorded in this session."
    }

    if ($global:GrokModuleState.LastApiCall) {
        Write-Output "Last Call: $($global:GrokModuleState.LastApiCall)"
    }
}

#endregion

#region BusBuddy Analysis Commands

# Command: Invoke-GrokCIAnalysis
function Invoke-GrokCIAnalysis {
    <#
    .SYNOPSIS
        Analyzes CI/CD failures using Grok
    .DESCRIPTION
        Provides intelligent analysis of build failures, test failures, and deployment issues
    .PARAMETER ErrorMessage
        The error message or failure description
    .PARAMETER BuildLog
        Optional build log content for additional context
    .EXAMPLE
        Invoke-GrokCIAnalysis -ErrorMessage "MSBuild failed with error CS0246"
        grok-ci "Unit tests failed in VehicleServiceTests"
    #>
    [CmdletBinding()]
    [Alias("grok-ci")]
    param(
        [Parameter(Mandatory=$true)]
        [string]$ErrorMessage,

        [string]$BuildLog = ""
    )

    if (-not $Global:GrokConfig.EnableCIAnalysis) {
        throw "CI Analysis feature is disabled in configuration. Enable it with: `$Global:GrokConfig.EnableCIAnalysis = `$true"
    }

    Write-Information "Analyzing CI/CD failure with Grok..." -InformationAction Continue

    $fullDetails = "$ErrorMessage`n`nBuild Log: $BuildLog"
    $prompt = $Global:GrokConfig.Prompts.CIFailureAnalysis -f $fullDetails

    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokRouteOptimization
function Invoke-GrokRouteOptimization {
    <#
    .SYNOPSIS
        Optimizes bus routes using Grok AI
    .DESCRIPTION
        Analyzes route data and provides optimization recommendations
    .PARAMETER RouteData
        JSON or CSV route data including stops, distances, and timing
    .PARAMETER OptimizeFor
        Optimization target: FuelEfficiency, Time, Safety, Cost
    .PARAMETER MaxVehicles
        Maximum number of vehicles available
    .PARAMETER Constraints
        Additional constraints like driver availability, vehicle capacity
    .EXAMPLE
        Invoke-GrokRouteOptimization -RouteData $routeJson -OptimizeFor "FuelEfficiency"
        grok-routes $routeData -MaxVehicles 15
    #>
    [CmdletBinding()]
    [Alias("grok-routes")]
    param(
        [Parameter(Mandatory=$true)]
        [string]$RouteData,

        [ValidateSet("FuelEfficiency", "Time", "Safety", "Cost", "Balanced")]
        [string]$OptimizeFor = "FuelEfficiency",

        [int]$MaxVehicles = 0,

        [string[]]$Constraints = @()
    )

    if (-not $Global:GrokConfig.EnableRouteOptimization) {
        throw "Route Optimization feature is disabled in configuration."
    }

    Write-Information "Optimizing routes with Grok AI..." -InformationAction Continue

    $fullDetails = @"
Route Data: $RouteData

Optimization Parameters:
- Optimize For: $OptimizeFor
- Max Vehicles: $(if ($MaxVehicles -gt 0) { $MaxVehicles } else { "No limit" })
- Constraints: $(if ($Constraints.Count -gt 0) { $Constraints -join ', ' } else { "None" })
"@

    $prompt = $Global:GrokConfig.Prompts.RouteOptimization -f $fullDetails

    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokMaintenancePrediction
function Invoke-GrokMaintenancePrediction {
    <#
    .SYNOPSIS
        Predicts vehicle maintenance needs using Grok AI
    .DESCRIPTION
        Analyzes vehicle data to predict maintenance requirements and schedule
    .PARAMETER VehicleData
        Vehicle information including mileage, age, maintenance history
    .PARAMETER PredictionWindow
        Time window for predictions (e.g., "30days", "3months", "1year")
    .PARAMETER IncludeRiskAssessment
        Include risk assessment in the analysis
    .EXAMPLE
        Invoke-GrokMaintenancePrediction -VehicleData $vehicleJson -PredictionWindow "60days"
        grok-maintenance $vehicleData -IncludeRiskAssessment
    #>
    [CmdletBinding()]
    [Alias("grok-maintenance")]
    param(
        [Parameter(Mandatory=$true)]
        [string]$VehicleData,

        [ValidateSet("7days", "14days", "30days", "60days", "90days", "6months", "1year")]
        [string]$PredictionWindow = "30days",

        [switch]$IncludeRiskAssessment
    )

    if (-not $Global:GrokConfig.EnableMaintenancePrediction) {
        throw "Maintenance Prediction feature is disabled in configuration."
    }

    Write-Information "Predicting maintenance needs with Grok AI..." -InformationAction Continue

    $fullDetails = @"
Vehicle Data: $VehicleData

Prediction Parameters:
- Prediction Window: $PredictionWindow
- Include Risk Assessment: $IncludeRiskAssessment
- Analysis Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@

    $prompt = $Global:GrokConfig.Prompts.MaintenancePrediction -f $fullDetails

    return Invoke-GrokApi -Prompt $prompt
}

# Command: Get-GrokInsights
function Get-GrokInsights {
    <#
    .SYNOPSIS
        Gets general insights on BusBuddy topics using Grok
    .DESCRIPTION
        Provides expert insights on various BusBuddy-related topics
    .PARAMETER Topic
        The topic to get insights on
    .PARAMETER Context
        Additional context for the topic
    .EXAMPLE
        Get-GrokInsights -Topic "WPF performance optimization"
        Get-GrokInsights -Topic "Entity Framework migrations" -Context "Azure SQL Database"
        grok-insights "Syncfusion DataGrid best practices"
    #>
    [CmdletBinding()]
    [Alias("grok-insights")]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Topic,

        [string]$Context = ""
    )

    Write-Information "Getting Grok insights on: $Topic" -InformationAction Continue

    $systemPrompt = @"
You are Grok, providing expert insights for the BusBuddy school bus management system project.

Project Context:
- WPF application using MVVM pattern
- Syncfusion controls for professional UI
- Entity Framework Core for data access
- Azure SQL Database and LocalDB support
- .NET 8.0 framework
- PowerShell automation and tooling
- GitHub repository: https://github.com/Bigessfour/BusBuddy-3

Provide practical, actionable insights that are specific to this technology stack and domain.
"@

    $prompt = @"
Topic: $Topic

$(if ($Context) { "Additional Context: $Context" })

Please provide detailed insights including:
1. Best practices for this topic in the BusBuddy context
2. Common pitfalls to avoid
3. Specific recommendations for implementation
4. Integration considerations with existing technology stack
5. Performance and maintenance considerations
"@

    return Invoke-GrokApi -Prompt $prompt -SystemPrompt $systemPrompt
}

#endregion

#region Additional Analysis Commands

# Command: Invoke-GrokCodeReview
function Invoke-GrokCodeReview {
    <#
    .SYNOPSIS
        Performs AI-powered code review using Grok
    .DESCRIPTION
        Analyzes code for best practices, potential issues, and improvements
    .PARAMETER Code
        The code to review
    .EXAMPLE
        Invoke-GrokCodeReview -Code $csharpCode
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Code
    )

    if (-not $Global:GrokConfig.EnableCodeReview) {
        throw "Code Review feature is disabled in configuration."
    }

    Write-Information "Performing code review with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.CodeReview -f $Code
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokPerformanceAnalysis
function Invoke-GrokPerformanceAnalysis {
    <#
    .SYNOPSIS
        Analyzes performance data using Grok AI
    .DESCRIPTION
        Provides performance optimization recommendations
    .PARAMETER PerformanceData
        Performance metrics and profiling data
    .EXAMPLE
        Invoke-GrokPerformanceAnalysis -PerformanceData $perfData
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$PerformanceData
    )

    if (-not $Global:GrokConfig.EnablePerformanceAnalysis) {
        throw "Performance Analysis feature is disabled in configuration."
    }

    Write-Information "Analyzing performance data with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.PerformanceAnalysis -f $PerformanceData
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokSecurityAudit
function Invoke-GrokSecurityAudit {
    <#
    .SYNOPSIS
        Performs security audit using Grok AI
    .DESCRIPTION
        Analyzes security aspects and provides recommendations
    .PARAMETER SecurityContext
        Security-related information to audit
    .EXAMPLE
        Invoke-GrokSecurityAudit -SecurityContext $securityInfo
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$SecurityContext
    )

    if (-not $Global:GrokConfig.EnableSecurityAudit) {
        throw "Security Audit feature is disabled in configuration."
    }

    Write-Information "Performing security audit with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.SecurityAudit -f $SecurityContext
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokArchitectureReview
function Invoke-GrokArchitectureReview {
    <#
    .SYNOPSIS
        Reviews software architecture using Grok AI
    .DESCRIPTION
        Analyzes architectural decisions and provides recommendations
    .PARAMETER ArchitectureDetails
        Architecture documentation or code structure
    .EXAMPLE
        Invoke-GrokArchitectureReview -ArchitectureDetails $archInfo
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$ArchitectureDetails
    )

    if (-not $Global:GrokConfig.EnableArchitectureReview) {
        throw "Architecture Review feature is disabled in configuration."
    }

    Write-Information "Reviewing architecture with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.ArchitectureReview -f $ArchitectureDetails
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokTestOptimization
function Invoke-GrokTestOptimization {
    <#
    .SYNOPSIS
        Optimizes testing strategy using Grok AI
    .DESCRIPTION
        Analyzes test suite and provides optimization recommendations
    .PARAMETER TestContext
        Test suite information and metrics
    .EXAMPLE
        Invoke-GrokTestOptimization -TestContext $testInfo
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$TestContext
    )

    if (-not $Global:GrokConfig.EnableTestOptimization) {
        throw "Test Optimization feature is disabled in configuration."
    }

    Write-Information "Optimizing tests with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.TestOptimization -f $TestContext
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokDependencyAnalysis
function Invoke-GrokDependencyAnalysis {
    <#
    .SYNOPSIS
        Analyzes project dependencies using Grok AI
    .DESCRIPTION
        Reviews dependencies for security, compatibility, and optimization
    .PARAMETER DependencyData
        Dependency information (packages, versions, etc.)
    .EXAMPLE
        Invoke-GrokDependencyAnalysis -DependencyData $depData
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$DependencyData
    )

    if (-not $Global:GrokConfig.EnableDependencyAnalysis) {
        throw "Dependency Analysis feature is disabled in configuration."
    }

    Write-Information "Analyzing dependencies with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.DependencyAnalysis -f $DependencyData
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokDatabaseOptimization
function Invoke-GrokDatabaseOptimization {
    <#
    .SYNOPSIS
        Optimizes database performance using Grok AI
    .DESCRIPTION
        Analyzes database queries and structure for optimization opportunities
    .PARAMETER DatabaseContext
        Database schema, queries, or performance data
    .EXAMPLE
        Invoke-GrokDatabaseOptimization -DatabaseContext $dbInfo
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$DatabaseContext
    )

    Write-Information "Optimizing database with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.DatabaseOptimization -f $DatabaseContext
    return Invoke-GrokApi -Prompt $prompt
}

# Command: Invoke-GrokUIUXReview
function Invoke-GrokUIUXReview {
    <#
    .SYNOPSIS
        Reviews UI/UX design using Grok AI
    .DESCRIPTION
        Analyzes user interface and experience for improvements
    .PARAMETER UIUXContext
        UI/UX descriptions, screenshots, or design documents
    .EXAMPLE
        Invoke-GrokUIUXReview -UIUXContext $uiInfo
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$UIUXContext
    )

    Write-Information "Reviewing UI/UX with Grok..." -InformationAction Continue
    $prompt = $Global:GrokConfig.Prompts.UIUXReview -f $UIUXContext
    return Invoke-GrokApi -Prompt $prompt
}

#endregion

#region Session Management

# Command: Clear-GrokCache
function Clear-GrokCache {
    <#
    .SYNOPSIS
        Clears the Grok response cache
    .DESCRIPTION
        Removes all cached API responses to force fresh requests
    .EXAMPLE
        Clear-GrokCache
    #>
    [CmdletBinding()]
    param()

    $cacheCount = $global:GrokModuleState.ResponseCache.Count
    $global:GrokModuleState.ResponseCache.Clear()
    Write-Output "Cleared $cacheCount cached responses."
}

# Command: Reset-GrokSession
function Reset-GrokSession {
    <#
    .SYNOPSIS
        Resets the Grok session state
    .DESCRIPTION
        Clears all logs, cache, and resets session tracking
    .EXAMPLE
        Reset-GrokSession
    #>
    [CmdletBinding()]
    param()

    $callCount = $global:GrokModuleState.ApiCalls.Count
    $cacheCount = $global:GrokModuleState.ResponseCache.Count

    $global:GrokModuleState.ApiCalls = @()
    $global:GrokModuleState.ResponseCache = @{}
    $global:GrokModuleState.LastApiCall = $null
    $global:GrokModuleState.SessionStartTime = Get-Date

    Write-Output "Reset Grok session: Cleared $callCount API calls and $cacheCount cached responses."
}

#endregion

# Export module members
Export-ModuleMember -Function @(
    'Test-GrokConnection',
    'Get-GrokConfig',
    'Get-GrokAPILog',
    'Invoke-GrokCIAnalysis',
    'Invoke-GrokRouteOptimization',
    'Invoke-GrokMaintenancePrediction',
    'Get-GrokInsights',
    'Invoke-GrokCodeReview',
    'Invoke-GrokPerformanceAnalysis',
    'Invoke-GrokSecurityAudit',
    'Invoke-GrokArchitectureReview',
    'Invoke-GrokTestOptimization',
    'Invoke-GrokDependencyAnalysis',
    'Invoke-GrokDatabaseOptimization',
    'Invoke-GrokUIUXReview',
    'Clear-GrokCache',
    'Reset-GrokSession'
) -Alias @(
    'grok-test',
    'grok-config',
    'grok-log',
    'grok-ci',
    'grok-routes',
    'grok-maintenance',
    'grok-insights'
)
