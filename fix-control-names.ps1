# Fix Syncfusion control names based on official WPF 30.1.42 documentation
$xamlFiles = Get-ChildItem -Path "c:\Users\steve.mckitrick\Desktop\BusBuddy\BusBuddy.WPF" -Recurse -Filter "*.xaml"

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix SfTextBox to SfMaskedEdit (official Syncfusion text input control)
    $content = $content -replace "<syncfusion:SfTextBox", "<syncfusion:SfMaskedEdit"
    $content = $content -replace "</syncfusion:SfTextBox>", "</syncfusion:SfMaskedEdit>"

    # Fix SfComboBox to ComboBoxAdv (official Syncfusion ComboBox control)
    $content = $content -replace "<syncfusion:SfComboBox", "<syncfusion:ComboBoxAdv"
    $content = $content -replace "</syncfusion:SfComboBox>", "</syncfusion:ComboBoxAdv>"

    # Fix SfListView to ListView (standard WPF control for now)
    $content = $content -replace "<syncfusion:SfListView", "<ListView"
    $content = $content -replace "</syncfusion:SfListView>", "</ListView>"

    if ($content -ne $originalContent) {
        Write-Host "Fixed control names in: $($file.Name)" -ForegroundColor Green
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Control name fixing complete!" -ForegroundColor Cyan
