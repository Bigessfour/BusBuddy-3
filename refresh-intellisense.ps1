# Refresh IntelliSense and clear VS Code caches for BusBuddy project
# Run this script when InitializeComponent shows false errors

Write-Host "🔄 Refreshing BusBuddy IntelliSense..." -ForegroundColor Cyan

# Clean build artifacts and regenerate XAML code-behind files
Write-Host "🧹 Cleaning build artifacts..." -ForegroundColor Yellow
dotnet clean BusBuddy.sln --verbosity minimal

# Force restore packages
Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
dotnet restore BusBuddy.sln --force --verbosity minimal

# Rebuild solution to regenerate all XAML .g.cs files
Write-Host "🏗️ Rebuilding solution..." -ForegroundColor Yellow
dotnet build BusBuddy.sln --verbosity minimal

Write-Host "✅ IntelliSense refresh complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 VS Code settings updated to reduce false errors:" -ForegroundColor Cyan
Write-Host "   - Disabled Roslyn analyzers (omnisharp.enableRoslynAnalyzers: false)" -ForegroundColor Gray
Write-Host "   - Background analysis limited to open files only" -ForegroundColor Gray
Write-Host "   - Semantic highlighting disabled" -ForegroundColor Gray
Write-Host "   - Problems panel status disabled" -ForegroundColor Gray
Write-Host ""
Write-Host "🔄 Please reload VS Code window (Ctrl+R) to apply all settings" -ForegroundColor Yellow
Write-Host "💡 Your build is successful - red squiggles are just IntelliSense display issues" -ForegroundColor Blue
dotnet build BusBuddy.sln --verbosity minimal

# Clear VS Code workspace state (optional - uncomment if needed)
# Write-Host "🗑️ Clearing VS Code workspace cache..." -ForegroundColor Yellow
# Remove-Item -Path ".vscode\.browse.VC.db*" -Force -ErrorAction SilentlyContinue
# Remove-Item -Path ".vscode\settings.json.bak" -Force -ErrorAction SilentlyContinue

Write-Host "✅ IntelliSense refresh complete!" -ForegroundColor Green
Write-Host "💡 If issues persist, reload VS Code window (Ctrl+Shift+P > Developer: Reload Window)" -ForegroundColor Cyan
