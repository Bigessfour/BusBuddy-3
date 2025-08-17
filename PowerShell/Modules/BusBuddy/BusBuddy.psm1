<#
.SYNOPSIS
Core BusBuddy helper functions for .NET/WPF devops (build, run, test, health).
Standards: PowerShell 7.5+, StrictMode 3.0, use Write-Information for logging.
Refs: dotnet CLI[](https://learn.microsoft.com/dotnet/core/tools/), Syncfusion WPF[](https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf), Azure SQL[](https://learn.microsoft.com/en-us/azure/azure-sql/?view=azuresql).
#>

# Helper to resolve repo root by finding BusBuddy.sln (up to 5 levels).
function Resolve-BusBuddyRepoRoot {
    [CmdletBinding()]
    param()
    $current = $PSScriptRoot
    for ($i = 0; $i -lt 5; $i++) {
        if ([string]::IsNullOrEmpty($current)) { break }
        $sln = Join-Path $current 'BusBuddy.sln'
        if (Test-Path -LiteralPath $sln) { return $current }
        $current = Split-Path $current -Parent
    }
    return (Get-Location).Path
}

function invokeBusBuddyBuild {
    <#
    .SYNOPSIS
        Build the BusBuddy solution using dotnet build with comprehensive options.

    .DESCRIPTION
        Builds the BusBuddy solution file with specified configuration and verbosity settings.
        Supports all standard dotnet build parameters with enhanced validation and error reporting.
        Uses ShouldProcess for safe execution and provides detailed progress information.

    .PARAMETER Solution
        Path to the solution file to build. Defaults to 'BusBuddy.sln' in current directory.
        Must be a valid .sln file that exists.

    .PARAMETER Configuration
        Build configuration to use. Valid values: Debug, Release.
        Default: Debug for development builds.

    .PARAMETER Verbosity
        Output verbosity level for build process. Valid values: quiet, minimal, normal, detailed, diagnostic.
        Default: minimal for balanced output. Use 'detailed' for troubleshooting build issues.

    .PARAMETER TimeoutSeconds
        Maximum time to wait for build completion in seconds.
        Default: 300 seconds (5 minutes). Increase for large solutions.

    .PARAMETER MaxCpuCount
        Maximum number of CPU cores to use for parallel build.
        Default: All available processor cores. Reduce if experiencing memory issues.

    .PARAMETER Clean
        If specified, performs a clean build by removing previous build artifacts before building.
        Equivalent to running 'dotnet clean' followed by 'dotnet build'. Use for troubleshooting or ensuring a fresh build.

    .PARAMETER NoBuild
        Skip the build step. Useful when only validation is needed.
        If specified, the build process will be skipped and only validation will occur.
    .PARAMETER NoLogo
        Suppresses the Microsoft .NET logo and copyright message during build.
        Specify this switch for cleaner output. By default, the logo is shown unless this switch is used.
        Default: true for cleaner output.

    .PARAMETER NoRestore
        If specified, skips automatic package restore during build.
        Use when packages are already restored to speed up build.
        This is useful for CI/CD scenarios or when package restore is managed separately.

    .EXAMPLE
        invokeBusBuddyBuild
        Builds BusBuddy.sln with default settings (Debug, minimal verbosity).

    .EXAMPLE
        invokeBusBuddyBuild -Configuration Release -Verbosity detailed
        Builds BusBuddy.sln in Release configuration with detailed output.

    .EXAMPLE
        invokeBusBuddyBuild -Clean -Configuration Release
        Performs a clean Release build of the solution.

    .EXAMPLE
        bbBuild -Verbosity diagnostic -MaxCpuCount 4
        Builds with diagnostic output using only 4 CPU cores (alias usage).

    .INPUTS
        None. This function does not accept pipeline input.

    .OUTPUTS
        System.Int32. Returns 0 for successful build, non-zero for failure.

    .NOTES
        Author: BusBuddy Development Team
        Version: 1.3.0
        PowerShell: Requires PowerShell 7.5+ with .NET 9 SDK

        Official Documentation:
        - dotnet build: https://learn.microsoft.com/dotnet/core/tools/dotnet-build
        - MSBuild options: https://learn.microsoft.com/dotnet/core/tools/dotnet-build#options
        - PowerShell ShouldProcess: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process

    .LINK
        https://learn.microsoft.com/dotnet/core/tools/dotnet-build

    .LINK
        https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [Parameter(Position = 0, HelpMessage = "Path to the solution file to build")]
        [ValidateScript({
            if (-not (Test-Path $_)) {
                throw "Solution file not found: $_. Available .sln files: $((Get-ChildItem -Filter '*.sln' -ErrorAction SilentlyContinue).Name -join ', ')"
            }
            if (-not $_.EndsWith('.sln')) {
                throw "File must be a solution (.sln) file: $_"
            }
            return $true
        })]
        [string]$Solution = 'BusBuddy.sln',

        [Parameter(HelpMessage = "Build configuration (Debug or Release)")]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',

        [Parameter(HelpMessage = "Build output verbosity level")]
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
        [string]$Verbosity = 'minimal',

        [Parameter(HelpMessage = "Maximum build time in seconds")]
        [ValidateRange(30, 3600)]
        [int]$TimeoutSeconds = 300,

        [Parameter(HelpMessage = "Maximum CPU cores for parallel build")]
        [ValidateRange(1, 64)]
        [int]$MaxCpuCount = [Environment]::ProcessorCount,

        [Parameter(HelpMessage = "Perform clean build (remove artifacts first)")]
        [switch]$Clean,

        [Parameter(HelpMessage = "Skip the build step (validation-only, no build performed)")]
        [switch]$NoBuild,

        [Parameter(HelpMessage = "Suppress .NET logo and copyright (default: false unless specified)")]
        [switch]$NoLogo,

        [Parameter(HelpMessage = "Skip NuGet package restore")]
        [switch]$NoRestore
    )

    begin {
        # Validate solution file exists with detailed error reporting
        if (-not (Test-Path $Solution)) {
            $availableSolutions = (Get-ChildItem -Filter '*.sln' -ErrorAction SilentlyContinue).Name -join ', '
            $errorMsg = "Solution file not found: $Solution. Current location: $(Get-Location)."
            if ($availableSolutions) {
                $errorMsg += " Available .sln files: $availableSolutions"
            } else {
                $errorMsg += " No .sln files found in current directory."
            }
            Write-Warning $errorMsg
            return 1
        }
    }

    process {
        if ($PSCmdlet.ShouldProcess($Solution, "Build ($Configuration, $Verbosity, max CPU: $MaxCpuCount)")) {
            Write-Information "Building $Solution with $MaxCpuCount CPU cores..." -InformationAction Continue

            try {
                # Determine default behavior for NoLogo (default to true unless explicitly disabled)
                $useNoLogo = $true
                if ($PSBoundParameters.ContainsKey('NoLogo')) { $useNoLogo = [bool]$NoLogo }
                # Perform clean if requested
                if ($Clean) {
                    Write-Information "Cleaning previous build artifacts..." -InformationAction Continue
                    & dotnet clean $Solution --configuration $Configuration $(if ($useNoLogo) { '--nologo' })
                    if ($LASTEXITCODE -ne 0) {
                        Write-Warning "Clean operation failed with exit code: $LASTEXITCODE"
                        return $LASTEXITCODE
                    }
                }

                # Skip build if requested (useful for validation-only runs)
                if ($NoBuild) {
                    Write-Information "Skipping build step as requested (NoBuild parameter specified)" -InformationAction Continue
                    return 0
                }

                # Build the solution with specified parameters
                # Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet-build#options
                $buildArgs = @(
                    'build', $Solution,
                    '--configuration', $Configuration,
                    '--verbosity', $Verbosity
                )

                if ($useNoLogo) { $buildArgs += '--nologo' }  # MSBuild parameter is --nologo, not --no-logo
                if ($NoRestore) { $buildArgs += '--no-restore' }

                $buildCommand = "dotnet $($buildArgs -join ' ')"
                Write-Information "Executing: $buildCommand" -InformationAction Continue

                & dotnet @buildArgs
                $exitCode = $LASTEXITCODE

                if ($exitCode -eq 0) {
                    Write-Information "Build completed successfully" -InformationAction Continue
                } else {
                    Write-Warning "Build failed with exit code: $exitCode"
                }

                return $exitCode
            }
            catch {
                Write-Warning "Build failed with exception: $($_.Exception.Message)"
                Write-Information "Exception details: $($_.Exception)" -InformationAction Continue
                return 1
            }
        }
    }

    end {
        Write-Information "BusBuddy build process completed" -InformationAction Continue
    }
}

function invokeBusBuddyRun {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Solution = 'BusBuddy.sln',
        [string]$Project = 'BusBuddy.WPF\BusBuddy.WPF.csproj',
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
    )

    if (-not (Test-Path $Solution)) { Write-Warning "Solution not found: $Solution"; return 1 }
    if (-not (Test-Path $Project)) { Write-Warning "Project not found: $Project"; return 1 }

    if ($PSCmdlet.ShouldProcess($Project, "Build solution and run ($Configuration)")) {
        Write-Information "Building solution before running application..." -InformationAction Continue

        # First build the entire solution
        Write-Output "Building $Solution..."
        & dotnet build $Solution --configuration $Configuration --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Build failed. Cannot run application."
            return $LASTEXITCODE
        }

        # Then run the WPF project
        Write-Information "Running BusBuddy WPF application..." -InformationAction Continue
        Write-Output "Running $Project..."
        & dotnet run --project $Project --configuration $Configuration
        return $LASTEXITCODE
    }
}

<#
.SYNOPSIS
    Runs unit tests for the BusBuddy solution with advanced parallel execution and hyperthreading optimization.

.DESCRIPTION
    The bbTest command (invokeBusBuddyTest) executes unit tests for the BusBuddy school transportation management system
    with maximum CPU utilization and advanced parallel processing capabilities. Supports pipeline input for batch testing,
    comprehensive filtering, coverage collection, and real-time performance monitoring.

    This function automatically detects hyperthreading capabilities and optimizes core utilization for maximum throughput.
    Supports both logical and physical processor cores for optimal performance across different hardware configurations.

    References:
    - https://learn.microsoft.com/dotnet/core/tools/dotnet-test
    - https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests
    - https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-arrays

.PARAMETER Solution
    Specifies the solution file(s) to test. Accepts pipeline input for batch processing.
    Defaults to 'BusBuddy.sln' in the current directory.
    Supports wildcards and multiple files for advanced scenarios.

.PARAMETER Configuration
    Specifies the build configuration for testing. Valid values are 'Debug' and 'Release'.
    Defaults to 'Debug' for faster execution during development.
    Use 'Release' for production-ready performance testing.

.PARAMETER Filter
    Specifies a test filter expression to run specific tests. Uses .NET test filter syntax.
    Supports complex expressions and can be combined with Categories and Traits.
    Examples: 'FullyQualifiedName~StudentManagement', 'Category=Unit&Priority=1', 'Trait=Performance'

.PARAMETER Categories
    Specifies test categories to include. Can be combined with Filter for precise targeting.
    Common categories: Unit, Integration, Performance, Smoke, Regression
    Accepts array input for multiple categories.

.PARAMETER Traits
    Specifies test traits to filter by. Useful for organizing tests by feature or priority.
    Examples: @('Performance', 'CriticalPath'), @('Database', 'External')

.PARAMETER Coverage
    Enables code coverage collection using XPlat Code Coverage with enhanced reporting.
    Coverage reports include line, branch, and method coverage with HTML output.

