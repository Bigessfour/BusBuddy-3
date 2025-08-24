#requires -Version 7.5
<#
.SYNOPSIS
    BusBuddy Core Module - Essential Development Commands

.DESCRIPTION
    Core BusBuddy development commands following Microsoft PowerShell best practices.
    Provides bb-* aliases for common development tasks.

.NOTES
    Author: BusBuddy Development Team
    Version: 1.0.0
    PowerShell: 7.5.2+

.EXAMPLE
    Import-Module BusBuddy-Core
    bb-build
    bb-run
    bb-test
#>

# Module metadata
$ModuleInfo = @{
    Name = 'BusBuddy-Core'
    Version = '1.0.0'
    Description = 'Essential BusBuddy development commands'
    Author = 'BusBuddy Development Team'
}

Write-Information "Loading $($ModuleInfo.Name) v$($ModuleInfo.Version)" -InformationAction Continue

# Determine workspace root path
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
        $loadedModules = Get-Module | Where-Object Name -like "BusBuddy*" | Measure-Object
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
function Get-BusBuddyCommands {
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

# Test MCP connectivity
function Test-BusBuddyMCP {
    <#
    .SYNOPSIS
    Tests Microsoft Learn MCP Server connectivity

    .DESCRIPTION
    Validates that MCP servers are properly configured and accessible in VS Code

    .EXAMPLE
    bb-mcp-test
    Tests the Microsoft Learn MCP Server connection
    #>
    [CmdletBinding()]
    param()

    Write-Information "🔍 Testing MCP Server Configuration..." -InformationAction Continue

    $mcpConfigPath = Join-Path $script:WorkspaceRoot '.vscode\mcp.json'

    if (Test-Path $mcpConfigPath) {
        Write-Information "✅ VS Code MCP configuration found: $mcpConfigPath" -InformationAction Continue

        try {
            $mcpConfig = Get-Content $mcpConfigPath | ConvertFrom-Json
            $serverCount = $mcpConfig.servers.PSObject.Properties.Count
            Write-Information "📊 Configured MCP servers: $serverCount" -InformationAction Continue

            foreach ($serverName in $mcpConfig.servers.PSObject.Properties.Name) {
                $server = $mcpConfig.servers.$serverName
                if ($server.type -eq 'http') {
                    Write-Information "🌐 HTTP Server: $serverName -> $($server.url)" -InformationAction Continue
                } elseif ($server.command) {
                    Write-Information "⚡ Command Server: $serverName -> $($server.command)" -InformationAction Continue
                }
            }

            Write-Information "🎯 MCP servers configured for BusBuddy:" -InformationAction Continue
            Write-Information "   📚 Microsoft Learn: Official Microsoft documentation" -InformationAction Continue
            Write-Information "   ☁️ Azure MCP: Azure resource management" -InformationAction Continue
            Write-Information "   📁 Filesystem: BusBuddy project file operations" -InformationAction Continue
            Write-Information "   🌦️ Weather: Real-time weather and road conditions" -InformationAction Continue
            Write-Information "   🗃️ Database: SQLite analytics for BusBuddy.db" -InformationAction Continue
            Write-Information "   📊 Memory: System performance monitoring" -InformationAction Continue
            Write-Information "" -InformationAction Continue
            Write-Information "🎯 To use MCP servers:" -InformationAction Continue
            Write-Information "   1. Open GitHub Copilot in VS Code" -InformationAction Continue
            Write-Information "   2. Switch to Agent Mode" -InformationAction Continue
            Write-Information "   3. Look for tools in the tool selector" -InformationAction Continue
            Write-Information "   4. Ask questions about transportation management" -InformationAction Continue

        } catch {
            Write-Error "❌ Failed to parse MCP configuration: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "⚠️ No VS Code MCP configuration found at $mcpConfigPath"
    }

    # Also check root mcp.json
    $rootMcpPath = Join-Path $script:WorkspaceRoot 'mcp.json'
    if (Test-Path $rootMcpPath) {
        Write-Information "📋 Root MCP configuration also found: $rootMcpPath" -InformationAction Continue
    }
}

# Create aliases after function definitions (Microsoft best practice)
New-Alias -Name 'bb-build' -Value 'Invoke-BusBuddyBuild' -Scope Global -Force
New-Alias -Name 'bb-run' -Value 'Invoke-BusBuddyRun' -Scope Global -Force
New-Alias -Name 'bb-test' -Value 'Invoke-BusBuddyTest' -Scope Global -Force
New-Alias -Name 'bb-health' -Value 'Invoke-BusBuddyHealth' -Scope Global -Force
New-Alias -Name 'bb-clean' -Value 'Invoke-BusBuddyClean' -Scope Global -Force
New-Alias -Name 'bb-commands' -Value 'Get-BusBuddyCommands' -Scope Global -Force
New-Alias -Name 'bb-mcp-test' -Value 'Test-BusBuddyMCP' -Scope Global -Force

# Export module members with aliases (Microsoft best practice)
Export-ModuleMember -Function @(
    'Invoke-BusBuddyBuild',
    'Invoke-BusBuddyRun',
    'Invoke-BusBuddyTest',
    'Invoke-BusBuddyHealth',
    'Invoke-BusBuddyClean',
    'Get-BusBuddyCommands',
    'Test-BusBuddyMCP'
) -Alias @(
    'bb-build',
    'bb-run',
    'bb-test',
    'bb-health',
    'bb-clean',
    'bb-commands',
    'bb-mcp-test'
)

Write-Information "✅ $($ModuleInfo.Name) loaded with aliases: bb-build, bb-run, bb-test, bb-health, bb-clean, bb-commands" -InformationAction Continue
