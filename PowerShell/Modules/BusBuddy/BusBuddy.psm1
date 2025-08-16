<#
.SYNOPSIS
Core BusBuddy helper functions for .NET/WPF devops (build, run, test, health).
Standards: PowerShell 7.5+, StrictMode 3.0, no Write-Host, Write-Information logging.
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
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Solution = 'BusBuddy.sln',
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')][string]$Verbosity = 'minimal',
        [int]$TimeoutSeconds = 300,
        [int]$MaxCpuCount = [Environment]::ProcessorCount
    )

    # Enhanced validation with detailed error reporting
    if (-not (Test-Path $Solution)) {
        Write-Warning "Solution file not found: $Solution. Current location: $(Get-Location). Available .sln files: $((Get-ChildItem -Filter '*.sln' -ErrorAction SilentlyContinue).Name -join ', ')"
        return 1
    }

    if ($PSCmdlet.ShouldProcess($Solution, "Build ($Configuration, $Verbosity, max CPU: $MaxCpuCount)")) {
    Write-Output "Building $Solution with $MaxCpuCount CPU cores..."

        try {
            # Use parallel builds and timeout protection
            # Reference: https://learn.microsoft.com/dotnet/core/tools/dotnet-build#options
            $process = Start-Process -FilePath 'dotnet' -ArgumentList @(
                'build', $Solution,
                '--configuration', $Configuration,
                '-v', $Verbosity,
                '--maxcpucount', $MaxCpuCount,
                '--nologo'
            ) -NoNewWindow -Wait -PassThru -RedirectStandardOutput -RedirectStandardError

            # Timeout protection
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                $process.Kill()
                Write-Warning "Build timed out after $TimeoutSeconds seconds"
                return 124  # Standard timeout exit code
            }

            return $process.ExitCode
        }
        catch {
            Write-Warning "Build failed with exception: $($_.Exception.Message)"
            return 1
        }
    }
}

function invokeBusBuddyRun {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Project = 'BusBuddy.csproj',
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
    )
    if (-not (Test-Path $Project)) { Write-Warning "Project not found: $Project"; return 1 }
    if ($PSCmdlet.ShouldProcess($Project, "Run ($Configuration)")) {
    Write-Output "Running $Project..."
        & dotnet run --project $Project --configuration $Configuration
        return $LASTEXITCODE
    }
}