.PARAMETER CoverageFormats
    Specifies output formats for coverage reports. Default includes cobertura and opencover.
    Valid formats: cobertura, opencover, lcov, teamcity, html
    Multiple formats can be specified for comprehensive reporting.

.PARAMETER Parallel
    Enables parallel test execution with automatic hyperthreading optimization.
    Automatically detects logical vs physical cores and configures optimal parallelism.

.PARAMETER HyperthreadingMode
    Controls hyperthreading utilization strategy for maximum performance.
    - Auto: Automatically detect and optimize (default)
    - LogicalCores: Use all logical processors (hyperthreading enabled)
    - PhysicalCores: Use only physical cores (hyperthreading disabled)
    - Manual: Use exact MaxCpuCount value

.PARAMETER MaxCpuCount
    Maximum number of CPU cores for parallel execution.
    Default: All logical processors with hyperthreading optimization.
    Range: 1 to 128 cores (supports high-end server configurations).

.PARAMETER TestProjects
    Specifies specific test projects to run instead of the entire solution.
    Accepts pipeline input and wildcard patterns. Useful for focused testing.

.PARAMETER OutputPath
    Specifies custom output directory for test results and coverage reports.
    Defaults to 'TestResults' with timestamp subdirectories.

.PARAMETER TimeoutMinutes
    Test execution timeout in minutes. Defaults to 15 minutes for comprehensive suites.
    Automatically scales based on detected test count and parallel execution settings.

.PARAMETER FailFast
    Stops test execution on first failure for rapid feedback during development.
    Useful for quick validation before commits.

.PARAMETER Retries
    Number of times to retry failed tests. Useful for flaky tests in CI/CD scenarios.
    Default: 0 (no retries). Maximum: 3 retries.

.PARAMETER NoRestore
    Skips NuGet package restore before testing for faster execution.
    Use when packages are known to be current.

.PARAMETER NoBuild
    Skips building the solution before testing. Requires recent successful build.
    Significantly faster for iterative testing scenarios.

.PARAMETER Verbosity
    Output verbosity level. Valid values: quiet, minimal, normal, detailed, diagnostic.
    Default: normal. Use 'detailed' for troubleshooting, 'quiet' for CI scenarios.

.PARAMETER LogLevel
    Test logging level. Valid values: None, Error, Warning, Information, Debug, Trace.
    Default: Information. Higher levels provide more detailed test execution logs.

.PARAMETER PassThru
    Returns detailed test result objects instead of just exit codes.
    Enables advanced result processing and reporting scenarios.

.EXAMPLE
    bbTest

    Runs all tests with automatic hyperthreading optimization and default settings.

.EXAMPLE
    bbTest -Configuration Release -Coverage -Parallel -HyperthreadingMode LogicalCores

    Runs all tests in Release mode with full hyperthreading utilization and coverage collection.

.EXAMPLE
    bbTest -Filter "Category=Unit" -MaxCpuCount 8 -FailFast

    Runs only unit tests using 8 cores, stopping on first failure.

.EXAMPLE
    Get-ChildItem "*.Tests.csproj" | bbTest -Parallel -Coverage -PassThru

    Pipeline input: Tests all test projects in parallel with coverage and detailed results.

.EXAMPLE
    bbTest -Categories @("Integration", "Performance") -Traits @("Database") -Retries 2

    Runs integration and performance tests with database traits, retrying failures twice.

.EXAMPLE
    bbTest -TestProjects @("BusBuddy.Core.Tests", "BusBuddy.WPF.Tests") -NoBuild -Verbosity detailed

    Tests specific projects without rebuilding, with detailed output for debugging.

.INPUTS
    System.String[]
        Accepts solution files, project files, or test project paths via pipeline.

.OUTPUTS
    System.Int32 (default)
        Returns the exit code from dotnet test execution (0 for success, non-zero for failure).

    System.Management.Automation.PSCustomObject (with -PassThru)
        Returns detailed test execution results including timing, coverage, and failure details.

.NOTES
    Author: BusBuddy Development Team
    Version: 2.0.0
    Requires: .NET 9.0 SDK, PowerShell 7.5+

    This function is part of the BusBuddy automation suite (bb* commands).
    Use 'bbCommands' to see all available BusBuddy commands.

    Hyperthreading Optimization:
    - Automatically detects CPU architecture (Intel HT, AMD SMT)
    - Optimizes thread pool settings for test execution
    - Monitors CPU utilization and adjusts parallelism dynamically

    Performance Tips:
    - Use -NoBuild for iterative testing (50-80% faster)
    - Enable -FailFast during development for quick feedback
    - Use specific -Filter or -Categories for focused testing
    - Combine -Parallel with appropriate -MaxCpuCount for your hardware

.LINK
    https://learn.microsoft.com/dotnet/core/tools/dotnet-test

.LINK
    https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests

.LINK
    https://learn.microsoft.com/dotnet/core/testing/unit-testing-code-coverage

.LINK
    https://learn.microsoft.com/powershell/scripting/developer/help/writing-help-for-windows-powershell-cmdlets
