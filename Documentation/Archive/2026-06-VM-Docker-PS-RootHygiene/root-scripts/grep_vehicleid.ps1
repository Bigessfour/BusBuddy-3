<#!
.SYNOPSIS
    Reports remaining occurrences of 'VehicleId' across active source files to support BusId refactor completion.
.DESCRIPTION
    Searches for the token 'VehicleId' (case-insensitive optional) in code files (.cs, .xaml, .ps1) while excluding
    build artifacts, migration folders, and documentation-only paths. Outputs a colorized summary to the console
    and writes a CSV plus a JSON detail file under ./logs/ for traceability.

    Based on Microsoft PowerShell module & output stream guidance:
    - Uses Write-Output / Write-Information instead of Write-Host
    - Structured objects emitted for pipeline consumption
    Docs: https://learn.microsoft.com/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines

.NOTES
    Remove/retire this script after the compatibility property 'VehicleId' is eliminated from the Bus entity.
#>
[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path -LiteralPath '.').Path,
    [switch]$CaseSensitive,
    [switch]$IncludeDocs,
    [switch]$IncludeMigrations
)
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Write-Information "Starting VehicleId usage scan in: $Root" -InformationAction Continue

$excludeDirectories = @('bin', 'obj', '.git', 'TestResults', 'artifacts')
if (-not $IncludeMigrations) { $excludeDirectories += 'Migrations' }
if (-not $IncludeDocs) { $excludeDirectories += 'Documentation', 'docs' }

$includeExtensions = '.cs', '.xaml', '.ps1'

$allFiles = Get-ChildItem -Path $Root -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $includeExtensions -contains $_.Extension } |
    Where-Object { $excludeDirectories -notcontains ($_.DirectoryName.Split([IO.Path]::DirectorySeparatorChar) | Select-Object -Last 1) } |
    Where-Object { $excludeDirectories -notmatch (($_.FullName -replace [regex]::Escape($Root),'').Split([IO.Path]::DirectorySeparatorChar)) }

$pattern = 'VehicleId'
$results = @()
foreach ($file in $allFiles) {
    try {
        $selectParams = @{ Path = $file.FullName; Pattern = $pattern }
        if ($CaseSensitive) { $selectParams.Add('CaseSensitive',$true) }
        $matches = Select-String @selectParams
        foreach ($m in $matches) {
            $results += [PSCustomObject]@{
                File        = $m.Path.Substring($Root.Length).TrimStart('\\','/')
                LineNumber  = $m.LineNumber
                LineText    = ($m.Line.Trim())
            }
        }
    }
    catch {
        Write-Warning "Failed scanning $($file.FullName): $($_.Exception.Message)"
    }
}

$logsDir = Join-Path $Root 'logs'
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }

$csvPath = Join-Path $logsDir 'vehicleid-usages.csv'
$jsonPath = Join-Path $logsDir 'vehicleid-usages.json'

if ($results.Count -eq 0) {
    Write-Output "✅ No remaining VehicleId usages found (per current filter set)."
} else {
    $results | Sort-Object File, LineNumber | Tee-Object -Variable sorted | Format-Table -AutoSize | Out-String | Write-Information -InformationAction Continue
    $results | Sort-Object File, LineNumber | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
    $results | Sort-Object File, LineNumber | ConvertTo-Json -Depth 4 | Set-Content -Path $jsonPath -Encoding UTF8
    Write-Output "⚠️ Found $($results.Count) VehicleId usages. CSV: $csvPath JSON: $jsonPath"
}

$stopwatch.Stop()
Write-Output ("Scan duration: {0:N2} seconds" -f $stopwatch.Elapsed.TotalSeconds)

# Emit objects last for pipeline use
$results
