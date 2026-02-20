# BusBuddy Vehicle Reference Scanner
# Finds all remaining instances of "vehicle" in the codebase
# Helps with systematic cleanup of deprecated Vehicle references
#
# Updated: Replaced Write-Host with Write-Output for better PowerShell practices
# Write-Host bypasses the pipeline and should be avoided in scripts

param(
    [string]$Path = $PSScriptRoot,
    [switch]$CaseSensitive,
    [string[]]$ExcludeDirectories = @('.git', 'bin', 'obj', 'node_modules', '.vs', 'TestResults'),
    [string[]]$IncludeExtensions = @('.cs', '.xaml', '.ps1', '.md', '.json', '.xml', '.config'),
    [switch]$SummaryOnly,
    [switch]$FixCommonPatterns
)

function Write-Header {
    param([string]$Text)
    Write-Output "`n$Text"
    Write-Output ("=" * $Text.Length)
}

function Get-VehicleReferences {
    param([string]$SearchPath)

    $results = @()

    # Build include pattern for files
    $includePattern = $IncludeExtensions | ForEach-Object { "*$_" }

    # Build exclude patterns for directories
    $excludePatterns = $ExcludeDirectories | ForEach-Object { "**/$_/**" }

    try {
        # Use Select-String for efficient pattern matching
        $selectStringParams = @{
            Path = $SearchPath
            Pattern = if ($CaseSensitive) { 'vehicle' } else { '(?i)vehicle' }
            Include = $includePattern
            Exclude = $excludePatterns
            AllMatches = $true
        }

        $searchMatches = Select-String @selectStringParams

        # Ensure $searchMatches is not null before grouping
        if ($searchMatches) {
            # Group matches by file
            $fileGroups = $searchMatches | Group-Object Path

            foreach ($fileGroup in $fileGroups) {
                $filePath = $fileGroup.Name
                $fileName = Split-Path $filePath -Leaf
                $fileExtension = [IO.Path]::GetExtension($filePath)

                # Get all matches for this file
                $matchValues = $fileGroup.Group | ForEach-Object { $_.Matches.Value }
                $lineNumbers = $fileGroup.Group | ForEach-Object { $_.LineNumber }

                $results += [PSCustomObject]@{
                    FilePath = $filePath
                    FileName = $fileName
                    Extension = $fileExtension
                    MatchCount = $fileGroup.Count
                    Matches = $matchValues
                    LineNumbers = $lineNumbers
                }
            }
        }
    }
    catch {
        Write-Warning "Error during search: $($_.Exception.Message)"
    }

    # Ensure we always return an array
    if ($null -eq $results) {
        return @()
    } elseif ($results -isnot [array]) {
        return @($results)
    }

    return $results
}

function Show-Summary {
    param([array]$Results)

    Write-Header "VEHICLE REFERENCE SUMMARY"

    # Ensure Results is treated as an array
    if ($null -eq $Results) {
        $Results = @()
    } elseif ($Results -isnot [array]) {
        $Results = @($Results)
    }

    if ($Results.Count -eq 0) {
        Write-Output "✅ No vehicle references found!"
        return
    }

    Write-Output "Found $($Results.Count) files with vehicle references:"
    Write-Output ""

    # Group by file extension
    $byExtension = $Results | Group-Object Extension
    foreach ($group in $byExtension) {
        Write-Output "$($group.Name): $($group.Count) files"
    }

    Write-Output ""
    Write-Output "Total matches: $($Results | Measure-Object -Property MatchCount -Sum | Select-Object -ExpandProperty Sum)"

    # Show top files by match count
    Write-Output "`nTop files by match count:"
    $Results | Sort-Object MatchCount -Descending | Select-Object -First 5 | ForEach-Object {
        Write-Output "  $($_.FileName): $($_.MatchCount) matches"
    }
}

