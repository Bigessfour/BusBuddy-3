# BusBuddy.Build.psm1
# PowerShell module for build operations
#requires -Version 7.5
[CmdletBinding()]
param()
# Module-level logging configuration
$ModuleName = "BusBuddy.Build"
$LogPath = Join-Path $PSScriptRoot "../../logs/build-module.log"
# Ensure logs directory exists
$LogDir = Split-Path $LogPath -Parent
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}
#region Logging Functions
function Write-ModuleLog {
    <#
    .SYNOPSIS
    Writes structured log messages for the build module
    .PARAMETER Message
    The log message to write
    .PARAMETER Level
    The log level (Information, Warning, Error, Debug)
    .PARAMETER FunctionName
    The name of the function writing the log
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [Parameter()]
        [ValidateSet('Information', 'Warning', 'Error', 'Debug', 'Verbose')]
        [string]$Level = 'Information',
        [Parameter()]
        [string]$FunctionName = (Get-PSCallStack)[1].FunctionName
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] [$ModuleName::$FunctionName] $Message"
    # Write to appropriate stream based on level
    switch ($Level) {
        'Information' {
            Write-Information $Message -InformationAction Continue
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        'Warning' {
            Write-Warning $Message
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        'Error' {
            Write-Error $Message
            Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
        }
        'Debug' {
            Write-Debug $Message
            if ($DebugPreference -ne 'SilentlyContinue') {
                Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
            }
        }
        'Verbose' {
            Write-Verbose $Message
            if ($VerbosePreference -ne 'SilentlyContinue') {
                Add-Content -Path $LogPath -Value $logEntry -Encoding UTF8
            }
        }
    }
}
function Start-BuildOperation {
    <#
    .SYNOPSIS
    Starts a build operation with proper logging
    .PARAMETER OperationName
    Name of the build operation
    .PARAMETER ProjectPath
    Path to the project being built
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$OperationName,
        [Parameter()]
        [string]$ProjectPath = $PWD
    )

    if ($PSCmdlet.ShouldProcess($ProjectPath, "Start build operation: $OperationName")) {
        Write-ModuleLog -Message "Starting build operation: $OperationName" -Level Information
        Write-ModuleLog -Message "Project path: $ProjectPath" -Level Debug
        return [PSCustomObject]@{
            OperationName = $OperationName
            StartTime = Get-Date
            ProjectPath = $ProjectPath
        }
    }
}
function Complete-BuildOperation {
    <#
    .SYNOPSIS
    Completes a build operation with timing and result logging
    .PARAMETER Operation
    The operation object returned from Start-BuildOperation
    .PARAMETER Success
    Whether the operation was successful
    .PARAMETER ErrorMessage
    Error message if the operation failed
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Operation,
        [Parameter()]
        [bool]$Success = $true,
        [Parameter()]
        [string]$ErrorMessage
    )
    $duration = (Get-Date) - $Operation.StartTime
    $durationText = "{0:F2} seconds" -f $duration.TotalSeconds
    if ($Success) {
        Write-ModuleLog -Message "Build operation '$($Operation.OperationName)' completed successfully in $durationText" -Level Information
    } else {
        Write-ModuleLog -Message "Build operation '$($Operation.OperationName)' failed after $durationText" -Level Error
        if ($ErrorMessage) {
            Write-ModuleLog -Message "Error details: $ErrorMessage" -Level Error
        }
    }
}
#endregion
#region Example Build Functions
function Invoke-BusBuddyBuild {
    <#
    .SYNOPSIS
    Builds the BusBuddy solution with comprehensive logging
    .PARAMETER Configuration
    Build configuration (Debug or Release)
    .PARAMETER SolutionPath
    Path to the solution file
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',
        [Parameter()]
        [string]$SolutionPath = "BusBuddy.sln"
    )

    if (-not $PSCmdlet.ShouldProcess($SolutionPath, "Build solution with configuration: $Configuration")) {
        return $false
    }

    $operation = Start-BuildOperation -OperationName "Solution Build" -ProjectPath $SolutionPath -WhatIf:$false
    try {
        Write-ModuleLog -Message "Building solution with configuration: $Configuration" -Level Information
        # Validate solution file exists
        if (-not (Test-Path $SolutionPath)) {
            throw "Solution file not found: $SolutionPath"
        }
        # Execute build command
        $buildArgs = @('build', $SolutionPath, '--configuration', $Configuration, '--verbosity', 'minimal')
        Write-ModuleLog -Message "Executing: dotnet $($buildArgs -join ' ')" -Level Debug

        & dotnet @buildArgs
        if ($LASTEXITCODE -eq 0) {
            Write-ModuleLog -Message "Build completed successfully" -Level Information
            Complete-BuildOperation -Operation $operation -Success $true
            return $true
        } else {
            throw "Build failed with exit code: $LASTEXITCODE"
        }
    }
    catch {
        Write-ModuleLog -Message $_.Exception.Message -Level Error
        Complete-BuildOperation -Operation $operation -Success $false -ErrorMessage $_.Exception.Message
        return $false
    }
}
function Test-BuildHealth {
    <#
    .SYNOPSIS
    Performs health checks on the build environment
    #>
    [CmdletBinding()]
    param()
    $operation = Start-BuildOperation -OperationName "Build Health Check"
    try {
        Write-ModuleLog -Message "Checking build environment health" -Level Information
        # Check .NET SDK
        $dotnetVersion = & dotnet --version
        Write-ModuleLog -Message ".NET SDK Version: $dotnetVersion" -Level Information
        # Check solution file
        if (Test-Path "BusBuddy.sln") {
            Write-ModuleLog -Message "Solution file found: BusBuddy.sln" -Level Information
        } else {
            Write-ModuleLog -Message "Solution file not found in current directory" -Level Warning
        }
        # Check NuGet packages
        Write-ModuleLog -Message "Checking package restore status" -Level Debug
        Complete-BuildOperation -Operation $operation -Success $true
        Write-ModuleLog -Message "Build environment health check completed" -Level Information
        return $true
    }
    catch {
        Write-ModuleLog -Message $_.Exception.Message -Level Error
        Complete-BuildOperation -Operation $operation -Success $false -ErrorMessage $_.Exception.Message
        return $false
    }
}
#endregion
# Initialize module
Write-ModuleLog -Message "BusBuddy.Build module loaded successfully" -Level Information
# Export module members
Export-ModuleMember -Function @(
    'Write-ModuleLog',
    'Start-BuildOperation',
    'Complete-BuildOperation',
    'Invoke-BusBuddyBuild',
    'Test-BuildHealth'
)
