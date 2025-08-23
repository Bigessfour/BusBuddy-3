#Requires -Version 7.5

<#
.SYNOPSIS
BusBuddy-Development PowerShell Module - Development Workflow and VS Code Integration

.DESCRIPTION
Comprehensive development workflow module for BusBuddy-3 .NET 9.0 WPF application.
Provides essential development commands, health checks, database testing, dependency management,
and VS Code integration following Microsoft PowerShell module best practices.

.NOTES
File Name      : BusBuddy-Development.psm1
Version        : 3.0.0
Author         : BusBuddy Development Team
Created        : August 22, 2025
Requires       : PowerShell 7.5+
Module Type    : Script Module
Dependencies   : BusBuddy-HardwareDetection
Compatibility  : Windows, VS Code PowerShell Extension

.LINK
https://learn.microsoft.com/en-us/powershell/scripting/developer/module/writing-a-powershell-script-module

.EXAMPLE
Import-Module BusBuddy-Development
bb-run

.EXAMPLE
Get-BusBuddyProjectInfo | Format-Table
#>

#region Module Initialization
# Microsoft recommended module-scoped variables
$script:ModuleName = 'BusBuddy-Development'
$script:ModuleVersion = '3.0.0'
$script:SolutionFile = 'BusBuddy.sln'
$script:DefaultConfiguration = 'Debug'

# Initialize module state with error handling
try {
    Import-Module BusBuddy-HardwareDetection -ErrorAction SilentlyContinue
} catch {
    Write-Warning "BusBuddy-HardwareDetection module not available - some features may be limited"
}
#endregion Module Initialization

#region Core Development Functions

function Start-BusBuddyDevSession {
    <#
    .SYNOPSIS
    Starts a complete BusBuddy development session with application launch

    .DESCRIPTION
    Microsoft recommended development session initialization pattern.
    Builds the solution, checks health, and optionally starts the WPF application.

    .PARAMETER Configuration
    Build configuration (Debug or Release)

    .PARAMETER NoHealthCheck
    Skip the health check before starting

    .PARAMETER Background
    Start the application in background mode

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Session start result with build and health status

    .EXAMPLE
    Start-BusBuddyDevSession

    .EXAMPLE
    Start-BusBuddyDevSession -Configuration Release -NoHealthCheck

    .NOTES
    Based on Microsoft .NET development workflow patterns
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = $script:DefaultConfiguration,

        [Parameter()]
        [switch]$NoHealthCheck,

        [Parameter()]
        [switch]$Background
    )

    if ($PSCmdlet.ShouldProcess("BusBuddy Development Session", "Start")) {
        $sessionResult = [PSCustomObject]@{
            StartTime = Get-Date
            Configuration = $Configuration
            BuildSuccess = $false
            HealthCheckPassed = $false
            ApplicationStarted = $false
            ProcessId = $null
            WorkspacePath = $env:BUSBUDDY_ROOT ?? (Get-Location).Path
            Errors = @()
        }

        try {
            Write-Information "🚀 Starting BusBuddy-3 development session..." -InformationAction Continue

            # Step 1: Build solution
            Write-Information "📦 Building solution in $Configuration mode..." -InformationAction Continue
            $buildResult = Invoke-BusBuddyBuild -Configuration $Configuration
            $sessionResult.BuildSuccess = $buildResult.Success

            if (-not $buildResult.Success) {
                $sessionResult.Errors += "Build failed with exit code: $($buildResult.ExitCode)"
                Write-Warning "❌ Build failed - cannot start development session"
                return $sessionResult
            }

            # Step 2: Health check (unless skipped)
            if (-not $NoHealthCheck) {
                Write-Information "🔍 Running health checks..." -InformationAction Continue
                $healthResult = Test-BusBuddySystemHealth
                $sessionResult.HealthCheckPassed = $healthResult.OverallHealth -eq 'Healthy'

                if (-not $sessionResult.HealthCheckPassed) {
                    $sessionResult.Errors += "Health check failed: $($healthResult.Issues -join '; ')"
                    Write-Warning "⚠️ Health check failed - continuing with caution"
                }
            } else {
                $sessionResult.HealthCheckPassed = $true
            }

            # Step 3: Start application
            Write-Information "🎯 Starting BusBuddy WPF application..." -InformationAction Continue
            $appPath = Join-Path $sessionResult.WorkspacePath 'BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe'

            if (Test-Path $appPath) {
                $processArgs = @{
                    FilePath = $appPath
                    WorkingDirectory = Split-Path $appPath -Parent
                    PassThru = $true
                }

                if ($Background) {
                    $processArgs.WindowStyle = 'Hidden'
                }

                $process = Start-Process @processArgs
                $sessionResult.ApplicationStarted = $true
                $sessionResult.ProcessId = $process.Id

                Write-Information "✅ BusBuddy application started (PID: $($process.Id))" -InformationAction Continue
            } else {
                $sessionResult.Errors += "Application executable not found at: $appPath"
                Write-Warning "❌ Could not find application executable"
            }

            # Session summary
            $duration = ((Get-Date) - $sessionResult.StartTime).TotalSeconds
            Write-Information "🎉 Development session started in $([math]::Round($duration, 1))s" -InformationAction Continue

            return $sessionResult
        }
        catch {
            $sessionResult.Errors += $_.Exception.Message
            Write-Error "Failed to start development session: $($_.Exception.Message)"
            return $sessionResult
        }
    }
}

