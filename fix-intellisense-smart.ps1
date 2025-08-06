# Smart IntelliSense Fix - Build First, Then Analyze
# This forces VS Code to see generated code BEFORE showing errors

Write-Host "🔧 Fixing IntelliSense the RIGHT way..." -ForegroundColor Yellow
Write-Host "🎯 The problem: IntelliSense runs before generated code exists" -ForegroundColor Red
Write-Host "✅ The solution: Build first, then restart language server" -ForegroundColor Green

# Step 1: Build to generate all the missing code
Write-Host "`n🏗️ Step 1: Building to generate InitializeComponent and other generated code..." -ForegroundColor Cyan
dotnet build BusBuddy.sln --verbosity quiet

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful - All generated code now exists" -ForegroundColor Green

    # Step 2: Clear VS Code's analysis cache
    Write-Host "`n🧹 Step 2: Clearing VS Code analysis cache..." -ForegroundColor Cyan
    if (Test-Path ".vscode/omnisharp") {
        Remove-Item ".vscode/omnisharp" -Recurse -Force
        Write-Host "   ✅ Cleared OmniSharp cache" -ForegroundColor Green
    }

    # Step 3: Check for generated files
    Write-Host "`n📋 Step 3: Verifying generated files exist..." -ForegroundColor Cyan
    $generatedFiles = Get-ChildItem -Path "BusBuddy.WPF/obj" -Filter "*.g.cs" -Recurse -ErrorAction SilentlyContinue
    if ($generatedFiles.Count -gt 0) {
        Write-Host "   ✅ Found $($generatedFiles.Count) generated .g.cs files" -ForegroundColor Green
        Write-Host "   📁 These contain InitializeComponent() methods" -ForegroundColor Blue
    } else {
        Write-Host "   ⚠️ No generated files found - this might be the issue" -ForegroundColor Yellow
    }

    Write-Host "`n🎯 SOLUTION FOR NEW DEVELOPERS:" -ForegroundColor Yellow
    Write-Host "   1. ✅ Always build FIRST when you see red squiggles" -ForegroundColor White
    Write-Host "   2. ✅ If build succeeds, the 'errors' are fake" -ForegroundColor White
    Write-Host "   3. ✅ Use Ctrl+Shift+P → 'C#: Restart OmniSharp' to refresh" -ForegroundColor White
    Write-Host "   4. ✅ Real errors = build fails, Fake errors = build succeeds" -ForegroundColor White

    Write-Host "`n💡 GOLDEN RULE: Trust the build, not the red squiggles!" -ForegroundColor Gold

} else {
    Write-Host "❌ Build failed - these ARE real errors that need fixing" -ForegroundColor Red
    Write-Host "   Run 'dotnet build BusBuddy.sln' to see the actual errors" -ForegroundColor Yellow
}
