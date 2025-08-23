# Enhanced Grok Azure SQL Integration Module
# Integrates Grok AI analysis results with Azure SQL Database storage

#requires -Version 7.0

[CmdletBinding()]
param()

# Import required modules
Import-Module SqlServer -ErrorAction SilentlyContinue

# Module variables
$Script:ModuleName = "BusBuddy-GrokAzureSQL"
$Script:ModuleVersion = "1.0.0"

# Load Grok configuration
$ConfigPath = "$PSScriptRoot\grok-config.ps1"
if (Test-Path $ConfigPath) {
    . $ConfigPath
}

function Write-GrokSQLLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Information', 'Warning', 'Error', 'Verbose', 'Debug')]
        [string]$Level = 'Information'
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [GROK-SQL] [$Level] $Message"

    switch ($Level) {
        'Information' { Write-Information $logMessage -InformationAction Continue }
        'Warning' { Write-Warning $logMessage }
        'Error' { Write-Error $logMessage }
        'Verbose' { Write-Verbose $logMessage }
        'Debug' { Write-Debug $logMessage }
    }
}

function Get-AzureSQLConnectionString {
    [CmdletBinding()]
    param()

    try {
        # Get connection string from environment or configuration
        $serverName = $env:AZURE_SQL_SERVER ?? "busbuddy-server-sm2.database.windows.net"
        $databaseName = $env:AZURE_SQL_DATABASE ?? "BusBuddyDB"
        $username = $env:AZURE_SQL_USER
        $password = $env:AZURE_SQL_PASSWORD

        if ([string]::IsNullOrEmpty($username) -or [string]::IsNullOrEmpty($password)) {
            throw "Azure SQL credentials not found. Set AZURE_SQL_USER and AZURE_SQL_PASSWORD environment variables."
        }

        $connectionString = "Server=tcp:$serverName,1433;Initial Catalog=$databaseName;User ID=$username;Password=$password;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

        Write-GrokSQLLog "Azure SQL connection string configured for database: $databaseName" -Level Verbose
        return $connectionString
    }
    catch {
        Write-GrokSQLLog "Failed to get Azure SQL connection string: $($_.Exception.Message)" -Level Error
        throw
    }
}

function Test-AzureSQLConnection {
    [CmdletBinding()]
    param()

    try {
        Write-GrokSQLLog "Testing Azure SQL Database connection..." -Level Information

        $connectionString = Get-AzureSQLConnectionString
        $query = "SELECT COUNT(*) as InsightCount FROM AIInsights WHERE CreatedDate >= DATEADD(day, -7, GETUTCDATE())"

        $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query $query -QueryTimeout 30

        Write-GrokSQLLog "✅ Azure SQL connection successful. Recent insights: $($result.InsightCount)" -Level Information

        return [PSCustomObject]@{
            Success = $true
            Database = "BusBuddyDB"
            RecentInsights = $result.InsightCount
            TestTime = Get-Date
            Message = "Connection successful"
        }
    }
    catch {
        Write-GrokSQLLog "❌ Azure SQL connection failed: $($_.Exception.Message)" -Level Error

        return [PSCustomObject]@{
            Success = $false
            Database = "BusBuddyDB"
            RecentInsights = 0
            TestTime = Get-Date
            Message = "Connection failed: $($_.Exception.Message)"
            Error = $_.Exception.Message
        }
    }
}