function Test-BusBuddySystemHealth {
    <#
    .SYNOPSIS
    Performs comprehensive system health check for BusBuddy development environment

    .DESCRIPTION
    Microsoft recommended health check pattern covering hardware, software,
    configuration, and development environment status.

    .PARAMETER IncludePerformance
    Include performance benchmarks in health check

    .PARAMETER IncludeDatabase
    Include database connectivity checks

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Comprehensive health check result

    .EXAMPLE
    Test-BusBuddySystemHealth

    .EXAMPLE
    Test-BusBuddySystemHealth -IncludePerformance -IncludeDatabase

    .NOTES
    Based on Microsoft system diagnostics best practices
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$IncludePerformance,

        [Parameter()]
        [switch]$IncludeDatabase
    )

    $healthResult = [PSCustomObject]@{
        Timestamp = Get-Date
        OverallHealth = 'Unknown'
        SystemInfo = $null
        EnvironmentCheck = @{}
        SoftwareCheck = @{}
        ConfigurationCheck = @{}
        PerformanceMetrics = @{}
        DatabaseCheck = @{}
        Issues = @()
        Recommendations = @()
        HealthScore = 0
    }

    try {
        Write-Information "🔍 Running BusBuddy system health diagnostics..." -InformationAction Continue

        # Hardware and system information
        if (Get-Command Get-BusBuddyHardwareInfo -ErrorAction SilentlyContinue) {
            $healthResult.SystemInfo = Get-BusBuddyHardwareInfo
        } else {
            $healthResult.Issues += "BusBuddy-HardwareDetection module not available"
        }

        # Environment variables check
        $envVars = @(
            'BUSBUDDY_ROOT', 'DOTNET_VERSION', 'BUSBUDDY_LOGICAL_CORES',
            'BUSBUDDY_MEMORY_GB', 'BUSBUDDY_MAX_PARALLEL_JOBS'
        )

        foreach ($var in $envVars) {
            $value = [Environment]::GetEnvironmentVariable($var)
            $healthResult.EnvironmentCheck[$var] = @{
                Configured = $null -ne $value
                Value = $value
            }

            if (-not $value) {
                $healthResult.Issues += "Environment variable $var not configured"
            }
        }

        # Software requirements check
        $softwareChecks = @{
            'PowerShell' = @{
                Command = { $PSVersionTable.PSVersion }
                MinVersion = '7.5.0'
                Type = 'Version'
            }
            'DotNet' = @{
                Command = { & dotnet --version 2>$null }
                MinVersion = '9.0.0'
                Type = 'Version'
            }
            'Git' = @{
                Command = { & git --version 2>$null }
                Type = 'Availability'
            }
        }

        foreach ($check in $softwareChecks.GetEnumerator()) {
            try {
                $result = & $check.Value.Command
                $healthResult.SoftwareCheck[$check.Key] = @{
                    Available = $true
                    Version = $result
                    Meets_Requirements = $true
                }

                if ($check.Value.Type -eq 'Version' -and $check.Value.MinVersion) {
                    $currentVersion = [version]($result -replace '[^\d\.].*$')
                    $minVersion = [version]$check.Value.MinVersion
                    $meets = $currentVersion -ge $minVersion
                    $healthResult.SoftwareCheck[$check.Key].Meets_Requirements = $meets

                    if (-not $meets) {
                        $healthResult.Issues += "$($check.Key) version $currentVersion below minimum $minVersion"
                    }
                }
            }
            catch {
                $healthResult.SoftwareCheck[$check.Key] = @{
                    Available = $false
                    Error = $_.Exception.Message
                }
                $healthResult.Issues += "$($check.Key) not available or accessible"
            }
        }

        # Configuration files check
        $configFiles = @(
            @{ Path = 'BusBuddy.sln'; Required = $true; Name = 'Solution File' }
            @{ Path = 'appsettings.json'; Required = $true; Name = 'App Settings' }
            @{ Path = 'global.json'; Required = $false; Name = 'Global .NET Config' }
            @{ Path = '.vscode\tasks.json'; Required = $false; Name = 'VS Code Tasks' }
        )

        foreach ($config in $configFiles) {
            $fullPath = Join-Path ($env:BUSBUDDY_ROOT ?? '.') $config.Path
            $exists = Test-Path $fullPath

            $healthResult.ConfigurationCheck[$config.Name] = @{
                Path = $fullPath
                Exists = $exists
                Required = $config.Required
            }

            if ($config.Required -and -not $exists) {
                $healthResult.Issues += "Required configuration file missing: $($config.Path)"
            }
        }

        # Performance metrics (if requested)
        if ($IncludePerformance) {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

            # Simple build performance test
            try {
                $buildTest = Invoke-BusBuddyBuild -Configuration Debug -NoRestore
                $stopwatch.Stop()

                $healthResult.PerformanceMetrics.BuildTime = $buildTest.Duration
                $healthResult.PerformanceMetrics.BuildSuccess = $buildTest.Success

                if ($buildTest.Duration.TotalMinutes -gt 2) {
                    $healthResult.Recommendations += "Build time ($($buildTest.Duration.TotalSeconds.ToString('F1'))s) is high - consider optimizing"
                }
            }
            catch {
                $healthResult.PerformanceMetrics.BuildError = $_.Exception.Message
            }
        }

        # Database connectivity (if requested)
        if ($IncludeDatabase) {
            if (Get-Command Test-BusBuddyDatabaseConnection -ErrorAction SilentlyContinue) {
                try {
                    $dbResult = Test-BusBuddyDatabaseConnection
                    $healthResult.DatabaseCheck = $dbResult

                    if (-not $dbResult.Connected) {
                        $healthResult.Issues += "Database connectivity failed"
                    }
                }
                catch {
                    $healthResult.DatabaseCheck = @{ Error = $_.Exception.Message }
                    $healthResult.Issues += "Database check failed: $($_.Exception.Message)"
                }
            } else {
                $healthResult.DatabaseCheck = @{ Available = $false }
                $healthResult.Issues += "Database testing function not available"
            }
        }

        # Calculate health score and overall status
        $totalChecks = $healthResult.EnvironmentCheck.Count + $healthResult.SoftwareCheck.Count + $healthResult.ConfigurationCheck.Count
        $passedChecks = 0

        $passedChecks += ($healthResult.EnvironmentCheck.Values | Where-Object Configured).Count
        $passedChecks += ($healthResult.SoftwareCheck.Values | Where-Object Available).Count
        $passedChecks += ($healthResult.ConfigurationCheck.Values | Where-Object { -not $_.Required -or $_.Exists }).Count

        $healthResult.HealthScore = [math]::Round(($passedChecks / $totalChecks) * 100, 1)

        $healthResult.OverallHealth = switch ($healthResult.HealthScore) {
            { $_ -ge 90 } { 'Healthy' }
            { $_ -ge 70 } { 'Warning' }
            { $_ -ge 50 } { 'Degraded' }
            default { 'Critical' }
        }

        # Display results
        $statusIcon = switch ($healthResult.OverallHealth) {
            'Healthy' { '✅' }
            'Warning' { '⚠️' }
            'Degraded' { '🔶' }
            'Critical' { '❌' }
        }

        Write-Information "$statusIcon BusBuddy health: $($healthResult.OverallHealth) ($($healthResult.HealthScore)% score)" -InformationAction Continue

        if ($healthResult.Issues.Count -gt 0) {
            Write-Information "Issues found: $($healthResult.Issues.Count)" -InformationAction Continue
            $healthResult.Issues | ForEach-Object { Write-Verbose "  - $_" }
        }

        return $healthResult
    }
    catch {
        $healthResult.OverallHealth = 'Error'
        $healthResult.Issues += $_.Exception.Message
        Write-Error "Health check failed: $($_.Exception.Message)"
        return $healthResult
    }
}

