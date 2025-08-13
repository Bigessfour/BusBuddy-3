#Requires -Version 7.5
<#!
.SYNOPSIS
  Validates XML and optionally XAML files for well-formed syntax.

.DESCRIPTION
  Scans the given path (file or directory) and validates XML syntax. When -IncludeXaml is set,
  it also treats .xaml files as XML. Returns an array of PSCustomObject entries with
  File, Line, Column, Severity, and Message fields for any issues found. If no issues, returns $null.

.PARAMETER Path
  File or directory path to validate. If a directory, recurse with -Recurse.

.PARAMETER Recurse
  Recurse into subdirectories when Path is a directory.

.PARAMETER IncludeXaml
  Include .xaml files in validation.

.PARAMETER GenerateReport
  Ignored in this lightweight implementation (kept for caller compatibility).

.EXAMPLE
  .\Test-XmlSyntax.ps1 -Path BusBuddy.WPF -Recurse -IncludeXaml

.NOTES
  Lightweight validator used by Invoke-BusBuddyXamlValidation.ps1
!#>
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [string]$Path = '.',
    [switch]$Recurse,
    [switch]$IncludeXaml,
    [switch]$GenerateReport
)

$ErrorActionPreference = 'Stop'

# Analyzer-friendly: acknowledge -GenerateReport without changing behavior
if ($GenerateReport) {
  Write-Verbose "GenerateReport flag is ignored in this lightweight validator." -Verbose:$false
}

function Get-TargetFiles {
    param(
        [string]$Root,
        [bool]$Recurse,
        [bool]$IncludeXaml
    )

    if (Test-Path $Root -PathType Leaf) {
        return ,(Get-Item $Root)
    }

    $patterns = @('*.xml')
    if ($IncludeXaml) { $patterns += '*.xaml' }

  $files = @()
    foreach ($p in $patterns) {
        $files += Get-ChildItem -Path $Root -Filter $p -Recurse:$Recurse -File -ErrorAction SilentlyContinue
    }
    return $files | Sort-Object FullName -Unique
}

$results = @()
$files = Get-TargetFiles -Root $Path -Recurse:$Recurse -IncludeXaml:$IncludeXaml
foreach ($file in $files) {
    try {
        # Read raw to preserve line info for error location
        [void][xml](Get-Content -Path $file.FullName -Raw)
    }
    catch {
        $msg = $_.Exception.Message
        $line = 1
        $col = 1
        if ($_.Exception -is [System.Xml.XmlException]) {
            $line = $_.Exception.LineNumber
            $col  = $_.Exception.LinePosition
        }
        $results += [PSCustomObject]@{
            File     = $file.FullName
            Line     = $line
            Column   = $col
            Severity = 'Error'
            Message  = $msg
        }
    }
}

if ($results.Count -gt 0) { return $results } else { return $null }
