#requires -Version 7.5
<#
.SYNOPSIS
BusBuddy Core Module - Essential Development Commands

.DESCRIPTION
Core BusBuddy development commands following Microsoft PowerShell best practices.
Provides bb-* aliases for common development tasks.

.NOTES
Author: BusBuddy Development Team
Version: 1.0.1
PowerShell: 7.5.2+
#>

# Module metadata
$ModuleInfo = @{
    Name = 'BusBuddy-Core'
    Version = '1.0.1'
    Description = 'Essential BusBuddy development commands'
    Author = 'BusBuddy Development Team'
}

Write-Information "Loading $($ModuleInfo.Name) v$($ModuleInfo.Version)" -InformationAction Continue

# Determine workspace root path
$WorkspaceRoot = if ($env:BUSBUDDY_WORKSPACE) {
    $env:BUSBUDDY_WORKSPACE
} elseif (Test-Path "$PSScriptRoot\..\..\BusBuddy.sln") {
    Split-Path $PSScriptRoot -Parent | Split-Path -Parent
} else {
    $PWD.Path
}

<#
.SYNOPSIS
Build the BusBuddy solution

.DESCRIPTION
Builds the BusBuddy solution using dotnet build with optimized settings

.EXAMPLE
bb-build
#>
function Invoke-BusBuddyBuild {
    [CmdletBinding()]
    param()

    try {
        Write-Information "Building BusBuddy solution..." -InformationAction Continue
        Set-Location $WorkspaceRoot
        $result = dotnet build BusBuddy.sln --configuration Debug --verbosity minimal
        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Build completed successfully" -InformationAction Continue
        } else {
            Write-Error "❌ Build failed with exit code $LASTEXITCODE"
        }
        return $result
    }
    catch {
        Write-Error "Build error: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
Run the BusBuddy application

.DESCRIPTION
Runs the BusBuddy WPF application using dotnet run

.EXAMPLE
bb-run
#>
function Invoke-BusBuddyRun {
    [CmdletBinding()]
    param()

    try {
        Write-Information "Starting BusBuddy application..." -InformationAction Continue
        Set-Location $WorkspaceRoot
        dotnet run --project BusBuddy.WPF\BusBuddy.WPF.csproj
    }
    catch {
        Write-Error "Run error: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
Run tests for the BusBuddy solution

.DESCRIPTION
Runs all tests in the BusBuddy solution using dotnet test

.EXAMPLE
bb-test
#>
function Invoke-BusBuddyTest {
    [CmdletBinding()]
    param()

    try {
        Write-Information "Running BusBuddy tests..." -InformationAction Continue
        Set-Location $WorkspaceRoot
        dotnet test BusBuddy.sln --configuration Debug --verbosity minimal
    }
    catch {
        Write-Error "Test error: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
Perform health check on the BusBuddy environment

.DESCRIPTION
Checks the health of the BusBuddy development environment

.EXAMPLE
bb-health
#>
function Invoke-BusBuddyHealth {
    [CmdletBinding()]
    param()

    try {
        Write-Information "Performing BusBuddy health check..." -InformationAction Continue

        # Check .NET version
        $dotnetVersion = dotnet --version
        Write-Information "✅ .NET Version: $dotnetVersion" -InformationAction Continue

        # Check solution file
        if (Test-Path "$WorkspaceRoot\BusBuddy.sln") {
            Write-Information "✅ Solution file found" -InformationAction Continue
        } else {
            Write-Warning "❌ Solution file not found"
        }

        # Check Azure authentication
        if (Get-Command Get-AzContext -ErrorAction SilentlyContinue) {
            $azContext = Get-AzContext
            if ($azContext) {
                Write-Information "✅ Azure PowerShell authenticated" -InformationAction Continue
            } else {
                Write-Warning "❌ Azure PowerShell not authenticated"
            }
        }

        # Check Syncfusion license
        if ($env:SYNCFUSION_LICENSE_KEY) {
            Write-Information "✅ Syncfusion license configured" -InformationAction Continue
        } else {
            Write-Warning "❌ Syncfusion license not configured"
        }

    }
    catch {
        Write-Error "Health check error: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
Clean the BusBuddy solution

.DESCRIPTION
Cleans the BusBuddy solution using dotnet clean

.EXAMPLE
bb-clean
#>
function Invoke-BusBuddyClean {
    [CmdletBinding()]
    param()

    try {
        Write-Information "Cleaning BusBuddy solution..." -InformationAction Continue
        Set-Location $WorkspaceRoot
        dotnet clean BusBuddy.sln --verbosity minimal
        Write-Information "✅ Clean completed" -InformationAction Continue
    }
    catch {
        Write-Error "Clean error: $($_.Exception.Message)"
    }
}

<#
.SYNOPSIS
Show available BusBuddy commands

.DESCRIPTION
Lists all available bb-* commands in the BusBuddy-Core module

.EXAMPLE
bb-commands
#>
function Get-BusBuddyCommand {
    [CmdletBinding()]
    param()

    Write-Information "BusBuddy Core Commands:" -InformationAction Continue
    Write-Information "  bb-build    - Build the solution" -InformationAction Continue
    Write-Information "  bb-run      - Run the application" -InformationAction Continue
    Write-Information "  bb-test     - Run tests" -InformationAction Continue
    Write-Information "  bb-health   - Health check" -InformationAction Continue
    Write-Information "  bb-clean    - Clean solution" -InformationAction Continue
    Write-Information "  bb-commands - Show this help" -InformationAction Continue
    Write-Information "  bb-sql-test - Test SQL connections" -InformationAction Continue
}

<#
.SYNOPSIS
Test SQL database connections

.DESCRIPTION
Tests both LocalDB and Azure SQL connections

.EXAMPLE
bb-sql-test
#>
function Test-BusBuddySqlConnection {
    [CmdletBinding()]
    param()

    Write-Information "Testing BusBuddy SQL connections..." -InformationAction Continue

    # Test LocalDB
    try {
        # Check if LocalDB is installed
        $localDbInstances = & "C:\Program Files\Microsoft SQL Server\*\Tools\Binn\SqlLocalDB.exe" info 2>$null
        if (-not $localDbInstances) {
            Write-Warning "❌ LocalDB not installed. Install SQL Server Express with LocalDB feature."
            Write-Information "   Download: https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -InformationAction Continue
        } else {
            $localDbResult = Invoke-Sqlcmd -ServerInstance "(localdb)\MSSQLLocalDB" -Database "BusBuddy" -Query "SELECT @@VERSION" -ErrorAction Stop
            Write-Information "✅ LocalDB connection successful" -InformationAction Continue
        }
    }
    catch {
        Write-Warning "❌ LocalDB connection failed: $($_.Exception.Message)"
        Write-Information "   Try: sqllocaldb start MSSQLLocalDB" -InformationAction Continue
    }

    # Test Azure SQL if authenticated
    try {
        if (Get-Command Get-AzAccessToken -ErrorAction SilentlyContinue) {
            # Check if we have a valid Azure context
            $azContext = Get-AzContext
            if (-not $azContext) {
                Write-Warning "❌ Azure PowerShell not authenticated. Run: Connect-AzAccount -AuthScope https://database.windows.net/"
                return
            }

            # Try to get database-scoped token
            try {
                $token = Get-AzAccessToken -ResourceUrl "https://database.windows.net/" -ErrorAction Stop
                if ($token -and $token.Token) {
                    $azureSqlResult = Invoke-Sqlcmd -ServerInstance "busbuddy-server-sm2.database.windows.net" -Database "BusBuddyDB" -Query "SELECT @@VERSION AS 'Azure SQL Version'" -AccessToken $token.Token -ErrorAction Stop
                    Write-Information "✅ Azure SQL connection successful" -InformationAction Continue
                    Write-Information "   Azure SQL Version: $($azureSqlResult.'Azure SQL Version')" -InformationAction Continue
                } else {
                    Write-Warning "❌ Failed to get Azure SQL access token"
                }
            }
            catch {
                Write-Warning "❌ Azure SQL authentication failed. Run: Connect-AzAccount -AuthScope https://database.windows.net/"
                Write-Information "   Or try: az login --scope https://database.windows.net//.default" -InformationAction Continue
            }
        } else {
            Write-Warning "❌ Azure PowerShell module not available. Install: Install-Module Az"
        }
    }
    catch {
        Write-Warning "❌ Azure SQL connection failed: $($_.Exception.Message)"
    }
}

# Create aliases
New-Alias -Name "bb-build" -Value "Invoke-BusBuddyBuild" -Force
New-Alias -Name "bb-run" -Value "Invoke-BusBuddyRun" -Force
New-Alias -Name "bb-test" -Value "Invoke-BusBuddyTest" -Force
New-Alias -Name "bb-health" -Value "Invoke-BusBuddyHealth" -Force
New-Alias -Name "bb-clean" -Value "Invoke-BusBuddyClean" -Force
New-Alias -Name "bb-commands" -Value "Get-BusBuddyCommands" -Force
New-Alias -Name "bb-sql-test" -Value "Test-BusBuddySqlConnection" -Force

# Export functions and aliases
Export-ModuleMember -Function @(
    'Invoke-BusBuddyBuild',
    'Invoke-BusBuddyRun',
    'Invoke-BusBuddyTest',
    'Invoke-BusBuddyHealth',
    'Invoke-BusBuddyClean',
    'Get-BusBuddyCommands',
    'Test-BusBuddySqlConnection'
) -Alias @(
    'bb-build',
    'bb-run',
    'bb-test',
    'bb-health',
    'bb-clean',
    'bb-commands',
    'bb-sql-test'
)

Write-Information "✅ BusBuddy-Core loaded with aliases: bb-build, bb-run, bb-test, bb-health, bb-clean, bb-commands, bb-sql-test" -InformationAction Continue
${2:Long description}

.EXAMPLE
${3:An example}

.NOTES
${4:General notes}
#>
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
function Get-BusBuddyWorkspaceRoot {
    if ($PWD.Path -like "*BusBuddy*") {
        return $PWD.Path.Split('BusBuddy')[0] + 'BusBuddy'
    } else {
        return 'C:\Users\biges\Desktop\BusBuddy'
    }
}

$script:WorkspaceRoot = Get-BusBuddyWorkspaceRoot

# Core build function
function Invoke-BusBuddyBuild {
    <#
    .SYNOPSIS
    Builds the BusBuddy solution

    .DESCRIPTION
    Executes dotnet build on the BusBuddy solution with optimized settings

    .EXAMPLE
    Invoke-BusBuddyBuild
    bb-build
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🔨 Building BusBuddy solution..." -InformationAction Continue

        Push-Location $script:WorkspaceRoot

        $buildArgs = @(
            'build'
            'BusBuddy.sln'
            '--configuration'
            'Debug'
            '--verbosity'
            'minimal'
        )

        $env:BUSBUDDY_NO_WELCOME = '1'
        $env:BUSBUDDY_NO_XAI_WARN = '1'
        $env:BUSBUDDY_SILENT = '1'
        $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

        & dotnet @buildArgs

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ Build completed successfully" -InformationAction Continue
        } else {
            Write-Error "❌ Build failed with exit code $LASTEXITCODE"
        }

        return $LASTEXITCODE

    } catch {
        Write-Error "Build error: $($_.Exception.Message)"
        throw
    } finally {
        Pop-Location
    }
}

# Core run function
function Invoke-BusBuddyRun {
    <#
    .SYNOPSIS
    Runs the BusBuddy WPF application

    .DESCRIPTION
    Executes dotnet run on the BusBuddy WPF project

    .EXAMPLE
    Invoke-BusBuddyRun
    bb-run
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🚀 Starting BusBuddy application..." -InformationAction Continue

        Push-Location $script:WorkspaceRoot

        $runArgs = @(
            'run'
            '--project'
            'BusBuddy.WPF/BusBuddy.WPF.csproj'
        )

        $env:BUSBUDDY_NO_WELCOME = '1'

        & dotnet @runArgs

        return $LASTEXITCODE

    } catch {
        Write-Error "Run error: $($_.Exception.Message)"
        throw
    } finally {
        Pop-Location
    }
}

# Core test function
function Invoke-BusBuddyTest {
    <#
    .SYNOPSIS
    Runs BusBuddy unit tests

    .DESCRIPTION
    Executes dotnet test on the BusBuddy test projects

    .EXAMPLE
    Invoke-BusBuddyTest
    bb-test
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🧪 Running BusBuddy tests..." -InformationAction Continue

        Push-Location $script:WorkspaceRoot

        $testArgs = @(
            'test'
            'BusBuddy.sln'
            '--configuration'
            'Debug'
            '--verbosity'
            'normal'
            '--logger'
            'trx'
        )

        & dotnet @testArgs

        if ($LASTEXITCODE -eq 0) {
            Write-Information "✅ All tests passed" -InformationAction Continue
        } else {
            Write-Warning "⚠️ Some tests failed (exit code: $LASTEXITCODE)"
        }

        return $LASTEXITCODE

    } catch {
        Write-Error "Test error: $($_.Exception.Message)"
        throw
    } finally {
        Pop-Location
    }
}

# Health check function
function Invoke-BusBuddyHealth {
    <#
    .SYNOPSIS
    Performs BusBuddy system health check

    .DESCRIPTION
    Checks system dependencies, build status, and configuration

    .EXAMPLE
    Invoke-BusBuddyHealth
    bb-health
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🏥 BusBuddy Health Check" -InformationAction Continue
        Write-Information "========================" -InformationAction Continue

        # Check .NET SDK
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion) {
            Write-Information "✅ .NET SDK: $dotnetVersion" -InformationAction Continue
        } else {
            Write-Warning "❌ .NET SDK not found"
        }

        # Check workspace
        if (Test-Path (Join-Path $script:WorkspaceRoot 'BusBuddy.sln')) {
            Write-Information "✅ Workspace: $script:WorkspaceRoot" -InformationAction Continue
        } else {
            Write-Warning "❌ BusBuddy solution not found"
        }

        # Check PowerShell version
        Write-Information "✅ PowerShell: $($PSVersionTable.PSVersion)" -InformationAction Continue

        # Check loaded modules
        $loadedModules = Get-Module | Where-Object Name -Like "BusBuddy*" | Measure-Object
        Write-Information "✅ BusBuddy Modules: $($loadedModules.Count) loaded" -InformationAction Continue

        Write-Information "========================" -InformationAction Continue
        Write-Information "🎯 Health check complete" -InformationAction Continue

    } catch {
        Write-Error "Health check error: $($_.Exception.Message)"
        throw
    }
}

# Clean function
function Invoke-BusBuddyClean {
    <#
    .SYNOPSIS
    Cleans BusBuddy build artifacts

    .DESCRIPTION
    Executes dotnet clean and removes bin/obj directories

    .EXAMPLE
    Invoke-BusBuddyClean
    bb-clean
    #>

    [CmdletBinding()]
    param()

    try {
        Write-Information "🧹 Cleaning BusBuddy solution..." -InformationAction Continue

        Push-Location $script:WorkspaceRoot

        # dotnet clean
        & dotnet clean BusBuddy.sln --verbosity minimal

        # Remove bin/obj directories
        Get-ChildItem -Path '.\*\bin', '.\*\obj' -Recurse -Directory -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

        Write-Information "✅ Clean completed" -InformationAction Continue

    } catch {
        Write-Error "Clean error: $($_.Exception.Message)"
        throw
    } finally {
        Pop-Location
    }
}

# Commands listing function
function Get-BusBuddyCommand {
    <#
    .SYNOPSIS
    Lists all available BusBuddy commands

    .DESCRIPTION
    Displays all bb-* commands available in the current session

    .EXAMPLE
    Get-BusBuddyCommands
    bb-commands
    #>

    [CmdletBinding()]
    param()

    Write-Information "🚌 Available BusBuddy Commands:" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    $commands = Get-Command bb-* | Sort-Object Name

    if ($commands) {
        foreach ($cmd in $commands) {
            $source = if ($cmd.Source) { " ($($cmd.Source))" } else { "" }
            Write-Information "  $($cmd.Name)$source" -InformationAction Continue
        }
    } else {
        Write-Warning "No bb-* commands found. Profile may not be loaded properly."
    }

    Write-Information "" -InformationAction Continue
    Write-Information "Use 'Get-Help <command>' for detailed information about each command." -InformationAction Continue
}

# Create aliases after function definitions (Microsoft best practice)
New-Alias -Name 'bb-build' -Value 'Invoke-BusBuddyBuild' -Scope Global -Force
New-Alias -Name 'bb-run' -Value 'Invoke-BusBuddyRun' -Scope Global -Force
New-Alias -Name 'bb-test' -Value 'Invoke-BusBuddyTest' -Scope Global -Force
New-Alias -Name 'bb-health' -Value 'Invoke-BusBuddyHealth' -Scope Global -Force
New-Alias -Name 'bb-clean' -Value 'Invoke-BusBuddyClean' -Scope Global -Force
New-Alias -Name 'bb-commands' -Value 'Get-BusBuddyCommands' -Scope Global -Force

# Export module members with aliases (Microsoft best practice)
Export-ModuleMember -Function @(
    'Invoke-BusBuddyBuild',
    'Invoke-BusBuddyRun',
    'Invoke-BusBuddyTest',
    'Invoke-BusBuddyHealth',
    'Invoke-BusBuddyClean',
    'Get-BusBuddyCommands'
) -Alias @(
    'bb-build',
    'bb-run',
    'bb-test',
    'bb-health',
    'bb-clean',
    'bb-commands'
)

Write-Information "✅ $($ModuleInfo.Name) loaded with aliases: bb-build, bb-run, bb-test, bb-health, bb-clean, bb-commands" -InformationAction Continue