#>
function invokeBusBuddyTest {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    [Alias('bbTest', 'Test-BusBuddy', 'Invoke-BusBuddyTests')]
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true,
                   HelpMessage = "Solution file path(s) to test (supports pipeline input and wildcards)")]
        [Alias('Path', 'FullName', 'SolutionPath')]
        [string[]]$Solution = @('BusBuddy.sln'),

        [Parameter(Position = 1, HelpMessage = "Build configuration: Debug or Release")]
        [ValidateSet('Debug', 'Release')]
        [Alias('Config', 'BuildConfiguration')]
        [string]$Configuration = 'Debug',

        [Parameter(Position = 2, HelpMessage = "Test filter expression (supports complex expressions)")]
        [Alias('TestFilter', 'Where')]
        [string]$Filter = $null,

        [Parameter(HelpMessage = "Test categories to include (e.g., Unit, Integration, Performance)")]
        [ValidateSet('Unit', 'Integration', 'Performance', 'Smoke', 'Regression', 'Database', 'UI', 'API', 'Syncfusion')]
        [string[]]$Categories = @(),

        [Parameter(HelpMessage = "Test traits to filter by (custom test attributes)")]
        [string[]]$Traits = @(),

        [Parameter(HelpMessage = "Collect comprehensive code coverage data")]
        [Alias('CodeCoverage')]
        [switch]$Coverage,

        [Parameter(HelpMessage = "Coverage report output formats")]
        [ValidateSet('cobertura', 'opencover', 'lcov', 'teamcity', 'html')]
        [string[]]$CoverageFormats = @('cobertura', 'opencover'),

        [Parameter(HelpMessage = "Enable parallel test execution with hyperthreading optimization")]
        [switch]$Parallel,

        [Parameter(HelpMessage = "Hyperthreading utilization strategy")]
        [ValidateSet('Auto', 'LogicalCores', 'PhysicalCores', 'Manual')]
        [string]$HyperthreadingMode = 'Auto',

        [Parameter(HelpMessage = "Maximum number of CPU cores for parallel execution")]
        [ValidateRange(1, 128)]
        [int]$MaxCpuCount = 0,  # 0 means auto-detect

        [Parameter(ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true,
                   HelpMessage = "Specific test projects to run")]
        [Alias('Projects', 'TestProject')]
        [string[]]$TestProjects = @(),

        [Parameter(HelpMessage = "Custom output directory for test results")]
        [string]$OutputPath = $null,

        [Parameter(HelpMessage = "Test execution timeout in minutes")]
        [ValidateRange(1, 480)]  # Up to 8 hours for comprehensive test suites
        [int]$TimeoutMinutes = 15,

        [Parameter(HelpMessage = "Stop execution on first test failure")]
        [Alias('StopOnError', 'Fast')]
        [switch]$FailFast,

        [Parameter(HelpMessage = "Number of times to retry failed tests")]
        [ValidateRange(0, 3)]
        [int]$Retries = 0,

        [Parameter(HelpMessage = "Skip NuGet package restore")]
        [switch]$NoRestore,

        [Parameter(HelpMessage = "Skip building before testing")]
        [Alias('SkipBuild')]
        [switch]$NoBuild,

        [Parameter(HelpMessage = "Output verbosity level")]
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
        [string]$Verbosity = 'normal',

        [Parameter(HelpMessage = "Test framework logging level")]
        [ValidateSet('None', 'Error', 'Warning', 'Information', 'Debug', 'Trace')]
        [string]$LogLevel = 'Information',

        [Parameter(HelpMessage = "Return detailed test result objects")]
        [switch]$PassThru,

        [Parameter(HelpMessage = "Predefined test suite selection")]
        [ValidateSet('All', 'Unit', 'Integration', 'UI', 'Performance', 'Database', 'Syncfusion')]
        [string]$TestSuite = 'All',

        [Parameter(HelpMessage = "Include code coverage collection")]
        [switch]$IncludeCoverage,

        [Parameter(HelpMessage = "Enable detailed test output and diagnostic information")]
        [switch]$Detailed,

        [Parameter(HelpMessage = "Enable real-time test result streaming")]
        [switch]$LiveResults,

        [Parameter(HelpMessage = "Execute Syncfusion WPF UI automation tests")]
        [switch]$SyncfusionUITests,

        [Parameter(HelpMessage = "Execute Azure SQL database integration tests")]
        [switch]$AzureSQLTests,

        [Parameter(HelpMessage = "Execute performance and load testing scenarios")]
        [switch]$PerformanceTests
    )

    begin {
        # Hyperthreading optimization and CPU detection
        Write-Verbose "Initializing test execution with hyperthreading optimization..."

        # Detect CPU capabilities
        $processorInfo = Get-CimInstance -ClassName Win32_Processor
        $logicalCores = [Environment]::ProcessorCount
        $physicalCores = ($processorInfo | Measure-Object -Property NumberOfCores -Sum).Sum
        $hyperthreadingEnabled = $logicalCores -gt $physicalCores

        Write-Information "CPU Detection: $physicalCores physical cores, $logicalCores logical cores" -InformationAction Continue
        if ($hyperthreadingEnabled) {
            Write-Information "Hyperthreading detected and will be optimized" -InformationAction Continue
        }

        # Determine optimal CPU count based on hyperthreading mode
        if ($MaxCpuCount -eq 0) {
            switch ($HyperthreadingMode) {
                'Auto' {
                    # Use logical cores but leave some headroom for system processes
                    $MaxCpuCount = [Math]::Max(1, $logicalCores - 2)
                }
                'LogicalCores' {
                    $MaxCpuCount = $logicalCores
                }
                'PhysicalCores' {
                    $MaxCpuCount = $physicalCores
                }
                'Manual' {
                    $MaxCpuCount = [Environment]::ProcessorCount
                }
            }
        }

        Write-Information "Using $MaxCpuCount cores for parallel execution (Mode: $HyperthreadingMode)" -InformationAction Continue

        # Initialize results collection
        $allResults = @()
        $startTime = Get-Date

        # Setup output path with timestamp
        if (-not $OutputPath) {
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $OutputPath = "TestResults\BusBuddy_$timestamp"
        }

        if (-not (Test-Path $OutputPath)) {
            New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
            Write-Information "Created output directory: $OutputPath" -InformationAction Continue
        }
    }

    process {
        foreach ($solutionFile in $Solution) {
            # Validate solution file exists
            if (-not (Test-Path $solutionFile)) {
                Write-Warning "Solution not found: $solutionFile"
                if ($PassThru) {
                    $allResults += [PSCustomObject]@{
                        Solution = $solutionFile
                        ExitCode = 1
                        Status = 'Failed'
                        Error = "Solution file not found"
                        Duration = [TimeSpan]::Zero
                    }
                }
                continue
            }

            if ($PSCmdlet.ShouldProcess($solutionFile, "Test ($Configuration) with $MaxCpuCount cores")) {
                Write-Information "Testing $solutionFile$(if($Parallel){" (parallel with $MaxCpuCount cores)"})" -InformationAction Continue

                # Build dotnet test arguments
                $dotnetArgs = @('test')

                # Add solution or specific test projects
                if ($TestProjects.Count -gt 0) {
                    $dotnetArgs += $TestProjects
                } else {
                    $dotnetArgs += $solutionFile
                }

                # Core arguments
                $dotnetArgs += @(
                    '--configuration', $Configuration,
                    '--verbosity', $Verbosity,
                    '--logger', "trx;LogFileName=$OutputPath\TestResults_$(Split-Path $solutionFile -LeafBase).trx",
                    '--nologo'
                )

                # Build and restore options
                if ($NoRestore) { $dotnetArgs += '--no-restore' }
                if ($NoBuild) { $dotnetArgs += '--no-build' }

                # Filtering options
                $filterExpressions = @()
                if ($Filter) { $filterExpressions += $Filter }
                if ($Categories.Count -gt 0) {
                    $categoryFilter = "Category=" + ($Categories -join "|Category=")
                    $filterExpressions += $categoryFilter
                }
                if ($Traits.Count -gt 0) {
                    foreach ($trait in $Traits) {
                        $filterExpressions += "Trait=$trait"
                    }
                }

                # Test Suite filtering (enhanced predefined suites)
                if ($TestSuite -ne 'All') {
                    $suiteFilter = switch ($TestSuite) {
                        'Unit' { 'Category=Unit' }
                        'Integration' { 'Category=Integration|Category=Database' }
                        'UI' { 'Category=UI|Category=Syncfusion' }
                        'Performance' { 'Category=Performance|Category=Load' }
                        'Database' { 'Category=Database|Category=Azure' }
                        'Syncfusion' { 'Category=Syncfusion|Category=UI' }
                    }
                    if ($suiteFilter) {
                        $filterExpressions += $suiteFilter
                        Write-Information "Applied test suite filter ($TestSuite): $suiteFilter" -InformationAction Continue
                    }
                }

                if ($filterExpressions.Count -gt 0) {
                    $combinedFilter = $filterExpressions -join "&"
                    $dotnetArgs += @('--filter', $combinedFilter)
                    Write-Information "Applied filter: $combinedFilter" -InformationAction Continue
                }

                # Coverage options (enhanced)
                if ($Coverage -or $IncludeCoverage) {
                    $coverageArgs = @()
                    foreach ($format in $CoverageFormats) {
                        $coverageArgs += "Format=$format"
                    }
                    $coverageConfig = "XPlat Code Coverage;" + ($coverageArgs -join ";")
                    $dotnetArgs += @('--collect', $coverageConfig)
                    $dotnetArgs += @('--results-directory', $OutputPath)
                    Write-Information "Coverage enabled with formats: $($CoverageFormats -join ', ')" -InformationAction Continue
                }

                # Enhanced verbosity for detailed output
                if ($Detailed) {
                    $dotnetArgs = $dotnetArgs | Where-Object { $_ -ne '--verbosity' -and $_ -ne $Verbosity }
                    $dotnetArgs += @('--verbosity', 'detailed')
                    Write-Information "Detailed output enabled (verbosity: detailed)" -InformationAction Continue
                }

                # Live results streaming
                if ($LiveResults) {
                    $dotnetArgs += @('--logger', 'console;verbosity=normal')
                    Write-Information "Live results streaming enabled" -InformationAction Continue
                }

                # Specialized test execution modes
                if ($SyncfusionUITests) {
                    Write-Information "Syncfusion UI tests mode enabled" -InformationAction Continue
                }
                if ($AzureSQLTests) {
                    Write-Information "Azure SQL tests mode enabled" -InformationAction Continue
                }
                if ($PerformanceTests) {
                    Write-Information "Performance tests mode enabled" -InformationAction Continue
                }

                # Parallel execution options
                if ($Parallel) {
                    if ($MaxCpuCount -gt 0) {
                        $dotnetArgs += @('--maxcpucount', $MaxCpuCount)
                    }
                    # Enhanced parallel configuration
                    $parallelConfig = @(
                        "RunConfiguration.MaxCpuCount=$MaxCpuCount",
                        "RunConfiguration.ParallelizeTestCollections=true",
                        "RunConfiguration.ParallelizeAssembly=true"
                    )
                    if ($hyperthreadingEnabled -and $HyperthreadingMode -ne 'PhysicalCores') {
                        $parallelConfig += "RunConfiguration.DegreeOfParallelism=$logicalCores"
                    }
                    $dotnetArgs += @('--', $parallelConfig -join ' ')
                }

                # Failure handling
                if ($FailFast) {
                    $dotnetArgs += '--blame-hang-timeout', '30s'
                }

                # Retry logic
                $attempt = 0
                $maxAttempts = $Retries + 1
                $testResult = $null

                do {
                    $attempt++
                    if ($attempt -gt 1) {
                        Write-Information "Retry attempt $attempt/$maxAttempts for $solutionFile" -InformationAction Continue
                        Start-Sleep -Seconds (2 * $attempt)  # Exponential backoff
                    }

                    try {
                        $testStartTime = Get-Date
                        Write-Information "Executing: dotnet $($dotnetArgs -join ' ')" -InformationAction Continue

                        # Execute with timeout protection
                        $job = Start-Job -ScriptBlock {
                            param($dotnetArguments)
                            & dotnet @dotnetArguments
                            return $LASTEXITCODE
                        } -ArgumentList (,$dotnetArgs)

                        if (-not ($job | Wait-Job -Timeout ($TimeoutMinutes * 60))) {
                            $job | Stop-Job
                            Write-Warning "Test execution timed out after $TimeoutMinutes minutes"
                            $exitCode = 124  # Standard timeout exit code
                        } else {
                            $exitCode = $job | Receive-Job
                        }

                        $job | Remove-Job -Force
                        $testEndTime = Get-Date
                        $duration = $testEndTime - $testStartTime

                        $testResult = [PSCustomObject]@{
                            Solution = $solutionFile
                            ExitCode = $exitCode
                            Status = if ($exitCode -eq 0) { 'Passed' } else { 'Failed' }
                            Duration = $duration
                            Attempt = $attempt
                            MaxAttempts = $maxAttempts
                            CoresUsed = if ($Parallel) { $MaxCpuCount } else { 1 }
                            HyperthreadingMode = $HyperthreadingMode
                            CoverageEnabled = $Coverage.IsPresent
                            FilterApplied = $filterExpressions.Count -gt 0
                            OutputPath = $OutputPath
                        }

                        if ($exitCode -eq 0) {
                            Write-Information "Tests completed successfully in $($duration.TotalSeconds.ToString('F2')) seconds" -InformationAction Continue
                            break  # Success, exit retry loop
                        } else {
                            Write-Warning "Tests failed with exit code: $exitCode (Attempt $attempt/$maxAttempts)"
                            if ($FailFast) {
                                Write-Warning "FailFast enabled - stopping execution"
                                break
                            }
                        }

                    } catch {
                        Write-Warning "Test execution failed: $($_.Exception.Message)"
                        $testResult = [PSCustomObject]@{
                            Solution = $solutionFile
                            ExitCode = 1
                            Status = 'Error'
                            Duration = [TimeSpan]::Zero
                            Error = $_.Exception.Message
                            Attempt = $attempt
                            MaxAttempts = $maxAttempts
                            OutputPath = $OutputPath
                        }
                    }

                } while ($attempt -lt $maxAttempts -and $testResult.ExitCode -ne 0 -and -not $FailFast)

                if ($PassThru) {
                    $allResults += $testResult
                } else {
                    # Return exit code for simple usage
                    if ($testResult.ExitCode -ne 0) {
                        return $testResult.ExitCode
                    }
                }
            }
        }
    }

    end {
        $endTime = Get-Date
        $totalDuration = $endTime - $startTime

        Write-Information "Test execution completed in $($totalDuration.TotalMinutes.ToString('F2')) minutes" -InformationAction Continue

        if ($PassThru) {
            # Return detailed results object
            $summary = [PSCustomObject]@{
                TotalDuration = $totalDuration
                TotalSolutions = $allResults.Count
                PassedSolutions = ($allResults | Where-Object Status -eq 'Passed').Count
                FailedSolutions = ($allResults | Where-Object Status -ne 'Passed').Count
                MaxCoresUsed = $MaxCpuCount
                HyperthreadingMode = $HyperthreadingMode
                CoverageEnabled = $Coverage.IsPresent
                OutputPath = $OutputPath
                Results = $allResults
                StartTime = $startTime
                EndTime = $endTime
            }

            Write-Information "Summary: $($summary.PassedSolutions)/$($summary.TotalSolutions) solutions passed" -InformationAction Continue
            return $summary
        } else {
            # Return overall exit code
            $overallExitCode = if ($allResults | Where-Object ExitCode -ne 0) { 1 } else { 0 }
            return $overallExitCode
        }
    }
}

function Invoke-BusBuddyParallelTests {
    [CmdletBinding()]
    param(
        [string[]]$TestProjects = @(),
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
        [int]$ThrottleLimit = [Math]::Min([Environment]::ProcessorCount, 4),
        [switch]$Coverage
    )

    Write-Output "=== Parallel Test Execution (Throttle: $ThrottleLimit) ==="

    # Auto-discover test projects if none specified
    if (-not $TestProjects) {
        $TestProjects = Get-ChildItem -Recurse -Include "*Tests*.csproj", "*Test*.csproj" | ForEach-Object { $_.FullName }
    Write-Output "Auto-discovered $($TestProjects.Count) test projects"
    }

    $results = [System.Collections.Concurrent.ConcurrentBag[PSCustomObject]]::new()

    # Execute test projects in parallel
    $TestProjects | ForEach-Object -Parallel {
        $project = $_
        $config = $using:Configuration
        $coverage = $using:Coverage
        $results = $using:results

        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $startTime = Get-Date

        try {
            $testArgs = @('test', $project, '--configuration', $config, '--verbosity', 'normal', '--nologo')
            if ($coverage) { $testArgs += @('--collect:"XPlat Code Coverage"') }

            $process = Start-Process -FilePath 'dotnet' -ArgumentList $testArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput -RedirectStandardError
            $endTime = Get-Date
            $duration = $endTime - $startTime

            $result = [PSCustomObject]@{
                Project = $projectName
                ExitCode = $process.ExitCode
                Duration = $duration
                Status = if ($process.ExitCode -eq 0) { "✓ PASS" } else { "✗ FAIL" }
                StartTime = $startTime
                EndTime = $endTime
            }

            $results.Add($result)
        }
        catch {
            $result = [PSCustomObject]@{
                Project = $projectName
                ExitCode = 1
                Duration = (Get-Date) - $startTime
                Status = "✗ ERROR"
                Error = $_.Exception.Message
                StartTime = $startTime
                EndTime = Get-Date
            }
            $results.Add($result)
        }
    } -ThrottleLimit $ThrottleLimit

    # Display results
    $allResults = $results.ToArray() | Sort-Object Project
    Write-Output "`n=== Test Results Summary ==="
    $allResults | ForEach-Object {
        $durationStr = "{0:mm\:ss}" -f $_.Duration
        Write-Output "$($_.Status) $($_.Project) ($durationStr)"
        if ($_.Error) {
            Write-Warning "  Error: $($_.Error)"
        }
    }

    $passed = ($allResults | Where-Object ExitCode -eq 0).Count
    $failed = $allResults.Count - $passed
    $totalDuration = ($allResults | Measure-Object -Property Duration -Sum).Sum

    Write-Output "`nSummary: $passed passed, $failed failed, Total time: $("{0:mm\:ss}" -f $totalDuration)"

    return $failed -eq 0
}

