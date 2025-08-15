[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$InformationPreference = 'Continue'

# References:
# - Modules: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Modules
# - Manifests: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Module_Manifests
# - Import-Module: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/import-module
# - Test-ModuleManifest: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/test-modulemanifest
# - Export-ModuleMember: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/export-modulemember
# - Try/Catch: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Try_Catch_Finally
# - Streams (no Write-Host): https://learn.microsoft.com/powershell/scripting/learn/deep-dives/everything-about-output-streams
# - ShouldProcess: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/should-process
# - PSScriptAnalyzer: https://learn.microsoft.com/powershell/utility-modules/psscriptanalyzer/overview
# - Import-PowerShellDataFile: https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/import-powershelldatafile
# - Select-String (code scanning): https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/select-string
# - Get-Alias: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/get-alias

$reportDir = Join-Path $RepoRoot 'Documentation/Reports'
$null = New-Item -ItemType Directory -Path $reportDir -Force
$psaReport = Join-Path $reportDir 'PSScriptAnalyzerReport-Diag.txt'
$logConfReport = Join-Path $reportDir 'LoggingConflicts-Diag.txt'

Write-Information "RepoRoot: $RepoRoot"
Write-Information "PowerShell: $($PSVersionTable.PSVersion)"
Write-Information "Report directory: $reportDir"

function Invoke-TryImportViaProfile {
    param([string]$Root)

    $profileLoader = Join-Path $Root 'PowerShell/Profiles/Import-BusBuddyModule.ps1'
    if (-not (Test-Path $profileLoader)) {
        Write-Information "Profile loader not found: $profileLoader"
        return $false
    }

    try {
        Write-Information "Invoking profile loader: $profileLoader"
        & $profileLoader
        return $true
    }
    catch {
        Write-Information "Profile loader failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-And-ImportManifests {
    param([string]$Root)

    $found = @()
    try {
        $found = Get-ChildItem -Path $Root -Recurse -Filter '*.psd1' -ErrorAction Stop |
                 Where-Object { $_.Name -match '^BusBuddy(\.Testing)?\.psd1$' }
    }
    catch {
        Write-Information "Manifest search failed: $($_.Exception.Message)"
    }

    if (-not $found) {
        Write-Information "No BusBuddy*.psd1 manifests found under $Root"
        return @()
    }

    $loaded = @()
    foreach ($m in $found) {
        Write-Information "Testing manifest: $($m.FullName)"
        try {
            $manifestInfo = Test-ModuleManifest -Path $m.FullName -ErrorAction Stop
            Write-Information "Manifest OK — ModuleName: $($manifestInfo.Name); Version: $($manifestInfo.Version)"
            try {
                $mod = Import-Module -Name $m.FullName -PassThru -Force -ErrorAction Stop
                Write-Information "Imported: $($mod.Name) v$($mod.Version)"
                $loaded += $mod
            }
            catch {
                Write-Information "Import-Module failed for $($m.FullName): $($_.Exception.Message)"
            }
        }
        catch {
            Write-Information "Test-ModuleManifest failed for $($m.FullName): $($_.Exception.Message)"
        }
    }
    return $loaded
}

function Test-Aliases {
    param([string[]]$Names)

    $missing = @()
    foreach ($n in $Names) {
        $cmd = Get-Command -Name $n -ErrorAction SilentlyContinue
        if (-not $cmd) { $missing += $n }
    }
    return $missing
}

# New: Inspect manifest exports for correctness (FunctionsToExport/AliasesToExport).
# Docs: Module manifests — FunctionsToExport/AliasesToExport
# https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Module_Manifests
function Get-ManifestExports {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$Root,
        [Parameter(Mandatory)] [string[]]$ExpectedAliases
    )
    $results = @()
    try {
        $manifests = Get-ChildItem -Path $Root -Recurse -Filter '*.psd1' -ErrorAction Stop |
                     Where-Object { $_.Name -match '^BusBuddy(\.Testing)?\.psd1$' }
    }
    catch {
        Write-Information "Get-ManifestExports — search failed: $($_.Exception.Message)"
        return @()
    }

    foreach ($m in $manifests) {
        try {
            # Parse manifest data safely
            # Docs: Import-PowerShellDataFile https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/import-powershelldatafile
            $data = Import-PowerShellDataFile -Path $m.FullName
            $aliases = @($data.AliasesToExport)
            $funcs   = @($data.FunctionsToExport)
            $aliasesIsAll = ($aliases -and $aliases.Count -eq 1 -and $aliases[0] -eq '*')
            $funcsIsAll   = ($funcs -and $funcs.Count -eq 1 -and $funcs[0] -eq '*')

            $missingFromManifest = @()
            if (-not $aliasesIsAll -and $aliases) {
                $missingFromManifest = $ExpectedAliases | Where-Object { $_ -notin $aliases }
            }

            $results += [pscustomobject]@{
                ManifestPath          = $m.FullName
                RootModule            = ($data.RootModule ?? $data.ModuleToProcess)
                ModuleVersion         = $data.ModuleVersion
                AliasesToExport       = if ($aliasesIsAll) { '*' } else { $aliases }
                FunctionsToExport     = if ($funcsIsAll) { '*' } else { $funcs }
                MissingAliasesInPsd1  = $missingFromManifest
                AliasesWildcardExport = $aliasesIsAll
                FunctionsWildcard     = $funcsIsAll
            }
        }
        catch {
            Write-Information "Get-ManifestExports — parse failed for $($m.FullName): $($_.Exception.Message)"
            $results += [pscustomobject]@{
                ManifestPath          = $m.FullName
                RootModule            = $null
                ModuleVersion         = $null
                AliasesToExport       = $null
                FunctionsToExport     = $null
                MissingAliasesInPsd1  = @()
                AliasesWildcardExport = $false
                FunctionsWildcard     = $false
                Error                 = $_.Exception.Message
            }
        }
    }
    return $results
}

# New: Show what each expected alias currently points to (to surface export/alias wiring issues).
# Docs: Get-Alias https://learn.microsoft.com/powershell/module/microsoft.powershell.core/get-alias
function Get-AliasTargets {
    [CmdletBinding()]
    param([string[]]$Names)

    $map = @{
    }
    foreach ($n in $Names) {
        $a = Get-Alias -Name $n -ErrorAction SilentlyContinue
        if ($a) {
            $map[$n] = @{
                Name       = $n
                Definition = $a.Definition
                Options    = "$($a.Options)"
                Module     = $a.Module
            }
        }
    }
    return $map
}

# New: Scan source for logging policy conflicts — Serilog-only rule enforcement.
# - Flags Microsoft.Extensions.Logging and Application Insights usages
# Docs: Select-String https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/select-string
function Find-LoggingConflicts {
    [CmdletBinding()]
    param([Parameter(Mandatory)] [string]$Root)

    $patterns = @(
        'Microsoft.Extensions.Logging',
        'Microsoft.ApplicationInsights',
        'ApplicationInsights',
        'Serilog'
    )
    $paths = @(
        (Join-Path $Root '**\*.cs'),
        (Join-Path $Root '**\*.xaml.cs')
    )

    $result = [ordered]@{ }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("Logging Conflict Scan — Serilog-only policy")
    [void]$sb.AppendLine("Root: $Root")
    [void]$sb.AppendLine("Rules: No Microsoft.Extensions.Logging, prefer Serilog (see repo copilot-instructions.md)")
    [void]$sb.AppendLine("")

        foreach ($p in $patterns) {
            try {
                $matchResults = Select-String -Path $paths -Pattern $p -SimpleMatch -ErrorAction SilentlyContinue
                $files = $matchResults | Select-Object -ExpandProperty Path -Unique
                $result[$p] = [pscustomobject]@{
                    OccurrenceCount = ($matchResults | Measure-Object).Count
                    Files           = $files
                }

            [void]$sb.AppendLine(("Pattern: {0} — Matches: {1}" -f $p, ($matchResults | Measure-Object).Count))
            ($files | Select-Object -First 10) | ForEach-Object { [void]$sb.AppendLine("  - $_") }
            if ($files.Count -gt 10) { [void]$sb.AppendLine("  - ...") }
            [void]$sb.AppendLine("")
        }
        catch {
            [void]$sb.AppendLine("Scan failed for pattern '$p': $($_.Exception.Message)")
        }
    }

    try { $sb.ToString() | Set-Content -Path $logConfReport -Encoding UTF8 -Force } catch {}
    return $result
}

$expectedAliases = @(
    'bbHealth','bbBuild','bbRun','bbTest','bbMvpCheck',
    'bbAntiRegression','bbXamlValidate','bbDevSession','bbRefresh',
    'bbCommands','bbTestWatch','bbTestReport'
)

$importOk = Invoke-TryImportViaProfile -Root $RepoRoot
if (-not $importOk) {
    Write-Information "Falling back to direct manifest discovery/import…"
    $mods = Test-And-ImportManifests -Root $RepoRoot
    if (-not $mods) {
        Write-Information "No modules imported via fallback."
    }
}

Write-Information "Verifying expected bb* aliases…"
$missingAliases = Test-Aliases -Names $expectedAliases
if ($missingAliases.Count -gt 0) {
    Write-Information ("Missing aliases: " + ($missingAliases -join ', '))
} else {
    Write-Information "All expected bb* aliases are available."
}

Write-Information "Verifying BusBuddy modules present in session…"
$presentMods = Get-Module | Where-Object { $_.Name -in @('BusBuddy','BusBuddy.Testing') }
if ($presentMods) {
    $presentMods | ForEach-Object { Write-Information "Loaded: $($_.Name) v$($_.Version)" }
} else {
    Write-Information "BusBuddy modules are not loaded."
}

# New: Manifest export inspection and alias target mapping (actionable diagnostics).
$manifestExportInfo = Get-ManifestExports -Root $RepoRoot -ExpectedAliases $expectedAliases
if ($manifestExportInfo) {
    foreach ($i in $manifestExportInfo) {
        if ($i.MissingAliasesInPsd1 -and -not $i.AliasesWildcardExport) {
            Write-Information "Manifest missing aliases in $($i.ManifestPath): $($i.MissingAliasesInPsd1 -join ', ')"
        }
    }
} else {
    Write-Information "No manifest export info collected."
}

$aliasTargets = Get-AliasTargets -Names $expectedAliases
if ($aliasTargets.Keys.Count -gt 0) {
    Write-Information "Alias targets detected:"
    foreach ($k in $aliasTargets.Keys) {
        $t = $aliasTargets[$k]
        Write-Information ("  {0} -> {1}" -f $t.Name, $t.Definition)
    }
} else {
    Write-Information "No alias targets resolved."
}

Write-Information "Running ScriptAnalyzer (PSAvoidEmptyCatchBlock, PSUseShouldProcess, PSAvoidUsingWriteHost)…"
try {
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        Write-Information "Installing PSScriptAnalyzer for current user…"
        Install-Module PSScriptAnalyzer -Scope CurrentUser -Force -ErrorAction Stop
    }
    Import-Module PSScriptAnalyzer -ErrorAction Stop
    $psPath = Join-Path $RepoRoot 'PowerShell'
    if (Test-Path $psPath) {
        $analysis = Invoke-ScriptAnalyzer -Path $psPath -Recurse `
            -Severity Error,Warning `
            -IncludeRule PSAvoidEmptyCatchBlock,PSUseShouldProcess,PSAvoidUsingWriteHost `
            -ErrorAction SilentlyContinue
        $analysis | Tee-Object -FilePath $psaReport | Out-Null
        $emptyCatch = @($analysis | Where-Object { $_.RuleName -eq 'PSAvoidEmptyCatchBlock' })
        $shouldProc = @($analysis | Where-Object { $_.RuleName -eq 'PSUseShouldProcess' })
        $writeHost  = @($analysis | Where-Object { $_.RuleName -eq 'PSAvoidUsingWriteHost' })

        Write-Information ("Analyzer findings — EmptyCatch:{0} ShouldProcess:{1} Write-Host:{2}" -f `
            $emptyCatch.Count, $shouldProc.Count, $writeHost.Count)
        Write-Information "Full analyzer report: $psaReport"
    } else {
        Write-Information "PowerShell folder not found at: $psPath"
    }
}
catch {
    Write-Information "ScriptAnalyzer step failed: $($_.Exception.Message)"
}

# New: Serilog-only policy check (flags Microsoft.Extensions.Logging/Application Insights usage).
$loggingConflicts = Find-LoggingConflicts -Root $RepoRoot
Write-Information "Logging conflict scan written to: $logConfReport"

Write-Information "Summary:"
Write-Output (@{
    RepoRoot        = $RepoRoot
    ModulesLoaded   = ($presentMods | ForEach-Object Name)
    MissingAliases  = $missingAliases
    AliasTargets    = ($aliasTargets.GetEnumerator() | ForEach-Object { $_.Value })
    Manifests       = $manifestExportInfo
    AnalyzerReport  = (Test-Path $psaReport)
    AnalyzerPath    = $psaReport
    LoggingConflicts= $loggingConflicts
    LoggingReport   = (Test-Path $logConfReport)
    LoggingPath     = $logConfReport
} | ConvertTo-Json -Depth 5)
