# BusBuddy.ThemeValidation Module
# Validates Syncfusion theme/style consistency across WPF XAML files.
# Docs reference: https://help.syncfusion.com/wpf/themes/overview

function Test-BusBuddyThemeConsistency {
    <#
    .SYNOPSIS
        Scan XAML views for inline Syncfusion control styling violations.
    .DESCRIPTION
        Looks for Syncfusion controls (SfDataGrid, ButtonAdv, DockingManager etc.)
        with inline size/margin/brush properties that should be centralized in resource dictionaries.
    .OUTPUTS
        Hashtable with Issues (string[]) and Status (Success|Failed)
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [string]$ViewsPath = 'BusBuddy.WPF/Views'
    )
    $issues = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path $ViewsPath)) {
        Write-Warning "Views path not found: $ViewsPath"
        return @{ Issues = @('Views path missing'); Status = 'Failed' }
    }
    $xamlFiles = Get-ChildItem $ViewsPath -Recurse -Include *.xaml -ErrorAction SilentlyContinue
    $mergedDictionaries = @{}
    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw
        $mergeMatches = [regex]::Matches($content,'<ResourceDictionary\s+Source="([^"]+)"')
        foreach ($m in $mergeMatches) { $mergedDictionaries[$m.Groups[1].Value] = $true }
        # Detect inline width/height/margin on Syncfusion ButtonAdv or SfButton / SfDataGrid
        if ($content -match '<syncfusion:(?:ButtonAdv|SfButton)[^>]*(Width|Height|Margin)=') {
            $issues.Add("Inline layout property on Syncfusion button in $($file.Name)")
        }
        if ($content -match '<syncfusion:SfDataGrid[^>]*(RowHeight|HeaderRowHeight|Background|BorderBrush)=') {
            $issues.Add("Inline SfDataGrid visual property in $($file.Name)")
        }
        # Missing namespace check (should declare syncfusion namespace where controls appear)
        if ($content -match '<syncfusion:' -and $content -notmatch 'xmlns:syncfusion=') {
            $issues.Add("Missing syncfusion xmlns in $($file.Name)")
        }
        if ($content -match '<syncfusion:' -and $content -notmatch 'FluentDark' -and $content -notmatch 'FluentLight') {
            $issues.Add("Potential missing theme reference (FluentDark/FluentLight) in $($file.Name)")
        }
    }
    if (-not ($mergedDictionaries.Keys | Where-Object { $_ -match 'Themes' })) {
        $issues.Add('No merged resource dictionaries referencing Themes folder detected')
    }
    $status = if ($issues.Count -gt 0) { 'Failed' } else { 'Success' }
    return @{ Issues = $issues.ToArray(); Status = $status; FileCount = $xamlFiles.Count; MergedDictionaries = $mergedDictionaries.Keys }
}

function Invoke-BusBuddyThemeRemediation {
    <#
    .SYNOPSIS
        Attempt automatic remediation of common inline styling issues.
    .DESCRIPTION
        Replaces common inline size/margin attributes on Syncfusion buttons with Style reference.
        Only performs safe textual substitutions; more advanced refactoring deferred post-MVP.
    #>
    [CmdletBinding(SupportsShouldProcess=$true)]
    param(
        [string]$ViewsPath = 'BusBuddy.WPF/Views',
        [string]$ButtonStyleKey = 'SyncfusionPrimaryButtonStyle'
    )
    if (-not (Test-Path $ViewsPath)) { Write-Warning "Views path not found: $ViewsPath"; return }
    $xamlFiles = Get-ChildItem $ViewsPath -Recurse -Include *.xaml -ErrorAction SilentlyContinue
    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw
        $original = $content
        $modified = $false
        # Replace inline width/height/margin on buttons with style if not already styled
        if ($content -match '<syncfusion:(?:ButtonAdv|SfButton)' -and $content -notmatch 'Style="{StaticResource') {
            $content = [regex]::Replace($content,'(<syncfusion:(?:ButtonAdv|SfButton)[^>]*)(Width|Height|Margin)="[^"]+"','${1}')
            if ($content -ne $original) { $modified = $true }
            if ($modified) {
                $content = [regex]::Replace($content,'<syncfusion:(ButtonAdv|SfButton)([^>]*)>','<syncfusion:$1$2 Style="{StaticResource ' + $ButtonStyleKey + '}">')
            }
        }
        if ($modified -and $PSCmdlet.ShouldProcess($file.FullName,'Apply style remediation')) {
            $content | Set-Content $file.FullName -Encoding UTF8
            Write-Information "Remediated: $($file.Name)" -InformationAction Continue
        }
    }
}

Export-ModuleMember -Function Test-BusBuddyThemeConsistency,Invoke-BusBuddyThemeRemediation