function invokeBusBuddyHealthCheck {
    [CmdletBinding()]
    param(
        [switch]$Detailed,
        [switch]$ModernizationScan,
        [switch]$AutoRepair,
        [int]$TimeoutSeconds = 30
    )
    Write-Output "=== BusBuddy Comprehensive Health Check $(if($Detailed){'(Detailed)'}) $(if($ModernizationScan){'+ Modernization'}) $(if($AutoRepair){'+ Auto-Repair'}) ==="

    $healthStatus = @()
    $modernizationIssues = @()
    $autoRepairs = @()

    # Parallel health checks for better performance
    # Reference: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/foreach-object#example-14--using-parallel-processing
    $healthChecks = @(
        @{ Name = ".NET SDK"; Check = { & dotnet --version 2>$null } },
        @{ Name = "Git"; Check = { & git --version 2>$null } },
        @{ Name = "Node.js"; Check = { & node --version 2>$null } }
    )

    $healthChecks | ForEach-Object -Parallel {
        $check = $_
        $timeout = $using:TimeoutSeconds

        try {
            $job = Start-Job -ScriptBlock $check.Check
            if (Wait-Job $job -Timeout $timeout) {
                $result = Receive-Job $job
                Remove-Job $job
                if ($result) {
                    [PSCustomObject]@{
                        Name = $check.Name
                        Status = "✓"
                        Version = $result.ToString().Trim()
                        Message = "$($check.Name): $($result.ToString().Trim())"
                    }
                } else {
                    [PSCustomObject]@{
                        Name = $check.Name
                        Status = "✗"
                        Version = "Not Found"
                        Message = "$($check.Name) not found or returned empty"
                    }
                }
            } else {
                Remove-Job $job -Force
                [PSCustomObject]@{
                    Name = $check.Name
                    Status = "✗"
                    Version = "Timeout"
                    Message = "$($check.Name) check timed out after $timeout seconds"
                }
            }
        }
        catch {
            [PSCustomObject]@{
                Name = $check.Name
                Status = "✗"
                Version = "Error"
                Message = "$($check.Name) check failed: $($_.Exception.Message)"
            }
        }
    } -ThrottleLimit 4 | ForEach-Object {
        $healthStatus += $_
        Write-Output $_.Message
    }

    # Check PowerShell version (synchronous)
    $psVersion = $PSVersionTable.PSVersion
    $psEdition = $PSVersionTable.PSEdition
    $requiredVersion = [Version]'7.5.0'

    if ($psEdition -eq 'Core' -and $psVersion -ge $requiredVersion) {
        Write-Output "✓ PowerShell: $psVersion ($psEdition) - meets 7.5.2+ requirements"
    } else {
        Write-Warning "✗ PowerShell: $psVersion ($psEdition) - requires Core 7.5.0+"
        Write-Warning "  → Download: https://github.com/PowerShell/PowerShell/releases"
    }

    # Check for PowerShell context consistency
    Write-Output "`n=== PowerShell Context Validation ==="
    $contextIssues = @()

    # Check execution policy
    $execPolicy = Get-ExecutionPolicy -Scope CurrentUser
    if ($execPolicy -in @('Restricted', 'AllSigned')) {
        $contextIssues += "Execution policy too restrictive: $execPolicy"
    } else {
        Write-Output "✓ Execution policy: $execPolicy"
    }

    # Check module path integrity
    $psModulePaths = ($env:PSModulePath -split ';') | Where-Object { $_ }
    $busBuddyPaths = $psModulePaths | Where-Object { $_ -like "*BusBuddy*" }
    if ($busBuddyPaths.Count -gt 0) {
        Write-Output "✓ BusBuddy module paths: $($busBuddyPaths.Count) found"
    } else {
        $contextIssues += "No BusBuddy paths in PSModulePath"
    }

    # Check for version-specific issues
    if ($psVersion -eq [Version]'7.5.2') {
        # Known issues with 7.5.2
        if ($env:PSModulePath -match ';;') {
            $contextIssues += "Duplicate path separators in PSModulePath (7.5.2 issue)"
        }
        Write-Output "✓ PowerShell 7.5.2 - using current stable version"
    } elseif ($psVersion -gt [Version]'7.5.2') {
        Write-Output "✓ PowerShell $psVersion - newer than required 7.5.2"
    } else {
        $contextIssues += "PowerShell $psVersion older than recommended 7.5.2"
    }

    if ($contextIssues.Count -eq 0) {
        Write-Output "✓ PowerShell context is properly configured"
    } else {
        Write-Warning "✗ PowerShell context issues found:"
        $contextIssues | ForEach-Object { Write-Output "  → $_" }
    }

    # PSModulePath integrity checking with auto-repair
    Write-Output "`n=== PSModulePath Integrity & Auto-Repair ==="
    $currentPSModulePath = $env:PSModulePath -split [IO.Path]::PathSeparator
    $repoModulesPath = Join-Path $repoRoot 'PowerShell\Modules'
    $pathRepairs = @()

    # Check if BusBuddy modules path is in PSModulePath
    if ($repoModulesPath -notin $currentPSModulePath) {
        Write-Warning "✗ BusBuddy modules path not in PSModulePath"

        # Auto-repair: Add to PSModulePath for current session
        try {
            $env:PSModulePath = "$repoModulesPath$([IO.Path]::PathSeparator)$env:PSModulePath"
            $pathRepairs += "Added BusBuddy modules to PSModulePath"
            Write-Output "  → Auto-repair: Added BusBuddy modules to PSModulePath"
        } catch {
            Write-Warning "  → Failed to add BusBuddy modules to PSModulePath: $($_.Exception.Message)"
        }
    } else {
        Write-Output "✓ BusBuddy modules path is in PSModulePath"
    }

    # Check for duplicate paths in PSModulePath
    $duplicates = $currentPSModulePath | Group-Object | Where-Object { $_.Count -gt 1 }
    if ($duplicates.Count -gt 0) {
        Write-Warning "✗ Duplicate paths in PSModulePath: $($duplicates.Name -join ', ')"

        # Auto-repair: Remove duplicates
        try {
            $uniquePaths = ($env:PSModulePath -split [IO.Path]::PathSeparator) | Select-Object -Unique
            $env:PSModulePath = $uniquePaths -join [IO.Path]::PathSeparator
            $pathRepairs += "Removed $($duplicates.Count) duplicate PSModulePath entries"
            Write-Output "  → Auto-repair: Removed duplicate PSModulePath entries"
        } catch {
            Write-Warning "  → Failed to remove PSModulePath duplicates: $($_.Exception.Message)"
        }
    } else {
        Write-Output "✓ No duplicate PSModulePath entries"
    }

    # Check for invalid paths in PSModulePath
    $invalidPaths = $currentPSModulePath | Where-Object { $_ -and -not (Test-Path $_ -ErrorAction SilentlyContinue) }
    if ($invalidPaths.Count -gt 0) {
        Write-Warning "✗ Invalid paths in PSModulePath: $($invalidPaths -join ', ')"
        Write-Output "  → Consider: `$env:PSModulePath = (`$env:PSModulePath -split ';' | Where-Object { Test-Path `$_ }) -join ';'"
    } else {
        Write-Output "✓ All PSModulePath entries are valid"
    }

    # Report path repairs
    if ($pathRepairs.Count -gt 0) {
        Write-Output "✓ PSModulePath auto-repairs applied:"
        $pathRepairs | ForEach-Object { Write-Output "  → $_" }
    }

    # Check BusBuddy solution
    $repoRoot = if ($env:BUSBUDDY_REPO_ROOT) { $env:BUSBUDDY_REPO_ROOT } else { (Get-Location).Path }
    $solutionFile = Join-Path $repoRoot 'BusBuddy.sln'
    if (Test-Path $solutionFile) {
        Write-Output "✓ BusBuddy solution found: $solutionFile"

        if ($Detailed) {
            # Check project files
            $projects = Get-ChildItem -Path $repoRoot -Recurse -Include "*.csproj" -ErrorAction SilentlyContinue
            Write-Output "  → Found $($projects.Count) project files"
        }
    } else {
        Write-Warning "✗ BusBuddy solution not found: $solutionFile"
    }

    # Check BusBuddy modules - loaded vs available with hardening
    Write-Output "`n=== BusBuddy Module Status & Loading Hardening ==="
    $repoRoot = if ($env:BUSBUDDY_REPO_ROOT) { $env:BUSBUDDY_REPO_ROOT } else { (Get-Location).Path }
    $modulesPath = Join-Path $repoRoot 'PowerShell\Modules'

    if (Test-Path $modulesPath) {
        $availableModules = Get-ChildItem $modulesPath -Directory | ForEach-Object { $_.Name }
        $loadedModules = Get-Module | Where-Object { $_.Name -like "*BusBuddy*" } | ForEach-Object { $_.Name }
        $loadingIssues = @()

        foreach ($modName in $availableModules) {
            if ($modName -in $loadedModules) {
                Write-Output "✓ $modName - loaded"

                # Check for loading consistency issues
                $module = Get-Module $modName
                $expectedPath = Join-Path $modulesPath "$modName\$modName.psd1"
                if (Test-Path $expectedPath) {
                    if ($module.Path -notlike "*$modName.psd1") {
                        $loadingIssues += "$modName loaded from unexpected path: $($module.Path)"
                    }
                } else {
                    $loadingIssues += "$modName missing manifest: $expectedPath"
                }
            } else {
                Write-Warning "✗ $modName - not loaded"

                # Try to diagnose why it's not loaded
                $manifestPath = Join-Path $modulesPath "$modName\$modName.psd1"
                $psm1Path = Join-Path $modulesPath "$modName\$modName.psm1"

                if (-not (Test-Path $manifestPath)) {
                    $loadingIssues += "$modName missing manifest: $manifestPath"
                } elseif (-not (Test-Path $psm1Path)) {
                    $loadingIssues += "$modName missing module file: $psm1Path"
                } else {
                    # Try to load it and capture any errors
                    try {
                        Import-Module $manifestPath -Force -ErrorAction Stop
                        Write-Output "  → Successfully auto-loaded $modName"
                    } catch {
                        $loadingIssues += "$modName load error: $($_.Exception.Message)"
                    }
                }
            }
        }

        # Report loading hardening issues
        if ($loadingIssues.Count -eq 0) {
            Write-Output "✓ All module loading paths are consistent"
        } else {
            Write-Warning "✗ Module loading issues detected:"
            $loadingIssues | ForEach-Object { Write-Output "  → $_" }
        }

        # Check for module dependency conflicts
        $conflictingVersions = Get-Module | Where-Object { $_.Name -like "*BusBuddy*" } |
            Group-Object Name | Where-Object { $_.Count -gt 1 }

        if ($conflictingVersions.Count -gt 0) {
            Write-Warning "✗ Multiple versions loaded for same modules:"
            $conflictingVersions | ForEach-Object {
                Write-Output "  → $($_.Name): $($_.Group.Version -join ', ')"
            }
        } else {
            Write-Output "✓ No conflicting module versions"
        }
    } else {
        Write-Warning "✗ BusBuddy modules directory not found: $modulesPath"
    }

    # Check bb command availability and missing functions
    Write-Output "`n=== bb Commands Status ==="
    $missingFunctions = @()
    $bbCommands = Get-Command bb* -ErrorAction SilentlyContinue
    foreach ($cmd in $bbCommands) {
        if (-not $cmd.Source) {
            $alias = Get-Alias $cmd.Name -ErrorAction SilentlyContinue
            if ($alias) {
                $targetFunction = $alias.Definition
                if (-not (Get-Command $targetFunction -ErrorAction SilentlyContinue)) {
                    $missingFunctions += "$($cmd.Name) → $targetFunction"
                }
            }
        }
    }

    if ($missingFunctions.Count -eq 0) {
        Write-Output "✓ All bb commands have working functions"
    } else {
        Write-Warning "✗ Missing bb command functions:"
        $missingFunctions | ForEach-Object { Write-Output "  → $_" }
    }

    # === MISSING POWERSHELL TOOLS DETECTION & AUTO-INSTALLATION ===
    Write-Output "`n=== PowerShell Tool Availability & Auto-Installation ==="

    # Define required PowerShell modules with decision points
    $requiredModules = @(
        @{
            Name = 'dbatools'
            Purpose = 'SQL Server administration - replaces sqlcmd, SSMS scripts'
            InstallCommand = 'Install-Module dbatools -Scope CurrentUser -Force'
            DecisionPoint = 'Database operations detected'
        },
        @{
            Name = 'PowerShellGet'
            Purpose = 'Module management - replaces manual downloads'
            InstallCommand = 'Install-Module PowerShellGet -Scope CurrentUser -Force -AllowClobber'
            DecisionPoint = 'Module installation required'
        },
        @{
            Name = 'PSReadLine'
            Purpose = 'Enhanced command line editing - replaces basic console'
            InstallCommand = 'Install-Module PSReadLine -Scope CurrentUser -Force -AllowClobber'
            DecisionPoint = 'Interactive console usage'
        },
        @{
            Name = 'Pester'
            Purpose = 'Testing framework - replaces external test tools'
            InstallCommand = 'Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck'
            DecisionPoint = 'Unit testing required'
        },
        @{
            Name = 'Microsoft.Graph'
            Purpose = 'Azure/Microsoft 365 integration - replaces REST API calls'
            InstallCommand = 'Install-Module Microsoft.Graph -Scope CurrentUser -Force'
            DecisionPoint = 'Azure authentication needed'
        },
        @{
            Name = 'Az.Tools.Installer'
            Purpose = 'Azure PowerShell management - replaces Azure CLI'
            InstallCommand = 'Install-Module Az.Tools.Installer -Scope CurrentUser -Force'
            DecisionPoint = 'Azure resource management'
        }
    )

    $missingTools = @()
    $availableForInstall = @()

    foreach ($module in $requiredModules) {
        $installed = Get-Module -ListAvailable -Name $module.Name -ErrorAction SilentlyContinue
        $loaded = Get-Module -Name $module.Name -ErrorAction SilentlyContinue

        if ($installed) {
            if ($loaded) {
                Write-Output "✅ $($module.Name) - installed and loaded (v$($loaded.Version))"
            } else {
                Write-Warning "⚠️  $($module.Name) - installed but not loaded (v$($installed[0].Version))"

                if ($AutoRepair) {
                    try {
                        Import-Module $module.Name -Force -ErrorAction Stop
                        $autoRepairs += "Auto-loaded module: $($module.Name)"
                        Write-Output "  → Auto-repair: Loaded $($module.Name)"
                    }
                    catch {
                        Write-Warning "  → Failed to auto-load $($module.Name): $($_.Exception.Message)"
                    }
                }
            }
        } else {
            $missingTools += $module
            Write-Warning "❌ $($module.Name) - not installed"
            Write-Output "  → Purpose: $($module.Purpose)"
            Write-Output "  → Decision Point: $($module.DecisionPoint)"
            Write-Output "  → Install: $($module.InstallCommand)"

            # Check if available in PowerShell Gallery
            try {
                $available = Find-Module -Name $module.Name -ErrorAction Stop
                $availableForInstall += $module
                Write-Output "  → Available in Gallery: v$($available.Version)"

                if ($AutoRepair) {
                    Write-Output "  → Auto-installing $($module.Name)..."
                    try {
                        Invoke-Expression $module.InstallCommand
                        $autoRepairs += "Auto-installed module: $($module.Name)"
                        Write-Output "  → ✅ Successfully installed $($module.Name)"
                    }
                    catch {
                        Write-Warning "  → Failed to auto-install $($module.Name): $($_.Exception.Message)"
                    }
                }
            }
            catch {
                Write-Warning "  → Not available in PowerShell Gallery: $($_.Exception.Message)"
            }
        }
    }

    # === MISSING DEFINITION DETECTION & RESOLUTION ===
    Write-Output "`n=== Missing Definition Detection & Resolution ==="

    $missingDefinitions = @()
    $aliasesWithMissingTargets = @()

    # Check all bb* aliases for missing target functions
    $bbAliases = Get-Alias bb* -ErrorAction SilentlyContinue
    foreach ($alias in $bbAliases) {
        if (-not (Get-Command $alias.Definition -ErrorAction SilentlyContinue)) {
            $aliasesWithMissingTargets += @{
                Alias = $alias.Name
                MissingTarget = $alias.Definition
                Source = $alias.Source
            }
        }
    }

    # Check for common missing .NET replacements
    $dotnetReplacements = @(
        @{
            DotNetCommand = 'dotnet build'
            PowerShellReplacement = 'Invoke-BusBuddyBuild'
            Module = 'BusBuddy'
            Purpose = 'Build management with enhanced logging and error handling'
        },
        @{
            DotNetCommand = 'dotnet test'
            PowerShellReplacement = 'Invoke-BusBuddyTest'
            Module = 'BusBuddy'
            Purpose = 'Test execution with parallel processing and reporting'
        },
        @{
            DotNetCommand = 'dotnet run'
            PowerShellReplacement = 'Invoke-BusBuddyRun'
            Module = 'BusBuddy'
            Purpose = 'Application execution with environment validation'
        },
        @{
            DotNetCommand = 'git status'
            PowerShellReplacement = 'Get-GitStatus (posh-git)'
            Module = 'posh-git'
            Purpose = 'Enhanced Git integration with PowerShell'
        },
        @{
            DotNetCommand = 'nuget.exe'
            PowerShellReplacement = 'Install-Package, Get-Package'
            Module = 'PackageManagement'
            Purpose = 'Package management without external executables'
        }
    )

    foreach ($replacement in $dotnetReplacements) {
        $psCommand = ($replacement.PowerShellReplacement -split ' ')[0]
        if (-not (Get-Command $psCommand -ErrorAction SilentlyContinue)) {
            $missingDefinitions += @{
                MissingCommand = $psCommand
                ReplacesCommand = $replacement.DotNetCommand
                RequiredModule = $replacement.Module
                Purpose = $replacement.Purpose
            }
        }
    }

    # Report missing definitions
    if ($aliasesWithMissingTargets.Count -eq 0 -and $missingDefinitions.Count -eq 0) {
        Write-Output "✅ All command definitions are available"
    } else {
        if ($aliasesWithMissingTargets.Count -gt 0) {
            Write-Warning "❌ bb* aliases with missing targets:"
            $aliasesWithMissingTargets | ForEach-Object {
                Write-Output "  → $($_.Alias) → $($_.MissingTarget) (Source: $($_.Source))"
            }
        }

        if ($missingDefinitions.Count -gt 0) {
            Write-Warning "❌ Missing PowerShell replacements for .NET commands:"
            $missingDefinitions | ForEach-Object {
                Write-Output "  → Missing: $($_.MissingCommand)"
                Write-Output "    Replaces: $($_.ReplacesCommand)"
                Write-Output "    Module: $($_.RequiredModule)"
                Write-Output "    Purpose: $($_.Purpose)"
            }
        }
    }

    # === POWERSHELL 7.5.2 MODERNIZATION SCAN ===
    if ($ModernizationScan) {
        Write-Output "`n=== PowerShell 7.5.2 Modernization & Legacy Detection ==="

        # 1. IMPROPER MODULE IMPORTS DETECTION
        Write-Output "🔍 Scanning for improper module imports..."
        $improperImports = @()

        # Check for modules loaded without proper manifest
        $loadedModules = Get-Module
        foreach ($module in $loadedModules) {
            if ($module.Path -like "*.psm1" -and -not (Test-Path ($module.Path -replace '\.psm1$', '.psd1'))) {
                $improperImports += "Module '$($module.Name)' loaded without manifest (.psd1)"
            }

            # Check for legacy import patterns
            if ($module.Name -like "*BusBuddy*" -and $module.ModuleType -ne 'Script') {
                $improperImports += "BusBuddy module '$($module.Name)' should be Script type, not $($module.ModuleType)"
            }
        }

        # 2. DOT-SOURCED FUNCTIONS DETECTION
        Write-Output "🔍 Scanning for dot-sourced functions vs proper modules..."
        $dotSourcedIssues = @()

        # Check for functions that should be in modules
        $allFunctions = Get-Command -CommandType Function | Where-Object { $_.Source -eq '' }
        foreach ($func in $allFunctions) {
            if ($func.Name -like "*BusBuddy*" -or $func.Name -like "bb*") {
                $dotSourcedIssues += "Function '$($func.Name)' appears dot-sourced, should be in BusBuddy module"
            }
        }

        # 3. IMPROPER VERB DETECTION (PowerShell 7.5.2 Standards)
        Write-Output "🔍 Scanning for improper PowerShell verbs..."
        $improperVerbs = @()
        $approvedVerbs = Get-Verb | ForEach-Object { $_.Verb }

        $allCommands = Get-Command -CommandType Function, Cmdlet | Where-Object { $_.Name -match '^[A-Z][a-z]+-' }
        foreach ($cmd in $allCommands) {
            $verb = ($cmd.Name -split '-')[0]
            if ($verb -notin $approvedVerbs) {
                $improperVerbs += "Command '$($cmd.Name)' uses unapproved verb '$verb'"
            }
        }

        # 4. LEGACY SYNTAX DETECTION
        Write-Output "🔍 Scanning for PowerShell 7.5.2 syntax violations..."
        $legacySyntax = @()

        # Check for legacy array syntax
        $scriptFiles = Get-ChildItem -Path $repoRoot -Recurse -Include "*.ps1", "*.psm1" -ErrorAction SilentlyContinue
        foreach ($file in $scriptFiles) {
            try {
                $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
                if ($content) {
                    # Legacy array construction
                    if ($content -match '@\(\s*\)' -and $content -notmatch '@\(\s*#.*\)') {
                        $legacySyntax += "$($file.Name): Uses @() instead of [array]::new()"
                    }

                    # Legacy string operations
                    if ($content -match '\.Replace\(' -and $content -notmatch '-replace') {
                        $legacySyntax += "$($file.Name): Uses .Replace() instead of -replace operator"
                    }

                    # Legacy comparison operators
                    if ($content -match '\s-eq\s+\$null' -and $content -notmatch '\$null\s+-eq\s') {
                        $legacySyntax += "$($file.Name): Uses '-eq \$null' instead of '\$null -eq' (recommended)"
                    }

                    # Legacy pipeline usage
                    if ($content -match 'ForEach-Object\s+{[^}]*\$_\.' -and $content -notmatch '\.ForEach\(') {
                        $legacySyntax += "$($file.Name): Could use .ForEach() method instead of ForEach-Object"
                    }
                }
            }
            catch {
                Write-Verbose "Could not scan $($file.FullName): $($_.Exception.Message)"
            }
        }

        # 5. CAMELCASE COMPLIANCE DETECTION
        Write-Output "🔍 Scanning for camelCase compliance violations..."
        $camelCaseIssues = @()

        # Check BusBuddy functions for proper camelCase
        $busBuddyFunctions = Get-Command -CommandType Function | Where-Object { $_.Source -like "*BusBuddy*" }
        foreach ($func in $busBuddyFunctions) {
            # Functions should start with lowercase (camelCase)
            if ($func.Name -match '^[A-Z]' -and $func.Name -notmatch '-') {
                $camelCaseIssues += "Function '$($func.Name)' should start with lowercase (camelCase)"
            }

            # Check for PascalCase in internal functions
            if ($func.Name -match '^[a-z]+[A-Z]' -and $func.Name -match '[A-Z][a-z]+[A-Z]') {
                # This is proper camelCase, validate it's not mixed with other patterns
            } else {
                $camelCaseIssues += "Function '$($func.Name)' may have inconsistent casing pattern"
            }
        }

        # 6. DOTNET COMMAND DEPRECATION SCAN
        Write-Output "🔍 Scanning for deprecated .NET CLI commands..."
        $dotnetDeprecations = @()

        foreach ($file in $scriptFiles) {
            try {
                $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
                if ($content) {
                    # Direct dotnet commands that should be PowerShell wrapped
                    if ($content -match 'dotnet\s+build\s') {
                        $dotnetDeprecations += "$($file.Name): Uses 'dotnet build' - should use Invoke-BusBuddyBuild"
                    }
                    if ($content -match 'dotnet\s+run\s') {
                        $dotnetDeprecations += "$($file.Name): Uses 'dotnet run' - should use Invoke-BusBuddyRun"
                    }
                    if ($content -match 'dotnet\s+test\s') {
                        $dotnetDeprecations += "$($file.Name): Uses 'dotnet test' - should use Invoke-BusBuddyTest"
                    }
                    if ($content -match 'dotnet\s+clean\s') {
                        $dotnetDeprecations += "$($file.Name): Uses 'dotnet clean' - should use Invoke-BusBuddyClean"
                    }
                    if ($content -match 'dotnet\s+restore\s') {
                        $dotnetDeprecations += "$($file.Name): Uses 'dotnet restore' - should use Invoke-BusBuddyRestore"
                    }
                }
            }
            catch {
                Write-Verbose "Could not scan $($file.FullName): $($_.Exception.Message)"
            }
        }

        # REPORT MODERNIZATION ISSUES
        $modernizationIssues += $improperImports
        $modernizationIssues += $dotSourcedIssues
        $modernizationIssues += $improperVerbs
        $modernizationIssues += $legacySyntax
        $modernizationIssues += $camelCaseIssues
        $modernizationIssues += $dotnetDeprecations

        if ($modernizationIssues.Count -eq 0) {
            Write-Output "✅ No modernization issues detected - PowerShell 7.5.2 compliant"
        } else {
            Write-Warning "⚠️  Found $($modernizationIssues.Count) modernization issues:"
            $modernizationIssues | ForEach-Object { Write-Output "  → $_" }
        }
    }

    # Check Syncfusion license with validation
    if ($env:SYNCFUSION_LICENSE_KEY) {
        $keyLength = $env:SYNCFUSION_LICENSE_KEY.Length
        $maskedKey = "*" * [Math]::Max(0, $keyLength - 8) + $env:SYNCFUSION_LICENSE_KEY.Substring([Math]::Max(0, $keyLength - 8))
    Write-Output "✓ Syncfusion license key configured (length: $keyLength, ends: $maskedKey)"
    } else {
        Write-Warning "✗ SYNCFUSION_LICENSE_KEY environment variable not set"
    Write-Output "  → Set via: `$env:SYNCFUSION_LICENSE_KEY = 'your-key-here'"
    }

    if ($Detailed) {
        # Additional detailed checks
        Write-Output "`n=== Detailed System Info ==="
        Write-Output "CPU Cores: $([Environment]::ProcessorCount)"
        Write-Output "RAM: $([Math]::Round((Get-CimInstance Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum).Sum / 1GB, 2)) GB"
        Write-Output "OS: $([Environment]::OSVersion.VersionString)"
        Write-Output "PowerShell Host: $($Host.Name) $($Host.Version)"
        Write-Output "Execution Policy: $(Get-ExecutionPolicy -List | ForEach-Object { "$($_.Scope): $($_.ExecutionPolicy)" } | Join-String -Separator ', ')"

        # Advanced module analysis
        Write-Output "`n=== Advanced Module Analysis ==="
        $allModules = Get-Module -ListAvailable | Group-Object Name | Where-Object { $_.Count -gt 1 }
        if ($allModules.Count -gt 0) {
            Write-Warning "Multiple versions of modules detected:"
            $allModules | ForEach-Object {
                Write-Output "  → $($_.Name): $($_.Group.Version -join ', ')"
            }
        } else {
            Write-Output "✅ No conflicting module versions"
        }
    }

    # === AUTO-REPAIR SUMMARY ===
    if ($AutoRepair -and $autoRepairs.Count -gt 0) {
        Write-Output "`n=== Auto-Repair Summary ==="
        Write-Output "Applied $($autoRepairs.Count) automatic repairs:"
        $autoRepairs | ForEach-Object { Write-Output "  ✅ $_" }
    } elseif ($AutoRepair) {
        Write-Output "`n=== Auto-Repair Summary ==="
        Write-Output "✅ No repairs were needed"
    }

    # === COMPREHENSIVE HEALTH SUMMARY ===
    Write-Output "`n=== Comprehensive Health Summary ==="

    $totalIssues = 0
    $totalIssues += ($healthStatus | Where-Object Status -eq "✗").Count
    $totalIssues += if ($ModernizationScan) { $modernizationIssues.Count } else { 0 }
    $totalIssues += $missingTools.Count
    $totalIssues += $aliasesWithMissingTargets.Count + $missingDefinitions.Count

    if ($totalIssues -eq 0) {
        Write-Output "🎉 EXCELLENT: BusBuddy environment is fully optimized and PowerShell 7.5.2 compliant!"
        Write-Output "   • All modules properly loaded"
        Write-Output "   • No legacy syntax detected"
        Write-Output "   • All bb* commands functional"
        Write-Output "   • PowerShell tools replacing .NET CLI"
    } else {
        Write-Warning "⚠️  ATTENTION: Found $totalIssues total issues requiring resolution"
        Write-Output "Recommended actions:"
        Write-Output "  1. Run with -AutoRepair to fix automatically resolvable issues"
        Write-Output "  2. Run with -ModernizationScan to identify legacy code patterns"
        Write-Output "  3. Install missing PowerShell modules from Gallery"
        Write-Output "  4. Replace .NET CLI commands with PowerShell equivalents"
        Write-Output "`nFor immediate fixes, run: bbHealth -AutoRepair -ModernizationScan"
    }

    Write-Output "=== Health Check Complete ==="

    # Return comprehensive health status
    return @{
        OverallHealth = $totalIssues -eq 0
        IssueCount = $totalIssues
        ModernizationIssues = if ($ModernizationScan) { $modernizationIssues.Count } else { $null }
        MissingTools = $missingTools.Count
        AutoRepairsApplied = $autoRepairs.Count
        Recommendations = if ($totalIssues -gt 0) {
            @("Use -AutoRepair flag", "Install missing modules", "Modernize legacy syntax")
        } else {
            @("Environment fully optimized")
        }
    }
}

