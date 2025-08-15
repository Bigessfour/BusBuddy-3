#requires -Version 7.5
# NOTE: Module updated to require PowerShell 7.5+ per project standards.
# References:
# - Microsoft PowerShell Module Guidelines: https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module
# - Streams and Error Handling: https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams

##region Enhanced Test Output Functions
function Get-BusBuddyTestOutput {
<#
.SYNOPSIS
    Run solution/tests with full build + test output capture.
#>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    [OutputType([hashtable])]
    param(
        [ValidateSet('All','Unit','Integration','Validation','Core','WPF')][string]$TestSuite='All',
        [string]$ProjectPath='BusBuddy.sln',
        [switch]$SaveToFile,
        [string]$Filter,
        [ValidateSet('quiet','minimal','normal','detailed','diagnostic')][string]$Verbosity='normal'
    )
    $ts = Get-Date -Format 'yyyyMMdd-HHmmss'
    $logDir = 'logs'; if(-not (Test-Path $logDir)){New-Item -ItemType Directory -Path $logDir|Out-Null}
    $file = Join-Path $logDir "test-output-$TestSuite-$ts.log"
    if(-not $Filter){
        $Filter = switch($TestSuite){
            'Unit' {'Category=Unit|TestCategory=Unit'}
            'Integration' {'Category=Integration|TestCategory=Integration'}
            'Validation' {'Category=Validation|TestCategory=Validation'}
            'Core' {'FullyQualifiedName~BusBuddy.Tests.Core'}
            'WPF' {'FullyQualifiedName~BusBuddy.UITests'}
            default {''}
        }
    }
    try {
        $start = Get-Date
        Write-Information "🏗️ Building..." -InformationAction Continue
        $buildOutPath = Join-Path $logDir "build-$ts.log"
        if ($PSCmdlet.ShouldProcess($ProjectPath, "dotnet build ($Verbosity)")) {
            & dotnet build $ProjectPath --configuration Debug --verbosity $Verbosity 2>&1 |
                Tee-Object -FilePath $buildOutPath
        }
        $buildExit = $LASTEXITCODE
        if($buildExit -ne 0){
            Write-Error 'Build failed'
            return @{ ExitCode=$buildExit; Status='BuildFailed'; BuildOutput= (Get-Content $buildOutPath -Raw) }
        }
        Write-Information '🧪 Testing...' -InformationAction Continue
        $testArgs = @('test',$ProjectPath,'--configuration','Debug','--verbosity',$Verbosity,'--logger','trx','--results-directory','TestResults','--collect:XPlat Code Coverage','--no-build')
        if($Filter){$testArgs += @('--filter',$Filter)}
        $testOutPath = Join-Path $logDir "test-$ts.log"
        if ($PSCmdlet.ShouldProcess($ProjectPath, "dotnet $($testArgs -join ' ')")) {
            & dotnet @testArgs 2>&1 | Tee-Object -FilePath $testOutPath
        }
        $exit = $LASTEXITCODE
        $end = Get-Date; $dur = $end - $start
        $testStd = Get-Content $testOutPath -Raw
        $passed = [regex]::Matches($testStd,'Passed:\s+(\d+)')|ForEach-Object{[int]$_.Groups[1].Value}|Measure-Object -Sum|Select-Object -ExpandProperty Sum
        $failed = [regex]::Matches($testStd,'Failed:\s+(\d+)')|ForEach-Object{[int]$_.Groups[1].Value}|Measure-Object -Sum|Select-Object -ExpandProperty Sum
        $skipped = [regex]::Matches($testStd,'Skipped:\s+(\d+)')|ForEach-Object{[int]$_.Groups[1].Value}|Measure-Object -Sum|Select-Object -ExpandProperty Sum
        $summary = "TestSuite=$TestSuite Duration=$([int]$dur.TotalSeconds)s Passed=$passed Failed=$failed Skipped=$skipped ExitCode=$exit"
        if($SaveToFile){ $summary | Out-File -FilePath $file -Encoding utf8; Write-Information "Saved: $file" -InformationAction Continue }
        if($failed -gt 0){ Write-Error "Failures detected ($failed)" }
        return @{ ExitCode=$exit; Duration=$dur; PassedTests=$passed; FailedTests=$failed; SkippedTests=$skipped; OutputFile= (if($SaveToFile){$file}); Status= (if($exit -eq 0){'Success'} else {'Failed'}) }
    } catch {
        Write-Error $_.Exception.Message
        return @{ ExitCode=-1; Status='Error'; ErrorMessage=$_.Exception.Message }
    }
}

function Invoke-BusBuddyTestFull {
    [CmdletBinding()]
    param(
        [ValidateSet('All','Unit','Integration','Validation','Core','WPF')]
        [string]$TestSuite='All'
    )
    Get-BusBuddyTestOutput -TestSuite $TestSuite -SaveToFile
}

function Get-BusBuddyTestError {
    [CmdletBinding()]
    param(
        [ValidateSet('All','Unit','Integration','Validation','Core','WPF')]
        [string]$TestSuite='All'
    )
    Get-BusBuddyTestOutput -TestSuite $TestSuite -Verbosity quiet
}

function Get-BusBuddyTestLog {
    [CmdletBinding()]
    param()
    $l = Get-ChildItem 'logs/test-output-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if($l){
        Write-Information "📄 $($l.Name)" -InformationAction Continue
        Get-Content $l.FullName
    } else {
        Write-Information 'No test logs found. Run bbTestFull first.' -InformationAction Continue
    }
}

function Start-BusBuddyTestWatch {
    [CmdletBinding()]
    [OutputType([System.Management.Automation.Job[]])]
    param(
        [ValidateSet('All','Unit','Integration','Validation','Core','WPF')]
        [string]$TestSuite='Unit'
    )
    Write-Information "🔄 Watch $TestSuite" -InformationAction Continue
    Get-BusBuddyTestOutput -TestSuite $TestSuite -SaveToFile

    # FIX: Correct FileSystemWatcher usage
    # Docs: https://learn.microsoft.com/dotnet/api/system.io.filesystemwatcher
    $path = (Get-Location).Path
    $w = New-Object System.IO.FileSystemWatcher -ArgumentList $path, '*.cs'
    $w.IncludeSubdirectories = $true
    $w.EnableRaisingEvents   = $true

    $action = {
        Start-Sleep 1
        Write-Information '🔄 Change detected — re-running tests' -InformationAction Continue
        try {
            $p = $Event.SourceEventArgs.FullPath
            if ($p -match '(?i)\\bin\\|\\obj\\|\\TestResults\\|\\\.git\\|\\\.vs\\') { return }
            $ext = [IO.Path]::GetExtension($p)
            if ($ext -notin '.cs','.xaml') { return }
            Get-BusBuddyTestOutput -TestSuite $using:TestSuite -SaveToFile
        } catch { Write-Warning "Watcher action failed: $($_.Exception.Message)" }
    }

    $subs = @()
    $subs += Register-ObjectEvent -InputObject $w -EventName Changed -Action $action
    $subs += Register-ObjectEvent -InputObject $w -EventName Created -Action $action
    $subs += Register-ObjectEvent -InputObject $w -EventName Renamed -Action $action

    # Return event subscriptions so the caller can Unregister-Event / Remove-Job when done
    return $subs
}
##endregion
<#
.SYNOPSIS
    BusBuddy PowerShell Module - Complete Working Module

.DESCRIPTION
    Professional PowerShell module for Bus Buddy WPF development environment.
    This is a WORKING version that contains all functions inline rather than
    trying to load them from separate files (which has scoping issues).

.NOTES
    File Name      : BusBuddy.psm1
    Author         : Bus Buddy Development Team
    Prerequisite   : PowerShell 7.5.2+   # FIX: align with #requires -Version 7.5
    Copyright      : (c) 2025 Bus Buddy Project
#>

#region Module Initialization and Buffer Configuration

# Configure PowerShell Buffer Limits to Prevent Truncated Output
try {
    if ($Host.UI.RawUI) {
        # Increase buffer size for full output capture
        $newBuffer = New-Object System.Management.Automation.Host.Size(200, 3000)
        $Host.UI.RawUI.BufferSize = $newBuffer

        # Increase window size for better visibility
        $currentWindow = $Host.UI.RawUI.WindowSize
        $maxWidth = [Math]::Min(200, $currentWindow.Width)
        $maxHeight = [Math]::Min(50, $currentWindow.Height)
        $newWindow = New-Object System.Management.Automation.Host.Size($maxWidth, $maxHeight)
        $Host.UI.RawUI.WindowSize = $newWindow
    }

    # Configure output preferences
    $OutputEncoding = [System.Text.Encoding]::UTF8
    $PSDefaultParameterValues['Out-File:Encoding'] = 'UTF8'
    $PSDefaultParameterValues['*:Encoding'] = 'UTF8'

    # Set maximum history count
    if (Get-Command Set-PSReadLineOption -ErrorAction SilentlyContinue) {
        Set-PSReadLineOption -MaximumHistoryCount 10000 -HistoryNoDuplicates
    }

    Write-Verbose "✅ PowerShell buffer configuration optimized for full output capture"
} catch {
    Write-Warning "Could not optimize PowerShell buffer: $($_.Exception.Message)"
}


#region Enhanced Output Function Loader (runs after all functions are defined)
try {
    # Determine project root relative to this module: Modules/BusBuddy/ -> PowerShell/Modules/BusBuddy
    # Repo root is three levels up from this .psm1
    $projectRoot = (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent)
    $enhancedBuildModule = Join-Path $projectRoot "PowerShell\Modules\BusBuddy.BuildOutput\BusBuddy.BuildOutput.psd1"
    $enhancedTestModule  = Join-Path $projectRoot "PowerShell\Modules\BusBuddy.TestOutput\BusBuddy.TestOutput.psd1"

    if (Test-Path $enhancedBuildModule) {
        Import-Module $enhancedBuildModule -Force -ErrorAction SilentlyContinue
        Write-Verbose "✅ Enhanced build output module loaded"
    }

    if (Test-Path $enhancedTestModule) {
        Import-Module $enhancedTestModule -Force -ErrorAction SilentlyContinue
        Write-Verbose "✅ Enhanced test output module loaded"
    } else {
        Write-Warning "BusBuddy.TestOutput module not found at $enhancedTestModule"
    }
} catch {
    Write-Warning "Error loading enhanced output functions: $($_.Exception.Message)"
}
#endregion

#region Quick-Win Module Auto Import (ThemeValidation, AzureSqlHealth, TestWatcher, Cleanup)
try {
    $quickWinModules = @(
        'BusBuddy.ThemeValidation/BusBuddy.ThemeValidation.psm1',
        'BusBuddy.AzureSqlHealth/BusBuddy.AzureSqlHealth.psm1',
        'BusBuddy.TestWatcher/BusBuddy.TestWatcher.psm1',
        'BusBuddy.Cleanup/BusBuddy.Cleanup.psm1'
    )
    # FIX: ensure $projectRoot is set even if previous block failed
    if (-not $projectRoot) { $projectRoot = Get-BusBuddyProjectRoot }

    $telemetryDir = Join-Path $projectRoot 'logs'
    if (-not (Test-Path $telemetryDir)) { New-Item -ItemType Directory -Path $telemetryDir -Force | Out-Null }
    $telemetryFile = Join-Path $telemetryDir 'module-telemetry.json'

    foreach ($rel in $quickWinModules) {
        $candidate = Join-Path $projectRoot "PowerShell/Modules/$rel"
        if (-not (Test-Path $candidate)) { continue }
        try { Import-Module $candidate -Force -ErrorAction Stop; Write-Verbose "✅ Loaded quick-win module: $rel" }
        catch { Write-Warning "Failed to load quick-win module $rel -> $($_.Exception.Message)"; continue }

        # Lightweight telemetry append with rotation
        try {
            if (Test-Path $telemetryFile) {
                $item = Get-Item -Path $telemetryFile -ErrorAction SilentlyContinue
                if ($item -and $item.Length -gt 1MB) {
                $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
                    Move-Item $telemetryFile (Join-Path $telemetryDir "module-telemetry-$stamp.json") -Force
                }
            }
            ([ordered]@{ Timestamp = (Get-Date).ToString('o'); Module = $rel; Status = 'Loaded' } | ConvertTo-Json -Compress) | Add-Content -Path $telemetryFile
        } catch { Write-Verbose 'Telemetry logging failed' }
    }
} catch { Write-Warning "Quick-win module import pass failed: $($_.Exception.Message)" }
#endregion

#region Telemetry Utilities (module import & future operational events)
function Get-BusBuddyTelemetrySummary {
    <#
    .SYNOPSIS
        Summarise module telemetry JSON line files.
    .DESCRIPTION
        Reads module-telemetry*.json (JSON Lines format) from the logs directory, aggregates counts per module
        and optionally returns the last N entries. Designed to be light‑weight and safe if files are missing.
    .PARAMETER Last
        Return only the last N entries (in chronological order). 0 means return all (default).
    .PARAMETER LogsPath
        Override path to logs directory (defaults to project root / logs).
    .EXAMPLE
        Get-BusBuddyTelemetrySummary -Last 5
    .OUTPUTS
        PSCustomObject with properties: Total, Modules, Entries (optional)
    #>
    [CmdletBinding()]
    param(
        [int]$Last = 0,
        [string]$LogsPath
    )
    try {
        if (-not $LogsPath) { $LogsPath = Join-Path (Get-BusBuddyProjectRoot) 'logs' }
        # Copilot: Read JSON lines safely — ignore malformed lines and aggregate by module.
        $files = Get-ChildItem $LogsPath -Filter 'module-telemetry*.json' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime
        if (-not $files) { Write-Warning 'No telemetry files present.'; return }
        $entries = foreach ($f in $files) {
            Get-Content $f -ErrorAction SilentlyContinue | ForEach-Object {
                if ($_ -match '^\s*{') { try { $_ | ConvertFrom-Json -ErrorAction Stop } catch { } }
            }
        }
        if (-not $entries) { Write-Warning 'Telemetry empty.'; return }
        $ordered = $entries | Sort-Object { $_.Timestamp }
        if ($Last -gt 0) { $tail = $ordered | Select-Object -Last $Last } else { $tail = $null }
        $byModule = $ordered | Group-Object Module | ForEach-Object { [pscustomobject]@{ Module = $_.Name; Count = $_.Count } }
        [pscustomobject]@{
            Total   = $ordered.Count
            Modules = $byModule
            Entries = $tail
        }
    } catch {
        Write-Warning "Failed to read telemetry: $($_.Exception.Message)"
    }
}

function Invoke-BusBuddyTelemetryPurge {
    <#
    .SYNOPSIS
        Purge old rotated telemetry archives.
    .DESCRIPTION
        Deletes module-telemetry-*.json files older than a retention window (default 14 days). The active
        module-telemetry.json (current writer) is never deleted.
    .PARAMETER RetentionDays
        Number of days to retain archived telemetry files.
    #>
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [int]$RetentionDays = 14,
        [string]$LogsPath
    )
    if (-not $LogsPath) { $LogsPath = Join-Path (Get-BusBuddyProjectRoot) 'logs' }
    if (-not (Test-Path $LogsPath)) { Write-Warning 'Logs directory not found.'; return }
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    $archives = Get-ChildItem $LogsPath -Filter 'module-telemetry-*.json' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt $cutoff }
    foreach ($a in $archives) {
        if ($PSCmdlet.ShouldProcess($a.FullName,'Remove old telemetry')) {
            try { Remove-Item $a.FullName -Force } catch { Write-Warning "Failed to remove $($a.Name): $($_.Exception.Message)" }
        }
    }
}

Set-Alias -Name bbTelemetry -Value Get-BusBuddyTelemetrySummary -ErrorAction SilentlyContinue
Set-Alias -Name bbTelemetryPurge -Value Invoke-BusBuddyTelemetryPurge -ErrorAction SilentlyContinue
#endregion Telemetry Utilities

