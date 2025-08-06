# Remove ShowWatermark properties from XAML files
$xamlFiles = Get-ChildItem -Path "c:\Users\steve.mckitrick\Desktop\BusBuddy\BusBuddy.WPF" -Recurse -Filter "*.xaml"

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Remove ShowWatermark="True" or ShowWatermark="False"
    $content = $content -replace '\s*ShowWatermark="True"', ''
    $content = $content -replace '\s*ShowWatermark="False"', ''

    if ($content -ne $originalContent) {
        Write-Host "Removed ShowWatermark from: $($file.Name)" -ForegroundColor Magenta
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "ShowWatermark removal complete!" -ForegroundColor Green