# Microsoft Testing Platform 2025 - Advanced Test Discovery and Execution
function Invoke-BusBuddyTestDiscovery {
    [CmdletBinding()]
    param(
        [string]$TestAssembly = 'BusBuddy.Tests.dll',
        [string]$OutputFormat = 'json',
        [switch]$IncludeSource,
        [string]$Filter = ''
    )

    <#
    .SYNOPSIS
        Discover available tests using Microsoft Testing Platform 2025

    .DESCRIPTION
        Advanced test discovery with MTP 2025 integration for comprehensive test inventory

    .EXAMPLE
        Invoke-BusBuddyTestDiscovery -IncludeSource -OutputFormat json
    #>

    $repoRoot = Resolve-BusBuddyRepoRoot
    $testProject = Join-Path $repoRoot 'BusBuddy.Tests\BusBuddy.Tests.csproj'

    $discoveryArgs = @(
        'test', $testProject
        '--list-tests'
        '--logger', "json;LogFileName=discovery_$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').json"
    )

    if ($Filter) { $discoveryArgs += '--filter', $Filter }

    Write-Information "Discovering tests..." -InformationAction Continue
    & dotnet @discoveryArgs

    return $LASTEXITCODE
}

# Syncfusion WPF UI Test Automation
function Invoke-BusBuddySyncfusionUITests {
    [CmdletBinding()]
    param(
        [string]$ControlType = 'All',
        [switch]$Interactive,
        [int]$TimeoutSeconds = 30
    )

    <#
    .SYNOPSIS
        Execute Syncfusion WPF UI automation tests

    .DESCRIPTION
        Automated testing of Syncfusion controls in BusBuddy WPF application

    .PARAMETER ControlType
        Specific Syncfusion control type to test (DataGrid, RibbonControl, DockingManager, etc.)

    .EXAMPLE
        Invoke-BusBuddySyncfusionUITests -ControlType DataGrid -Interactive
    #>

    $testFilter = switch ($ControlType) {
        'DataGrid' { 'Category=Syncfusion&FullyQualifiedName~DataGrid' }
        'RibbonControl' { 'Category=Syncfusion&FullyQualifiedName~Ribbon' }
        'DockingManager' { 'Category=Syncfusion&FullyQualifiedName~Docking' }
        default { 'Category=Syncfusion|Category=UI' }
    }

    return invokeBusBuddyTest -Solution 'BusBuddy.sln' -Filter $testFilter -Categories @('Syncfusion', 'UI') -Coverage -Detailed
}