function Test-BusBuddyDatabaseConnection {
    <#
    .SYNOPSIS
    Tests database connectivity for BusBuddy application

    .DESCRIPTION
    Microsoft recommended database connectivity testing with support for
    both local SQL Server and Azure SQL Database connections.

    .PARAMETER ConnectionType
    Type of database connection to test

    .PARAMETER Timeout
    Connection timeout in seconds

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Database connection test result

    .EXAMPLE
    Test-BusBuddyDatabaseConnection

    .EXAMPLE
    Test-BusBuddyDatabaseConnection -ConnectionType Azure -Timeout 30

    .NOTES
    Requires appropriate database permissions and network connectivity
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [ValidateSet('Local', 'Azure', 'Auto')]
        [string]$ConnectionType = 'Auto',

        [Parameter()]
        [ValidateRange(5, 120)]
        [int]$Timeout = 15
    )

    $dbResult = [PSCustomObject]@{
        TestTime = Get-Date
        ConnectionType = $ConnectionType
        Connected = $false
        ResponseTime = [TimeSpan]::Zero
        ServerVersion = $null
        DatabaseName = $null
        Error = $null
        ConnectionString = $null
    }

    try {
        Write-Information "🗄️ Testing database connectivity..." -InformationAction Continue

        # Determine connection string based on type
        $connectionString = switch ($ConnectionType) {
            'Local' {
                'Server=(localdb)\MSSQLLocalDB;Database=BusBuddy;Trusted_Connection=true;TrustServerCertificate=true;'
            }
            'Azure' {
                if ($env:BUSBUDDY_AZURE_SQL_CONNECTION) {
                    $env:BUSBUDDY_AZURE_SQL_CONNECTION
                } else {
                    throw "Azure SQL connection string not configured in BUSBUDDY_AZURE_SQL_CONNECTION"
                }
            }
            'Auto' {
                # Try Azure first, then Local
                if ($env:BUSBUDDY_AZURE_SQL_CONNECTION) {
                    $env:BUSBUDDY_AZURE_SQL_CONNECTION
                } else {
                    'Server=(localdb)\MSSQLLocalDB;Database=BusBuddy;Trusted_Connection=true;TrustServerCertificate=true;'
                }
            }
        }

        $dbResult.ConnectionString = $connectionString -replace 'Password=[^;]*', 'Password=***'

        # Test connection with timeout
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        Add-Type -AssemblyName System.Data.SqlClient -ErrorAction SilentlyContinue
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.ConnectionTimeout = $Timeout

        $connection.Open()

        # Get server information
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT @@VERSION as ServerVersion, DB_NAME() as DatabaseName"
        $reader = $command.ExecuteReader()

        if ($reader.Read()) {
            $dbResult.ServerVersion = $reader['ServerVersion']
            $dbResult.DatabaseName = $reader['DatabaseName']
        }

        $reader.Close()
        $connection.Close()

        $stopwatch.Stop()
        $dbResult.ResponseTime = $stopwatch.Elapsed
        $dbResult.Connected = $true

        Write-Information "✅ Database connected successfully ($($dbResult.ResponseTime.TotalMilliseconds.ToString('F0'))ms)" -InformationAction Continue
        Write-Information "   Database: $($dbResult.DatabaseName)" -InformationAction Continue

        return $dbResult
    }
    catch {
        $stopwatch?.Stop()
        $dbResult.Error = $_.Exception.Message
        $dbResult.ResponseTime = $stopwatch?.Elapsed ?? [TimeSpan]::Zero

        Write-Warning "❌ Database connection failed: $($_.Exception.Message)"
        return $dbResult
    }
    finally {
        try { $connection?.Dispose() } catch { }
    }
}

