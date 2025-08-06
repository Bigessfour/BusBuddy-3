# Fix namespace declarations in all XAML files
$xamlFiles = Get-ChildItem -Path "c:\Users\steve.mckitrick\Desktop\BusBuddy\BusBuddy.WPF" -Recurse -Filter "*.xaml"

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match "clr-namespace:Syncfusion\.UI\.Xaml\.Controls\.Input;assembly=Syncfusion\.SfInput\.Wpf") {
        Write-Host "Fixing namespace in: $($file.Name)" -ForegroundColor Cyan
        $newContent = $content -replace "clr-namespace:Syncfusion\.UI\.Xaml\.Controls\.Input;assembly=Syncfusion\.SfInput\.Wpf", "http://schemas.syncfusion.com/wpf"
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
    }
}

Write-Host "Namespace fixing complete!" -ForegroundColor Green
