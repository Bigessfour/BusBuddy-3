# Enhanced Build Functions - No Truncation Output
# Location: PowerShell/Functions/Build/BuildFunctions.ps1

#region Enhanced Build Functions

function Invoke-BusBuddyBuild {
    <#
    .SYNOPSIS
        Execute dotnet build with complete output capture

    .DESCRIPTION
        Captures full dotnet build output to both console and file, preventing truncation issues

    .PARAMETER ProjectPath
        Path to solution or project file

    .PARAMETER Configuration
        Build configuration (Debug/Release)

    .PARAMETER SaveLog
        Save complete output to timestamped file

    .PARAMETER Clean
        Perform clean before build

    .PARAMETER Restore
        Perform restore before build

    .EXAMPLE
        Invoke-BusBuddyBuild -SaveLog

    .EXAMPLE
        Invoke-BusBuddyBuild -ProjectPath "BusBuddy.WPF\BusBuddy.WPF.csproj" -Clean
    #>
    [CmdletBinding()]
    param(
        [string]$ProjectPath = "BusBuddy.sln",
        [string]$Configuration = "Debug",
        [switch]$SaveLog,
        [switch]$Clean,
        [switch]$Restore
    )

    # Load buffer configuration if not already loaded
    $bufferConfigPath = Join-Path $PSScriptRoot "..\..\Config\BufferConfiguration.ps1"
    if (Test-Path $bufferConfigPath) {
        . $bufferConfigPath
    }

    $arguments = @("build", $ProjectPath, "--configuration", $Configuration, "--verbosity", "detailed")

    if (-not $Restore) {
        $arguments += "--no-restore"
    }

    # Perform clean if requested
    if ($Clean) {
        Write-Host "🧹 Cleaning $ProjectPath first..." -ForegroundColor Yellow
        Invoke-WithFullOutput -Command "dotnet" -Arguments @("clean", $ProjectPath) -LogPrefix "clean"
    }

    # Perform restore if requested
    if ($Restore) {
        Write-Host "📦 Restoring packages for $ProjectPath..." -ForegroundColor Yellow
        Invoke-WithFullOutput -Command "dotnet" -Arguments @("restore", $ProjectPath) -LogPrefix "restore"
    }

    # Execute build with full output capture
    return Invoke-WithFullOutput -Command "dotnet" -Arguments $arguments -SaveLog:$SaveLog -LogPrefix "build"
}

function Invoke-BusBuddyTest {
    <#
    .SYNOPSIS
        Execute dotnet test with complete output capture

    .PARAMETER ProjectPath
        Path to solution or project file

    .PARAMETER SaveLog
        Save complete output to timestamped file

    .PARAMETER Framework
        Target framework for tests

    .PARAMETER Filter
        Test filter expression
    #>
    [CmdletBinding()]
    param(
        [string]$ProjectPath = "BusBuddy.sln",
        [switch]$SaveLog,
        [string]$Framework,
        [string]$Filter
    )

    $arguments = @("test", $ProjectPath, "--verbosity", "detailed", "--no-restore")

    if ($Framework) {
        $arguments += @("--framework", $Framework)
    }

    if ($Filter) {
        $arguments += @("--filter", $Filter)
    }

    return Invoke-WithFullOutput -Command "dotnet" -Arguments $arguments -SaveLog:$SaveLog -LogPrefix "test"
}

function Invoke-BusBuddyRun {
    <#
    .SYNOPSIS
        Execute dotnet run with complete output capture

    .PARAMETER ProjectPath
        Path to project file

    .PARAMETER SaveLog
        Save complete output to timestamped file

    .PARAMETER Arguments
        Arguments to pass to the application
    #>
    [CmdletBinding()]
    param(
        [string]$ProjectPath = "BusBuddy.WPF\BusBuddy.WPF.csproj",
        [switch]$SaveLog,
        [string[]]$Arguments = @()
    )

    $runArgs = @("run", "--project", $ProjectPath, "--no-restore")

    if ($Arguments.Count -gt 0) {
        $runArgs += "--"
        $runArgs += $Arguments
    }

    return Invoke-WithFullOutput -Command "dotnet" -Arguments $runArgs -SaveLog:$SaveLog -LogPrefix "run"
}

function Get-BusBuddyBuildErrors {
    <#
    .SYNOPSIS
        Get only build errors without full output
    #>
    [CmdletBinding()]
    param(
        [string]$ProjectPath = "BusBuddy.sln"
    )

    $result = Invoke-WithFullOutput -Command "dotnet" -Arguments @("build", $ProjectPath, "--verbosity", "quiet") -LogPrefix "errors"

    if ($result -and $result.ErrorLines) {
        return $result.ErrorLines
    } else {
        Write-Host "✅ No build errors found!" -ForegroundColor Green
        return @()
    }
}

function Show-BusBuddyBuildLog {
    <#
    .SYNOPSIS
        Show the most recent build log
    #>
    [CmdletBinding()]
    param(
        [string]$LogType = "build"
    )

    $pattern = "logs\$LogType-*.log"
    $latestLog = Get-ChildItem $pattern -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($latestLog) {
        Write-Host "📄 Most recent $LogType log: $($latestLog.Name)" -ForegroundColor Cyan
        Write-Host "📅 Created: $($latestLog.LastWriteTime)" -ForegroundColor Gray
        Write-Host ""
        Get-Content $latestLog.FullName
    } else {
        Write-Host "No $LogType logs found. Run with -SaveLog first." -ForegroundColor Yellow
    }
}

#endregion

#region VS Code Task Integration

function Invoke-VSCodeTask {
    <#
    .SYNOPSIS
        Execute a VS Code task with full output capture

    .PARAMETER TaskName
        Name of the VS Code task to execute

    .PARAMETER SaveLog
        Save complete output to timestamped file
    #>
    [CmdletBinding()]
    param(
        [string]$TaskName,
        [switch]$SaveLog
    )

    # This would integrate with VS Code tasks if needed
    Write-Host "🔧 VS Code Task execution with full output capture" -ForegroundColor Cyan
    Write-Host "Task: $TaskName" -ForegroundColor Gray

    # For now, this is a placeholder for VS Code task integration
    # In practice, this would call the actual VS Code task runner
}

#endregion

# Export all functions
Export-ModuleMember -Function Invoke-BusBuddyBuild, Invoke-BusBuddyTest, Invoke-BusBuddyRun, Get-BusBuddyBuildErrors, Show-BusBuddyBuildLog, Invoke-VSCodeTask