function Invoke-BusBuddyDependencyCheck {
    <#
    .SYNOPSIS
    Performs comprehensive dependency analysis for BusBuddy project

    .DESCRIPTION
    Microsoft recommended dependency management pattern that analyzes NuGet packages,
    .NET framework versions, and external dependencies for security and compatibility.

    .PARAMETER OutputFormat
    Output format for dependency report

    .PARAMETER IncludeVulnerabilities
    Include vulnerability scanning in dependency check

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Comprehensive dependency analysis result

    .EXAMPLE
    Invoke-BusBuddyDependencyCheck

    .EXAMPLE
    Invoke-BusBuddyDependencyCheck -OutputFormat Json -IncludeVulnerabilities

    .NOTES
    Based on Microsoft .NET dependency management best practices
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [ValidateSet('Table', 'Json', 'Detailed')]
        [string]$OutputFormat = 'Table',

        [Parameter()]
        [switch]$IncludeVulnerabilities
    )

    $depResult = [PSCustomObject]@{
        CheckTime = Get-Date
        ProjectsAnalyzed = @()
        TotalPackages = 0
        OutdatedPackages = @()
        VulnerablePackages = @()
        DuplicateVersions = @()
        FrameworkTargets = @()
        Issues = @()
        Recommendations = @()
        OverallStatus = 'Unknown'
    }

    try {
        Write-Information "📦 Analyzing BusBuddy project dependencies..." -InformationAction Continue

        # Find all project files
        $projectFiles = Get-ChildItem -Path ($env:BUSBUDDY_ROOT ?? '.') -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue

        foreach ($project in $projectFiles) {
            try {
                Write-Verbose "Analyzing project: $($project.Name)"
                [xml]$projectXml = Get-Content $project.FullName

                $projectInfo = @{
                    Name = $project.BaseName
                    Path = $project.FullName
                    TargetFramework = $projectXml.Project.PropertyGroup.TargetFramework
                    Packages = @()
                }

                # Extract package references
                $packageRefs = $projectXml.Project.ItemGroup.PackageReference
                if ($packageRefs) {
                    foreach ($pkg in $packageRefs) {
                        $projectInfo.Packages += @{
                            Name = $pkg.Include
                            Version = $pkg.Version
                        }
                    }
                }

                $depResult.ProjectsAnalyzed += $projectInfo
                $depResult.TotalPackages += $projectInfo.Packages.Count

                # Track framework targets
                if ($projectInfo.TargetFramework -and $projectInfo.TargetFramework -notin $depResult.FrameworkTargets) {
                    $depResult.FrameworkTargets += $projectInfo.TargetFramework
                }
            }
            catch {
                $depResult.Issues += "Failed to analyze project $($project.Name): $($_.Exception.Message)"
            }
        }

        # Check for duplicate package versions across projects
        $allPackages = $depResult.ProjectsAnalyzed | ForEach-Object { $_.Packages } | ForEach-Object { $_ }
        $packageGroups = $allPackages | Group-Object Name

        foreach ($group in $packageGroups) {
            $versions = $group.Group.Version | Sort-Object -Unique
            if ($versions.Count -gt 1) {
                $depResult.DuplicateVersions += @{
                    PackageName = $group.Name
                    Versions = $versions
                    ProjectCount = $group.Count
                }
                $depResult.Issues += "Package $($group.Name) has multiple versions: $($versions -join ', ')"
            }
        }

        # Run dotnet list package --outdated (if available)
        try {
            $outdatedOutput = & dotnet list package --outdated --format json 2>$null
            if ($LASTEXITCODE -eq 0 -and $outdatedOutput) {
                $outdatedData = $outdatedOutput | ConvertFrom-Json
                # Process outdated package data (simplified)
                $depResult.OutdatedPackages = @($outdatedData.projects | ForEach-Object { $_.frameworks | ForEach-Object { $_.topLevelPackages } } | Where-Object { $_ })
            }
        }
        catch {
            Write-Verbose "Could not check for outdated packages: $($_.Exception.Message)"
        }

        # Vulnerability check (if requested and available)
        if ($IncludeVulnerabilities) {
            try {
                Write-Information "🔍 Checking for package vulnerabilities..." -InformationAction Continue
                $vulnOutput = & dotnet list package --vulnerable --format json 2>$null
                if ($LASTEXITCODE -eq 0 -and $vulnOutput) {
                    $vulnData = $vulnOutput | ConvertFrom-Json
                    $depResult.VulnerablePackages = @($vulnData.projects | ForEach-Object { $_.frameworks | ForEach-Object { $_.vulnerablePackages } } | Where-Object { $_ })

                    if ($depResult.VulnerablePackages.Count -gt 0) {
                        $depResult.Issues += "$($depResult.VulnerablePackages.Count) vulnerable packages found"
                    }
                }
            }
            catch {
                Write-Verbose "Could not check for vulnerable packages: $($_.Exception.Message)"
            }
        }

        # Generate recommendations
        if ($depResult.DuplicateVersions.Count -gt 0) {
            $depResult.Recommendations += "Consolidate package versions using Directory.Build.props or Central Package Management"
        }

        if ($depResult.OutdatedPackages.Count -gt 0) {
            $depResult.Recommendations += "Update outdated packages using 'dotnet add package' or Package Manager UI"
        }

        if ($depResult.VulnerablePackages.Count -gt 0) {
            $depResult.Recommendations += "Address security vulnerabilities immediately by updating affected packages"
        }

        # Determine overall status
        $depResult.OverallStatus = if ($depResult.VulnerablePackages.Count -gt 0) {
            'Critical'
        } elseif ($depResult.Issues.Count -gt 0) {
            'Warning'
        } else {
            'Healthy'
        }

        # Display results based on format
        switch ($OutputFormat) {
            'Table' {
                Write-Information "📊 Dependency Analysis Summary:" -InformationAction Continue
                Write-Information "   Projects: $($depResult.ProjectsAnalyzed.Count)" -InformationAction Continue
                Write-Information "   Total Packages: $($depResult.TotalPackages)" -InformationAction Continue
                Write-Information "   Framework Targets: $($depResult.FrameworkTargets -join ', ')" -InformationAction Continue
                Write-Information "   Issues: $($depResult.Issues.Count)" -InformationAction Continue
                Write-Information "   Status: $($depResult.OverallStatus)" -InformationAction Continue
            }
            'Json' {
                return $depResult | ConvertTo-Json -Depth 5
            }
            'Detailed' {
                return $depResult
            }
        }

        return $depResult
    }
    catch {
        $depResult.Issues += $_.Exception.Message
        $depResult.OverallStatus = 'Error'
        Write-Error "Dependency check failed: $($_.Exception.Message)"
        return $depResult
    }
}

