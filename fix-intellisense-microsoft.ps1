# Microsoft's Official WPF IntelliSense Fix
# Based on: https://learn.microsoft.com/en-us/answers/questions/1185434/

Write-Host "🎯 Microsoft's Official Solution for WPF IntelliSense Issues" -ForegroundColor Cyan
Write-Host "📚 Reference: https://learn.microsoft.com/en-us/answers/questions/1185434/" -ForegroundColor Blue

Write-Host "`n🔍 The Problem (From Microsoft):" -ForegroundColor Yellow
Write-Host "   • IntelliSense analyzes code BEFORE XAML compilation" -ForegroundColor White
Write-Host "   • InitializeComponent() is generated during build" -ForegroundColor White
Write-Host "   • Red squiggles appear for valid code" -ForegroundColor White

Write-Host "`n✅ Step 1: Build to generate missing code..." -ForegroundColor Green
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj --verbosity minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Build successful - Generated code now exists" -ForegroundColor Green

    Write-Host "`n✅ Step 2: Verify generated files exist..." -ForegroundColor Green
    $objFolder = "BusBuddy.WPF/obj/Debug/net9.0-windows"

    if (Test-Path $objFolder) {
        $generatedFiles = Get-ChildItem -Path $objFolder -Filter "*.g.cs" -Recurse
        Write-Host "   📁 Found $($generatedFiles.Count) generated .g.cs files" -ForegroundColor Blue

        # Show specific generated files
        $generatedFiles | ForEach-Object {
            Write-Host "      • $($_.Name)" -ForegroundColor Gray
        }

        Write-Host "`n✅ Step 3: Microsoft's recommended VS Code restart..." -ForegroundColor Green
        Write-Host "   🔄 Press Ctrl+Shift+P and run: 'C#: Restart OmniSharp'" -ForegroundColor Yellow
        Write-Host "   🔄 Or run: 'Developer: Reload Window'" -ForegroundColor Yellow

        Write-Host "`n🎯 MICROSOFT'S DEVELOPER WORKFLOW:" -ForegroundColor Cyan
        Write-Host "   1. 🏗️ Build first (generates missing code)" -ForegroundColor White
        Write-Host "   2. 🔄 Restart language server" -ForegroundColor White
        Write-Host "   3. ✅ IntelliSense now sees generated code" -ForegroundColor White
        Write-Host "   4. ❌ Ignore red squiggles if build succeeds" -ForegroundColor White

        Write-Host "`n💡 GOLDEN RULE FROM MICROSOFT:" -ForegroundColor Yellow
        Write-Host "   'If it builds successfully, the IntelliSense errors are false positives'" -ForegroundColor White

    } else {
        Write-Host "   ⚠️ obj folder not found - try building again" -ForegroundColor Yellow
    }

} else {
    Write-Host "   ❌ Build failed - these ARE real errors" -ForegroundColor Red
    Write-Host "   📋 Check build output for actual issues" -ForegroundColor Yellow
}

Write-Host "`n🚀 Ready to code! Trust the build, not the squiggles." -ForegroundColor Green