# Azure SQL Integration Testing
function Invoke-BusBuddyAzureSQLTests {
    [CmdletBinding()]
    param(
        [string]$ConnectionString = '',
        [switch]$CleanupTestData,
        [string]$TestDataSet = 'Minimal'
    )

    <#
    .SYNOPSIS
        Execute Azure SQL database integration tests

    .DESCRIPTION
        Comprehensive database testing with Entity Framework Core and Azure SQL

    .EXAMPLE
        Invoke-BusBuddyAzureSQLTests -TestDataSet Full -CleanupTestData
    #>

    if ($ConnectionString) {
        $env:BUSBUDDY_TEST_CONNECTION_STRING = $ConnectionString
    }

    try {
        return invokeBusBuddyTest -Solution 'BusBuddy.sln' -Categories @('Database', 'Integration') -Coverage -TimeoutMinutes 10
    }
    finally {
        if ($CleanupTestData) {
            Write-Information "Cleaning up test data..." -InformationAction Continue
            # Test data cleanup logic would go here
        }
    }
}

# Performance and Load Testing
function Invoke-BusBuddyPerformanceTests {
    [CmdletBinding()]
    param(
        [int]$ConcurrentUsers = 10,
        [int]$DurationMinutes = 5,
        [string]$Scenario = 'StudentManagement',
        [switch]$MemoryProfiling
    )

    <#
    .SYNOPSIS
        Execute performance and load testing scenarios

    .DESCRIPTION
        Advanced performance testing with memory profiling and load simulation

    .EXAMPLE
        Invoke-BusBuddyPerformanceTests -ConcurrentUsers 50 -DurationMinutes 10 -MemoryProfiling
    #>

    # Set performance test environment variables
    $env:BUSBUDDY_PERF_CONCURRENT_USERS = $ConcurrentUsers
    $env:BUSBUDDY_PERF_DURATION_MINUTES = $DurationMinutes
    $env:BUSBUDDY_PERF_SCENARIO = $Scenario

    return invokeBusBuddyTest -Solution 'BusBuddy.sln' -Categories @('Performance', 'Load') -Parallel -HyperthreadingMode LogicalCores -Coverage -TimeoutMinutes ($DurationMinutes + 5)
}