function Get-BusBuddyProjectInfo {
    <#
    .SYNOPSIS
    Gets comprehensive information about the BusBuddy project structure and status

    .DESCRIPTION
    Microsoft recommended project analysis pattern that provides detailed information
    about the project structure, build status, dependencies, and development environment.

    .PARAMETER IncludeMetrics
    Include code metrics and statistics

    .PARAMETER IncludeGit
    Include Git repository information

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] Comprehensive project information

    .EXAMPLE
    Get-BusBuddyProjectInfo

    .EXAMPLE
    Get-BusBuddyProjectInfo -IncludeMetrics -IncludeGit | Format-Table

    .NOTES
    Based on Microsoft project analysis best practices
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$IncludeMetrics,

        [Parameter()]
        [switch]$IncludeGit
    )

    $projectInfo = [PSCustomObject]@{
        ProjectName = 'BusBuddy-3'
        RootPath = $env:BUSBUDDY_ROOT ?? (Get-Location).Path
        SolutionFile = $script:SolutionFile
        Version = $env:BUSBUDDY_VERSION ?? '3.0.0'
        LastAnalyzed = Get-Date
        Projects = @()
        BuildInfo = @{}
        Dependencies = @{}
        GitInfo = @{}
        Metrics = @{}
        Environment = @{}
    }

    try {
        # Basic project structure analysis
        if (Test-Path (Join-Path $projectInfo.RootPath $script:SolutionFile)) {
            $projectFiles = Get-ChildItem -Path $projectInfo.RootPath -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue

            foreach ($project in $projectFiles) {
                try {
                    [xml]$projectXml = Get-Content $project.FullName

                    $projectInfo.Projects += @{
                        Name = $project.BaseName
                        Path = $project.FullName
                        TargetFramework = $projectXml.Project.PropertyGroup.TargetFramework
                        OutputType = $projectXml.Project.PropertyGroup.OutputType
                        LastModified = $project.LastWriteTime
                    }
                }
                catch {
                    Write-Verbose "Could not analyze project: $($project.Name)"
                }
            }
        }

        # Build information
        $projectInfo.BuildInfo = @{
            Configuration = $env:BUILD_CONFIGURATION ?? $script:DefaultConfiguration
            Platform = $env:PROCESSOR_ARCHITECTURE ?? 'Unknown'
            DotNetVersion = $env:DOTNET_VERSION ?? 'Unknown'
            LastBuildTime = $null
        }

        # Get last build artifact information
        $outputDirs = Get-ChildItem -Path $projectInfo.RootPath -Recurse -Directory -Name 'bin' -ErrorAction SilentlyContinue
        if ($outputDirs) {
            $latestArtifact = Get-ChildItem -Path ($outputDirs | ForEach-Object { Join-Path $projectInfo.RootPath $_ }) -Recurse -File -ErrorAction SilentlyContinue |
                             Sort-Object LastWriteTime -Descending | Select-Object -First 1

            if ($latestArtifact) {
                $projectInfo.BuildInfo.LastBuildTime = $latestArtifact.LastWriteTime
            }
        }

        # Environment information
        $projectInfo.Environment = @{
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            OSVersion = [System.Environment]::OSVersion.VersionString
            MachineName = $env:COMPUTERNAME
            UserName = $env:USERNAME
            ProcessorCount = $env:BUSBUDDY_LOGICAL_CORES ?? [System.Environment]::ProcessorCount
            TotalMemoryGB = $env:BUSBUDDY_MEMORY_GB ?? 'Unknown'
        }

        # Git information (if requested)
        if ($IncludeGit) {
            try {
                $projectInfo.GitInfo = @{
                    Branch = & git branch --show-current 2>$null
                    LastCommit = & git log -1 --format="%H %s" 2>$null
                    Status = & git status --porcelain 2>$null
                    RemoteUrl = & git remote get-url origin 2>$null
                    HasUncommittedChanges = $null -ne (& git status --porcelain 2>$null)
                }
            }
            catch {
                $projectInfo.GitInfo.Error = "Git not available or not a Git repository"
            }
        }

        # Code metrics (if requested)
        if ($IncludeMetrics) {
            try {
                $codeFiles = Get-ChildItem -Path $projectInfo.RootPath -Recurse -Include '*.cs', '*.xaml' -ErrorAction SilentlyContinue

                $projectInfo.Metrics = @{
                    TotalFiles = $codeFiles.Count
                    CSharpFiles = ($codeFiles | Where-Object Extension -eq '.cs').Count
                    XamlFiles = ($codeFiles | Where-Object Extension -eq '.xaml').Count
                    TotalLines = 0
                    TotalSize = ($codeFiles | Measure-Object Length -Sum).Sum
                    LastModified = ($codeFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
                }

                # Count lines (sampling approach for performance)
                $sampleFiles = $codeFiles | Get-Random -Count ([math]::Min(10, $codeFiles.Count))
                $sampleLines = 0
                foreach ($file in $sampleFiles) {
                    try {
                        $sampleLines += (Get-Content $file.FullName -ErrorAction SilentlyContinue).Count
                    }
                    catch { }
                }

                if ($sampleFiles.Count -gt 0) {
                    $avgLinesPerFile = $sampleLines / $sampleFiles.Count
                    $projectInfo.Metrics.TotalLines = [math]::Round($avgLinesPerFile * $codeFiles.Count)
                }
            }
            catch {
                $projectInfo.Metrics.Error = "Could not calculate code metrics"
            }
        }

        return $projectInfo
    }
    catch {
        Write-Error "Failed to get project information: $($_.Exception.Message)"
        return $projectInfo
    }
}

