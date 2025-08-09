# Modern PowerShell 7.5.2 Welcome Menu
function Show-BbWelcome {
    [CmdletBinding()]
    param()
    $menu = @()
    $menu += '🚌  BusBuddy PowerShell Dev Shell'
    $menu += '─────────────────────────────────────────────'
    $menu += "Version: $($PSVersionTable.PSVersion)"
    $menu += "Loaded Modules: $(Get-Module BusBuddy.* | Sort-Object Name | ForEach-Object { $_.Name } | Join-String -Separator ', ')"
    $menu += ''
    $menu += 'Available Commands:'
    $menu += '  Get-BbCommands         # List all BusBuddy dev commands'
    $menu += '  Test-BbAntiRegression  # Scan for forbidden patterns (logging/xaml)'
    $menu += '  Test-BbXaml            # Ensure Syncfusion-only UI patterns in XAML'
    $menu += '  Invoke-BbBuild         # Build the solution with dotnet build'
    $menu += '  Get-BbWriteHost        # Find Write-Host usage in repo'
    $menu += '  Update-BbWriteHost     # Replace Write-Host with Write-Information/Output'
    $menu += ''
    $menu += 'Type Get-BbCommands for more.'
    $menu += ''
    $menu += 'For help, type Get-Help <CommandName>.'
    $menu += '─────────────────────────────────────────────'
    ($menu -join "`n") | Write-Information -InformationAction Continue
}
#requires -Version 7.5

# Exported functions will be declared below and exported explicitly.

$script:RepoRoot = (Resolve-Path -Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
$script:SlnPath   = Join-Path $script:RepoRoot 'BusBuddy.sln'


function Get-BbCommands {
    [CmdletBinding()] param()
    Write-Output @(
        'Test-BbAntiRegression  # Scan for disallowed patterns (logging/xaml)'
        'Test-BbXaml           # Ensure Syncfusion-only UI patterns in XAML'
        'Invoke-BbBuild        # Build the solution with dotnet build'
    )
}

function Test-BbAntiRegression {
    [CmdletBinding()] param()
    # Very lightweight checks to align with project rules
    $errors = @()

    # 1) Forbid Microsoft.Extensions.Logging usage
    $mel = Get-ChildItem -Path $script:RepoRoot -Recurse -Include *.cs | Select-String -Pattern 'Microsoft\.Extensions\.Logging' -SimpleMatch
    if ($mel) { $errors += "Found Microsoft.Extensions.Logging references in: $($mel.Path | Select-Object -Unique | Join-String -Separator ', ')" }

    # 2) Forbid Write-Host in PowerShell scripts
    $wh  = Get-ChildItem -Path $script:RepoRoot -Recurse -Include *.ps1,*.psm1 | Select-String -Pattern '\bWrite-Host\b'
    if ($wh) { $errors += "Found Write-Host in PowerShell files: $($wh.Path | Select-Object -Unique | Join-String -Separator ', ')" }

    if ($errors.Count -gt 0) {
        Write-Error ("Anti-regression checks failed:`n" + ($errors -join "`n"))
        return
    }
    Write-Information "Anti-regression checks passed" -InformationAction Continue
}

function Test-BbXaml {
    [CmdletBinding()] param()
    # Ensure Syncfusion controls are used instead of standard WPF DataGrid
    $xamlFiles = Get-ChildItem -Path $script:RepoRoot -Recurse -Include *.xaml
    $bad = foreach ($f in $xamlFiles) {
        $content = Get-Content -Raw -Path $f.FullName
        if ($content -match '<\s*DataGrid\b' -and $content -notmatch 'syncfusion') { $f.FullName }
    }
    if ($bad) {
        Write-Error ("XAML validation failed — standard DataGrid found:`n" + ($bad -join "`n"))
        return
    }
    Write-Information "XAML validation passed" -InformationAction Continue
}

function Invoke-BbBuild {
    [CmdletBinding()] param(
        [string]$Configuration = 'Debug'
    )
    if (-not (Test-Path $script:SlnPath)) {
        throw "Solution not found at $script:SlnPath"
    }
    dotnet build $script:SlnPath -c $Configuration
}

# Create alias for build convenience
Set-Alias -Name bb-build -Value Invoke-BbBuild





 # Report Write-Host occurrences across repo
 function Get-BbWriteHost {
     [CmdletBinding()] param(
         [string]$Path = $script:RepoRoot
     )
     Get-ChildItem -Path $Path -Recurse -Include *.ps1,*.psm1 | Select-String -Pattern '\bWrite-Host\b' -List | Select-Object -ExpandProperty Path -Unique
 }

 # Replace Write-Host with Write-Information (default) or Write-Output; preview with -WhatIf
 function Update-BbWriteHost {
     [CmdletBinding(SupportsShouldProcess)] param(
         [string]$Path = $script:RepoRoot,
         [ValidateSet('Information','Output')][string]$ReplaceWith = 'Information'
     )
     $files = Get-ChildItem -Path $Path -Recurse -Include *.ps1,*.psm1 | Where-Object { Select-String -Path $_.FullName -Pattern '\bWrite-Host\b' -Quiet }
     foreach ($f in $files) {
         if ($PSCmdlet.ShouldProcess($f.FullName, 'Replace Write-Host')) {
             $content = Get-Content -Raw -Path $f.FullName
             $replacement = if ($ReplaceWith -eq 'Output') { 'Write-Output' } else { 'Write-Information' }
             $new = [regex]::Replace($content, '\bWrite-Host\b', $replacement)
             if ($new -ne $content) { Set-Content -Path $f.FullName -Value $new -Encoding UTF8 }
         }
     }
 }

# Create discoverability alias for command listing
# Ref: about_Aliases — https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_Aliases
Set-Alias -Name bb-commands -Value Get-BbCommands

# Export public functions and aliases
# Ref: Export-ModuleMember — https://learn.microsoft.com/powershell/module/microsoft.powershell.core/export-modulemember
Export-ModuleMember -Function Get-BbCommands, Test-BbAntiRegression, Test-BbXaml, Invoke-BbBuild, Get-BbWriteHost, Update-BbWriteHost, Show-BbWelcome -Alias *
