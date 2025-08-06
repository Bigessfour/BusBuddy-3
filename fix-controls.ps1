# Fix Syncfusion control types in XAML files
$xamlFiles = Get-ChildItem -Path "c:\Users\steve.mckitrick\Desktop\BusBuddy\BusBuddy.WPF" -Recurse -Filter "*.xaml"

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix SfButton to ButtonAdv
    $content = $content -replace "<syncfusion:SfButton", "<syncfusion:ButtonAdv"
    $content = $content -replace "</syncfusion:SfButton>", "</syncfusion:ButtonAdv>"

    if ($content -ne $originalContent) {
        Write-Host "Fixed controls in: $($file.Name)" -ForegroundColor Yellow
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Control fixing complete!" -ForegroundColor Green