function Open-BusBuddyInVSCode {
    <#
    .SYNOPSIS
    Opens BusBuddy project in VS Code with optimal configuration

    .DESCRIPTION
    Microsoft recommended VS Code integration pattern that opens the project
    with proper workspace settings and extension configuration.

    .PARAMETER NewWindow
    Open in a new VS Code window

    .PARAMETER WaitForClose
    Wait for VS Code to close before returning

    .INPUTS
    None

    .OUTPUTS
    [PSCustomObject] VS Code launch result

    .EXAMPLE
    Open-BusBuddyInVSCode

    .EXAMPLE
    Open-BusBuddyInVSCode -NewWindow -WaitForClose

    .NOTES
    Requires VS Code to be installed and accessible via 'code' command
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [switch]$NewWindow,

        [Parameter()]
        [switch]$WaitForClose
    )

    $result = [PSCustomObject]@{
        LaunchTime = Get-Date
        Success = $false
        ProcessId = $null
        WorkspacePath = $env:BUSBUDDY_ROOT ?? (Get-Location).Path
        Error = $null
    }

    try {
        # Check if VS Code is available
        $codeCommand = Get-Command code -ErrorAction SilentlyContinue
        if (-not $codeCommand) {
            throw "VS Code 'code' command not found. Please ensure VS Code is installed and added to PATH."
        }

        # Build VS Code arguments
        $args = @($result.WorkspacePath)

        if ($NewWindow) {
            $args += '--new-window'
        }

        # Add recommended extensions for BusBuddy development
        $recommendedExtensions = @(
            'ms-dotnettools.csharp',
            'ms-vscode.powershell',
            'ms-dotnettools.dotnet-interactive-vscode'
        )

        foreach ($extension in $recommendedExtensions) {
            $args += '--install-extension'
            $args += $extension
        }

        Write-Information "🔧 Opening BusBuddy in VS Code..." -InformationAction Continue
        Write-Information "   Workspace: $($result.WorkspacePath)" -InformationAction Continue

        # Launch VS Code
        $processArgs = @{
            FilePath = $codeCommand.Source
            ArgumentList = $args
            PassThru = $true
        }

        if (-not $WaitForClose) {
            $processArgs.NoNewWindow = $true
        }

        $process = Start-Process @processArgs

        $result.Success = $true
        $result.ProcessId = $process.Id

        Write-Information "✅ VS Code launched successfully (PID: $($process.Id))" -InformationAction Continue

        if ($WaitForClose) {
            Write-Information "⏳ Waiting for VS Code to close..." -InformationAction Continue
            $process.WaitForExit()
            Write-Information "✅ VS Code closed" -InformationAction Continue
        }

        return $result
    }
    catch {
        $result.Error = $_.Exception.Message
        Write-Error "Failed to open VS Code: $($_.Exception.Message)"
        return $result
    }
}
#endregion Core Development Functions

