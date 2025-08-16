#Requires -Version 7.0

<#
.SYNOPSIS
    Finds duplicate files in the repository
.DESCRIPTION
    Scans the repository for duplicate files by comparing file hashes.
    Returns information about identical and different duplicates.
.PARAMETER RootPath
    The root path to scan for duplicates
.PARAMETER OutputDirectory
    Directory to output reports to
.PARAMETER PassThru
    Return the result object for further processing
.EXAMPLE
    .\Find-DuplicateFiles.ps1 -RootPath . -OutputDirectory . -PassThru
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [switch]$PassThru
)

# Initialize result object
$result = @{
    Identical = @()
    Different = @()
    Scanned = 0
    SkippedDirs = @()
}

# Directories to skip (common build/temp directories)
$skipDirs = @(
    'bin', 'obj', 'node_modules', '.git', '.vs', '.vscode',
    'TestResults', 'logs', 'artifacts', 'packages'
)

Write-Information "Starting duplicate file scan in: $RootPath" -InformationAction Continue

try {
    # Get all files excluding common build directories
    $allFiles = Get-ChildItem -Path $RootPath -File -Recurse | Where-Object {
        $skip = $false
        foreach ($skipDir in $skipDirs) {
            if ($_.FullName -like "*\$skipDir\*" -or $_.FullName -like "*/$skipDir/*") {
                $skip = $true
                break
            }
        }
        -not $skip
    }

    Write-Information "Found $($allFiles.Count) files to analyze" -InformationAction Continue
    $result.Scanned = $allFiles.Count

    # Group files by size first (quick filter)
    $sizeGroups = $allFiles | Group-Object Length | Where-Object { $_.Count -gt 1 }

    Write-Information "Found $($sizeGroups.Count) size groups with potential duplicates" -InformationAction Continue

    foreach ($sizeGroup in $sizeGroups) {
        if ($sizeGroup.Count -le 1) { continue }

        # Calculate hashes for files of same size
        $hashGroups = $sizeGroup.Group | ForEach-Object {
            try {
                $hash = Get-FileHash -Path $_.FullName -Algorithm SHA256
                [PSCustomObject]@{
                    File = $_
                    Hash = $hash.Hash
                    RelativePath = [System.IO.Path]::GetRelativePath($RootPath, $_.FullName)
                }
            }
            catch {
                Write-Warning "Failed to hash file: $($_.FullName) - $($_.Exception.Message)"
                $null
            }
        } | Where-Object { $_ -ne $null } | Group-Object Hash

        # Check for duplicates within each hash group
        foreach ($hashGroup in $hashGroups) {
            if ($hashGroup.Count -le 1) { continue }

            $files = $hashGroup.Group
            $firstFile = $files[0]
            $duplicateFiles = $files[1..($files.Count - 1)]

            # Check if files are identical in content
            $identical = $true
            try {
                $firstContent = Get-Content -Path $firstFile.File.FullName -Raw -ErrorAction Stop
                foreach ($dupFile in $duplicateFiles) {
                    $dupContent = Get-Content -Path $dupFile.File.FullName -Raw -ErrorAction Stop
                    if ($firstContent -ne $dupContent) {
                        $identical = $false
                        break
                    }
                }
            }
            catch {
                # If we can't read content, assume different
                $identical = $false
            }

            $duplicateInfo = @{
                Hash = $hashGroup.Name
                Files = @($files | ForEach-Object { $_.RelativePath })
                Size = $firstFile.File.Length
                Identical = $identical
            }

            if ($identical) {
                $result.Identical += $duplicateInfo
            } else {
                $result.Different += $duplicateInfo
            }
        }
    }

    # Generate reports
    $identicalReport = $result.Identical | ConvertTo-Json -Depth 10
    $differentReport = $result.Different | ConvertTo-Json -Depth 10

    # Save reports
    $identicalPath = Join-Path $OutputDirectory "duplicate_identical_list.json"
    $differentPath = Join-Path $OutputDirectory "duplicate_different_list.json"

    $identicalReport | Out-File -FilePath $identicalPath -Encoding UTF8
    $differentReport | Out-File -FilePath $differentPath -Encoding UTF8

    Write-Information "Reports saved to:" -InformationAction Continue
    Write-Information "  Identical: $identicalPath" -InformationAction Continue
    Write-Information "  Different: $differentPath" -InformationAction Continue

    # Summary
    Write-Information "Scan complete:" -InformationAction Continue
    Write-Information "  Files scanned: $($result.Scanned)" -InformationAction Continue
    Write-Information "  Identical duplicates: $($result.Identical.Count)" -InformationAction Continue
    Write-Information "  Different duplicates: $($result.Different.Count)" -InformationAction Continue

    if ($PassThru) {
        return $result
    }
}
catch {
    Write-Error "Duplicate scan failed: $($_.Exception.Message)"
    throw
}
