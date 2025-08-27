# BusBuddy XAML Files Browser
Write-Information "🎨 BusBuddy XAML Files Overview" -InformationAction Continue
Write-Information "=============================" -InformationAction Continue

$xamlFiles = Get-ChildItem -Path "BusBuddy.WPF" -Filter "*.xaml" -Recurse | Sort-Object Directory, Name

Write-Information "`n📁 Main Views:" -InformationAction Continue
$xamlFiles | Where-Object { $_.Directory.Name -eq "Main" } | ForEach-Object {
    Write-Information "   📄 $($_.Name)" -InformationAction Continue
}

Write-Information "`n🎓 Student Views:" -InformationAction Continue
$xamlFiles | Where-Object { $_.Directory.Name -eq "Student" } | ForEach-Object {
    Write-Information "   📄 $($_.Name)" -InformationAction Continue
}

Write-Information "`n🚌 Route Views:" -InformationAction Continue
$xamlFiles | Where-Object { $_.Directory.Name -eq "Route" } | ForEach-Object {
    Write-Information "   📄 $($_.Name)" -InformationAction Continue
}

Write-Information "`n📊 Reports Views:" -InformationAction Continue
$xamlFiles | Where-Object { $_.Directory.Name -eq "Reports" } | ForEach-Object {
    Write-Information "   📄 $($_.Name)" -InformationAction Continue
}

Write-Information "`n🎨 Controls & Resources:" -InformationAction Continue
$xamlFiles | Where-Object { $_.Directory.Name -in @("Controls", "Resources") } | ForEach-Object {
    Write-Information "   📄 $($_.Directory.Name)/$($_.Name)" -InformationAction Continue
}

Write-Information "`n💡 To view any XAML file:" -InformationAction Continue
Write-Information "   code `"BusBuddy.WPF\Views\Main\MainWindow.xaml`"" -InformationAction Continue