#region Alias Definitions
# Microsoft recommended alias patterns for development workflow
Set-Alias -Name 'bb-run' -Value 'Start-BusBuddyDevSession'
Set-Alias -Name 'bb-health' -Value 'Test-BusBuddySystemHealth'
Set-Alias -Name 'bb-sql-test' -Value 'Test-BusBuddyDatabaseConnection'
Set-Alias -Name 'bb-deps-check' -Value 'Invoke-BusBuddyDependencyCheck'
Set-Alias -Name 'bb-info' -Value 'Get-BusBuddyProjectInfo'
Set-Alias -Name 'bb-code' -Value 'Open-BusBuddyInVSCode'
Set-Alias -Name 'bb-vscode' -Value 'Open-BusBuddyInVSCode'

# Extended aliases for enhanced workflow
Set-Alias -Name 'bb-start' -Value 'Start-BusBuddyDevSession'
Set-Alias -Name 'bb-status' -Value 'Get-BusBuddyProjectInfo'
Set-Alias -Name 'bb-check' -Value 'Test-BusBuddySystemHealth'
Set-Alias -Name 'code-busbuddy' -Value 'Open-BusBuddyInVSCode'
#endregion Alias Definitions

#region Module Export
# Microsoft recommended explicit export pattern
Export-ModuleMember -Function @(
    'Start-BusBuddyDevSession',
    'Test-BusBuddySystemHealth',
    'Test-BusBuddyDatabaseConnection',
    'Invoke-BusBuddyDependencyCheck',
    'Get-BusBuddyProjectInfo',
    'Open-BusBuddyInVSCode'
) -Alias @(
    'bb-run', 'bb-health', 'bb-sql-test', 'bb-deps-check', 'bb-info', 'bb-code', 'bb-vscode',
    'bb-start', 'bb-status', 'bb-check', 'code-busbuddy'
)
#endregion Module Export