function invokeBusBuddyTest {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Solution = 'BusBuddy.sln',
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
        [string]$Filter = $null,
        [switch]$Coverage,
        [switch]$Parallel,
        [int]$MaxCpuCount = [Environment]::ProcessorCount,
        [int]$TimeoutMinutes = 10
    )
    if (-not (Test-Path $Solution)) { Write-Error "Solution not found: $Solution"; return 1 }
        if (-not (Test-Path $Solution)) { Write-Warning "Solution not found: $Solution"; return 1 }
    if ($PSCmdlet.ShouldProcess($Solution, "Test ($Configuration) with $MaxCpuCount cores")) {
        Write-Output "Testing $Solution$(if($Parallel){' (parallel)'})..."

        $dotnetArgs = @(
            'test', $Solution,
            '--configuration', $Configuration,
            '--verbosity', 'normal',
            '--logger', 'trx',
            '--nologo'
        )

        if ($Filter) { $dotnetArgs += @('--filter', $Filter) }
        if ($Coverage) { $dotnetArgs += @('--collect:"XPlat Code Coverage"') }
        if ($Parallel) {
            $dotnetArgs += @('--maxcpucount', $MaxCpuCount)
            # Enable parallel test execution
            # Reference: https://learn.microsoft.com/dotnet/core/testing/selective-unit-tests
            $dotnetArgs += @('--', 'RunConfiguration.MaxCpuCount=' + $MaxCpuCount)
        }

        try {
            # Add timeout protection for long-running tests
            $process = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput -RedirectStandardError

            if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
                $process.Kill()
                Write-Warning "Tests timed out after $TimeoutMinutes minutes"
                return 124  # Standard timeout exit code
            }

            return $process.ExitCode
        }
        catch {
            Write-Warning "Test execution failed: $($_.Exception.Message)"
            return 1
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
        [int]$TimeoutSeconds = 30
    )
    Write-Output "=== BusBuddy Health Check $(if($Detailed){'(Detailed)'}) ==="

    $healthStatus = @()

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
    Write-Output "✓ PowerShell: $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))"

    # Check BusBuddy solution
    $repoRoot = Resolve-BusBuddyRepoRoot
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
    }

    Write-Output "=== Health Check Complete ==="

    # Return overall health status
    $failures = $healthStatus | Where-Object Status -eq "✗"
    return $failures.Count
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
            Name = "Write-Host Usage"
            Pattern = "Write-Host"
            Include = @("*.ps1", "*.psm1")
            Message = "Write-Host found (use Write-Information/Write-Output)"
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
    Write-Output "=== BusBuddy Commands ==="
    Write-Output "Core Development Commands:"
    Get-Command bb* | Where-Object Source -eq "BusBuddy" | Sort-Object Name | ForEach-Object {
        $description = switch ($_.Name) {
            'bbHealth' { 'Health check (SDK, solution, environment)' }
            'bbBuild' { 'Build solution' }
            'bbTest' { 'Run tests with optional coverage' }
            'bbRun' { 'Run WPF application' }
            'bbClean' { 'Clean build artifacts' }
            'bbRestore' { 'Restore NuGet packages' }
            'bbMvpCheck' { 'Validate MVP readiness' }
            'bbAntiRegression' { 'Scan for anti-patterns (use -Detailed for full file lists)' }
            'bbXamlValidate' { 'Validate XAML (Syncfusion-only)' }
            default { 'BusBuddy command' }
        }
        Write-Output "  $($_.Name) - $description"
    }

    Write-Output "`nCLI Integration Commands:"
    Get-Command bb* | Where-Object Source -eq "BusBuddy.CLI" | Sort-Object Name | ForEach-Object {
        Write-Output "  $($_.Name) - CLI integration command"
    }
}

# Create aliases for bb* commands
Set-Alias -Name 'bbBuild' -Value 'invokeBusBuddyBuild'
Set-Alias -Name 'bbRun' -Value 'invokeBusBuddyRun'
Set-Alias -Name 'bbTest' -Value 'invokeBusBuddyTest'
Set-Alias -Name 'bbHealth' -Value 'invokeBusBuddyHealthCheck'
Set-Alias -Name 'bbRestore' -Value 'invokeBusBuddyRestore'
Set-Alias -Name 'bbClean' -Value 'invokeBusBuddyClean'
Set-Alias -Name 'bbAntiRegression' -Value 'invokeBusBuddyAntiRegression'
Set-Alias -Name 'bbXamlValidate' -Value 'invokeBusBuddyXamlValidation'
Set-Alias -Name 'bbMvpCheck' -Value 'testBusBuddyMvpReadiness'
Set-Alias -Name 'bbCommands' -Value 'Get-BusBuddyCommands'
# Enhanced aliases for parallel/performance features
Set-Alias -Name 'bbTestParallel' -Value 'Invoke-BusBuddyParallelTests'
Set-Alias -Name 'bbHealthDetailed' -Value 'invokeBusBuddyHealthCheck'

Export-ModuleMember -Function invokeBusBuddyBuild, invokeBusBuddyRun, invokeBusBuddyTest, invokeBusBuddyHealthCheck, invokeBusBuddyRestore, invokeBusBuddyClean, invokeBusBuddyAntiRegression, invokeBusBuddyXamlValidation, testBusBuddyMvpReadiness, Get-BusBuddyCommands, Invoke-BusBuddyParallelTests -Alias bbBuild, bbRun, bbTest, bbHealth, bbRestore, bbClean, bbAntiRegression, bbXamlValidate, bbMvpCheck, bbCommands, bbTestParallel, bbHealthDetailed
