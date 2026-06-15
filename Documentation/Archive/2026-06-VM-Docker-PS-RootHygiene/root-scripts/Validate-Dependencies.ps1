# Reference: Write-Information docs - https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/write-information?view=powershell-7.5.2
# Syncfusion examples: SfDataGrid/SfButton docs - https://help.syncfusion.com/wpf/datagrid/getting-started  https://help.syncfusion.com/wpf/button/getting-started

# Header start
Write-Information ("=" * 80) -InformationAction Continue
Write-Information "bb-anti-regression: Starting checks" -InformationAction Continue
Write-Information ("=" * 80) -InformationAction Continue

# Another header/summary (repeat as needed)
Write-Information ("=" * 80) -InformationAction Continue
Write-Information "Checking for banned patterns and regressions" -InformationAction Continue
Write-Information ("=" * 80) -InformationAction Continue

# Summary end
Write-Information ("=" * 80) -InformationAction Continue
Write-Information "bb-anti-regression: Completed checks" -InformationAction Continue
Write-Information ("=" * 80) -InformationAction Continue

function Test-SyncfusionCompliance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)]
        [string]$RootPath = '.'
    )

    # Informational header
    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "Test-SyncfusionCompliance: Scanning XAML for legacy/unsupported controls" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue

    # Map legacy control names to their current Sf equivalents (extend as needed)
    $legacyMap = @{
        'ButtonAdv'    = 'SfButton'
        'DataGrid'     = 'SfDataGrid'
        'GridControl'  = 'SfDataGrid'
        'ComboBoxAdv'  = 'SfComboBox'
        'DatePickerAdv' = 'SfDatePicker'
    }

    # Regex: capture element name, optionally with namespace prefix, stop at whitespace or '>' or '/'
    # This trims extra characters (namespace prefixes, attributes, closing chars)
    $pattern = '<\s*(?:[a-zA-Z_][\w\-]*:)?(?<ctrl>[a-zA-Z_][\w\d]*)\b'

    $xamlFiles = Get-ChildItem -Path $RootPath -Recurse -Include '*.xaml' -ErrorAction SilentlyContinue
    foreach ($file in $xamlFiles) {
        try {
            $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
        }
        catch {
            Write-Information "Skipping unreadable file: $($file.FullName)" -InformationAction Continue
            continue
        }

        $matches = [regex]::Matches($content, $pattern)
        if ($matches.Count -eq 0) { continue }

        $reported = @{}
        foreach ($m in $matches) {
            $name = $m.Groups['ctrl'].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($name)) { continue }

            # If control is a legacy name, suggest migration
            if ($legacyMap.ContainsKey($name) -and -not $reported.ContainsKey($name)) {
                $reported[$name] = $true
                $suggest = $legacyMap[$name]
                Write-Information ("{0} - File: {1} - Found legacy control '{2}' → suggest migrate to '{3}'" -f (Get-Date).ToString("u"), $file.FullName, $name, $suggest) -InformationAction Continue
                Write-Information ("Documentation: {0}" -f (if ($suggest -eq 'SfDataGrid') { 'https://help.syncfusion.com/wpf/datagrid/getting-started' } elseif ($suggest -eq 'SfButton') { 'https://help.syncfusion.com/wpf/button/getting-started' } else { 'https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf' })) -InformationAction Continue
            }

            # Optional: detect non-Syncfusion standard WPF controls that should be upgraded (example)
            # if ($name -eq 'DataGrid') { Write-Information "Consider migrating <DataGrid> → <syncfusion:SfDataGrid> per Syncfusion docs" -InformationAction Continue }
        }
    }

    # Footer
    Write-Information ("=" * 80) -InformationAction Continue
    Write-Information "Test-SyncfusionCompliance: Scan complete" -InformationAction Continue
    Write-Information ("=" * 80) -InformationAction Continue

    # Verification reminder
    Write-Information "After applying changes, run bb-xaml-validate and bb-anti-regression; then run bb-test and bb-health to verify." -InformationAction Continue
}