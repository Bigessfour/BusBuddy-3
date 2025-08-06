# Remove all Watermark properties from XAML files
$xamlFiles = Get-ChildItem -Path "c:\Users\steve.mckitrick\Desktop\BusBuddy\BusBuddy.WPF" -Recurse -Filter "*.xaml"

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Remove Watermark="..." properties
    $content = $content -replace '\s*Watermark="[^"]*"', ''

    if ($content -ne $originalContent) {
        Write-Host "Removed Watermark properties from: $($file.Name)" -ForegroundColor Cyan
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Watermark property removal complete!" -ForegroundColor Green