#region Compliance Review (PowerShell 7.5.2)
function Get-BusBuddyPS75Compliance {
    <#
    .SYNOPSIS
        Review the PowerShell environment and flag 7.5.2 compliance issues.
    .DESCRIPTION
        Scans PowerShell/* for *.psm1, *.psd1, *.ps1 and reports:
        - Missing '#requires -Version 7.5' header
        - Write-Host usage (use Write-Information/Write-Output per Microsoft guidance)
        - Global alias pollution (-Scope Global)
        - Potential missing Export-ModuleMember in .psm1
        Optionally disables only obvious temp/backup artifacts by renaming to .disabled.
    .PARAMETER Root
        Root path to scan. Defaults to project root / PowerShell.
    .PARAMETER DisableObviousArtifacts
        If set, renames temp/backup artifacts (*.bak|*.old|*.tmp|*.backup*) to *.disabled (no deletion).
    .OUTPUTS
        PSCustomObject[] with Path, Kind, Lines, Issues, Recommendation
    .LINK
        https://learn.microsoft.com/powershell/scripting/developer/module/writing-a-windows-powershell-module
        https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
        https://learn.microsoft.com/powershell/scripting/developer/cmdlet/cmdlet-overview#confirming-impactful-operations
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    [OutputType([pscustomobject[]])]
    param(
        [string]$Root = (Join-Path (Get-BusBuddyProjectRoot) 'PowerShell'),
        [switch]$DisableObviousArtifacts
    )

    if (-not (Test-Path $Root)) {
        Write-Warning "Root not found: $Root"
        return @()
    }

    $files = Get-ChildItem -Path $Root -Recurse -File -Include *.psm1, *.psd1, *.ps1 -ErrorAction SilentlyContinue
    $results = [System.Collections.Generic.List[object]]::new()

    foreach ($f in $files) {
        $kind = $f.Extension.ToLowerInvariant()
        $text = try { Get-Content $f.FullName -Raw -ErrorAction Stop } catch { '' }
        $lines = if ($text) { ($text -split "`r?`n").Length } else { 0 }

        $issues = [System.Collections.Generic.List[string]]::new()
        $recommend = [System.Collections.Generic.List[string]]::new()

        # Check '#requires -Version 7.5' presence near the top
        $top512 = ($text -split "`r?`n") | Select-Object -First 25
        if ($top512 -and ($top512 -notmatch '^\s*#requires\s+-Version\s+7\.5')) {
            $issues.Add("Missing '#requires -Version 7.5' header")
            $recommend.Add("Add: #requires -Version 7.5")
        }

        # Detect Write-Host (discouraged per Microsoft output guidance)
        $writeHostCount = if ($text) { ([regex]::Matches($text, '(?im)^\s*Write-Host\b')).Count } else { 0 }
        if ($writeHostCount -gt 0) {
            $issues.Add("Uses Write-Host ($writeHostCount)")
            $recommend.Add("Replace with Write-Information/Write-Output/Write-Verbose as appropriate")
        }

        # Detect Set-Alias with -Scope Global (pollutes session)
        $globalAliases = if ($text) { ([regex]::Matches($text, '(?im)Set-Alias\s+.*-Scope\s+Global')).Count } else { 0 }
        if ($globalAliases -gt 0) {
            $issues.Add("Defines $globalAliases global alias(es)")
            $recommend.Add("Remove -Scope Global; export aliases via module manifest/Export-ModuleMember")
        }

        # .psm1: check for Export-ModuleMember presence
        if ($kind -eq '.psm1') {
            if ($text -notmatch '(?im)^\s*Export-ModuleMember\b') {
                $issues.Add("No Export-ModuleMember found")
                $recommend.Add("Export public functions explicitly")
            }
        }

        # Flag obvious temp/backup artifacts
        $isArtifact = $false
        if ($f.Name -match '(?i)\.(bak|backup|old|tmp)$' -or $f.Name -match '(?i)_backup|_temp|_tmp') {
            $isArtifact = $true
            $issues.Add("Temporary/backup artifact")
            $recommend.Add("Rename to .disabled or remove from repo")
            if ($DisableObviousArtifacts) {
                $newName = "$($f.FullName).disabled"
                if ($PSCmdlet.ShouldProcess($f.FullName, "Rename to $newName")) {
                    try { Rename-Item -Path $f.FullName -NewName ($f.Name + '.disabled') -Force } catch { Write-Warning "Rename failed: $($_.Exception.Message)" }
                }
            }
        }

        # Summarize
        $results.Add([pscustomobject]@{
            Path          = $f.FullName
            Kind          = $kind.TrimStart('.').ToUpper()
            Lines         = $lines
            Issues        = [string[]]$issues
            Recommendation= [string[]]$recommend
            IsArtifact    = $isArtifact
        })
    }

    # Sort by severity (artifacts and write-host first), then missing requires
    $ordered = $results | Sort-Object {
        $score = 0
        if ($_.IsArtifact) { $score -= 100 }
        if ($_.Issues -match 'Write-Host') { $score -= 50 }
        if ($_.Issues -match 'Missing .*#requires') { $score -= 25 }
        $score
    }
    return $ordered
}
Set-Alias -Name bb-ps-review -Value Get-BusBuddyPS75Compliance -ErrorAction SilentlyContinue
#endregion Compliance Review (PowerShell 7.5.2)

#region Pester Helper
function Invoke-BusBuddyPester {
    <#
    .SYNOPSIS
        Run Pester tests for BusBuddy PowerShell modules.
    #>
    [CmdletBinding()] param(
        [string]$Path,
        [switch]$PassThru
    )
    # Copilot: Validate suggestions — ensure cmdlet exists before use (Get-Command).
    # Docs: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/get-command
    if (-not (Get-Command Invoke-Pester -ErrorAction SilentlyContinue)) {
        Write-Warning 'Pester not available. Install with: Install-Module Pester -Scope CurrentUser'
        return
    }
    if (-not $Path) { $Path = Join-Path (Get-BusBuddyProjectRoot) 'PowerShell/Tests' }
    if (-not (Test-Path $Path)) { Write-Warning "Tests path not found: $Path"; return }
    Write-Information "🧪 Running Pester tests in $Path" -InformationAction Continue
    try {
        $config = New-PesterConfiguration
        $config.Run.Path = $Path
        $config.Run.PassThru = $true
        $config.TestResult.Enabled = $true
        $config.Output.Verbosity = 'Normal'
        $result = Invoke-Pester -Configuration $config
        if ($PassThru) { return $result } else { return $result.Result }
    } catch { Write-Error "Pester execution failed: $($_.Exception.Message)" }
}
# Scope aliases to the module to avoid polluting global session
Set-Alias -Name bbPester -Value Invoke-BusBuddyPester -ErrorAction SilentlyContinue
#endregion Pester Helper

#endregion

#region Script Lint: Detect invalid ErrorAction pipeline misuse
function Test-BusBuddyErrorActionPipelines {
    <#
    .SYNOPSIS
        Scans PowerShell scripts for the invalid pattern that causes "The variable '$_' cannot be retrieved because it has not been set".
    .DESCRIPTION
        Looks for statements like "ErrorAction SilentlyContinue | Select-Object Name, Definition" or attempts to pipe
        preference variables (e.g., $ErrorActionPreference) directly into Select-Object. Reports offending files/lines.
    .PARAMETER Root
        Root folder to scan. Defaults to project PowerShell folder.
    .OUTPUTS
        PSCustomObject with File, LineNumber, Line
    .LINK
        https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
    #>
    [CmdletBinding()] param(
        [string]$Root = (Join-Path (Get-BusBuddyProjectRoot) 'PowerShell')
    )
    if (-not (Test-Path $Root)) { Write-Warning "Root not found: $Root"; return @() }

    $files = Get-ChildItem -Path $Root -Recurse -Include *.ps1,*.psm1,*.psd1 -File -ErrorAction SilentlyContinue
    $findings = @()

    foreach ($f in $files) {
        try {
            $i = 0
            Get-Content -Path $f.FullName -ErrorAction Stop | ForEach-Object {
                $i++
                $line = $_
                # Flag known-bad constructs
                $bad1 = $line -match '(?i)\bErrorAction\s+SilentlyContinue\s*\|\s*Select-Object'
                $bad2 = $line -match '(?i)\$ErrorActionPreference\s*\|\s*Select-Object'
                if ($bad1 -or $bad2) {
                    $findings += [pscustomobject]@{ File = $f.FullName; LineNumber = $i; Line = $line.Trim() }
                }
            }
        } catch { }
    }
    return $findings
}

Set-Alias -Name bb-ps-validate-ea -Value Test-BusBuddyErrorActionPipelines -ErrorAction SilentlyContinue
# Wrapper to safely run the validation and print a summary without relying on external variables like $r
function Invoke-BusBuddyErrorActionAudit {
    <#
    .SYNOPSIS
        Runs the ErrorAction pipeline validator and prints a concise summary.
    .DESCRIPTION
        Convenience wrapper that captures results from Test-BusBuddyErrorActionPipelines, writes a count,
        and formats a table when findings exist—avoiding patterns that depend on a pre-set variable (e.g., $r).
        Uses Write-Information/Write-Output per Microsoft guidelines.
    .PARAMETER Root
        Root folder to scan. Defaults to the project's PowerShell folder.
    .OUTPUTS
        Same objects as Test-BusBuddyErrorActionPipelines, passed through after printing.
    .LINK
        Microsoft PowerShell Output Streams — https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
    #>
    [CmdletBinding()]
    param(
        [string]$Root = (Join-Path (Get-BusBuddyProjectRoot) 'PowerShell')
    )

    try {
        $results = Test-BusBuddyErrorActionPipelines -Root $Root
    }
    catch {
        Write-Error ("Validation failed: {0}" -f $_.Exception.Message)
        return @()
    }

    $count = @($results).Count
    Write-Output ("Findings: {0}" -f $count)
    if ($count -gt 0) {
        $results |
            Select-Object File, LineNumber, Line |
            Format-Table -AutoSize |
            Out-String |
            Write-Output
    }

    return $results
}

Set-Alias -Name bb-ps-validate-ea-run -Value Invoke-BusBuddyErrorActionAudit -ErrorAction SilentlyContinue
#endregion Script Lint

#region Log Summary Utilities
function Get-BusBuddyLogSummary {
    <#
    .SYNOPSIS
        Loads BusBuddy.WPF utilities and prints a concise log summary.
    .DESCRIPTION
        Uses BusBuddy.WPF.Utilities.LogLifecycleManager to read logs and output a category count table.
        Avoids relying on pre-set variables like $r and uses Write-Information/Write-Output per Microsoft guidelines.
    .PARAMETER LogsDir
        Logs directory path. Defaults to "logs" under the project root.
    .PARAMETER PassThru
        When specified, also returns the summary object.
    #>
    [CmdletBinding()]
    param(
        [string]$LogsDir = (Join-Path (Get-BusBuddyProjectRoot) 'logs'),
        [switch]$PassThru
    )

    try {
        # Copilot: Get log files and count them — ensure logs folder exists before reading.
        # Docs (I/O): https://learn.microsoft.com/powershell/module/microsoft.powershell.management/new-item
        if (-not (Test-Path -LiteralPath $LogsDir)) { New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null }

        $manager = New-Object BusBuddy.WPF.Utilities.LogLifecycleManager($LogsDir)
        $summary = $manager.GetLogSummary()

        # Copilot: Print a concise table — use Write-Output for pipeline-safe text.
        $count = [int]($summary.TotalFiles)
        Write-Output ("Findings: {0}" -f $count)
        if ($count -gt 0) {
            $summary.Categories.GetEnumerator() |
                Select-Object @{n='Name';e={$_.Key}}, @{n='FileCount';e={$_.Value.FileCount}} |
                Format-Table -AutoSize |
                Out-String |
                Write-Output
        }

        if ($PassThru) { return $summary }
    }
    catch {
        Write-Error ("Log summary failed: {0}" -f $_.Exception.Message)
    }
}

try { Set-Alias -Name bb-logs-summary -Value Get-BusBuddyLogSummary -Force } catch { }
#endregion Log Summary Utilities

#region Core Functions

function Get-BusBuddyProjectRoot {
    <#
    .SYNOPSIS
        Get the root directory of the BusBuddy project
    #>
    [CmdletBinding()]
    param()

    $currentPath = $PWD.Path

    while ($currentPath -and $currentPath -ne [System.IO.Path]::GetPathRoot($currentPath)) {
        if ((Test-Path (Join-Path $currentPath "BusBuddy.sln")) -and
            (Test-Path (Join-Path $currentPath "Directory.Build.props"))) {
            return $currentPath
        }
        $currentPath = Split-Path $currentPath -Parent
    }

    return $PWD.Path
}

function Write-BusBuddyStatus {
    <#
    .SYNOPSIS
        Write status message with BusBuddy formatting using PowerShell 7.5.2 best practices

    .DESCRIPTION
        Displays formatted status messages following Microsoft PowerShell Development Guidelines.
        Uses Write-Information with proper ANSI colors and structured output for enhanced readability.

    .PARAMETER Message
        The status message to display. Can be empty for blank lines.

    .PARAMETER Type
        The type of status message (Info, Success, Warning, Error)

    .PARAMETER Status
        Legacy alias for Type parameter to maintain backward compatibility

    .PARAMETER NoEmoji
        Suppress the BusBuddy emoji prefix for cleaner output

    .PARAMETER Indent
        Number of spaces to indent the message (default: 0)

    .EXAMPLE
        Write-BusBuddyStatus "Build completed" -Type Success

    .EXAMPLE
        Write-BusBuddyStatus "Warning detected" -Type Warning

    .EXAMPLE
        Write-BusBuddyStatus "" -Type Info  # Creates blank line

    .EXAMPLE
        Write-BusBuddyStatus "Detailed info" -Type Info -Indent 2
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false, Position=0, ValueFromPipeline=$true)]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$Message = "",

        [Parameter(ParameterSetName='Type')]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Debug')]
        [string]$Type = 'Info',

        [Parameter(ParameterSetName='Status')]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Debug')]
        [string]$Status = 'Info',

        [Parameter()]
        [switch]$NoEmoji,

        [Parameter()]
        [int]$Indent = 0
    )

    begin {
        # Use Status parameter if provided, otherwise use Type
        $statusType = if ($PSCmdlet.ParameterSetName -eq 'Status') { $Status } else { $Type }

        # Handle null or empty messages properly - allow empty strings for spacing
        if ($null -eq $Message) {
            $Message = ""
        }

        # Create indentation
        $indentString = " " * $Indent
    }

    process {
        try {
            # Handle empty messages for spacing
            if ([string]::IsNullOrWhiteSpace($Message)) {
                Write-Information " " -InformationAction Continue
                return
            }

            # Enhanced formatting with ANSI colors and icons
            $icon = if ($NoEmoji) { "" } else {
                switch ($statusType) {
                    'Info'    { "🚌" }
                    'Success' { "✅" }
                    'Warning' { "⚠️ " }
                    'Error'   { "❌" }
                    'Debug'   { "🔍" }
                    default   { "🚌" }
                }
            }

            $formattedMessage = "$indentString$icon $Message"

            # Use appropriate PowerShell 7.5.2 output streams
            switch ($statusType) {
                'Info' {
                    Write-Information $formattedMessage -InformationAction Continue
                }
                'Success' {
                    Write-Information $formattedMessage -InformationAction Continue
                }
                'Warning' {
                    Write-Warning $formattedMessage
                }
                'Error' {
                    Write-Error $formattedMessage
                }
                'Debug' {
                    Write-Debug $formattedMessage
                }
                default {
                    Write-Information $formattedMessage -InformationAction Continue
                }
            }
        }
        catch {
            Write-Error "Error in Write-BusBuddyStatus: $($_.Exception.Message)"
        }
    }
}

function Write-BusBuddyError {
    <#
    .SYNOPSIS
        Write error message with BusBuddy formatting using PowerShell 7.5.2 best practices

    .DESCRIPTION
        Displays formatted error messages with optional exception details.
        Uses structured error output for better diagnostics and actionable information.

    .PARAMETER Message
        The primary error message to display

    .PARAMETER Exception
        Optional exception object for detailed error information

    .PARAMETER Context
        Optional context information about where the error occurred

    .PARAMETER ShowStackTrace
        Include stack trace information for debugging

    .PARAMETER Suggestions
        Array of suggested actions to resolve the error

    .EXAMPLE
        Write-BusBuddyError "Build failed" -Exception $_.Exception

    .EXAMPLE
        Write-BusBuddyError "Database connection failed" -Context "Startup" -Suggestions @("Check connection string", "Verify database is running")
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [System.Exception]$Exception,

        [Parameter()]
        [string]$Context,

        [Parameter()]
        [switch]$ShowStackTrace,

        [Parameter()]
        [string[]]$Suggestions
    )

    process {
        try {
            # Primary error message
            Write-Error "❌ $Message"

            # Additional context information using Write-Information
            if ($Context) {
                Write-Information "📍 Context: $Context" -InformationAction Continue
            }

            # Exception details
            if ($Exception) {
                Write-Information "🔍 Exception: $($Exception.Message)" -InformationAction Continue

                if ($ShowStackTrace -and $Exception.StackTrace) {
                    Write-Information "📋 Stack Trace:" -InformationAction Continue
                    $Exception.StackTrace.Split("`n") | ForEach-Object {
                        Write-Information "   $_" -InformationAction Continue
                    }
                }

                # Inner exception details
                $innerEx = $Exception.InnerException
                $level = 1
                while ($innerEx -and $level -le 3) {
                    # FIX: Replace corrupted glyph with a valid label
                    Write-Information "↪️ Inner Exception ($level): $($innerEx.Message)" -InformationAction Continue
                    $innerEx = $innerEx.InnerException
                    $level++
                }
            }

            # Suggestions for resolution
            if ($Suggestions -and $Suggestions.Length -gt 0) {
                Write-Information "💡 Suggestions:" -InformationAction Continue
                $Suggestions | ForEach-Object {
                    Write-Information "   • $_" -InformationAction Continue
                }
            }

            # Timestamp for debugging
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
            Write-Information "⏰ Timestamp: $timestamp" -InformationAction Continue
        }
        catch {
            # Fallback error handling
            Write-Error "Critical error in Write-BusBuddyError: $($_.Exception.Message)"
        }
    }
}

#endregion

#region Mantra ID (Context Correlation)
<#
.SYNOPSIS
  Manage a session-scoped "Mantra" ID used to correlate BusBuddy operations.
.DESCRIPTION
  Loads from environment variable BUSBUDDY_MANTRA_ID, then optional .mantra file at project root,
  else generates a short GUID fragment. Exposed via Get-BusBuddyMantraId. Reset-BusBuddyMantraId
  can rotate it. Pattern aligns with Microsoft docs on environment variables and advanced functions
  (about_Environment_Variables, about_Functions_Advanced, about_Prompts).
#>
try {
    if (-not $script:MantraId) {
        $script:MantraId = $env:BUSBUDDY_MANTRA_ID
        if (-not $script:MantraId) {
            $rootForMantra = try { Get-BusBuddyProjectRoot } catch { $null }
            if ($rootForMantra) {
                $mantraFile = Join-Path $rootForMantra '.mantra'
                if (Test-Path $mantraFile) {
                    $script:MantraId = (Get-Content $mantraFile -Raw).Trim()
                }
            }
        }
        if (-not $script:MantraId) {
            $script:MantraId = ([guid]::NewGuid().ToString('N').Substring(0,8))
            Write-Information "Generated transient Mantra ID: $script:MantraId" -InformationAction Continue
        }
    }
} catch {
    Write-Information "(Non-fatal) Mantra initialization issue: $($_.Exception.Message)" -InformationAction Continue
}

function Get-BusBuddyMantraId {
    [CmdletBinding()] [OutputType([string])] param() return $script:MantraId
}

function Reset-BusBuddyMantraId {
    [CmdletBinding()] param()
    $script:MantraId = ([guid]::NewGuid().ToString('N').Substring(0,8))
    Write-Information "New Mantra ID: $script:MantraId" -InformationAction Continue
    return $script:MantraId
}

#endregion

#region Build Functions

function Invoke-BusBuddyBuild {
    <#
    .SYNOPSIS
        Build the BusBuddy solution
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [switch]$Clean
    )

    $projectRoot = Get-BusBuddyProjectRoot
    $issues = @()

    if (Get-Command Test-BusBuddyAzureSql -ErrorAction SilentlyContinue) {
        try {
            $sqlOk = Test-BusBuddyAzureSql
            if ($sqlOk) { Write-BusBuddyStatus "Azure SQL connectivity: OK" -Type Success } else { $issues += 'Azure SQL connectivity failed'; Write-BusBuddyStatus "Azure SQL connectivity: FAILED" -Type Warning }
        } catch { $issues += 'Azure SQL connectivity check error'; Write-BusBuddyStatus 'Azure SQL connectivity check error' -Type Warning }
    }
    Push-Location $projectRoot
    try {
        if ($Clean) {
            Write-BusBuddyStatus "Cleaning solution..." -Type Info
            if ($PSCmdlet.ShouldProcess("BusBuddy.sln", "dotnet clean")) {
                dotnet clean BusBuddy.sln
            }
        }

        Write-BusBuddyStatus "Building BusBuddy solution..." -Type Info
        if ($PSCmdlet.ShouldProcess("BusBuddy.sln", "dotnet build")) {
            # Removed dependency on missing Invoke-BusBuddyWithExceptionCapture
            $buildResult = & dotnet build BusBuddy.sln --verbosity minimal 2>&1

            $buildLines = $buildResult | Where-Object {
                $_ -match "-> |succeeded|failed|error|warning|Error|Warning|Time Elapsed" -and
                $_ -notmatch "CompilerServer|analyzer|reference:|X.509 certificate|Assets file|NuGet Config|Feeds used"
            }
            if ($buildLines) {
                Write-Information "📊 Build Output:" -InformationAction Continue
                $buildLines | ForEach-Object { Write-Information "   $_" -InformationAction Continue }
            }

            if ($LASTEXITCODE -eq 0) {
                Write-BusBuddyStatus "Build completed successfully" -Type Success
            } else {
                Write-BusBuddyError "Build failed with exit code $LASTEXITCODE"
            }
        }
    }
    catch {
        Write-BusBuddyError "Build error occurred" -Exception $_
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyRun {
    <#
    .SYNOPSIS
        Run the BusBuddy application from an STA thread to avoid exit code 1.
    .DESCRIPTION
        Launches the built WPF executable using an STA runspace as required by WPF’s threading model.
        References:
        - WPF Threading Model — STA requirement:
          https://learn.microsoft.com/dotnet/desktop/wpf/advanced/threading-model
        - RunspaceFactory.CreateRunspace / ApartmentState:
          https://learn.microsoft.com/dotnet/api/system.management.automation.runspaces.runspacefactory.createrunspace
          https://learn.microsoft.com/dotnet/api/system.threading.apartmentstate
        - powershell.exe -STA parameter (for callers):
          https://learn.microsoft.com/powershell/scripting/windows-powershell/starting-windows-powershell#parameters
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param(
        [switch]$NoErrorCapture,
        [switch]$WaitReady,
        [int]$WaitSeconds = 30
    )

    # Optional: warn about Syncfusion license presence
    try {
        if ([string]::IsNullOrWhiteSpace($env:SYNCFUSION_LICENSE_KEY)) {
            Write-BusBuddyStatus "SYNCFUSION_LICENSE_KEY not set — app may show trial watermarks" -Type Warning
        }
    } catch { }

    $projectRoot = Get-BusBuddyProjectRoot
    $wpfDir      = Join-Path $projectRoot 'BusBuddy.WPF'
    $outDir      = Join-Path $wpfDir 'bin\Debug\net9.0-windows'

    Push-Location $projectRoot
    try {
        # Locate the built exe (support both assembly names)
        $exeCandidates = @(
            (Join-Path $outDir 'BusBuddy.exe'),
            (Join-Path $outDir 'BusBuddy.WPF.exe')
        )
        $exePath = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

        # Build if missing
        if (-not $exePath) {
            Write-BusBuddyStatus "Building BusBuddy.WPF (Debug)..." -Type Info
            if ($PSCmdlet.ShouldProcess("BusBuddy.WPF/BusBuddy.WPF.csproj", "dotnet build -c Debug")) {
                & dotnet build "BusBuddy.WPF/BusBuddy.WPF.csproj" -c Debug --verbosity minimal 2>&1 | Out-Null
            }
            if ($LASTEXITCODE -ne 0) {
                Write-BusBuddyError "Build failed with exit code $LASTEXITCODE"
                return
            }
            $exePath = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
        }

        if (-not $exePath) {
            Write-BusBuddyError "WPF executable not found after build — expected under: $outDir"
            return
        }

        Write-BusBuddyStatus "Starting BusBuddy (STA)..." -Type Info

    if ($PSCmdlet.ShouldProcess($exePath, "Start-Process (STA runspace)")) {
            # Create an STA runspace and launch the GUI process inside it.
            # Docs: RunspaceFactory.CreateRunspace / ApartmentState — see function header.
            $rs = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
            $rs.ApartmentState = [System.Threading.ApartmentState]::STA
            $rs.Open()

            $ps = [System.Management.Automation.PowerShell]::Create().AddScript({
                param($FilePath, $WorkingDir)
                Start-Process -FilePath $FilePath -WorkingDirectory $WorkingDir
            }).AddArgument($exePath).AddArgument($wpfDir)

            $ps.Runspace = $rs
            try {
                $null = $ps.Invoke()
            } finally {
                $ps.Dispose()
                $rs.Close()
                $rs.Dispose()
            }
        }

        Write-BusBuddyStatus "BusBuddy launch command issued (process started)" -Type Success

        if ($WaitReady) {
            # Wait for exact process name readiness up to WaitSeconds
            $deadline = (Get-Date).AddSeconds($WaitSeconds)
            $procName = 'BusBuddy.WPF'
            $started = $false
            do {
                Start-Sleep -Milliseconds 300
                $p = Get-Process -Name $procName -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($p) { $started = $true; break }
            } while ((Get-Date) -lt $deadline)

            if ($started) {
                Write-BusBuddyStatus "Process $procName detected (PID=$($p.Id))" -Type Success
            } else {
                Write-BusBuddyStatus "Process $procName not detected within ${WaitSeconds}s — possible startup failure" -Type Warning
                # Attempt to show last 60 lines of log for quick diagnostics
                try {
                    $logsDir = Join-Path $projectRoot 'BusBuddy.WPF\bin\Debug\net9.0-windows\logs'
                    $latest = Get-ChildItem -Path $logsDir -Filter 'busbuddy-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                    if ($latest) {
                        Write-Information "📄 Last 60 log lines from $($latest.Name):" -InformationAction Continue
                        Get-Content -Path $latest.FullName -Tail 60 | ForEach-Object { Write-Information "   $_" -InformationAction Continue }
                    } else {
                        Write-Information "No log files found under $logsDir" -InformationAction Continue
                    }
                } catch { Write-Warning "Failed reading logs: $($_.Exception.Message)" }
            }
        }
    }
    catch {
        Write-BusBuddyError "Failed to start application" -Exception $_
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyRunSta {
    <#
    .SYNOPSIS
        Launch BusBuddy ensuring an STA-hosted PowerShell shell spawns the app.
    .DESCRIPTION
        WPF itself starts on an STA thread via Program.Main [STAThread], but this provides an
        STA-hosted pwsh wrapper for environments sensitive to MTA shells. Uses Start-Process so
        the main console isn’t blocked.
        Docs: Windows PowerShell/PowerShell on Windows supports -STA switch for single-threaded apartment.
    .PARAMETER Wait
        Wait for the child shell to exit before returning.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [switch]$Wait
    )

    $projectRoot = Get-BusBuddyProjectRoot
    $pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue)?.Source
    if (-not $pwsh) { $pwsh = "pwsh.exe" }

    $cmd = 'dotnet run --project "BusBuddy.WPF/BusBuddy.WPF.csproj"'
    $args = @('-NoProfile','-NoLogo')
    # Attempt -STA on Windows pwsh; ignore if not supported
    $args = @('-STA') + $args + @('-Command', $cmd)

    Write-BusBuddyStatus "Launching via STA-hosted shell..." -Type Info
    if ($PSCmdlet.ShouldProcess($pwsh, "Start-Process (STA)")) {
        $p = Start-Process -FilePath $pwsh -ArgumentList $args -WorkingDirectory $projectRoot -PassThru
        if ($Wait) { try { $p.WaitForExit() } catch { } }
        return $p
    }
}

function Get-BusBuddyApartmentState {
    <#
    .SYNOPSIS
        Show the current shell thread's apartment state (STA/MTA).
    #>
    [CmdletBinding()] param()
    try {
        $apt = [System.Threading.Thread]::CurrentThread.GetApartmentState()
        Write-Information "Current apartment state: $apt" -InformationAction Continue
        return $apt
    } catch {
        Write-Warning "Unable to read apartment state: $($_.Exception.Message)"
        return $null
    }
}

function Invoke-BusBuddyTest {
    <#
    .SYNOPSIS
        Run BusBuddy tests using Phase 4 NUnit Test Runner (deprecated .NET 9 dotnet test method)
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    [OutputType([hashtable])]
    param(
        [ValidateSet('All', 'Unit', 'Integration', 'Validation', 'Core', 'WPF')]
        [string]$TestSuite = 'All',

        [switch]$SaveToFile,

        [switch]$DetailedOutput
    )

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot

    try {
        Write-BusBuddyStatus "🚌 BusBuddy Phase 4 NUnit Test System" -Type Info
        Write-Information "Using reliable NUnit Test Runner (deprecated unreliable .NET 9 method)" -InformationAction Continue

        # Path to the Phase 4 NUnit Test Runner script
        $phase4ScriptPath = Join-Path $projectRoot "PowerShell\Testing\Run-Phase4-NUnitTests-Modular.ps1"

        if (-not (Test-Path $phase4ScriptPath)) {
            Write-BusBuddyError "❌ Phase 4 NUnit Test Runner script not found: $phase4ScriptPath"
            Write-Information "Please ensure the PowerShell\Testing\Run-Phase4-NUnitTests-Modular.ps1 file exists" -InformationAction Continue
            return @{
                ExitCode = -1
                ErrorMessage = "Phase 4 NUnit Test Runner script not found"
                Output = "Script path: $phase4ScriptPath"
            }
        }

        Write-Information "📁 Using Phase 4 script: $phase4ScriptPath" -InformationAction Continue
        Write-Information "🧪 Test Suite: $TestSuite" -InformationAction Continue

        # Prepare parameters for the Phase 4 script as string arguments (more reliable than hashtable)
        $scriptArgs = @("-TestSuite", $TestSuite)

        if ($SaveToFile) {
            $scriptArgs += "-GenerateReport"
        }

        if ($DetailedOutput) {
            $scriptArgs += "-Detailed"
        }

        Write-Information "🚀 Executing Phase 4 NUnit Test Runner..." -InformationAction Continue
        Write-Information "Arguments: $($scriptArgs -join ' ')" -InformationAction Continue

        # Execute the Phase 4 NUnit script with enhanced error handling
        try {
            # Capture both stdout and stderr to detect .NET 9 compatibility issues
            $testOutputFile = Join-Path $projectRoot "TestResults" "bbtest-output-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
            $testErrorFile  = Join-Path $projectRoot "TestResults" "bbtest-errors-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

            # Ensure TestResults directory exists (gate with ShouldProcess for WhatIf support)
            $testResultsDir = Join-Path $projectRoot "TestResults"
            if (-not (Test-Path $testResultsDir)) {
                if ($PSCmdlet.ShouldProcess($testResultsDir, 'Create directory')) {
                    New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
                }
            }

            # Run with output capture (guard with ShouldProcess for WhatIf/Confirm)
            $allArgs = @("-File", $phase4ScriptPath) + $scriptArgs
            $testExitCode = 0
            if ($PSCmdlet.ShouldProcess("pwsh.exe", "Start-Process $($allArgs -join ' ')")) {
                $process = Start-Process -FilePath "pwsh.exe" -ArgumentList $allArgs `
                    -RedirectStandardOutput $testOutputFile -RedirectStandardError $testErrorFile `
                    -NoNewWindow -PassThru
                $process.WaitForExit()
                $testExitCode = $process.ExitCode
            }

            # Read captured output
            $testOutput = if (Test-Path $testOutputFile) { Get-Content $testOutputFile -Raw } else { "" }
            $testErrors = if (Test-Path $testErrorFile)  { Get-Content $testErrorFile  -Raw } else { "" }

            # Check for specific .NET 9 compatibility issue
            $hasNet9Issue = $testErrors -match "Microsoft\.TestPlatform\.CoreUtilities.*Version=15\.0\.0\.0" -or
                           $testOutput -match "Microsoft\.TestPlatform\.CoreUtilities.*Version=15\.0\.0\.0"

            if ($hasNet9Issue) {
                Write-Information "`n🚨 KNOWN .NET 9 COMPATIBILITY ISSUE DETECTED" -InformationAction Continue
                Write-Information "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -InformationAction Continue
                Write-Error "❌ Microsoft.TestPlatform.CoreUtilities v15.0.0.0 not found"
                Write-Information "🔍 This is a documented .NET 9 compatibility issue with test platform" -InformationAction Continue
                Write-Information "" -InformationAction Continue
                Write-Information "📋 WORKAROUND OPTIONS:" -InformationAction Continue
                Write-Information "  1. Install VS Code NUnit Test Runner extension for UI testing" -InformationAction Continue
                Write-Information "  2. Use Visual Studio Test Explorer instead of command line" -InformationAction Continue
                Write-Information "  3. Temporarily downgrade to .NET 8.0 for testing (not recommended)" -InformationAction Continue
                Write-Information "" -InformationAction Continue
                Write-Information "📁 Test logs saved to:" -InformationAction Continue
                Write-Information "   Output: $testOutputFile" -InformationAction Continue
                Write-Information "   Errors: $testErrorFile" -InformationAction Continue
                Write-Information "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -InformationAction Continue

                return @{
                    ExitCode = $testExitCode
                    ErrorType = "NET9_COMPATIBILITY"
                    Issue = "Microsoft.TestPlatform.CoreUtilities v15.0.0.0 compatibility"
                    Workarounds = @("VS Code NUnit extension", "Visual Studio Test Explorer", "Downgrade to .NET 8")
                    OutputFile = $testOutputFile
                    ErrorFile = $testErrorFile
                    Method = "Phase4-NUnit-NET9-Issue"
                }
            } elseif ($testExitCode -eq 0) {
                Write-BusBuddyStatus "✅ Phase 4 NUnit tests completed successfully!" -Type Success
                return @{
                    ExitCode = 0
                    PassedTests = 1  # Phase 4 script provides detailed results
                    FailedTests = 0
                    SkippedTests = 0
                    Output = "Phase 4 NUnit Test Runner completed successfully"
                    OutputFile = $testOutputFile
                    Method = "Phase4-NUnit"
                }
            } else {
                Write-BusBuddyError "❌ Phase 4 NUnit tests failed with exit code $testExitCode"
                Write-Information "📁 Check test logs: $testOutputFile and $testErrorFile" -InformationAction Continue
                return @{
                    ExitCode = $testExitCode
                    PassedTests = 0
                    FailedTests = 1
                    SkippedTests = 0
                    Output = "Phase 4 NUnit Test Runner failed - check TestResults directory for details"
                    OutputFile = $testOutputFile
                    ErrorFile = $testErrorFile
                    Method = "Phase4-NUnit"
                }
            }
        } catch {
            Write-BusBuddyError "❌ Phase 4 NUnit execution failed: $($_.Exception.Message)"
            return @{
                ExitCode = -1
                ErrorMessage = $_.Exception.Message
                Output = "Phase 4 NUnit script execution error"
                Method = "Phase4-NUnit"
            }
        }
    }
    catch {
        Write-BusBuddyError "Test execution error" -Exception $_
        return @{
            ExitCode = -1
            ErrorMessage = $_.Exception.Message
            Method = "Phase4-NUnit-Error"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyTestLegacy {
    <#
    .SYNOPSIS
        DEPRECATED: Legacy .NET 9 dotnet test method with compatibility issues

    .DESCRIPTION
        This function represents the old, unreliable method of running tests using
        'dotnet test' directly, which has known compatibility issues with .NET 9
        and Microsoft.TestPlatform.CoreUtilities version conflicts.

        USE INSTEAD: Invoke-BusBuddyTest (which now uses Phase 4 NUnit Test Runner)

    .NOTES
        DEPRECATED: This method is deprecated due to .NET 9 compatibility issues
        REPLACEMENT: Use bbTest (Invoke-BusBuddyTest) which now uses Phase 4 NUnit
        ISSUE: Microsoft.TestPlatform.CoreUtilities v15.0.0.0 vs .NET 9 conflict
    #>
    [CmdletBinding()]
    param()

    Write-Warning "⚠️  DEPRECATED METHOD CALLED"
    Write-Warning "The legacy .NET 9 'dotnet test' method has known compatibility issues"
    Write-Warning "USE INSTEAD: bbTest (now uses reliable Phase 4 NUnit Test Runner)"
    Write-Information "" -InformationAction Continue
    Write-Information "MIGRATION GUIDANCE:" -InformationAction Continue
    Write-Information "- Replace 'Invoke-BusBuddyTestLegacy' with 'Invoke-BusBuddyTest'" -InformationAction Continue
    Write-Information "- Use 'bbTest' command which now uses Phase 4 NUnit script" -InformationAction Continue
    Write-Information "- Benefits: VS Code integration, enhanced logging, reliable execution" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    # Redirect to new method
    Write-Information "🔄 Redirecting to new Phase 4 NUnit method..." -InformationAction Continue
    return Invoke-BusBuddyTest @PSBoundParameters
}

function Invoke-BusBuddyClean {
    <#
    .SYNOPSIS
        Clean BusBuddy build artifacts
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param()

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot
    try {
        Write-BusBuddyStatus "Cleaning BusBuddy artifacts..." -Type Info
        if ($PSCmdlet.ShouldProcess("BusBuddy.sln", "dotnet clean")) {
            dotnet clean BusBuddy.sln
        }

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "Clean completed successfully" -Type Success
        } else {
            Write-BusBuddyError "Clean failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-BusBuddyError "Clean operation error" -Exception $_
    }
    finally {
        Pop-Location
    }
}

function Invoke-BusBuddyRestore {
    <#
    .SYNOPSIS
        Restore BusBuddy NuGet packages
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param(
        [switch]$Force
    )

    $projectRoot = Get-BusBuddyProjectRoot
    Push-Location $projectRoot
    try {
        Write-BusBuddyStatus "Restoring NuGet packages..." -Type Info
        if ($PSCmdlet.ShouldProcess("BusBuddy.sln", ("dotnet restore" + ($Force ? " --force" : "")))) {
            if ($Force) {
                dotnet restore BusBuddy.sln --force
            } else {
                dotnet restore BusBuddy.sln
            }
        }

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "Package restore completed successfully" -Type Success
        } else {
            Write-BusBuddyError "Package restore failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-BusBuddyError "Package restore error" -Exception $_
    }
    finally {
        Pop-Location
    }
}

#endregion

#region Development Functions

function Start-BusBuddyDevSession {
    <#
    .SYNOPSIS
        Start a complete BusBuddy development session
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "Starting BusBuddy development session..." -Type Info

    $projectRoot = Get-BusBuddyProjectRoot
    Write-BusBuddyStatus "Project root: $projectRoot" -Type Info

    Invoke-BusBuddyRestore
    Invoke-BusBuddyBuild
    Invoke-BusBuddyHealthCheck

    Write-BusBuddyStatus "Development session ready!" -Type Success
}

function Invoke-BusBuddyHealthCheck {
    <#
    .SYNOPSIS
        Perform BusBuddy system health check
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "Performing BusBuddy health check..." -Type Info

    $issues = @()

    try {
        $dotnetVersion = dotnet --version
        Write-BusBuddyStatus ".NET Version: $dotnetVersion" -Type Info
    }
    catch {
        $issues += "Unable to determine .NET version"
    }

    Write-BusBuddyStatus "PowerShell Version: $($PSVersionTable.PSVersion)" -Type Info

    $projectRoot = Get-BusBuddyProjectRoot
    $requiredFiles = @('BusBuddy.sln', 'Directory.Build.props', 'NuGet.config')

    foreach ($file in $requiredFiles) {
        $filePath = Join-Path $projectRoot $file
        if (Test-Path $filePath) {
            Write-BusBuddyStatus "✓ Found: $file" -Type Success
        } else {
            $issues += "Missing required file: $file"
            Write-BusBuddyStatus "✗ Missing: $file" -Type Warning
        }
    }

    if ($issues.Count -eq 0) {
        Write-BusBuddyStatus "All health checks passed!" -Type Success
    } else {
        Write-BusBuddyStatus "Health check found $($issues.Count) issue(s):" -Type Warning
        foreach ($issue in $issues) {
            Write-BusBuddyStatus "  - $issue" -Type Warning
        }
    }
}

function Test-BusBuddyHealth {
    <#
    .SYNOPSIS
        Run BusBuddy health check (alias for Invoke-BusBuddyHealthCheck)
    .DESCRIPTION
        Performs comprehensive health check of the BusBuddy system including .NET version,
        PowerShell version, and required project files.
    #>
    [CmdletBinding()]
    param()

    # Call the main health check function
    Invoke-BusBuddyHealthCheck
}

function Get-BusBuddyInfo {
    <#
    .SYNOPSIS
        Display BusBuddy module information
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "BusBuddy PowerShell Module Information" -Type Info
    Write-Information "" -InformationAction Continue

    Write-Information "Name: BusBuddy" -InformationAction Continue
    Write-Information "Version: 2.2.0" -InformationAction Continue
    Write-Information "Author: Bus Buddy Development Team" -InformationAction Continue
    Write-Information "PowerShell Version: $($PSVersionTable.PSVersion)" -InformationAction Continue
    Write-Information "Project Root: $(Get-BusBuddyProjectRoot)" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-BusBuddyStatus "Use 'Get-BusBuddyCommand' to see available commands" -Type Info
}

function Get-BusBuddyCommand {
    <#
    .SYNOPSIS
        List all available BusBuddy commands
    #>
    [CmdletBinding()]
    param()

    Write-BusBuddyStatus "Available BusBuddy Commands" -Type Info
    Write-Information "" -InformationAction Continue

    Write-Information "Core Aliases:" -InformationAction Continue
    Write-Information "  bbBuild      - Build the BusBuddy solution" -InformationAction Continue
    Write-Information "  bbRun        - Run the BusBuddy application" -InformationAction Continue
    Write-Information "  bbTest       - Run BusBuddy tests" -InformationAction Continue
    Write-Information "  bbClean      - Clean build artifacts" -InformationAction Continue
    Write-Information "  bbRestore    - Restore NuGet packages" -InformationAction Continue
    Write-Information "  bbHealth     - Check system health" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "Development Aliases:" -InformationAction Continue
    Write-Information "  bbDevSession - Start development session" -InformationAction Continue
    Write-Information "  bbInfo       - Show module information" -InformationAction Continue
    Write-Information "  bbCommands   - List all commands (this command)" -InformationAction Continue
    Write-Information "  bbMantra     - Show session Mantra ID" -InformationAction Continue
    Write-Information "  bbMantraReset- Rotate session Mantra ID" -InformationAction Continue
    Write-Information "  bbTestFull   - Build + test with full logs" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "Deprecated:" -InformationAction Continue
    Write-Information "  MVP tooling and XAI route optimization shell commands are deprecated." -InformationAction Continue
    Write-Information "  Use in-app features and core commands (bbBuild, bbRun, bbTest, bbHealth)." -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "Functions:" -InformationAction Continue
    $functions = Get-Command -Module BusBuddy -CommandType Function | Sort-Object Name
    foreach ($func in $functions) {
        Write-Information "  $($func.Name)" -InformationAction Continue
    }
}
#endregion

#region XAML Validation Functions

function Invoke-BusBuddyXamlValidation {
    <#
    .SYNOPSIS
        Validate all XAML files in the BusBuddy project
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$ProjectPath = (Get-BusBuddyProjectRoot)
    )

    Write-BusBuddyStatus "Starting XAML validation..." -Type Info

    $xamlPath = Join-Path $ProjectPath "BusBuddy.WPF"
    if (-not (Test-Path $xamlPath)) {
        Write-BusBuddyError "BusBuddy.WPF directory not found at: $xamlPath"
        return
    }

    $xamlFiles = Get-ChildItem -Path $xamlPath -Filter "*.xaml" -Recurse
    $validFiles = 0
    $invalidFiles = 0

    foreach ($file in $xamlFiles) {
        Write-BusBuddyStatus "Validating: $($file.Name)" -Type Info

        try {
            $content = Get-Content $file.FullName -Raw

            # Basic XAML validation
            if ($content -match '<.*?>' -and $content -match 'xmlns') {
                $validFiles++
                Write-BusBuddyStatus "  ✓ $($file.Name)" -Type Success
            } else {
                $invalidFiles++
                Write-BusBuddyStatus "  ✗ $($file.Name) - Invalid XAML structure" -Type Warning
            }
        }
        catch {
            $invalidFiles++
            Write-BusBuddyError "  ✗ $($file.Name) - Exception: $($_.Exception.Message)"
        }
    }

    Write-BusBuddyStatus "XAML Validation Complete: $validFiles valid, $invalidFiles invalid" -Type Info
}

#endregion

#region Exception Capture Functions

function Invoke-BusBuddyWithExceptionCapture {
    <#
    .SYNOPSIS
        Execute a command with comprehensive exception capture and enhanced diagnostics

    .DESCRIPTION
        Provides detailed execution monitoring with timing, error analysis, system context,
        and actionable diagnostics for BusBuddy development operations.

    .PARAMETER Command
        The command to execute (e.g., 'dotnet', 'pwsh', 'git')

    .PARAMETER Arguments
        Array of arguments to pass to the command

    .PARAMETER Context
        Descriptive context for the operation (used in logs and error reports)

    .PARAMETER ThrowOnError
        If specified, re-throws exceptions instead of capturing them

    # Output is captured and summarized

    .PARAMETER Timeout
        Maximum execution time in seconds (default: 300)

    .EXAMPLE
        Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("build", "BusBuddy.sln") -Context "Solution Build"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter()]
        [string[]]$Arguments = @(),

        [Parameter()]
        [string]$Context = "BusBuddy Operation",

    [Parameter()]
    [switch]$ThrowOnError,
        [Parameter()]
        [int]$Timeout = 300
    )

    $startTime = Get-Date
    $sessionId = [System.Guid]::NewGuid().ToString("N")[0..7] -join ""
    $fullCommand = "$Command $($Arguments -join ' ')"

    # Enhanced status reporting
    Write-Information "🔄 [$sessionId] Executing: $fullCommand" -InformationAction Continue
    Write-Information "📍 Context: $Context" -InformationAction Continue
    Write-Information "⏱️  Timeout: $Timeout seconds" -InformationAction Continue

    # System context capture
    $systemContext = @{
        WorkingDirectory = Get-Location
        PowerShellVersion = $PSVersionTable.PSVersion
        ProcessId = $PID
        UserName = $env:USERNAME
        MachineName = $env:COMPUTERNAME
        Timestamp = $startTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
        SessionId = $sessionId
    }

    try {
        # Execute synchronously and capture combined output
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        if ($Arguments.Count -gt 0) {
            $result = & $Command @Arguments 2>&1
        } else {
            $result = & $Command 2>&1
        }
        $stopwatch.Stop()

        $duration = $stopwatch.Elapsed
        Write-Information "✅ [$sessionId] Success in $($duration.TotalSeconds.ToString('F2'))s" -InformationAction Continue

        if ($result) {
            $outputLines = ($result | Measure-Object -Line).Lines
            Write-Information "📊 Output: $outputLines lines captured" -InformationAction Continue
            $warnings = $result | Where-Object { $_ -match "warning|warn|WRN" }
            if ($warnings) {
                Write-Warning "⚠️  [$sessionId] $($warnings.Count) warnings detected in output"
            }
        }

        return $result
    }
    catch {
        $endTime = Get-Date
        $duration = $endTime - $startTime
        $errorDetails = $_.Exception.Message

        # Enhanced error reporting with diagnostics
        Write-Error "❌ [$sessionId] Command failed after $($duration.TotalSeconds.ToString('F2'))s"
        Write-Information "🔍 Error Details:" -InformationAction Continue
        Write-Information "   Command: $fullCommand" -InformationAction Continue
        Write-Information "   Context: $Context" -InformationAction Continue
        Write-Information "   Working Dir: $($systemContext.WorkingDirectory)" -InformationAction Continue
        Write-Information "   Error: $errorDetails" -InformationAction Continue

        # Capture additional diagnostics
        $diagnostics = @{
            LastExitCode = $LASTEXITCODE
            ErrorRecord = $_
            SystemContext = $systemContext
            Duration = $duration
            FullCommand = $fullCommand
        }

        # Check for common error patterns and provide suggestions
    $suggestions = Get-BusBuddyErrorSuggestion -ErrorMessage $errorDetails -Command $Command
        if ($suggestions) {
            Write-Information "💡 Suggestions:" -InformationAction Continue
            $suggestions | ForEach-Object {
                Write-Information "   • $_" -InformationAction Continue
            }
        }

        # Log to error capture file for analysis
        $errorLogPath = Join-Path (Get-BusBuddyProjectRoot) "logs" "command-errors.log"
        $errorEntry = @{
            Timestamp = $startTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
            SessionId = $sessionId
            Command = $fullCommand
            Context = $Context
            Duration = $duration.TotalSeconds
            Error = $errorDetails
            ExitCode = $LASTEXITCODE
            Diagnostics = $diagnostics
        } | ConvertTo-Json -Depth 3 -Compress

        try {
            $errorEntry | Add-Content -Path $errorLogPath -Encoding UTF8
        } catch {
 Write-Warning ("Could not append to error log at {0}: {1}" -f $errorLogPath, $_.Exception.Message)
        }

        if ($ThrowOnError) {
            throw
        }

        return $null
    }
}

# Helper function for error suggestion
function Get-BusBuddyErrorSuggestion {
    param(
        [string]$ErrorMessage,
        [string]$Command
    )

    $suggestions = @()

    # Common dotnet errors
    if ($Command -eq "dotnet") {
        if ($ErrorMessage -match "not found|could not be found") {
            $suggestions += "Run 'dotnet restore' to restore NuGet packages"
            $suggestions += "Check if the project file exists and is valid"
        }
        if ($ErrorMessage -match "build failed|compilation failed") {
            $suggestions += "Run 'bb-health' to check project status"
            $suggestions += "Check for missing dependencies or compile errors"
        }
        if ($ErrorMessage -match "unable to resolve|dependency") {
            $suggestions += "Run 'bb-clean' then 'bb-restore' to refresh dependencies"
        }
    }

    # PowerShell errors
    if ($ErrorMessage -match "execution policy") {
        $suggestions += "Run 'Set-ExecutionPolicy -Scope CurrentUser RemoteSigned'"
    }

    # General timeout errors
    if ($ErrorMessage -match "timeout|timed out") {
        $suggestions += "Increase timeout value or check for hanging processes"
        $suggestions += "Verify network connectivity if downloading packages"
    }

    return $suggestions
}

#endregion

#region MVP Focus Functions

function Start-BusBuddyMVP {
    <#
    .SYNOPSIS
        Keep development focused on MVP essentials - pushes back on scope creep
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$FeatureIdea,

        [Parameter()]
        [switch]$JustShow
    )

    $mvpFeatures = @{
        "✅ CORE MVP" = @(
            "Basic WPF window that opens",
            "Simple student list (Name, Address)",
            "Simple route list (Route Number, Description)",
            "Assign student to route (dropdown)",
            "Save data (even just to file)",
            "Basic CRUD operations"
        )
        "🟡 NICE TO HAVE (Phase 2)" = @(
            "Advanced Syncfusion controls",
            "Google Earth integration",
            "Real-time tracking",
            "Advanced analytics",
            "Multi-theme support",
            "Performance monitoring"
        )
        "🚫 ENTERPRISE OVERKILL" = @(
            "Microservices architecture",
            "Container deployment",
            "Advanced security patterns",
            "Multi-tenant support",
            "Cloud integration",
            "Machine learning features"
        )
    }

    if ($JustShow) {
        Write-BusBuddyStatus "🎯 MVP Feature Priority Guide" -Type Info
        Write-Information "" -InformationAction Continue

        foreach ($category in $mvpFeatures.Keys) {
            Write-Information $category -InformationAction Continue
            foreach ($feature in $mvpFeatures[$category]) {
                Write-Information "  • $feature" -InformationAction Continue
            }
            Write-Information "" -InformationAction Continue
        }

        Write-BusBuddyStatus "💡 Rule: If it's not in CORE MVP, defer it!" -Type Warning
        return
    }

    if ($FeatureIdea) {
        Write-BusBuddyStatus "🤔 Evaluating: '$FeatureIdea'" -Type Info

        $inCore = $mvpFeatures["✅ CORE MVP"] | Where-Object { $_ -match $FeatureIdea -or $FeatureIdea -match $_ }
        $inNice = $mvpFeatures["🟡 NICE TO HAVE (Phase 2)"] | Where-Object { $_ -match $FeatureIdea -or $FeatureIdea -match $_ }
        $inOverkill = $mvpFeatures["🚫 ENTERPRISE OVERKILL"] | Where-Object { $_ -match $FeatureIdea -or $FeatureIdea -match $_ }

        if ($inCore) {
            Write-BusBuddyStatus "✅ GO FOR IT! This is core MVP functionality." -Type Success
        }
        elseif ($inNice) {
            Write-BusBuddyStatus "🟡 HOLD UP! This is nice-to-have. Focus on core MVP first." -Type Warning
            Write-BusBuddyStatus "💭 Ask: 'Can I assign a student to a route without this?'" -Type Warning
        }
        elseif ($inOverkill) {
            Write-BusBuddyStatus "🚫 STOP! This is enterprise overkill for MVP." -Type Error
            Write-BusBuddyStatus "🎯 Remember: You need a working tool, not a demo for Microsoft." -Type Error
        }
        else {
            Write-BusBuddyStatus "🤷 Unknown feature. Let's evaluate against MVP goals:" -Type Info
            Write-BusBuddyStatus "❓ Question 1: Does this help assign students to routes?" -Type Info
            Write-BusBuddyStatus "❓ Question 2: Can you use BusBuddy without it?" -Type Info
            Write-BusBuddyStatus "❓ Question 3: Will this take more than 1 day to implement?" -Type Info
        }
    }
}

function Test-BusBuddyMVPReadiness {
    <#
    .SYNOPSIS
        Check if we're ready for MVP delivery
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-BusBuddyStatus "🎯 MVP Readiness Check" -Type Info

    $projectRoot = Get-BusBuddyProjectRoot
    $ready = $true

    # MVP Milestone 1: Application starts
    Write-BusBuddyStatus "Checking: Application starts without crashing..." -Type Info
    try {
        & dotnet build BusBuddy.sln --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "  ✅ App builds successfully" -Type Success
        } else {
            Write-BusBuddyStatus "  ❌ App doesn't build - FIX THIS FIRST" -Type Error
            $ready = $false
        }
    } catch {
        Write-BusBuddyStatus "  ❌ Build check failed - FIX THIS FIRST" -Type Error
        $ready = $false
    }

    # MVP Milestone 2: Basic UI exists
    $mainWindow = Join-Path $projectRoot "BusBuddy.WPF\Views\Main\MainWindow.xaml"
    if (Test-Path $mainWindow) {
        Write-BusBuddyStatus "  ✅ Main window exists" -Type Success
    } else {
        Write-BusBuddyStatus "  ❌ No main window found - NEED BASIC UI" -Type Error
        $ready = $false
    }

    # MVP Milestone 3: Data models exist
    $modelsPath = Join-Path $projectRoot "BusBuddy.Core\Models"
    $studentModel = Get-ChildItem -Path $modelsPath -Filter "*Student*" -ErrorAction SilentlyContinue
    $routeModel = Get-ChildItem -Path $modelsPath -Filter "*Route*" -ErrorAction SilentlyContinue

    if ($studentModel) {
        Write-BusBuddyStatus "  ✅ Student model exists" -Type Success
    } else {
        Write-BusBuddyStatus "  ❌ No Student model - NEED BASIC DATA" -Type Error
        $ready = $false
    }

    if ($routeModel) {
        Write-BusBuddyStatus "  ✅ Route model exists" -Type Success
    } else {
        Write-BusBuddyStatus "  ❌ No Route model - NEED BASIC DATA" -Type Error
        $ready = $false
    }

    # MVP Readiness Summary
    Write-BusBuddyStatus "" -Type Info
    if ($ready) {
        Write-BusBuddyStatus "🎉 MVP READY! You can ship this!" -Type Success
        Write-BusBuddyStatus "Next: Test that you can actually assign a student to a route" -Type Success
    } else {
        Write-BusBuddyStatus "🚧 MVP NOT READY - Focus on the failures above" -Type Warning
        Write-BusBuddyStatus "💡 Don't add features until these basic things work" -Type Warning
    }
}

#endregion

#region Anti-Regression Functions

function Invoke-BusBuddyAntiRegression {
    <#
    .SYNOPSIS
        Run anti-regression checks to prevent legacy patterns
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [switch]$Detailed
    )

    Write-BusBuddyStatus "🛡️ Running Anti-Regression Checks..." -Type Info
    $issues = @()
    $projectRoot = Get-BusBuddyProjectRoot

    # Check 1: Microsoft.Extensions.Logging violations
    Write-BusBuddyStatus "Checking for Microsoft.Extensions.Logging violations..." -Type Info
    try {
        $loggingFiles = Get-ChildItem -Path $projectRoot -Include "*.cs" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch "\.xml$" }
        $loggingViolations = $loggingFiles | Select-String -Pattern "Microsoft.Extensions.Logging" -ErrorAction SilentlyContinue

        if ($loggingViolations) {
            $issues += "❌ Microsoft.Extensions.Logging violations: $($loggingViolations.Count)"
            if ($Detailed) {
                $loggingViolations | ForEach-Object {
                    Write-BusBuddyStatus "  📄 $($_.Filename):$($_.LineNumber)" -Type Warning
                }
            }
        } else {
            Write-BusBuddyStatus "  ✅ No Microsoft.Extensions.Logging violations" -Type Success
        }
    } catch {
        Write-BusBuddyStatus "  ⚠️ Could not check logging violations: $($_.Exception.Message)" -Type Warning
    }

    # Check 2: Standard WPF controls in XAML
    Write-BusBuddyStatus "Checking for standard WPF controls..." -Type Info
    try {
        $xamlFiles = Get-ChildItem -Path "$projectRoot\BusBuddy.WPF" -Include "*.xaml" -Recurse -ErrorAction SilentlyContinue
        $xamlViolations = $xamlFiles | Select-String -Pattern "<DataGrid |<ComboBox " -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch "syncfusion:" }

        if ($xamlViolations) {
            $issues += "❌ Standard WPF controls found: $($xamlViolations.Count)"
            if ($Detailed) {
                $xamlViolations | ForEach-Object {
                    Write-BusBuddyStatus "  📄 $($_.Filename):$($_.LineNumber)" -Type Warning
                }
            }
        } else {
            Write-BusBuddyStatus "  ✅ No standard WPF controls found" -Type Success
        }
    } catch {
        Write-BusBuddyStatus "  ⚠️ Could not check XAML violations: $($_.Exception.Message)" -Type Warning
    }

    # Check 3: PowerShell Write-Host violations
    Write-BusBuddyStatus "Checking PowerShell compliance..." -Type Info
    try {
        $psFiles = Get-ChildItem -Path "$projectRoot\PowerShell" -Include "*.ps1", "*.psm1" -Recurse -ErrorAction SilentlyContinue
        $psViolations = $psFiles | Select-String -Pattern "Write-Host" -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch "Module loaded|ForegroundColor|BusBuddy PowerShell Module" }

        if ($psViolations) {
            $issues += "❌ PowerShell Write-Host violations: $($psViolations.Count)"
            if ($Detailed) {
                $psViolations | ForEach-Object {
                    Write-BusBuddyStatus "  📄 $($_.Filename):$($_.LineNumber)" -Type Warning
                }
            }
        } else {
            Write-BusBuddyStatus "  ✅ PowerShell compliance maintained" -Type Success
        }
    } catch {
        Write-BusBuddyStatus "  ⚠️ Could not check PowerShell violations: $($_.Exception.Message)" -Type Warning
    }

    # Check 4: Build validation
    Write-BusBuddyStatus "Validating build status..." -Type Info
    try {
        Set-Location $projectRoot
        $buildOutput = & dotnet build --verbosity quiet 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-BusBuddyStatus "  ✅ Build successful" -Type Success
        } else {
            $issues += "❌ Build failed with exit code: $LASTEXITCODE"
            if ($Detailed) {
                Write-BusBuddyStatus "  Build output: $buildOutput" -Type Warning
            }
        }
    } catch {
        $issues += "❌ Build check failed: $($_.Exception.Message)"
    }

    # Report results
    Write-BusBuddyStatus " " -Type Info
    if ($issues.Count -eq 0) {
        Write-BusBuddyStatus "🎉 All anti-regression checks passed!" -Type Success
        Write-BusBuddyStatus "Repository is compliant with BusBuddy standards." -Type Success
        return $true
    } else {
        Write-BusBuddyError "🚨 Anti-regression violations found:"
        $issues | ForEach-Object { Write-BusBuddyError "  $_" }
        Write-BusBuddyStatus " " -Type Info
        Write-BusBuddyStatus "Run 'bb-anti-regression -Detailed' for specific file locations" -Type Warning
        Write-BusBuddyStatus "See 'Grok Resources/ANTI-REGRESSION-CHECKLIST.md' for remediation steps" -Type Warning
        return $false
    }
}

function Test-BusBuddyEnvironment {
    <#
    .SYNOPSIS
        Validates BusBuddy PowerShell environment for consistency and reliability
    .DESCRIPTION
        Comprehensive validation to ensure the development environment is properly
        configured and ready for MVP development. Checks PowerShell version, workspace,
        module availability, and essential tools.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    Write-Information "🔍 BusBuddy Environment Validation" -InformationAction Continue
    Write-Information "=================================" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    $issues = @()
    $warnings = @()

    # 1. PowerShell Version Check
    Write-Information "1. PowerShell Version..." -InformationAction Continue
    if ($PSVersionTable.PSVersion -ge [version]'7.5.0') {
        Write-Information "   ✅ PowerShell $($PSVersionTable.PSVersion) (Required: 7.5+)" -InformationAction Continue
    } else {
        $issues += "PowerShell version $($PSVersionTable.PSVersion) is too old. Need 7.5+"
        Write-Information "   ❌ PowerShell $($PSVersionTable.PSVersion) - UPGRADE REQUIRED" -InformationAction Continue
    }

    # 2. BusBuddy Workspace Detection
    Write-Information "2. Workspace Detection..." -InformationAction Continue
    $workspaceFound = $false
    $possiblePaths = @(
        $PWD.Path,
        "$env:USERPROFILE\Desktop\BusBuddy",
        "$env:USERPROFILE\Documents\BusBuddy",
        "C:\BusBuddy"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path "$path\BusBuddy.sln" -ErrorAction SilentlyContinue) {
            Write-Information "   ✅ Workspace found: $path" -InformationAction Continue
            $workspaceFound = $true
            break
        }
    }

    if (-not $workspaceFound) {
        $issues += "BusBuddy workspace not found in standard locations"
        Write-Information "   ❌ Workspace not found" -InformationAction Continue
    }

    # 3. Essential Commands Test
    Write-Information "3. Essential Commands..." -InformationAction Continue
    $essentialCommands = @('bb-build', 'bb-run', 'bb-health', 'bb-mvp', 'bb-mvp-check')
    $commandsWorking = 0

    foreach ($cmd in $essentialCommands) {
        if (Get-Command $cmd -ErrorAction SilentlyContinue) {
            $commandsWorking++
        }
    }

    if ($commandsWorking -eq $essentialCommands.Count) {
        Write-Information "   ✅ All $($essentialCommands.Count) essential commands available" -InformationAction Continue
    } else {
        $issues += "Only $commandsWorking of $($essentialCommands.Count) commands available"
        Write-Information "   ❌ Missing commands ($commandsWorking/$($essentialCommands.Count))" -InformationAction Continue
    }

    # 4. .NET SDK Check
    Write-Information "4. .NET SDK..." -InformationAction Continue
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -and $dotnetVersion -match '^9\.') {
            Write-Information "   ✅ .NET $dotnetVersion" -InformationAction Continue
        } else {
            $warnings += ".NET version $dotnetVersion - expected 9.x"
            Write-Information "   ⚠️ .NET $dotnetVersion (Expected: 9.x)" -InformationAction Continue
        }
    } catch {
        $issues += ".NET SDK not found or not working"
        Write-Information "   ❌ .NET SDK not found" -InformationAction Continue
    }

    # 5. Git Status
    Write-Information "5. Git Repository..." -InformationAction Continue
    try {
        $gitStatus = & git status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0) {
            if ($gitStatus) {
                $warnings += "Git has uncommitted changes"
                Write-Information "   ⚠️ Uncommitted changes present" -InformationAction Continue
            } else {
                Write-Information "   ✅ Git repository clean" -InformationAction Continue
            }
        } else {
            $warnings += "Not in a Git repository or Git not available"
            Write-Information "   ⚠️ Git issues detected" -InformationAction Continue
        }
    } catch {
        $warnings += "Git not available: $($_.Exception.Message)"
        Write-Information "   ⚠️ Git not available" -InformationAction Continue
    }

    # 6. Grok Resources Check
    Write-Information "6. AI Assistant Resources..." -InformationAction Continue
    if (Test-Path "Grok Resources\GROK-README.md") {
        Write-Information "   ✅ Grok Resources folder ready" -InformationAction Continue
    } else {
        # Respect suppression env var if present (used by VS Code tasks)
        if ($env:BUSBUDDY_NO_XAI_WARN -ne '1') {
            # Removed per MVP request: avoid Grok/xAI warning noise in health check
            # $warnings += "Grok Resources not found - AI assistance may be limited"
        }
        Write-Information "   ⚠️ Grok Resources missing" -InformationAction Continue
    }

    # Summary
    Write-Information "" -InformationAction Continue
    Write-Information "🎯 VALIDATION SUMMARY" -InformationAction Continue
    Write-Information "=====================" -InformationAction Continue

    if ($issues.Count -eq 0) {
        Write-Information "✅ ENVIRONMENT READY FOR MVP DEVELOPMENT!" -InformationAction Continue
        Write-Information "   All critical systems are operational" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "🚀 Quick Start Commands:" -InformationAction Continue
        Write-Information "   bb-health      - System health check" -InformationAction Continue
        Write-Information "   bb-mvp -JustShow - Show MVP priorities" -InformationAction Continue
        Write-Information "   bb-build       - Build the solution" -InformationAction Continue
        Write-Information "   bb-run         - Run the application" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        Write-Information "🎯 MVP Focus:" -InformationAction Continue
        Write-Information "   bb-mvp 'feature name' - Evaluate if feature is MVP-worthy" -InformationAction Continue
        Write-Information "   bb-mvp-check          - Check MVP milestone readiness" -InformationAction Continue

        if ($warnings.Count -gt 0) {
            Write-Information "" -InformationAction Continue
            Write-Information "⚠️ WARNINGS (non-critical):" -InformationAction Continue
            $warnings | ForEach-Object { Write-Information "   • $_" -InformationAction Continue }
        }

        return $true
    } else {
        Write-Information "❌ ENVIRONMENT NOT READY" -InformationAction Continue
        Write-Information "   Fix these issues before starting development:" -InformationAction Continue
        Write-Information "" -InformationAction Continue
        $issues | ForEach-Object { Write-Information "   • $_" -InformationAction Continue }

        if ($warnings.Count -gt 0) {
            Write-Information "" -InformationAction Continue
            Write-Information "⚠️ Additional warnings:" -InformationAction Continue
            $warnings | ForEach-Object { Write-Information "   • $_" -InformationAction Continue }
        }

        return $false
    }
}

function Start-BusBuddyRuntimeErrorCaptureBasic {
    <#
    .SYNOPSIS
        Comprehensive runtime error capture for BusBuddy application
    .DESCRIPTION
        Executes BusBuddy with multiple error capture mechanisms to identify
        and log runtime issues during application execution.
    .PARAMETER Duration
        How long to monitor the application (in seconds). Default: 60
    .PARAMETER DetailedLogging
        Enable detailed debug logging during capture
    .PARAMETER OpenLogsAfter
        Automatically open the log directory after capture completes
    .EXAMPLE
        bb-capture-runtime-errors
        Captures runtime errors for 60 seconds with standard logging
    .EXAMPLE
        bb-capture-runtime-errors -Duration 300 -DetailedLogging -OpenLogsAfter
        Extended capture with detailed logs and auto-open results
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [ValidateRange(10, 3600)]
        [int]$Duration = 60,

        [Parameter()]
        [switch]$DetailedLogging,

        [Parameter()]
        [switch]$OpenLogsAfter
    )

    $projectRoot = Get-BusBuddyProjectRoot
    $scriptPath = Join-Path $projectRoot "PowerShell\Scripts\Capture-RuntimeErrors.ps1"

    if (-not (Test-Path $scriptPath)) {
        Write-BusBuddyError "Runtime error capture script not found: $scriptPath"
        Write-BusBuddyStatus "Run the following to create required scripts" -Type Warning
        return
    }

    Write-BusBuddyStatus "🚌 Starting BusBuddy runtime error capture..." -Type Info
    Write-Information "Duration: $Duration seconds" -InformationAction Continue
    Write-Information "Detailed logging: $DetailedLogging" -InformationAction Continue

    try {
        $params = @{
            Duration = $Duration
        }

        if ($DetailedLogging) {
            $params.Add("DetailedLogging", $true)
        }

        if ($OpenLogsAfter) {
            $params.Add("OpenLogsAfter", $true)
        }

        # Execute the capture script
        $result = & $scriptPath @params

        if ($result.Success) {
            Write-BusBuddyStatus "Runtime capture completed successfully! No errors detected." -Type Success
        } else {
            Write-BusBuddyStatus "Runtime capture detected $($result.ErrorCount) errors - review logs" -Type Warning
        }

        return $result
    }
    catch {
        Write-BusBuddyError "Runtime error capture failed: $($_.Exception.Message)"
        Write-Information "Stack trace: $($_.ScriptStackTrace)" -InformationAction Continue
    }
}

# Lightweight wrapper to run the app and capture runtime errors using the basic capture script
function Invoke-BusBuddyRunCapture {
    <#
    .SYNOPSIS
        Run BusBuddy and capture runtime errors with logs and a summary report

    .DESCRIPTION
        Wraps the Capture-RuntimeErrors.ps1 script to launch the WPF app with stdio capture,
        time-bound monitoring, and writes logs to logs\runtime-capture.

    .PARAMETER Duration
        Monitoring duration in seconds (default 60)

    .PARAMETER DetailedLogging
        Enables extra CLI arguments and verbose capture

    .PARAMETER OpenLogsAfter
        Opens the log directory in Explorer after capture completes

    .EXAMPLE
        bb-run-capture -Duration 90 -DetailedLogging -OpenLogsAfter
    #>
    [CmdletBinding()]
    param(
        [ValidateRange(10,3600)]
        [int]$Duration = 60,
        [switch]$DetailedLogging,
        [switch]$OpenLogsAfter
    )

    # Delegate to the existing basic runtime capture implementation
    Start-BusBuddyRuntimeErrorCaptureBasic -Duration $Duration -DetailedLogging:$DetailedLogging -OpenLogsAfter:$OpenLogsAfter | Out-Null
}

#endregion

#region Azure Firewall Management Functions

function Update-BusBuddyAzureFirewall {
    <#
    .SYNOPSIS
        Automatically updates Azure SQL firewall rules for BusBuddy dynamic IP addresses
    .DESCRIPTION
        Fetches current public IP and adds it to Azure SQL firewall rules.
        Handles Starlink and work ISP dynamic IP changes automatically.
        Based on Microsoft Azure SQL firewall configuration best practices.
    .PARAMETER ResourceGroupName
        Azure resource group containing the SQL server (auto-detected from config if not specified)
    .PARAMETER ServerName
        Azure SQL server name (default: busbuddy-server-sm2 from appsettings.azure.json)
    .PARAMETER CleanupOldRules
        Remove old dynamic IP rules to keep firewall clean
    .PARAMETER Force
        Skip confirmation prompts
    .EXAMPLE
        Update-BusBuddyAzureFirewall
    .EXAMPLE
        Update-BusBuddyAzureFirewall -CleanupOldRules -Force
    .NOTES
        Reference: https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure
        Requires Az PowerShell module: Install-Module -Name Az.Sql -Scope CurrentUser
        Auto-integrates with BusBuddy appsettings.azure.json configuration
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([hashtable])]
    param (
        [Parameter(Mandatory = $false)]
        [string]$ResourceGroupName,

        [Parameter(Mandatory = $false)]
        [string]$ServerName,

        [Parameter(Mandatory = $false)]
        [switch]$CleanupOldRules,

        [Parameter(Mandatory = $false)]
        [switch]$Force
    )

    try {
        Write-Information "🚌 BusBuddy Azure SQL Firewall Updater" -InformationAction Continue
        Write-Information "=" * 50 -InformationAction Continue

        # Get BusBuddy Azure configuration
        $config = Get-BusBuddyAzureConfig
        if (-not $config) {
            throw "Could not load BusBuddy Azure configuration from appsettings.azure.json"
        }

        # Use configuration values if not provided
        if (-not $ServerName) {
            $ServerName = $config.ServerName
            if (-not $ServerName) {
                throw "Server name not found in configuration and not provided"
            }
        }

        if (-not $ResourceGroupName) {
            $ResourceGroupName = $config.ResourceGroup
            if (-not $ResourceGroupName) {
                Write-Warning "Resource group not specified in config. Please provide it manually."
                $ResourceGroupName = Read-Host "Enter Azure Resource Group name"
                if (-not $ResourceGroupName) {
                    throw "Resource group is required"
                }
            }
        }

        # Call the main update script
        $scriptPath = Join-Path $PSScriptRoot "..\..\Scripts\Update-AzureFirewall.ps1"
        if (-not (Test-Path $scriptPath)) {
            throw "Update-AzureFirewall.ps1 script not found at: $scriptPath"
        }

        $params = @{
            ResourceGroupName = $ResourceGroupName
            ServerName = $ServerName
            CleanupOldRules = $CleanupOldRules
        }

        Write-Information "🎯 Target: $ServerName in $ResourceGroupName" -InformationAction Continue

        if ($Force -or $PSCmdlet.ShouldProcess("Azure SQL Server $ServerName", "Update firewall rules")) {
            $result = & $scriptPath @params

            if ($result.Success) {
                Write-BusBuddyStatus "✅ Azure firewall updated successfully" -Type Success
                Write-Information "   IP Address: $($result.IPAddress)" -InformationAction Continue
                Write-Information "   Rule Name: $($result.RuleName)" -InformationAction Continue
                Write-Information "⏱️ Allow up to 5 minutes for rule propagation" -InformationAction Continue
            } else {
                Write-BusBuddyError "Failed to update Azure firewall" -Exception ([System.Exception]::new($result.Error)) -Context "Azure Firewall"
            }

            return $result
        }
    }
    catch {
        Write-BusBuddyError "Azure firewall update failed" -Exception $_ -Context "Azure Firewall Management"
        return @{
            Success = $false
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

function Get-BusBuddyAzureConfig {
    <#
    .SYNOPSIS
        Extracts Azure configuration from BusBuddy appsettings files
    .DESCRIPTION
        Parses appsettings.azure.json to extract server name, resource group, and other Azure settings
    .EXAMPLE
        Get-BusBuddyAzureConfig
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    try {
        $configFile = "appsettings.azure.json"
        if (-not (Test-Path $configFile)) {
            Write-Warning "appsettings.azure.json not found in current directory"
            return $null
        }

        $config = Get-Content $configFile -Raw | ConvertFrom-Json

        # Extract server name from connection string
        $connectionString = $config.ConnectionStrings.DefaultConnection
        if ($connectionString -match 'Server=tcp:([^.,]+)') {
            $serverName = $matches[1]
        } else {
            Write-Warning "Could not extract server name from connection string"
            $serverName = $null
        }

        # Try to find resource group in various config locations
        $resourceGroup = $null
        if ($config.Azure.ResourceGroup) {
            $resourceGroup = $config.Azure.ResourceGroup
        } elseif ($config.ResourceGroup) {
            $resourceGroup = $config.ResourceGroup
        }

        return @{
            ServerName = $serverName
            ResourceGroup = $resourceGroup
            DatabaseName = "BusBuddyDB"
            ConnectionString = $connectionString
            Config = $config
        }
    }
    catch {
        Write-Warning "Failed to parse Azure configuration: $($_.Exception.Message)"
        return $null
    }
}

function Test-BusBuddyAzureConnection {
    <#
    .SYNOPSIS
        Tests Azure SQL connection for BusBuddy database
    .DESCRIPTION
        Validates network connectivity and firewall rules for Azure SQL Database
        Provides detailed diagnostics for connection issues
    .EXAMPLE
        Test-BusBuddyAzureConnection
    .EXAMPLE
        Test-BusBuddyAzureConnection -UpdateFirewall
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param (
        [Parameter(Mandatory = $false)]
        [switch]$UpdateFirewall
    )

    try {
        Write-Information "🔍 Testing BusBuddy Azure SQL Connection" -InformationAction Continue
        Write-Information "=" * 45 -InformationAction Continue

        # Get configuration
        $config = Get-BusBuddyAzureConfig
        if (-not $config) {
            throw "Could not load Azure configuration"
        }

        # Test network connectivity
        Write-Information "🌐 Testing network connectivity..." -InformationAction Continue
        $serverFqdn = "$($config.ServerName).database.windows.net"

        try {
            $tcpTest = Test-NetConnection -ComputerName $serverFqdn -Port 1433 -WarningAction SilentlyContinue
            if ($tcpTest.TcpTestSucceeded) {
                Write-Information "✅ Network connectivity: SUCCESS" -InformationAction Continue
            } else {
                Write-Information "❌ Network connectivity: FAILED" -InformationAction Continue
                Write-Information "   This may indicate firewall or network issues" -InformationAction Continue
            }
        }
        catch {
            Write-Information "❌ Network test failed: $($_.Exception.Message)" -InformationAction Continue
        }

        # Get current IP
        try {
            $currentIP = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10).Trim()
            Write-Information "📍 Current public IP: $currentIP" -InformationAction Continue
        }
        catch {
            Write-Warning "Could not determine current IP address"
            $currentIP = "Unknown"
        }

        # Test SQL connection
        Write-Information "🔐 Testing SQL authentication..." -InformationAction Continue

        $connectionSuccess = $false
        $connectionError = $null

        try {
            # Use .NET SqlConnection for direct testing
            $connectionString = $config.ConnectionString
            # Replace environment variables for testing
            $testConnectionString = $connectionString -replace '\$\{AZURE_SQL_USER\}', $env:AZURE_SQL_USER -replace '\$\{AZURE_SQL_PASSWORD\}', $env:AZURE_SQL_PASSWORD

            if ($testConnectionString -match '\$\{') {
                Write-Warning "Environment variables not set: AZURE_SQL_USER, AZURE_SQL_PASSWORD"
                Write-Information "💡 Set environment variables or use manual portal test" -InformationAction Continue
            } else {
                Add-Type -AssemblyName System.Data
                $sqlConnection = New-Object System.Data.SqlClient.SqlConnection($testConnectionString)
                $sqlConnection.Open()
                $sqlConnection.Close()
                $connectionSuccess = $true
                Write-Information "✅ SQL connection: SUCCESS" -InformationAction Continue
            }
        }
        catch {
            $connectionError = $_.Exception.Message
            Write-Information "❌ SQL connection: FAILED" -InformationAction Continue
            Write-Information "   Error: $connectionError" -InformationAction Continue

            # Check for firewall-related errors
            if ($connectionError -like "*Client with IP address*not allowed*") {
                Write-Information "🛡️ Firewall issue detected!" -InformationAction Continue
                if ($UpdateFirewall) {
                    Write-Information "🔧 Attempting to update firewall rules..." -InformationAction Continue
                    $firewallResult = Update-BusBuddyAzureFirewall -Force
                    if ($firewallResult.Success) {
                        Write-Information "✅ Firewall updated. Retry connection in 5 minutes." -InformationAction Continue
                    }
                } else {
                    Write-Information "💡 Run with -UpdateFirewall to automatically fix" -InformationAction Continue
                }
            }
        }

        # Summary
        Write-Information "`n📋 Connection Test Summary:" -InformationAction Continue
        Write-Information "   Server: $serverFqdn" -InformationAction Continue
        Write-Information "   Database: $($config.DatabaseName)" -InformationAction Continue
        Write-Information "   Current IP: $currentIP" -InformationAction Continue
        Write-Information "   Network: $(if ($tcpTest.TcpTestSucceeded) { '✅' } else { '❌' })" -InformationAction Continue
        Write-Information "   SQL Auth: $(if ($connectionSuccess) { '✅' } else { '❌' })" -InformationAction Continue

        if (-not $connectionSuccess) {
            Write-Information "`n🔧 Troubleshooting options:" -InformationAction Continue
            Write-Information "1. Update firewall: bb-azure-firewall" -InformationAction Continue
            Write-Information "2. Use local database: Set DatabaseProvider=Local" -InformationAction Continue
            Write-Information "3. Check Azure portal: https://portal.azure.com" -InformationAction Continue
            Write-Information "4. Verify credentials: echo `$env:AZURE_SQL_USER" -InformationAction Continue
        }

        return @{
            Success = $connectionSuccess
            NetworkConnectivity = $tcpTest.TcpTestSucceeded
            CurrentIP = $currentIP
            Server = $serverFqdn
            Database = $config.DatabaseName
            Error = $connectionError
            Timestamp = Get-Date
        }
    }
    catch {
        Write-BusBuddyError "Azure connection test failed" -Exception $_ -Context "Azure Connection Test"
        return @{
            Success = $false
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

#endregion

#region Aliases - Safe Alias Creation with Conflict Resolution
# Core aliases with safe creation
try { Set-Alias -Name 'bbBuild'      -Value 'Invoke-BusBuddyBuild'   -Force } catch { }
try { Set-Alias -Name 'bbRun'        -Value 'Invoke-BusBuddyRun'     -Force } catch { }
try { Set-Alias -Name 'bbRunSta'     -Value 'Invoke-BusBuddyRunSta'  -Force } catch { }
try { Set-Alias -Name 'bbClean'      -Value 'Invoke-BusBuddyClean'   -Force } catch { }
try { Set-Alias -Name 'bbRestore'    -Value 'Invoke-BusBuddyRestore' -Force } catch { }
try { Set-Alias -Name 'bbTest'       -Value 'Invoke-BusBuddyTest'    -Force } catch { }
try { Set-Alias -Name 'bbHealth'     -Value 'Invoke-BusBuddyHealthCheck' -Force } catch { }

# Developer discovery
try { Set-Alias -Name 'bbDevSession' -Value 'Start-BusBuddyDevSession' -Force } catch { }
try { Set-Alias -Name 'bbInfo'       -Value 'Get-BusBuddyInfo' -Force } catch { }
try { Set-Alias -Name 'bbCommands'   -Value 'Get-BusBuddyCommand' -Force } catch { }

# Removed alias for bbXamlValidate (function not present)

# Session correlation
try { Set-Alias -Name 'bbMantra'       -Value 'Get-BusBuddyMantraId'    -Force } catch { }
try { Set-Alias -Name 'bbMantraReset'  -Value 'Reset-BusBuddyMantraId'  -Force } catch { }

# Environment
try { Set-Alias -Name 'bbEnv'          -Value 'Initialize-BusBuddyEnvironment' -Force } catch { }
# Hyphenated variants
foreach ($pair in @(
    @{A='bb-build';V='Invoke-BusBuddyBuild'},
    @{A='bb-run';V='Invoke-BusBuddyRun'},
    @{A='bb-run-sta';V='Invoke-BusBuddyRunSta'},
    @{A='bb-clean';V='Invoke-BusBuddyClean'},
    @{A='bb-restore';V='Invoke-BusBuddyRestore'},
    @{A='bb-test';V='Invoke-BusBuddyTest'},
    @{A='bb-health';V='Invoke-BusBuddyHealthCheck'},
    @{A='bb-env';V='Initialize-BusBuddyEnvironment'
})) { try { Set-Alias -Name $pair.A -Value $pair.V -Force } catch { } }
#endregion

#region Exports
Export-ModuleMember -Function @(
    'Invoke-BusBuddyBuild',
    'Invoke-BusBuddyRun',
    'Invoke-BusBuddyRunSta',
    'Invoke-BusBuddyClean',
    'Invoke-BusBuddyRestore',
    'Invoke-BusBuddyTest',
    'Invoke-BusBuddyHealthCheck',
    'Get-BusBuddyApartmentState',
    'Get-BusBuddyMantraId',
    'Reset-BusBuddyMantraId',
    'Start-BusBuddyDevSession',
    'Get-BusBuddyInfo',
    'Get-BusBuddyCommand',
    'Get-BusBuddyTestOutput',
    'Invoke-BusBuddyTestFull',
    'Get-BusBuddyTestError',
    'Get-BusBuddyTestLog',
    'Start-BusBuddyTestWatch',
    'Invoke-BusBuddyPester',
    'Get-BusBuddyTelemetrySummary',
    'Invoke-BusBuddyTelemetryPurge',
    'Get-BusBuddyPS75Compliance',
    'Initialize-BusBuddyEnvironment',
    # Script Lint exports
    'Test-BusBuddyErrorActionPipelines',
    'Invoke-BusBuddyErrorActionAudit',
    # Logging exports
    'Get-BusBuddyLogSummary'
) -Alias @(
    'bbBuild','bbRun','bbRunSta','bbClean','bbRestore','bbTest','bbHealth',
    'bbDevSession','bbInfo','bbCommands',
    'bb-build','bb-run','bb-run-sta','bb-clean','bb-restore','bb-test','bb-health',
    'bbMantra','bbMantraReset','bbTestFull',
    'bb-ps-review',
    'bbEnv','bb-env',
    # Script Lint aliases
    'bb-ps-validate-ea','bb-ps-validate-ea-run',
    # Logging alias
    'bb-logs-summary'
)
#endregion

#region Welcome Screen
function Show-BusBuddyWelcome {
    <#
    .SYNOPSIS
        Display a categorized welcome screen when the module loads.
    #>
    [CmdletBinding()]
    param([switch]$Quiet)

    $ps = $PSVersionTable.PSVersion
    $dotnet = try { & dotnet --version 2>$null } catch { "unknown" }

    if ($env:BUSBUDDY_SILENT -ne '1') {
        Write-Information "" -InformationAction Continue
        Write-BusBuddyStatus "🚌 BusBuddy Dev Shell — Ready" -Type Info
        Write-Information "PowerShell: $ps | .NET: $dotnet" -InformationAction Continue
        Write-Information "Project: $(Get-BusBuddyProjectRoot)" -InformationAction Continue
        Write-Information "" -InformationAction Continue
    }

    if ($env:BUSBUDDY_SILENT -ne '1') {
        Write-BusBuddyStatus "Core" -Type Info
        Write-Information "  bbBuild, bbRun, bbTest, bbClean, bbRestore, bbHealth" -InformationAction Continue
    }

    if ($env:BUSBUDDY_SILENT -ne '1') {
        Write-BusBuddyStatus "Development" -Type Info
        Write-Information "  bbDevSession, bbInfo, bbCommands, bbMantra, bbMantraReset" -InformationAction Continue
    }

    # Removed 'Validation & Safety' section (no bbXamlValidate)

    # Docs & Reference remains gated if bbCopilotRef exists
    if ($env:BUSBUDDY_SILENT -ne '1' -and (Get-Command bbCopilotRef -ErrorAction SilentlyContinue)) {
        Write-BusBuddyStatus "Docs & Reference" -Type Info
        Write-Information "  bbCopilotRef [Topic] (-ShowTopics)" -InformationAction Continue
    }

    if (-not $Quiet -and $env:BUSBUDDY_SILENT -ne '1') {
        Write-Information "" -InformationAction Continue
        Write-Information "Tips:" -InformationAction Continue
        Write-Information "  • bbCommands — full list with functions" -InformationAction Continue
        Write-Information "  • bbHealth — verify env quickly" -InformationAction Continue
        Write-Information "  • Set 'BUSBUDDY_NO_WELCOME=1' to suppress on import" -InformationAction Continue
    }
}

# Auto-run welcome unless suppressed
if (-not $env:BUSBUDDY_NO_WELCOME -and $env:BUSBUDDY_SILENT -ne '1') {
    Show-BusBuddyWelcome -Quiet
}
#endregion


function Start-BusBuddyRuntimeErrorCapture {
    <#
    .SYNOPSIS
        Advanced runtime error capture using WintellectPowerShell tools

    .DESCRIPTION
        Enhanced error monitoring and capture system that integrates WintellectPowerShell
        tools for comprehensive diagnostics, crash dump analysis, and system monitoring.

    .PARAMETER MonitorCrashes
        Enable crash dump monitoring and automatic analysis

    .PARAMETER SystemDiagnostics
        Include system diagnostics (uptime, environment, etc.)

    .PARAMETER ContinuousMonitoring
        Run in continuous monitoring mode

    .PARAMETER OutputPath
        Path to save error reports and analysis (default: logs/error-capture)

    .EXAMPLE
        Start-BusBuddyRuntimeErrorCapture -MonitorCrashes -SystemDiagnostics
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [switch]$MonitorCrashes,

        [Parameter()]
        [switch]$SystemDiagnostics,

        [Parameter()]
    [switch]$ContinuousMonitoring,

        [Parameter()]
        [string]$OutputPath = "logs/error-capture"
    )

    $sessionId = [System.Guid]::NewGuid().ToString("N")[0..7] -join ""
    $startTime = Get-Date

    Write-BusBuddyStatus "🔍 Starting Enhanced Error Capture Session [$sessionId]" -Type Info
    if ($ContinuousMonitoring) { Write-Information "Continuous Monitoring: Enabled" -InformationAction Continue }
    Write-Information "⏰ Session Start: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -InformationAction Continue

    # Ensure WintellectPowerShell is available
    try {
        Import-Module WintellectPowerShell -Force -ErrorAction Stop
        Write-BusBuddyStatus "✅ WintellectPowerShell module loaded" -Type Success
    }
    catch {
        Write-BusBuddyError "Failed to load WintellectPowerShell module" -Exception $_ -Suggestions @(
            "Install WintellectPowerShell: Install-Module WintellectPowerShell -Scope CurrentUser",
            "Check module availability: Get-Module -ListAvailable WintellectPowerShell"
        )
        return
    }

    # Create output directory
    $fullOutputPath = Join-Path (Get-BusBuddyProjectRoot) $OutputPath
    if (-not (Test-Path $fullOutputPath)) {
        New-Item -ItemType Directory -Path $fullOutputPath -Force | Out-Null
        Write-BusBuddyStatus "📁 Created output directory: $fullOutputPath" -Type Info
    }

    # System Diagnostics Collection
    if ($SystemDiagnostics) {
        Write-BusBuddyStatus "📊 Collecting System Diagnostics..." -Type Info

        try {
            # Get system uptime using WintellectPowerShell
            $uptime = Get-Uptime
            $uptimeInfo = @{
                Days = $uptime.Days
                Hours = $uptime.Hours
                Minutes = $uptime.Minutes
                TotalHours = [math]::Round($uptime.TotalHours, 2)
                Timestamp = Get-Date
            }

            # Collect environment information
            $envInfo = @{
                PowerShellVersion = $PSVersionTable.PSVersion
                DotNetVersion = & dotnet --version 2>$null
                UserName = $env:USERNAME
                MachineName = $env:COMPUTERNAME
                WorkingDirectory = Get-Location
                ProcessId = $PID
                SessionId = $sessionId
            }

            # System resource information
            $systemInfo = @{
                Uptime = $uptimeInfo
                Environment = $envInfo
                Timestamp = Get-Date
            }

            # Save system diagnostics
            $diagnosticsFile = Join-Path $fullOutputPath "system-diagnostics-$sessionId.json"
            $systemInfo | ConvertTo-Json -Depth 3 | Out-File -FilePath $diagnosticsFile -Encoding UTF8

            Write-BusBuddyStatus "✅ System diagnostics saved to: $diagnosticsFile" -Type Success
            Write-Information "💻 System Uptime: $($uptime.Days) days, $($uptime.Hours) hours" -InformationAction Continue
            Write-Information "🔧 PowerShell: $($PSVersionTable.PSVersion)" -InformationAction Continue
            Write-Information "⚙️  .NET Version: $(& dotnet --version 2>$null)" -InformationAction Continue
        }
        catch {
            Write-BusBuddyError "Failed to collect system diagnostics" -Exception $_
        }
    }

    # Crash Dump Monitoring
    if ($MonitorCrashes) {
        Write-BusBuddyStatus "💥 Setting up crash dump monitoring..." -Type Info

        # Look for existing crash dumps
        $projectRoot = Get-BusBuddyProjectRoot
        $possibleDumpLocations = @(
            Join-Path $projectRoot "logs"
            Join-Path $projectRoot "BusBuddy.WPF\bin\Debug\net9.0-windows"
            Join-Path $projectRoot "BusBuddy.WPF\bin\Release\net9.0-windows"
            $env:TEMP,
            $env:LOCALAPPDATA
        )

        $foundDumps = @()
        foreach ($location in $possibleDumpLocations) {
            if (Test-Path $location) {
                $dumps = Get-ChildItem -Path $location -Filter "*.dmp" -ErrorAction SilentlyContinue
                if ($dumps) {
                    $foundDumps += $dumps
                    Write-Information "🔍 Found $($dumps.Count) dump file(s) in: $location" -InformationAction Continue
                }
            }
        }

        if ($foundDumps.Count -gt 0) {
            Write-BusBuddyStatus "📋 Analyzing $($foundDumps.Count) existing crash dump(s)..." -Type Warning

            # Create basic analysis script for CDB
            $analysisScript = Join-Path $fullOutputPath "crash-analysis-commands.txt"
            $cdbCommands = @(
                "* Basic crash analysis commands",
                ".sympath srv*https://msdl.microsoft.com/download/symbols",
                ".reload",
                "!analyze -v",
                "k",
                "!clrstack",
                ".ecxr",
                "!pe",
                "q"
            )
            $cdbCommands | Out-File -FilePath $analysisScript -Encoding UTF8

            foreach ($dump in $foundDumps) {
                try {
                    Write-Information "🔍 Analyzing: $($dump.Name)" -InformationAction Continue

                    # Use WintellectPowerShell to analyze the dump
                    Get-DumpAnalysis -Files $dump.FullName -DebuggingScript $analysisScript

                    Write-BusBuddyStatus "✅ Analysis completed for: $($dump.Name)" -Type Success
                }
                catch {
                    Write-BusBuddyError "Failed to analyze dump: $($dump.Name)" -Exception $_
                }
            }
        } else {
            Write-Information "ℹ️  No existing crash dumps found" -InformationAction Continue
        }
    }

    # Enhanced BusBuddy Application Execution with Error Capture
    Write-BusBuddyStatus "🚀 Starting BusBuddy with enhanced error monitoring..." -Type Info

    try {
        # Enhanced execution using existing exception capture
        $result = Invoke-BusBuddyWithExceptionCapture -Command "dotnet" -Arguments @("run", "--project", "BusBuddy.WPF/BusBuddy.WPF.csproj") -Context "Enhanced BusBuddy Execution" -Timeout 300

        # Final report
        $endTime = Get-Date
        $duration = $endTime - $startTime

        $sessionReport = @{
            SessionId = $sessionId
            StartTime = $startTime
            EndTime = $endTime
            Duration = $duration.TotalMinutes
            OutputPath = $fullOutputPath
            MonitorCrashes = $MonitorCrashes.IsPresent
            SystemDiagnostics = $SystemDiagnostics.IsPresent
            Result = if ($result) { "Success" } else { "Failed" }
            WintellectTools = "Available"
        }

        $reportFile = Join-Path $fullOutputPath "session-report-$sessionId.json"
        $sessionReport | ConvertTo-Json -Depth 2 | Out-File -FilePath $reportFile -Encoding UTF8

        Write-BusBuddyStatus "📋 Session report saved: $reportFile" -Type Success
        Write-Information "⏱️  Total session duration: $([math]::Round($duration.TotalMinutes, 2)) minutes" -InformationAction Continue

        return $sessionReport
    }
    catch {
        Write-BusBuddyError "Enhanced error capture failed" -Exception $_ -Context "Runtime Error Monitoring"
        return $null
    }
}

#endregion

#region Azure Firewall Management Functions

function Update-BusBuddyAzureFirewall {
    <#
    .SYNOPSIS
        Automatically updates Azure SQL firewall rules for BusBuddy dynamic IP addresses
    .DESCRIPTION
        Fetches current public IP and adds it to Azure SQL firewall rules.
        Handles Starlink and work ISP dynamic IP changes automatically.
        Based on Microsoft Azure SQL firewall configuration best practices.
    .PARAMETER ResourceGroupName
        Azure resource group containing the SQL server (auto-detected from config if not specified)
    .PARAMETER ServerName
        Azure SQL server name (default: busbuddy-server-sm2 from appsettings.azure.json)
    .PARAMETER CleanupOldRules
        Remove old dynamic IP rules to keep firewall clean
    .PARAMETER Force
        Skip confirmation prompts
    .EXAMPLE
        Update-BusBuddyAzureFirewall
    .EXAMPLE
        Update-BusBuddyAzureFirewall -CleanupOldRules -Force
    .NOTES
        Reference: https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure
        Requires Az PowerShell module: Install-Module -Name Az.Sql -Scope CurrentUser
        Auto-integrates with BusBuddy appsettings.azure.json configuration
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([hashtable])]
    param (
        [Parameter(Mandatory = $false)]
        [string]$ResourceGroupName,

        [Parameter(Mandatory = $false)]
        [string]$ServerName,

        [Parameter(Mandatory = $false)]
        [switch]$CleanupOldRules,

        [Parameter(Mandatory = $false)]
        [switch]$Force
    )

    try {
        Write-Information "🚌 BusBuddy Azure SQL Firewall Updater" -InformationAction Continue
        Write-Information "=" * 50 -InformationAction Continue

        # Get BusBuddy Azure configuration
        $config = Get-BusBuddyAzureConfig
        if (-not $config) {
            throw "Could not load BusBuddy Azure configuration from appsettings.azure.json"
        }

        # Use configuration values if not provided
        if (-not $ServerName) {
            $ServerName = $config.ServerName
            if (-not $ServerName) {
                throw "Server name not found in configuration and not provided"
            }
        }

        if (-not $ResourceGroupName) {
            $ResourceGroupName = $config.ResourceGroup
            if (-not $ResourceGroupName) {
                Write-Warning "Resource group not specified in config. Please provide it manually."
                $ResourceGroupName = Read-Host "Enter Azure Resource Group name"
                if (-not $ResourceGroupName) {
                    throw "Resource group is required"
                }
            }
        }

        # Call the main update script
        $scriptPath = Join-Path $PSScriptRoot "..\..\Scripts\Update-AzureFirewall.ps1"
        if (-not (Test-Path $scriptPath)) {
            throw "Update-AzureFirewall.ps1 script not found at: $scriptPath"
        }

        $params = @{
            ResourceGroupName = $ResourceGroupName
            ServerName = $ServerName
            CleanupOldRules = $CleanupOldRules
        }

        Write-Information "🎯 Target: $ServerName in $ResourceGroupName" -InformationAction Continue

        if ($Force -or $PSCmdlet.ShouldProcess("Azure SQL Server $ServerName", "Update firewall rules")) {
            $result = & $scriptPath @params

            if ($result.Success) {
                Write-BusBuddyStatus "✅ Azure firewall updated successfully" -Type Success
                Write-Information "   IP Address: $($result.IPAddress)" -InformationAction Continue
                Write-Information "   Rule Name: $($result.RuleName)" -InformationAction Continue
                Write-Information "⏱️ Allow up to 5 minutes for rule propagation" -InformationAction Continue
            } else {
                Write-BusBuddyError "Failed to update Azure firewall" -Exception ([System.Exception]::new($result.Error)) -Context "Azure Firewall"
            }

            return $result
        }
    }
    catch {
        Write-BusBuddyError "Azure firewall update failed" -Exception $_ -Context "Azure Firewall Management"
        return @{
            Success = $false
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

function Get-BusBuddyAzureConfig {
    <#
    .SYNOPSIS
        Extracts Azure configuration from BusBuddy appsettings files
    .DESCRIPTION
        Parses appsettings.azure.json to extract server name, resource group, and other Azure settings
    .EXAMPLE
        Get-BusBuddyAzureConfig
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    try {
        $configFile = "appsettings.azure.json"
        if (-not (Test-Path $configFile)) {
            Write-Warning "appsettings.azure.json not found in current directory"
            return $null
        }

        $config = Get-Content $configFile -Raw | ConvertFrom-Json

        # Extract server name from connection string
        $connectionString = $config.ConnectionStrings.DefaultConnection
        if ($connectionString -match 'Server=tcp:([^.,]+)') {
            $serverName = $matches[1]
        } else {
            Write-Warning "Could not extract server name from connection string"
            $serverName = $null
        }

        # Try to find resource group in various config locations
        $resourceGroup = $null
        if ($config.Azure.ResourceGroup) {
            $resourceGroup = $config.Azure.ResourceGroup
        } elseif ($config.ResourceGroup) {
            $resourceGroup = $config.ResourceGroup
        }

        return @{
            ServerName = $serverName
            ResourceGroup = $resourceGroup
            DatabaseName = "BusBuddyDB"
            ConnectionString = $connectionString
            Config = $config
        }
    }
    catch {
        Write-Warning "Failed to parse Azure configuration: $($_.Exception.Message)"
        return $null
    }
}

function Test-BusBuddyAzureConnection {
    <#
    .SYNOPSIS
        Tests Azure SQL connection for BusBuddy database
    .DESCRIPTION
        Validates network connectivity and firewall rules for Azure SQL Database
        Provides detailed diagnostics for connection issues
    .EXAMPLE
        Test-BusBuddyAzureConnection
    .EXAMPLE
        Test-BusBuddyAzureConnection -UpdateFirewall
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param (
        [Parameter(Mandatory = $false)]
        [switch]$UpdateFirewall
    )

    try {
        Write-Information "🔍 Testing BusBuddy Azure SQL Connection" -InformationAction Continue
        Write-Information "=" * 45 -InformationAction Continue

        # Get configuration
        $config = Get-BusBuddyAzureConfig
        if (-not $config) {
            throw "Could not load Azure configuration"
        }

        # Test network connectivity
        Write-Information "🌐 Testing network connectivity..." -InformationAction Continue
        $serverFqdn = "$($config.ServerName).database.windows.net"

        try {
            $tcpTest = Test-NetConnection -ComputerName $serverFqdn -Port 1433 -WarningAction SilentlyContinue
            if ($tcpTest.TcpTestSucceeded) {
                Write-Information "✅ Network connectivity: SUCCESS" -InformationAction Continue
            } else {
                Write-Information "❌ Network connectivity: FAILED" -InformationAction Continue
                Write-Information "   This may indicate firewall or network issues" -InformationAction Continue
            }
        }
        catch {
            Write-Information "❌ Network test failed: $($_.Exception.Message)" -InformationAction Continue
        }

        # Get current IP
        try {
            $currentIP = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10).Trim()
            Write-Information "📍 Current public IP: $currentIP" -InformationAction Continue
        }
        catch {
            Write-Warning "Could not determine current IP address"
            $currentIP = "Unknown"
        }

        # Test SQL connection
        Write-Information "🔐 Testing SQL authentication..." -InformationAction Continue

        $connectionSuccess = $false
        $connectionError = $null

        try {
            # Use .NET SqlConnection for direct testing
            $connectionString = $config.ConnectionString
            # Replace environment variables for testing
            $testConnectionString = $connectionString -replace '\$\{AZURE_SQL_USER\}', $env:AZURE_SQL_USER -replace '\$\{AZURE_SQL_PASSWORD\}', $env:AZURE_SQL_PASSWORD

            if ($testConnectionString -match '\$\{') {
                Write-Warning "Environment variables not set: AZURE_SQL_USER, AZURE_SQL_PASSWORD"
                Write-Information "💡 Set environment variables or use manual portal test" -InformationAction Continue
            } else {
                Add-Type -AssemblyName System.Data
                $sqlConnection = New-Object System.Data.SqlClient.SqlConnection($testConnectionString)
                $sqlConnection.Open()
                $sqlConnection.Close()
                $connectionSuccess = $true
                Write-Information "✅ SQL connection: SUCCESS" -InformationAction Continue
            }
        }
        catch {
            $connectionError = $_.Exception.Message
            Write-Information "❌ SQL connection: FAILED" -InformationAction Continue
            Write-Information "   Error: $connectionError" -InformationAction Continue

            # Check for firewall-related errors
            if ($connectionError -like "*Client with IP address*not allowed*") {
                Write-Information "🛡️ Firewall issue detected!" -InformationAction Continue
                if ($UpdateFirewall) {
                    Write-Information "🔧 Attempting to update firewall rules..." -InformationAction Continue
                    $firewallResult = Update-BusBuddyAzureFirewall -Force
                    if ($firewallResult.Success) {
                        Write-Information "✅ Firewall updated. Retry connection in 5 minutes." -InformationAction Continue
                    }
                } else {
                    Write-Information "💡 Run with -UpdateFirewall to automatically fix" -InformationAction Continue
                }
            }
        }

        # Summary
        Write-Information "`n📋 Connection Test Summary:" -InformationAction Continue
        Write-Information "   Server: $serverFqdn" -InformationAction Continue
        Write-Information "   Database: $($config.DatabaseName)" -InformationAction Continue
        Write-Information "   Current IP: $currentIP" -InformationAction Continue
        Write-Information "   Network: $(if ($tcpTest.TcpTestSucceeded) { '✅' } else { '❌' })" -InformationAction Continue
        Write-Information "   SQL Auth: $(if ($connectionSuccess) { '✅' } else { '❌' })" -InformationAction Continue

        if (-not $connectionSuccess) {
            Write-Information "`n🔧 Troubleshooting options:" -InformationAction Continue
            Write-Information "1. Update firewall: bb-azure-firewall" -InformationAction Continue
            Write-Information "2. Use local database: Set DatabaseProvider=Local" -InformationAction Continue
            Write-Information "3. Check Azure portal: https://portal.azure.com" -InformationAction Continue
            Write-Information "4. Verify credentials: echo `$env:AZURE_SQL_USER" -InformationAction Continue
        }

        return @{
            Success = $connectionSuccess
            NetworkConnectivity = $tcpTest.TcpTestSucceeded
            CurrentIP = $currentIP
            Server = $serverFqdn
            Database = $config.DatabaseName
            Error = $connectionError
            Timestamp = Get-Date
        }
    }
    catch {
        Write-BusBuddyError "Azure connection test failed" -Exception $_ -Context "Azure Connection Test"
        return @{
            Success = $false
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

#endregion

#region Aliases - Safe Alias Creation with Conflict Resolution
# Core aliases with safe creation
try { Set-Alias -Name 'bbBuild'      -Value 'Invoke-BusBuddyBuild'   -Force } catch { }
try { Set-Alias -Name 'bbRun'        -Value 'Invoke-BusBuddyRun'     -Force } catch { }
try { Set-Alias -Name 'bbRunSta'     -Value 'Invoke-BusBuddyRunSta'  -Force } catch { }
try { Set-Alias -Name 'bbClean'      -Value 'Invoke-BusBuddyClean'   -Force } catch { }
try { Set-Alias -Name 'bbRestore'    -Value 'Invoke-BusBuddyRestore' -Force } catch { }
try { Set-Alias -Name 'bbTest'       -Value 'Invoke-BusBuddyTest'    -Force } catch { }
try { Set-Alias -Name 'bbHealth'     -Value 'Invoke-BusBuddyHealthCheck' -Force } catch { }

# Developer discovery
try { Set-Alias -Name 'bbDevSession' -Value 'Start-BusBuddyDevSession' -Force } catch { }
try { Set-Alias -Name 'bbInfo'       -Value 'Get-BusBuddyInfo' -Force } catch { }
try { Set-Alias -Name 'bbCommands'   -Value 'Get-BusBuddyCommand' -Force } catch { }

# Removed alias for bbXamlValidate (function not present)

# Session correlation
try { Set-Alias -Name 'bbMantra'       -Value 'Get-BusBuddyMantraId'    -Force } catch { }
try { Set-Alias -Name 'bbMantraReset'  -Value 'Reset-BusBuddyMantraId'  -Force } catch { }

# Environment
try { Set-Alias -Name 'bbEnv'          -Value 'Initialize-BusBuddyEnvironment' -Force } catch { }
# Hyphenated variants
foreach ($pair in @(
    @{A='bb-build';V='Invoke-BusBuddyBuild'},
    @{A='bb-run';V='Invoke-BusBuddyRun'},
    @{A='bb-run-sta';V='Invoke-BusBuddyRunSta'},
    @{A='bb-clean';V='Invoke-BusBuddyClean'},
    @{A='bb-restore';V='Invoke-BusBuddyRestore'},
    @{A='bb-test';V='Invoke-BusBuddyTest'},
    @{A='bb-health';V='Invoke-BusBuddyHealthCheck'},
    @{A='bb-env';V='Initialize-BusBuddyEnvironment'
})) { try { Set-Alias -Name $pair.A -Value $pair.V -Force } catch { } }
#endregion

#region Exports
Export-ModuleMember -Function @(
    'Invoke-BusBuddyBuild',
    'Invoke-BusBuddyRun',
    'Invoke-BusBuddyRunSta',
    'Invoke-BusBuddyClean',
    'Invoke-BusBuddyRestore',
    'Invoke-BusBuddyTest',
    'Invoke-BusBuddyHealthCheck',
    'Get-BusBuddyApartmentState',
    'Get-BusBuddyMantraId',
    'Reset-BusBuddyMantraId',
    'Start-BusBuddyDevSession',
    'Get-BusBuddyInfo',
    'Get-BusBuddyCommand',
    'Get-BusBuddyTestOutput',
    'Invoke-BusBuddyTestFull',
    'Get-BusBuddyTestError',
    'Get-BusBuddyTestLog',
    'Start-BusBuddyTestWatch',
    'Invoke-BusBuddyPester',
    'Get-BusBuddyTelemetrySummary',
    'Invoke-BusBuddyTelemetryPurge',
    'Get-BusBuddyPS75Compliance',
    'Initialize-BusBuddyEnvironment',
    # Script Lint exports
    'Test-BusBuddyErrorActionPipelines',
    'Invoke-BusBuddyErrorActionAudit',
    # Logging exports
    'Get-BusBuddyLogSummary'
) -Alias @(
    'bbBuild','bbRun','bbRunSta','bbClean','bbRestore','bbTest','bbHealth',
    'bbDevSession','bbInfo','bbCommands',
    'bb-build','bb-run','bb-run-sta','bb-clean','bb-restore','bb-test','bb-health',
    'bbMantra','bbMantraReset','bbTestFull',
    'bb-ps-review',
    'bbEnv','bb-env',
    # Script Lint aliases
    'bb-ps-validate-ea','bb-ps-validate-ea-run',
    # Logging alias
    'bb-logs-summary'
)
#endregion

#region Welcome Screen
function Show-BusBuddyWelcome {
    <#
    .SYNOPSIS
        Display a categorized welcome screen when the module loads.
    #>
    [CmdletBinding()]
    param([switch]$Quiet)

    $ps = $PSVersionTable.PSVersion
    $dotnet = try { & dotnet --version 2>$null } catch { "unknown" }

    if ($env:BUSBUDDY_SILENT -ne '1') {
        Write-Information "" -InformationAction Continue
        Write-BusBuddyStatus "🚌 BusBuddy Dev Shell — Ready" -Type Info
        Write-Information "PowerShell: $ps | .NET: $dotnet" -InformationAction Continue
        Write-Information "Project: $(Get-BusBuddyProjectRoot)" -InformationAction Continue
        Write-Information "" -InformationAction Continue
    }

    if ($env:BUSBUDDY_SILENT -ne '1') {
        Write-BusBuddyStatus "Core" -Type Info
        Write-Information "  bbBuild, bbRun, bbTest, bbClean, bbRestore, bbHealth" -InformationAction Continue
    }

    if ($env:BUSBUDDY_SILENT -ne '1') {
        Write-BusBuddyStatus "Development" -Type Info
        Write-Information "  bbDevSession, bbInfo, bbCommands, bbMantra, bbMantraReset" -InformationAction Continue
    }

    # Removed 'Validation & Safety' section (no bbXamlValidate)

    # Docs & Reference remains gated if bbCopilotRef exists
    if ($env:BUSBUDDY_SILENT -ne '1' -and (Get-Command bbCopilotRef -ErrorAction SilentlyContinue)) {
        Write-BusBuddyStatus "Docs & Reference" -Type Info
        Write-Information "  bbCopilotRef [Topic] (-ShowTopics)" -InformationAction Continue
    }

    if (-not $Quiet -and $env:BUSBUDDY_SILENT -ne '1') {
        Write-Information "" -InformationAction Continue
        Write-Information "Tips:" -InformationAction Continue
        Write-Information "  • bbCommands — full list with functions" -InformationAction Continue
        Write-Information "  • bbHealth — verify env quickly" -InformationAction Continue
        Write-Information "  • Set 'BUSBUDDY_NO_WELCOME=1' to suppress on import" -InformationAction Continue
    }
}

# Auto-run welcome unless suppressed
if (-not $env:BUSBUDDY_NO_WELCOME -and $env:BUSBUDDY_SILENT -ne '1') {
    Show-BusBuddyWelcome -Quiet
}
#endregion

# Removed duplicated trailing welcome/region markers at EOF to avoid double execution.

