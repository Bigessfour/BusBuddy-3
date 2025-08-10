param(
    [string]$Root = (Resolve-Path "$PSScriptRoot/../../").Path,
    [switch]$FailOnIssues
)

# Audit-Themes.ps1 — static checks for view theming compliance
# Microsoft PowerShell standards: use Write-Output/Information/Warning/Error
# References:
# - SfSkinManager API: https://help.syncfusion.com/cr/wpf/Syncfusion.SfSkinManager.html
# - WPF Themes Getting Started: https://help.syncfusion.com/wpf/themes/getting-started
# - Theme markup extension: https://help.syncfusion.com/cr/wpf/Syncfusion.Theme.html

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-DynamicResource {
    param([string]$xaml)
    # Look for StaticResource or hard-coded color literals where DynamicResource is expected
    $hasStatic = $xaml -match 'StaticResource\s*\{'
    $hasHex     = $xaml -match '#[0-9a-fA-F]{6,8}'
    return @{ StaticResource = $hasStatic; Hex = $hasHex }
}

function Test-SyncfusionNamespace {
    param([string]$xaml)
    return ($xaml -match 'xmlns:syncfusion\s*=\s*"http://schemas.syncfusion.com/wpf"')
}

function Test-ControlUsage {
    param([string]$xaml)
    # Find standard controls that should be Syncfusion equivalents
    $hasStd = @{
        DataGrid = ($xaml -match '<\s*DataGrid(\s|>)');
        Button   = ($xaml -match '<\s*Button(\s|>)');
        TextBox  = ($xaml -match '<\s*TextBox(\s|>)');
    }
    # Find required Syncfusion usages (any of these ok depending on view)
    $hasSf = @{
        ButtonAdv    = ($xaml -match '<\s*syncfusion:ButtonAdv(\s|>)');
        SfDataGrid   = ($xaml -match '<\s*syncfusion:SfDataGrid(\s|>)');
        SfTextBoxExt = ($xaml -match '<\s*syncfusion:SfTextBoxExt(\s|>)');
        SfChart      = ($xaml -match '<\s*syncfusion:SfChart(\s|>)');
        SfScheduler  = ($xaml -match '<\s*syncfusion:SfScheduler(\s|>)');
    }
    return @{ Standard = $hasStd; Syncfusion = $hasSf }
}

function Test-ThemeApplication {
    param([string]$xaml)
    $apply = ($xaml -match 'SfSkinManager\.ApplyStylesOnApplication\s*=\s*"True"')
    $explicit = ($xaml -match 'SfSkinManager\.Theme\s*=\s*"\{\s*syncfusion:Theme\s+[A-Za-z]+\s*\}"')
    return @{ ApplyOnApp = $apply; ExplicitTheme = $explicit }
}

$views = Get-ChildItem -Path $Root -Recurse -Include *.xaml -ErrorAction SilentlyContinue |
         Where-Object { $_.FullName -notmatch 'bin\\|obj\\|g\\|TemporaryGeneratedFile' }

$issues = @()
foreach ($view in $views) {
    try {
        $text = Get-Content -Raw -LiteralPath $view.FullName
        $nsOk = Test-SyncfusionNamespace -xaml $text
        $dyn  = Test-DynamicResource -xaml $text
        $ctrl = Test-ControlUsage -xaml $text
        $them = Test-ThemeApplication -xaml $text

        $entry = [PSCustomObject]@{
            View                 = $view.FullName
            SyncfusionNamespace  = $nsOk
            UsesDynamicResource  = -not $dyn.StaticResource
            HasHexColors         = $dyn.Hex
            ApplyStylesOnApp     = $them.ApplyOnApp
            HasExplicitTheme     = $them.ExplicitTheme
            HasStdDataGrid       = $ctrl.Standard.DataGrid
            HasStdButton         = $ctrl.Standard.Button
            HasStdTextBox        = $ctrl.Standard.TextBox
            HasSfButtonAdv       = $ctrl.Syncfusion.ButtonAdv
            HasSfDataGrid        = $ctrl.Syncfusion.SfDataGrid
            HasSfTextBoxExt      = $ctrl.Syncfusion.SfTextBoxExt
            HasSfChart           = $ctrl.Syncfusion.SfChart
            HasSfScheduler       = $ctrl.Syncfusion.SfScheduler
        }
        $issues += $entry
    }
    catch {
        Write-Warning "Failed to analyze: $($view.FullName) — $($_.Exception.Message)"
    }
}

# Output summary
$issues | Sort-Object View | Format-Table -AutoSize | Out-String | Write-Output

# Basic fail conditions aligned to the checklist
$violations = $issues | Where-Object {
    -not $_.SyncfusionNamespace -or
    -not $_.UsesDynamicResource -or
    $_.HasHexColors -or
    $_.HasStdDataGrid -or
    $_.HasStdButton -or
    $_.HasStdTextBox
}

if ($FailOnIssues -and $violations.Count -gt 0) {
    Write-Error "Theme audit found $($violations.Count) violations. See table above."
}
else {
    Write-Information "Theme audit complete. Views scanned: $($issues.Count)." -InformationAction Continue
}
