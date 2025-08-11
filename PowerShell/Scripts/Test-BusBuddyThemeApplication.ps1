[CmdletBinding()]
param()

Write-Information "Validating theme application and parsing logs..." -InformationAction Continue

if (Get-Command bb-health -ErrorAction SilentlyContinue) { bb-health -Quick | Out-Null }
if (Get-Command bb-anti-regression -ErrorAction SilentlyContinue) { bb-anti-regression | Out-Null }
if (Get-Command bb-xaml-validate -ErrorAction SilentlyContinue) { bb-xaml-validate | Out-Null }

$xamlFiles = Get-ChildItem -Path "BusBuddy.WPF/Views" -Filter "*.xaml" -Recurse -ErrorAction SilentlyContinue
$themeIssues = New-Object System.Collections.Generic.List[string]

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw

    if ($content -notmatch 'xmlns:syncfusion="http://schemas\.syncfusion\.com/wpf"') {
        $themeIssues.Add("Missing Syncfusion namespace: $($file.FullName)")
    }
    if ($content -notmatch 'DynamicResource.*(BusBuddy\.Brush|ButtonBackgroundBrush|GridBackgroundBrush|PrimaryTextBrush)') {
        $themeIssues.Add("No DynamicResource for theme brushes: $($file.FullName)")
    }

    if ($file.Name -eq "VehicleManagementView.xaml") {
        $resCount = ([regex]::Matches($content, "<\s*UserControl\.Resources")).Count
        if ($resCount -gt 1) { $themeIssues.Add("Duplicate Resources section detected: $($file.FullName)") }
    }
}

$logFiles = @(
    "bootstrap-20250809.txt",
    "logs/log-20250809.txt",
    "ui-interactions-20250809.log",
    "errors-actionable-20250809.log"
) | Where-Object { Test-Path $_ }

$logIssues = New-Object System.Collections.Generic.List[string]
foreach ($log in $logFiles) {
    $logContent = Get-Content $log -Raw
    if ($logContent -match "DataContext set to unexpected type") {
        $logIssues.Add("DataContext warning: $log")
    }
    if ($logContent -match "InvalidOperationException.*Students") {
        $logIssues.Add("InvalidOperationException in Students navigation: $log")
    }
    if ($logContent -match "XamlParseException.*VehicleManagementView") {
        $logIssues.Add("XamlParseException in VehicleManagementView: $log")
    }
}

$total = $themeIssues.Count + $logIssues.Count
if ($total -eq 0) {
    Write-Information "Theme application and logs validated — no issues found." -InformationAction Continue
} else {
    Write-Warning "Issues found ($total):"
    ($themeIssues + $logIssues) | ForEach-Object { Write-Warning $_ }
}

$docUpdate = @"
## Theme Verification Update
- Date: $(Get-Date -Format "yyyy-MM-dd")
- Files scanned: $($xamlFiles.Count)
- Logs scanned: $($logFiles.Count)
- Issues: $total
"@

New-Item -ItemType Directory -Force -Path "Documentation" | Out-Null
Add-Content -Path "Documentation/FILE-FETCHABILITY-GUIDE.md" -Value $docUpdate
Add-Content -Path "GROK-README.md" -Value $docUpdate