function Invoke-GrokMaintenancePredictionWithSQL {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$VehicleData,

        [Parameter()]
        [string]$CreatedBy = "PowerShell-Grok"
    )

    try {
        Write-GrokSQLLog "Starting maintenance prediction analysis for vehicle data..." -Level Information

        # Call Grok API for maintenance prediction
        $maintenancePrompt = @"
Analyze the following bus maintenance data and predict upcoming maintenance needs:

Vehicle Data: $($VehicleData | ConvertTo-Json -Depth 3)

Provide analysis in JSON format with:
{
    "summary": "Brief summary of findings",
    "recommendations": "Specific maintenance recommendations",
    "priority": "Critical/High/Medium/Low",
    "predictedIssues": ["issue1", "issue2"],
    "timeframe": "Estimated timeframe for maintenance",
    "confidenceScore": 0.85
}
"@

        # Import Grok assistant if not already loaded
        if (-not (Get-Command Invoke-GrokAPICall -ErrorAction SilentlyContinue)) {
            . "$PSScriptRoot\grok-busbuddy-assistant.ps1"
        }

        $insights = Invoke-GrokAPICall -Prompt $maintenancePrompt -MaxTokens 1000

        # Parse confidence score from response
        $confidenceScore = 0.75 # Default
        try {
            $jsonInsights = $insights | ConvertFrom-Json
            if ($jsonInsights.confidenceScore) {
                $confidenceScore = [decimal]$jsonInsights.confidenceScore
            }
        }
        catch {
            Write-GrokSQLLog "Could not parse confidence score from response, using default" -Level Warning
        }

        # Store in Azure SQL Database
        $connectionString = Get-AzureSQLConnectionString
        $vehicleId = $VehicleData.VehicleId ?? $VehicleData.BusId ?? 1

        $insertQuery = @"
INSERT INTO AIInsights (
    InsightType, Priority, EntityReference, VehicleId, InsightDetails,
    Summary, RecommendedActions, ConfidenceScore, Source, Status,
    CreatedBy, CreatedDate, ExpiryDate, Tags
) VALUES (
    'Maintenance',
    CASE
        WHEN @ConfidenceScore >= 0.9 THEN 'Critical'
        WHEN @ConfidenceScore >= 0.7 THEN 'High'
        WHEN @ConfidenceScore >= 0.5 THEN 'Medium'
        ELSE 'Low'
    END,
    'Vehicle_' + CAST(@VehicleId AS VARCHAR(10)),
    @VehicleId,
    @InsightDetails,
    CASE
        WHEN @InsightDetails LIKE '%"summary"%' THEN JSON_VALUE(@InsightDetails, '$.summary')
        ELSE LEFT(@InsightDetails, 500)
    END,
    CASE
        WHEN @InsightDetails LIKE '%"recommendations"%' THEN JSON_VALUE(@InsightDetails, '$.recommendations')
        ELSE 'See analysis details for recommendations'
    END,
    @ConfidenceScore,
    'Grok-4',
    'New',
    @CreatedBy,
    GETUTCDATE(),
    DATEADD(day, 30, GETUTCDATE()),
    'maintenance,prediction,vehicle'
)
SELECT SCOPE_IDENTITY() as InsightId
"@

        $parameters = @{
            VehicleId = $vehicleId
            InsightDetails = $insights
            ConfidenceScore = $confidenceScore
            CreatedBy = $CreatedBy
        }

        $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query $insertQuery -Variable $parameters
        $insightId = $result.InsightId

        Write-GrokSQLLog "✅ Maintenance insight stored in Azure SQL with ID: $insightId" -Level Information

        return [PSCustomObject]@{
            Success = $true
            InsightId = $insightId
            VehicleId = $vehicleId
            Analysis = $insights
            ConfidenceScore = $confidenceScore
            DatabaseResult = "Stored successfully"
            Timestamp = Get-Date
        }
    }
    catch {
        Write-GrokSQLLog "❌ Maintenance prediction with SQL storage failed: $($_.Exception.Message)" -Level Error

        return [PSCustomObject]@{
            Success = $false
            VehicleId = $VehicleData.VehicleId ?? $VehicleData.BusId ?? 0
            Analysis = $null
            ConfidenceScore = 0
            DatabaseResult = "Storage failed: $($_.Exception.Message)"
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

function Invoke-GrokRouteOptimizationWithSQL {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$RouteData,

        [Parameter()]
        [decimal]$EstimatedSavings,

        [Parameter()]
        [string]$CreatedBy = "PowerShell-Grok"
    )

    try {
        Write-GrokSQLLog "Starting route optimization analysis..." -Level Information

        # Call Grok API for route optimization
        $routePrompt = @"
Analyze the following bus route data and suggest optimizations:

Route Data: $($RouteData | ConvertTo-Json -Depth 3)

Provide analysis in JSON format with:
{
    "summary": "Brief summary of optimization opportunities",
    "recommendations": "Specific route optimization recommendations",
    "priority": "Critical/High/Medium/Low",
    "estimatedSavings": 250.00,
    "fuelSavings": "Estimated fuel cost savings",
    "timeSavings": "Estimated time savings",
    "confidenceScore": 0.85
}
"@

        # Import Grok assistant if not already loaded
        if (-not (Get-Command Invoke-GrokAPICall -ErrorAction SilentlyContinue)) {
            . "$PSScriptRoot\grok-busbuddy-assistant.ps1"
        }

        $insights = Invoke-GrokAPICall -Prompt $routePrompt -MaxTokens 1000

        # Parse response for savings and confidence
        $confidenceScore = 0.75
        $parsedSavings = $EstimatedSavings

        try {
            $jsonInsights = $insights | ConvertFrom-Json
            if ($jsonInsights.confidenceScore) {
                $confidenceScore = [decimal]$jsonInsights.confidenceScore
            }
            if ($jsonInsights.estimatedSavings -and -not $EstimatedSavings) {
                $parsedSavings = [decimal]$jsonInsights.estimatedSavings
            }
        }
        catch {
            Write-GrokSQLLog "Could not parse response values, using defaults" -Level Warning
        }

        # Store in Azure SQL Database
        $connectionString = Get-AzureSQLConnectionString
        $routeId = $RouteData.RouteId ?? 1

        $insertQuery = @"
INSERT INTO AIInsights (
    InsightType, Priority, EntityReference, RouteId, InsightDetails,
    Summary, RecommendedActions, ConfidenceScore, Source, Status,
    CreatedBy, CreatedDate, EstimatedSavings, ExpiryDate, Tags
) VALUES (
    'Route',
    CASE
        WHEN @ConfidenceScore >= 0.9 THEN 'Critical'
        WHEN @ConfidenceScore >= 0.7 THEN 'High'
        WHEN @ConfidenceScore >= 0.5 THEN 'Medium'
        ELSE 'Low'
    END,
    'Route_' + CAST(@RouteId AS VARCHAR(10)),
    @RouteId,
    @InsightDetails,
    CASE
        WHEN @InsightDetails LIKE '%"summary"%' THEN JSON_VALUE(@InsightDetails, '$.summary')
        ELSE 'Route optimization analysis completed'
    END,
    CASE
        WHEN @InsightDetails LIKE '%"recommendations"%' THEN JSON_VALUE(@InsightDetails, '$.recommendations')
        ELSE 'See analysis details for recommendations'
    END,
    @ConfidenceScore,
    'Grok-4',
    'New',
    @CreatedBy,
    GETUTCDATE(),
    @EstimatedSavings,
    DATEADD(day, 14, GETUTCDATE()),
    'route,optimization,efficiency'
)
SELECT SCOPE_IDENTITY() as InsightId
"@

        $parameters = @{
            RouteId = $routeId
            InsightDetails = $insights
            ConfidenceScore = $confidenceScore
            EstimatedSavings = $parsedSavings
            CreatedBy = $CreatedBy
        }

        $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query $insertQuery -Variable $parameters
        $insightId = $result.InsightId

        Write-GrokSQLLog "✅ Route optimization insight stored in Azure SQL with ID: $insightId" -Level Information

        return [PSCustomObject]@{
            Success = $true
            InsightId = $insightId
            RouteId = $routeId
            Analysis = $insights
            ConfidenceScore = $confidenceScore
            EstimatedSavings = $parsedSavings
            DatabaseResult = "Stored successfully"
            Timestamp = Get-Date
        }
    }
    catch {
        Write-GrokSQLLog "❌ Route optimization with SQL storage failed: $($_.Exception.Message)" -Level Error

        return [PSCustomObject]@{
            Success = $false
            RouteId = $RouteData.RouteId ?? 0
            Analysis = $null
            ConfidenceScore = 0
            EstimatedSavings = 0
            DatabaseResult = "Storage failed: $($_.Exception.Message)"
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

function Invoke-GrokUIAnalysisWithSQL {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$XAMLContent,

        [Parameter()]
        [string]$CreatedBy = "PowerShell-Grok"
    )

    try {
        Write-GrokSQLLog "Starting Syncfusion UI analysis..." -Level Information

        # Sanitize XAML content for API (remove sensitive data, limit size)
        $sanitizedXAML = $XAMLContent -replace 'x:Name="[^"]*"', 'x:Name="[sanitized]"'
        if ($sanitizedXAML.Length -gt 2000) {
            $sanitizedXAML = $sanitizedXAML.Substring(0, 2000) + "... [truncated]"
        }

        $uiPrompt = @"
Analyze this Syncfusion WPF XAML for performance optimizations:

XAML Content: $sanitizedXAML

Focus on Syncfusion-specific optimizations. Provide analysis in JSON format:
{
    "summary": "Brief summary of UI optimization opportunities",
    "recommendations": "Specific Syncfusion control optimizations",
    "priority": "Critical/High/Medium/Low",
    "performanceImpact": "High/Medium/Low",
    "syncfusionOptimizations": ["optimization1", "optimization2"],
    "confidenceScore": 0.85
}
"@

        # Import Grok assistant if not already loaded
        if (-not (Get-Command Invoke-GrokAPICall -ErrorAction SilentlyContinue)) {
            . "$PSScriptRoot\grok-busbuddy-assistant.ps1"
        }

        $insights = Invoke-GrokAPICall -Prompt $uiPrompt -MaxTokens 1000

        # Parse confidence score
        $confidenceScore = 0.75
        try {
            $jsonInsights = $insights | ConvertFrom-Json
            if ($jsonInsights.confidenceScore) {
                $confidenceScore = [decimal]$jsonInsights.confidenceScore
            }
        }
        catch {
            Write-GrokSQLLog "Could not parse confidence score, using default" -Level Warning
        }

        # Store in Azure SQL Database
        $connectionString = Get-AzureSQLConnectionString

        $insertQuery = @"
INSERT INTO AIInsights (
    InsightType, Priority, EntityReference, InsightDetails,
    Summary, RecommendedActions, ConfidenceScore, Source, Status,
    CreatedBy, CreatedDate, ExpiryDate, Tags
) VALUES (
    'UI',
    CASE
        WHEN @ConfidenceScore >= 0.9 THEN 'Critical'
        WHEN @ConfidenceScore >= 0.7 THEN 'High'
        WHEN @ConfidenceScore >= 0.5 THEN 'Medium'
        ELSE 'Low'
    END,
    'Syncfusion_UI',
    @InsightDetails,
    CASE
        WHEN @InsightDetails LIKE '%"summary"%' THEN JSON_VALUE(@InsightDetails, '$.summary')
        ELSE 'UI optimization analysis completed'
    END,
    CASE
        WHEN @InsightDetails LIKE '%"recommendations"%' THEN JSON_VALUE(@InsightDetails, '$.recommendations')
        ELSE 'See analysis details for recommendations'
    END,
    @ConfidenceScore,
    'Grok-4',
    'New',
    @CreatedBy,
    GETUTCDATE(),
    DATEADD(day, 7, GETUTCDATE()),
    'ui,syncfusion,xaml,performance'
)
SELECT SCOPE_IDENTITY() as InsightId
"@

        $parameters = @{
            InsightDetails = $insights
            ConfidenceScore = $confidenceScore
            CreatedBy = $CreatedBy
        }

        $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query $insertQuery -Variable $parameters
        $insightId = $result.InsightId

        Write-GrokSQLLog "✅ UI optimization insight stored in Azure SQL with ID: $insightId" -Level Information

        return [PSCustomObject]@{
            Success = $true
            InsightId = $insightId
            Analysis = $insights
            ConfidenceScore = $confidenceScore
            XAMLLength = $XAMLContent.Length
            DatabaseResult = "Stored successfully"
            Timestamp = Get-Date
        }
    }
    catch {
        Write-GrokSQLLog "❌ UI analysis with SQL storage failed: $($_.Exception.Message)" -Level Error

        return [PSCustomObject]@{
            Success = $false
            Analysis = $null
            ConfidenceScore = 0
            XAMLLength = $XAMLContent.Length
            DatabaseResult = "Storage failed: $($_.Exception.Message)"
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

function Get-ActionableInsights {
    [CmdletBinding()]
    param(
        [Parameter()]
        [int]$MaxResults = 10,

        [Parameter()]
        [ValidateSet('All', 'Maintenance', 'Route', 'UI', 'CI')]
        [string]$InsightType = 'All'
    )

    try {
        Write-GrokSQLLog "Retrieving actionable insights from Azure SQL..." -Level Information

        $connectionString = Get-AzureSQLConnectionString

        $query = @"
SELECT TOP (@MaxResults)
    InsightId, InsightType, Priority, EntityReference,
    Summary, RecommendedActions, ConfidenceScore,
    EstimatedSavings, CreatedDate, Source, Tags
FROM AIInsights
WHERE Status = 'New'
    AND Priority IN ('Critical', 'High')
    AND (ExpiryDate IS NULL OR ExpiryDate > GETUTCDATE())
    AND (@InsightType = 'All' OR InsightType = @InsightType)
ORDER BY
    CASE Priority WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 ELSE 3 END,
    ConfidenceScore DESC,
    CreatedDate DESC
"@

        $parameters = @{
            MaxResults = $MaxResults
            InsightType = $InsightType
        }

        $insights = Invoke-Sqlcmd -ConnectionString $connectionString -Query $query -Variable $parameters

        Write-GrokSQLLog "✅ Retrieved $($insights.Count) actionable insights" -Level Information

        return $insights | ForEach-Object {
            [PSCustomObject]@{
                InsightId = $_.InsightId
                Type = $_.InsightType
                Priority = $_.Priority
                Entity = $_.EntityReference
                Summary = $_.Summary
                Recommendations = $_.RecommendedActions
                Confidence = $_.ConfidenceScore
                EstimatedSavings = $_.EstimatedSavings
                Created = $_.CreatedDate
                Source = $_.Source
                Tags = $_.Tags
            }
        }
    }
    catch {
        Write-GrokSQLLog "❌ Failed to retrieve actionable insights: $($_.Exception.Message)" -Level Error
        throw
    }
}

function Update-InsightStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$InsightId,

        [Parameter(Mandatory)]
        [ValidateSet('New', 'Reviewed', 'InProgress', 'Resolved', 'Dismissed')]
        [string]$NewStatus,

        [Parameter()]
        [string]$UpdatedBy = $env:USERNAME
    )

    try {
        Write-GrokSQLLog "Updating insight $InsightId status to $NewStatus..." -Level Information

        $connectionString = Get-AzureSQLConnectionString

        $query = @"
UPDATE AIInsights
SET Status = @NewStatus,
    UpdatedBy = @UpdatedBy,
    UpdatedDate = GETUTCDATE()
WHERE InsightId = @InsightId

SELECT @@ROWCOUNT as RowsAffected
"@

        $parameters = @{
            InsightId = $InsightId
            NewStatus = $NewStatus
            UpdatedBy = $UpdatedBy
        }

        $result = Invoke-Sqlcmd -ConnectionString $connectionString -Query $query -Variable $parameters

        if ($result.RowsAffected -gt 0) {
            Write-GrokSQLLog "✅ Insight $InsightId status updated to $NewStatus" -Level Information
            return $true
        }
        else {
            Write-GrokSQLLog "⚠️ Insight $InsightId not found" -Level Warning
            return $false
        }
    }
    catch {
        Write-GrokSQLLog "❌ Failed to update insight status: $($_.Exception.Message)" -Level Error
        throw
    }
}

# Create aliases for easier use
New-Alias -Name "grok-sql-test" -Value "Test-AzureSQLConnection" -Force
New-Alias -Name "grok-maintenance-sql" -Value "Invoke-GrokMaintenancePredictionWithSQL" -Force
New-Alias -Name "grok-routes-sql" -Value "Invoke-GrokRouteOptimizationWithSQL" -Force
New-Alias -Name "grok-ui-sql" -Value "Invoke-GrokUIAnalysisWithSQL" -Force
New-Alias -Name "grok-insights" -Value "Get-ActionableInsights" -Force
New-Alias -Name "grok-update" -Value "Update-InsightStatus" -Force

# Export module members
Export-ModuleMember -Function @(
    'Test-AzureSQLConnection',
    'Invoke-GrokMaintenancePredictionWithSQL',
    'Invoke-GrokRouteOptimizationWithSQL',
    'Invoke-GrokUIAnalysisWithSQL',
    'Get-ActionableInsights',
    'Update-InsightStatus'
) -Alias @(
    'grok-sql-test',
    'grok-maintenance-sql',
    'grok-routes-sql',
    'grok-ui-sql',
    'grok-insights',
    'grok-update'
)

Write-GrokSQLLog "BusBuddy Grok Azure SQL integration module loaded (v$Script:ModuleVersion)" -Level Information