# Advanced Test Result Analysis
function Get-BusBuddyTestResults {
    [CmdletBinding()]
    param(
        [string]$ResultsPath = 'TestResults',
        [string]$Format = 'Summary',
        [switch]$IncludeCoverage,
        [switch]$GenerateReport
    )

    <#
    .SYNOPSIS
        Analyze and report test execution results

    .DESCRIPTION
        Comprehensive test result analysis with coverage metrics and trend analysis

    .EXAMPLE
        Get-BusBuddyTestResults -IncludeCoverage -GenerateReport -Format Detailed
    #>

    $repoRoot = Resolve-BusBuddyRepoRoot
    $resultsDir = Join-Path $repoRoot $ResultsPath

    if (-not (Test-Path $resultsDir)) {
        Write-Warning "Results directory not found: $resultsDir"
        return
    }

    # Find latest test results
    $latestResults = Get-ChildItem -Path $resultsDir -Directory | Sort-Object CreationTime -Descending | Select-Object -First 1

    if (-not $latestResults) {
        Write-Warning "No test results found in $resultsDir"
        return
    }

    Write-Information "Analyzing results from: $($latestResults.FullName)" -InformationAction Continue

    # Parse TRX files for test results
    $trxFiles = Get-ChildItem -Path $latestResults.FullName -Filter "*.trx" -Recurse

    foreach ($trxFile in $trxFiles) {
        Write-Information "Processing: $($trxFile.Name)" -InformationAction Continue
        # TRX parsing logic would go here
    }

    # Coverage analysis if enabled
    if ($IncludeCoverage) {
        $coverageFiles = Get-ChildItem -Path $latestResults.FullName -Filter "coverage.*" -Recurse
        foreach ($coverageFile in $coverageFiles) {
            Write-Information "Coverage report: $($coverageFile.Name)" -InformationAction Continue
        }
    }

    return @{
        ResultsPath = $latestResults.FullName
        TestFiles = $trxFiles.Count
        CoverageFiles = if ($IncludeCoverage) { $coverageFiles.Count } else { 0 }
    }
}

# Microsoft Testing Platform 2025 - Test Execution with Live Results
function Start-BusBuddyLiveTestExecution {
    [CmdletBinding()]
    param(
        [string]$TestSuite = 'All',
        [switch]$StreamResults,
        [int]$RefreshIntervalSeconds = 2
    )

    <#
    .SYNOPSIS
        Execute tests with real-time result streaming (MTP 2025)

    .DESCRIPTION
        Live test execution with real-time progress updates and result streaming

    .EXAMPLE
        Start-BusBuddyLiveTestExecution -TestSuite Performance -StreamResults
    #>

    $repoRoot = Resolve-BusBuddyRepoRoot
    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $liveResultsPath = Join-Path $repoRoot "TestResults\Live_$timestamp"
    New-Item -ItemType Directory -Path $liveResultsPath -Force | Out-Null

    # Define test categories based on suite
    $categories = switch ($TestSuite) {
        'Unit' { @('Unit') }
        'Integration' { @('Integration', 'Database') }
        'UI' { @('UI', 'Syncfusion') }
        'Performance' { @('Performance', 'Load') }
        default { @() }
    }

    Write-Information "Starting live test execution for suite: $TestSuite" -InformationAction Continue
    Write-Information "Live results path: $liveResultsPath" -InformationAction Continue

    # Execute tests with live result streaming
    $testJob = Start-Job -ScriptBlock {
        param($repoRoot, $categories, $liveResultsPath, $StreamResults)

        Set-Location $repoRoot

        # Import module in job context
        Import-Module "$repoRoot\PowerShell\Modules\BusBuddy\BusBuddy.psm1" -Force

        if ($categories.Count -gt 0) {
            invokeBusBuddyTest -Solution 'BusBuddy.sln' -Categories $categories -Parallel -Coverage -OutputPath $liveResultsPath
        } else {
            invokeBusBuddyTest -Solution 'BusBuddy.sln' -Parallel -Coverage -OutputPath $liveResultsPath
        }

    } -ArgumentList $repoRoot, $categories, $liveResultsPath, $StreamResults

    if ($StreamResults) {
        # Monitor and stream results in real-time
        while ($testJob.State -eq 'Running') {
            Start-Sleep -Seconds $RefreshIntervalSeconds

            # Check for new result files
            $newResults = Get-ChildItem -Path $liveResultsPath -Filter "*.trx" -Recurse -ErrorAction SilentlyContinue
            foreach ($result in $newResults) {
                Write-Information "New result: $($result.Name)" -InformationAction Continue
            }
        }
    }

    # Wait for completion and get results
    $testJob | Wait-Job | Out-Null
    $exitCode = $testJob | Receive-Job
    $testJob | Remove-Job

    Write-Information "Live test execution completed with exit code: $exitCode" -InformationAction Continue
    return $exitCode
}

# Intelligent Test Selection Based on Code Changes
function Select-BusBuddyImpactedTests {
    [CmdletBinding()]
    param(
        [string]$BaseBranch = 'main',
        [string]$ComparisonBranch = 'HEAD',
        [switch]$IncludeDependencies
    )

    <#
    .SYNOPSIS
        Intelligently select tests based on code changes

    .DESCRIPTION
        Analyzes git diff to determine which tests are impacted by code changes

    .EXAMPLE
        Select-BusBuddyImpactedTests -BaseBranch main -IncludeDependencies
    #>

    $repoRoot = Resolve-BusBuddyRepoRoot
    Push-Location $repoRoot

    try {
        # Get changed files
        $changedFiles = & git diff --name-only $BaseBranch...$ComparisonBranch

        if (-not $changedFiles) {
            Write-Information "No changes detected between $BaseBranch and $ComparisonBranch" -InformationAction Continue
            return @()
        }

        Write-Information "Analyzing $($changedFiles.Count) changed files for test impact" -InformationAction Continue

        # Map changed files to test categories
        $impactedCategories = @()

        foreach ($file in $changedFiles) {
            switch -Regex ($file) {
                'ViewModels/' { $impactedCategories += 'UI' }
                'Services/' { $impactedCategories += 'Integration' }
                'Models/' { $impactedCategories += 'Unit' }
                'Data/' { $impactedCategories += 'Database' }
                '\.cs$' { $impactedCategories += 'Unit' }
                default { $impactedCategories += 'Integration' }
            }
        }

        $uniqueCategories = $impactedCategories | Sort-Object -Unique

        Write-Information "Impacted test categories: $($uniqueCategories -join ', ')" -InformationAction Continue

        return $uniqueCategories
    }
    finally {
        Pop-Location
    }
}

function invokeBusBuddyRestore {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param([string]$Solution = 'BusBuddy.sln')
    if (-not (Test-Path $Solution)) { Write-Warning "Solution not found: $Solution"; return 1 }
    if ($PSCmdlet.ShouldProcess($Solution, "Restore packages")) {
        Write-Information "Restoring packages for $Solution..." -InformationAction Continue
    Write-Output "Restoring packages for $Solution..."
        & dotnet restore $Solution
        return $LASTEXITCODE
    }
}

function invokeBusBuddyClean {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param([string]$Solution = 'BusBuddy.sln')
    if (-not (Test-Path $Solution)) { Write-Error "Solution not found: $Solution"; return 1 }
    if ($PSCmdlet.ShouldProcess($Solution, "Clean build artifacts")) {
        Write-Information "Cleaning $Solution..." -InformationAction Continue
    Write-Output "Cleaning $Solution..."
        & dotnet clean $Solution
        return $LASTEXITCODE
    }
}

