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
    return (Get-Location).Path  # Fallback to current dir.
}

function invokeBusBuddyBuild {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Solution = 'BusBuddy.sln',
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')][string]$Verbosity = 'minimal'
    )
    if (-not (Test-Path $Solution)) { Write-Error "Solution not found: $Solution"; return 1 }
    if ($PSCmdlet.ShouldProcess($Solution, "Build ($Configuration, $Verbosity)")) {
        Write-Information "Building $Solution..." -InformationAction Continue
        & dotnet build $Solution --configuration $Configuration -v $Verbosity
        return $LASTEXITCODE
    }
}

function invokeBusBuddyRun {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param([string]$Project)
    if (-not $Project) { $Project = Join-Path (Resolve-BusBuddyRepoRoot) 'BusBuddy.WPF/BusBuddy.WPF.csproj' }
    if ($PSCmdlet.ShouldProcess($Project, 'Run')) {
        Write-Information "Running $Project..." -InformationAction Continue
        & dotnet run --project $Project
        return $LASTEXITCODE
    }
}

function invokeBusBuddyTest {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param([string]$Target = 'BusBuddy.sln')
    if ($PSCmdlet.ShouldProcess($Target, 'Test')) {
        Write-Information "Testing $Target..." -InformationAction Continue
        & dotnet test $Target --no-build
        return $LASTEXITCODE
    }
}

function invokeBusBuddyHealthCheck {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param()
    if ($PSCmdlet.ShouldProcess('Health Check')) {
        $ok = $true
        try {
            & dotnet --info | Out-Null
            if ($LASTEXITCODE -ne 0) { $ok = $false; Write-Warning 'dotnet SDK check failed.' }
            $root = Resolve-BusBuddyRepoRoot
            $sln = Join-Path $root 'BusBuddy.sln'
            if (-not (Test-Path $sln)) { $ok = $false; Write-Warning "Solution not found: $sln" }
            foreach ($var in 'SYNCFUSION_LICENSE_KEY', 'BUSBUDDY_CONNECTION') {
                $value = (Get-Item "Env:$var" -ErrorAction SilentlyContinue).Value
                if ([string]::IsNullOrEmpty($value)) { $ok = $false; Write-Warning "$var not set (required for WPF/Syncfusion or Azure SQL)." }
            }
            Write-Information "Health check: $($ok ? 'Passed' : 'Failed')" -InformationAction Continue
            return ($ok ? 0 : 1)
        } catch {
            Write-Error "Health error: $($_.Exception.Message)"
            return 1
        }
    }
}

function invokeBusBuddyRestore {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param([string]$Solution = 'BusBuddy.sln')
    if ($PSCmdlet.ShouldProcess($Solution, 'Restore')) {
        Write-Information "Restoring $Solution..." -InformationAction Continue
        & dotnet restore $Solution
        return $LASTEXITCODE
    }
}

function invokeBusBuddyClean {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param([string]$Path)
    if (-not $Path) { $Path = Resolve-BusBuddyRepoRoot }
    if ($PSCmdlet.ShouldProcess($Path, 'Clean')) {
        Write-Information "Cleaning $Path..." -InformationAction Continue
        & dotnet clean (Join-Path $Path 'BusBuddy.sln')
        foreach ($dir in 'bin', 'obj') {
            Get-ChildItem $Path -Recurse -Directory -Filter $dir | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
        return $LASTEXITCODE
    }
}

function invokeBusBuddyAntiRegression {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param([string]$Path)
    if (-not $Path) { $Path = Resolve-BusBuddyRepoRoot }
    if ($PSCmdlet.ShouldProcess($Path, 'Anti-Regression Scan')) {
        Write-Information "Scanning $Path for regressions..." -InformationAction Continue
        $violations = Select-String -Path (Get-ChildItem $Path -Recurse -File) -Pattern 'Write-Host|\<DataGrid'  # Example patterns
        if ($violations) { Write-Error "Violations found: $violations"; return 1 }
        Write-Information 'Scan passed.' -InformationAction Continue
        return 0
    }
}

function invokeBusBuddyXamlValidation {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param([string]$Path)
    if (-not $Path) { $Path = Resolve-BusBuddyRepoRoot }
    if ($PSCmdlet.ShouldProcess($Path, 'XAML Validation')) {
        Write-Information "Validating XAML in $Path..." -InformationAction Continue
        $bad = Select-String -Path (Get-ChildItem $Path -Recurse *.xaml) -Pattern '\<DataGrid'
        if ($bad) { Write-Error "Invalid XAML: $bad"; return 1 }
        Write-Information 'Validation passed.' -InformationAction Continue
        return 0
    }
}

function testBusBuddyMvpReadiness {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param()
    if ($PSCmdlet.ShouldProcess('MVP Readiness Tests')) {
        $proj = Join-Path (Resolve-BusBuddyRepoRoot) 'BusBuddy.Tests/BusBuddy.Tests.csproj'
        if (Test-Path $proj) {
            Write-Information 'Running MVP tests...' -InformationAction Continue
            & dotnet test $proj --no-build
            return $LASTEXITCODE
        } else {
            Write-Warning "Test project not found: $proj"
            return 1
        }
    }
}

Export-ModuleMember -Function invokeBusBuddyBuild, invokeBusBuddyRun, invokeBusBuddyTest, invokeBusBuddyHealthCheck, invokeBusBuddyRestore, invokeBusBuddyClean, invokeBusBuddyAntiRegression, invokeBusBuddyXamlValidation, testBusBuddyMvpReadiness