function Show-DetailedResults {
    param([array]$Results)

    Write-Header "DETAILED VEHICLE REFERENCES"

    # Ensure Results is treated as an array
    if ($null -eq $Results) {
        $Results = @()
    } elseif ($Results -isnot [array]) {
        $Results = @($Results)
    }

    foreach ($result in $Results | Sort-Object FilePath) {
        Write-Output "📁 $($result.FileName)"
        Write-Output "   Path: $($result.FilePath)"
        Write-Output "   Matches: $($result.MatchCount)"

        for ($i = 0; $i -lt $result.Matches.Count; $i++) {
            $match = $result.Matches[$i]
            $lineNum = $result.LineNumbers[$i]
            Write-Output "     Line $($lineNum): '$match'"
        }
        Write-Output ""
    }
}

function Update-CommonPatterns {
    param([array]$Results)

    Write-Header "ATTEMPTING TO FIX COMMON PATTERNS"

    $fixedCount = 0

    foreach ($result in $Results) {
        $content = Get-Content $result.FilePath -Raw

        # Common patterns to fix
        $patterns = @(
            @{ Pattern = 'AssignedVehicleId'; Replacement = 'AssignedBusId' }
            @{ Pattern = 'AMVehicleId'; Replacement = 'AMBusId' }
            @{ Pattern = 'PMVehicleId'; Replacement = 'PMBusId' }
            @{ Pattern = 'VehicleFueledId'; Replacement = 'BusFueledId' }
            @{ Pattern = 'AssignedVehicle'; Replacement = 'AssignedBus' }
            @{ Pattern = 'AMVehicle'; Replacement = 'AMBus' }
            @{ Pattern = 'PMVehicle'; Replacement = 'PMBus' }
            @{ Pattern = 'Vehicle'; Replacement = 'Bus' }
        )

        $fileChanged = $false

        foreach ($pattern in $patterns) {
            if ($content -match $pattern.Pattern) {
                $content = $content -replace $pattern.Pattern, $pattern.Replacement
                $fileChanged = $true
                Write-Output "  Fixed: $($pattern.Pattern) -> $($pattern.Replacement)"
            }
        }

        if ($fileChanged) {
            $content | Set-Content $result.FilePath -Encoding UTF8
            $fixedCount++
            Write-Output "  ✅ Updated: $($result.FileName)"
        }
    }

    Write-Output "`nFixed $fixedCount files with common patterns."
}

# Main execution
Write-Header "BUSBUDDY VEHICLE REFERENCE SCANNER"
Write-Output "Searching in: $Path"
Write-Output "Include extensions: $($IncludeExtensions -join ', ')"
Write-Output "Exclude directories: $($ExcludeDirectories -join ', ')"
Write-Output ""

Write-Output "DEBUG: Starting vehicle reference scan..."
$results = Get-VehicleReferences -SearchPath $Path
Write-Output "DEBUG: Scan completed. Results type: $($results.GetType().Name)"
Write-Output "DEBUG: Results count: $($results.Count)"

# Ensure results is always an array
if ($null -eq $results) {
    $results = @()
    Write-Output "DEBUG: Results was null, set to empty array"
} elseif ($results -isnot [array]) {
    $results = @($results)
    Write-Output "DEBUG: Results converted to array"
}

Write-Output "DEBUG: Final results count: $($results.Count)"

if ($SummaryOnly) {
    Show-Summary -Results $results
} else {
    Show-Summary -Results $results
    Show-DetailedResults -Results $results
}

if ($FixCommonPatterns -and $results.Count -gt 0) {
    $response = Read-Host "`nDo you want to attempt fixing common patterns? (y/N)"
    if ($response -eq 'y' -or $response -eq 'Y') {
        Update-CommonPatterns -Results $results
        Write-Output "`nRe-running scan to show remaining references..."
        $remainingResults = Get-VehicleReferences -SearchPath $Path
        Show-Summary -Results $remainingResults
    }
}

Write-Header "SCAN COMPLETE"
Write-Header "SCAN COMPLETE"