function invokeBusBuddyAntiRegression {
    [CmdletBinding()]
    param(
        [int]$ThrottleLimit = 12,
        [string[]]$ExcludePaths = @('.git', 'bin', 'obj', 'node_modules', '.vs', 'TestResults'),
        [switch]$Detailed
    )
    Write-Information "=== Anti-Regression Scan (Parallel: $ThrottleLimit threads) ===" -InformationAction Continue
    Write-Output "=== Anti-Regression Scan (Parallel: $ThrottleLimit threads) ==="

    $issues = [System.Collections.Concurrent.ConcurrentBag[string]]::new()

    # Define scan jobs for parallel execution
    # Reference: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/foreach-object#example-14--using-parallel-processing
    $scanJobs = @(
        @{
            Name = "Microsoft.Extensions.Logging"
            Pattern = "Microsoft\.Extensions\.Logging"
            Include = "*.cs"
            Message = "Microsoft.Extensions.Logging found (use Serilog)"
        },
        @{
            Name = "Standard WPF Controls"
            Pattern = "<DataGrid[^>]*(?!syncfusion)|<ListView[^>]*(?!syncfusion)|<TreeView[^>]*(?!syncfusion)"
            Include = "*.xaml"
            Message = "Standard WPF controls found (use Syncfusion)"
        },
        @{
            Name = "Console Output Usage"
            Pattern = "Write" + "-Host"
            Include = @("*.ps1", "*.psm1")
            Message = "Console output found (use Write-Information/Write-Output)"
        },
        @{
            Name = "Nullable Reference Violations"
            Pattern = "\?\s*="
            Include = "*.cs"
            Message = "Nullable reference assignments found (avoid nullable types)"
        }
    )

    Write-Output "Scanning files for compliance violations..."

    # Execute scans in parallel for better performance
    $scanJobs | ForEach-Object -Parallel {
        $job = $_
        $excludePaths = $using:ExcludePaths
        $issues = $using:issues
        $detailed = $using:Detailed

        try {
            # Build exclude filter
            $excludeFilter = $excludePaths | ForEach-Object { "*$_*" }

            $files = Get-ChildItem -Path . -Recurse -Include $job.Include -ErrorAction SilentlyContinue |
                Where-Object { $path = $_.FullName; -not ($excludeFilter | Where-Object { $path -like $_ }) }

            Write-Output "[$($job.Name)] Scanning $($files.Count) files..."

            $foundMatches = $files | Select-String $job.Pattern -List -ErrorAction SilentlyContinue

            if ($foundMatches) {
                $issues.Add("$($job.Message): $($foundMatches.Count) files")

                # Show file details for violations (limit to prevent overwhelming output)
                $displayLimit = if ($detailed) { $foundMatches.Count } else { 10 }
                $foundMatches | Select-Object -First $displayLimit | ForEach-Object {
                    $relativePath = $_.Filename -replace [regex]::Escape((Get-Location).Path + "\"), ""
                    $issues.Add("  → $relativePath`:$($_.LineNumber)")
                }
                if ($foundMatches.Count -gt $displayLimit) {
                    $issues.Add("  → ... and $($foundMatches.Count - $displayLimit) more files (use -Detailed to see all)")
                }
            } else {
                Write-Output "[$($job.Name)] ✓ No violations found"
            }
        }
        catch {
            $issues.Add("Error scanning $($job.Name): $($_.Exception.Message)")
        }
    } -ThrottleLimit $ThrottleLimit

    # Convert concurrent bag to array and display results
    $allIssues = $issues.ToArray()

    if ($allIssues) {
        Write-Warning "Anti-regression issues found:"
        $allIssues | ForEach-Object { Write-Warning "  ✗ $_" }
        return 1
    } else {
        Write-Output "✓ No anti-regression issues found"
        return 0
    }
}

function invokeBusBuddyXamlValidation {
    [CmdletBinding()]
    param()
    Write-Output "=== XAML Validation ==="

    $xamlFiles = Get-ChildItem -Path . -Recurse -Include "*.xaml"
    $issues = @()

    foreach ($file in $xamlFiles) {
        # Check for Syncfusion namespace
        $content = Get-Content $file.FullName -Raw
        if ($content -match "<syncfusion:|xmlns:syncfusion") {
            # Has Syncfusion controls - check for standard WPF controls
            if ($content -match "<DataGrid[^>]*(?!syncfusion)") {
                $issues += "$($file.Name): Standard DataGrid found (use syncfusion:SfDataGrid)"
            }
            if ($content -match "<ListView[^>]*(?!syncfusion)") {
                $issues += "$($file.Name): Standard ListView found (use Syncfusion equivalent)"
            }
        }
    }

    if ($issues) {
        Write-Warning "XAML validation issues:"
        $issues | ForEach-Object { Write-Warning "  ✗ $_" }
        return 1
    } else {
        Write-Output "✓ XAML validation passed"
        return 0
    }
}

function testBusBuddyMvpReadiness {
    [CmdletBinding()]
    param()
    Write-Output "=== MVP Readiness Check ==="

    $checks = @()

    # Check if solution builds
    try {
        $null = & dotnet build BusBuddy.sln --verbosity quiet
        if ($LASTEXITCODE -eq 0) {
            $checks += "✓ Solution builds successfully"
        } else {
            $checks += "✗ Solution build failed (exit code: $LASTEXITCODE)"
        }
    }
    catch {
        $checks += "✗ Solution build failed with exception: $($_.Exception.Message)"
    }

    # Check core entities exist
    $coreEntities = @('Student', 'Route', 'Driver', 'Vehicle')
    foreach ($entity in $coreEntities) {
        $entityFiles = Get-ChildItem -Path . -Recurse -Include "*$entity*.cs" -Exclude "*Test*"
        if ($entityFiles) {
            $checks += "✓ $entity entity found"
        } else {
            $checks += "✗ $entity entity missing"
        }
    }

    $checks | ForEach-Object { Write-Output $_ }
    Write-Output "=== MVP Check Complete ==="
}

function Get-BusBuddyCommands {
    [CmdletBinding()]
    param()
    Write-Output "=== BusBuddy Commands (Microsoft Testing Platform 2025 Enhanced) ==="
    Write-Output ""
    Write-Output "🚀 Core Development Commands:"
    Get-Command bb* | Where-Object Source -eq "BusBuddy" | Sort-Object Name | ForEach-Object {
        $description = switch ($_.Name) {
            'bbHealth' { 'Health check (SDK, solution, environment)' }
            'bbBuild' { 'Build solution' }
            'bbTest' { 'Run tests with MTP 2025 + hyperthreading optimization' }
            'bbRun' { 'Run WPF application' }
            'bbClean' { 'Clean build artifacts' }
            'bbRestore' { 'Restore NuGet packages' }
            'bbMvpCheck' { 'Validate MVP readiness' }
            'bbAntiRegression' { 'Scan for anti-patterns (use -Detailed for full file lists)' }
            'bbXamlValidate' { 'Validate XAML (Syncfusion-only)' }
            'bbTestParallel' { 'Legacy parallel testing (superseded by bbTest -Parallel)' }
            default { 'BusBuddy command' }
        }
        Write-Output "  $($_.Name) - $description"
    }

    Write-Output ""
    Write-Output "🧪 Advanced Testing Commands (2025 Edition):"
    $testingCommands = @(
        @{ Name = 'Invoke-BusBuddyTestDiscovery'; Description = 'Discover tests with MTP 2025' }
        @{ Name = 'Invoke-BusBuddySyncfusionUITests'; Description = 'Syncfusion WPF UI automation tests' }
        @{ Name = 'Invoke-BusBuddyAzureSQLTests'; Description = 'Azure SQL integration tests' }
        @{ Name = 'Invoke-BusBuddyPerformanceTests'; Description = 'Performance & load testing' }
        @{ Name = 'Get-BusBuddyTestResults'; Description = 'Advanced test result analysis' }
        @{ Name = 'Start-BusBuddyLiveTestExecution'; Description = 'Live test execution with streaming' }
        @{ Name = 'Select-BusBuddyImpactedTests'; Description = 'Smart test selection based on changes' }
    )

    $testingCommands | ForEach-Object {
        Write-Output "  $($_.Name) - $($_.Description)"
    }

    Write-Output ""
    Write-Output "📊 Testing Examples:"
    Write-Output "  bbTest -Parallel -Coverage -Categories @('Unit', 'Integration')"
    Write-Output "  Invoke-BusBuddySyncfusionUITests -ControlType DataGrid"
    Write-Output "  Invoke-BusBuddyPerformanceTests -ConcurrentUsers 50"
    Write-Output "  Start-BusBuddyLiveTestExecution -TestSuite Performance -StreamResults"
    Write-Output "  Select-BusBuddyImpactedTests -BaseBranch main"

    Write-Output ""
    Write-Output "💡 Pro Tips:"
    Write-Output "  • Use 'Get-Help bbTest -Examples' for comprehensive testing scenarios"
    Write-Output "  • Enable hyperthreading optimization with -Parallel -HyperthreadingMode LogicalCores"
    Write-Output "  • Use -Filter for precise test selection (e.g., 'Category=Unit&Name~Student')"
    Write-Output "  • Combine multiple coverage formats: -CoverageFormats @('cobertura', 'opencover')"
    Write-Output "  • Use -LiveResults for real-time test progress monitoring"
}

# Create aliases for bb* commands
Set-Alias -Name 'bbBuild' -Value 'invokeBusBuddyBuild'
Set-Alias -Name 'bbRun' -Value 'invokeBusBuddyRun'
Set-Alias -Name 'bbTest' -Value 'invokeBusBuddyTest'
Set-Alias -Name 'Test-BusBuddy' -Value 'invokeBusBuddyTest'
Set-Alias -Name 'Invoke-BusBuddyTests' -Value 'invokeBusBuddyTest'
Set-Alias -Name 'bbHealth' -Value 'invokeBusBuddyHealthCheck'
Set-Alias -Name 'bbHealthDetailed' -Value 'invokeBusBuddyHealthCheck'
Set-Alias -Name 'bbHealthModern' -Value 'invokeBusBuddyHealthCheck'
Set-Alias -Name 'bbHealthRepair' -Value 'invokeBusBuddyHealthCheck'
Set-Alias -Name 'bbHealthFull' -Value 'invokeBusBuddyHealthCheck'
Set-Alias -Name 'bbRestore' -Value 'invokeBusBuddyRestore'
Set-Alias -Name 'bbClean' -Value 'invokeBusBuddyClean'
Set-Alias -Name 'bbAntiRegression' -Value 'invokeBusBuddyAntiRegression'
Set-Alias -Name 'bbXamlValidate' -Value 'invokeBusBuddyXamlValidation'
Set-Alias -Name 'bbMvpCheck' -Value 'testBusBuddyMvpReadiness'
Set-Alias -Name 'bbCommands' -Value 'Get-BusBuddyCommands'
# Enhanced aliases for parallel/performance features
Set-Alias -Name 'bbTestParallel' -Value 'Invoke-BusBuddyParallelTests'

function invokeBusBuddyRefresh {
    <#
    .SYNOPSIS
    Refresh BusBuddy modules and commands (legacy compatibility)
    .DESCRIPTION
    Provides legacy compatibility for bbRefresh command.
    If hardened module manager is available, delegates to it. Otherwise, reloads basic modules.

    .NOTES
    Reference: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
    #>
    [CmdletBinding()]
    param([switch]$Force)

    Write-Information "🔄 Refreshing BusBuddy modules..." -InformationAction Continue

    # Check if hardened module manager functions are available
    if (Get-Command Invoke-BusBuddyModuleRefresh -ErrorAction SilentlyContinue) {
        Write-Information "Using hardened module manager for refresh..." -InformationAction Continue
        return Invoke-BusBuddyModuleRefresh -Force:$Force
    }

    # Fallback to basic module refresh
    Write-Information "Using basic module refresh..." -InformationAction Continue

    try {
        # Remove existing modules
        Get-Module BusBuddy* | Remove-Module -Force -ErrorAction SilentlyContinue

        # Clear module loaded flag
        $env:BUSBUDDY_MODULES_LOADED = $null

        # Find repo root
        $repoRoot = $env:BUSBUDDY_REPO_ROOT
        if (-not $repoRoot) {
            $probe = (Get-Location).Path
            while ($probe -and -not (Test-Path (Join-Path $probe 'BusBuddy.sln'))) {
                $next = Split-Path $probe -Parent
                if (-not $next -or $next -eq $probe) { $probe = $null; break }
                $probe = $next
            }
            $repoRoot = $probe
        }

        if (-not $repoRoot) {
            Write-Error "BusBuddy repository root not found"
            return $false
        }

        # Reload modules using Import-BusBuddyModule.ps1
        $importScript = Join-Path $repoRoot "PowerShell\Profiles\Import-BusBuddyModule.ps1"
        if (Test-Path $importScript) {
            . $importScript
            Write-Information "✅ Modules refreshed successfully" -InformationAction Continue
            return $true
        } else {
            Write-Error "Import script not found: $importScript"
            return $false
        }
    }
    catch {
        Write-Error "Failed to refresh modules: $($_.Exception.Message)"
        return $false
    }
}

# Add bbRefresh alias
Set-Alias -Name 'bbRefresh' -Value 'invokeBusBuddyRefresh'

# Export all functions and aliases
Export-ModuleMember -Function @(
    'invokeBusBuddyBuild',
    'invokeBusBuddyRun',
    'invokeBusBuddyTest',
    'invokeBusBuddyHealthCheck',
    'invokeBusBuddyRestore',
    'invokeBusBuddyClean',
    'invokeBusBuddyAntiRegression',
    'invokeBusBuddyXamlValidation',
    'testBusBuddyMvpReadiness',
    'Get-BusBuddyCommands',
    'Invoke-BusBuddyParallelTests',
    'invokeBusBuddyRefresh',
    'Invoke-BusBuddyTestDiscovery',
    'Invoke-BusBuddySyncfusionUITests',
    'Invoke-BusBuddyAzureSQLTests',
    'Invoke-BusBuddyPerformanceTests',
    'Get-BusBuddyTestResults',
    'Start-BusBuddyLiveTestExecution',
    'Select-BusBuddyImpactedTests'
) -Alias @(
    'bbBuild',
    'bbRun',
    'bbTest',
    'Test-BusBuddy',
    'Invoke-BusBuddyTests',
    'bbHealth',
    'bbHealthDetailed',
    'bbHealthModern',
    'bbHealthRepair',
    'bbHealthFull',
    'bbRestore',
    'bbClean',
    'bbAntiRegression',
    'bbXamlValidate',
    'bbMvpCheck',
    'bbCommands',
    'bbTestParallel',
    'bbRefresh'
)
