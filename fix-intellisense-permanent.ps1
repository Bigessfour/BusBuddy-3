# Fix IntelliSense Permanently - Remove Duplicate Settings
# This script removes conflicting C# extension settings that cause IntelliSense malfunctions

Write-Host "🔧 Fixing IntelliSense permanently by removing duplicate settings..." -ForegroundColor Yellow

$settingsFile = ".vscode/settings.json"
if (Test-Path $settingsFile) {
    # Read the current settings
    $content = Get-Content $settingsFile -Raw

    # Create backup
    Copy-Item $settingsFile "$settingsFile.backup" -Force
    Write-Host "📋 Backup created: $settingsFile.backup" -ForegroundColor Green

    # Parse JSON to object
    $settings = $content | ConvertFrom-Json -AsHashtable

    # Remove all duplicate/conflicting dotnet and csharp settings that cause issues
    $conflictingKeys = @(
        "dotnet.completion.showCompletionItemsFromUnimportedNamespaces",
        "dotnet.inlayHints.enableInlayHintsForTypes",
        "dotnet.inlayHints.enableInlayHintsForImplicitVariableTypes",
        "dotnet.inlayHints.enableInlayHintsForLambdaParameterTypes",
        "dotnet.inlayHints.enableInlayHintsForImplicitObjectCreation",
        "csharp.format.enable",
        "csharp.inlayHints.enableInlayHintsForTypes",
        "csharp.inlayHints.enableInlayHintsForImplicitVariableTypes"
    )

    Write-Host "🗑️ Removing conflicting settings..." -ForegroundColor Red
    foreach ($key in $conflictingKeys) {
        if ($settings.ContainsKey($key)) {
            $settings.Remove($key)
            Write-Host "   ❌ Removed: $key" -ForegroundColor DarkRed
        }
    }

    # Add our optimized, non-conflicting settings
    Write-Host "✅ Adding optimized IntelliSense settings..." -ForegroundColor Green
    $settings["dotnet.backgroundAnalysis.analyzerDiagnosticsScope"] = "none"
    $settings["dotnet.backgroundAnalysis.compilerDiagnosticsScope"] = "openFiles"
    $settings["csharp.semanticHighlighting.enabled"] = $false
    $settings["problems.showCurrentInStatus"] = $false
    $settings["dotnet.completion.showCompletionItemsFromUnimportedNamespaces"] = $false
    $settings["csharp.format.enable"] = $false

    # Write back to file
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile

    Write-Host "✅ Settings fixed! Restart VS Code for changes to take effect." -ForegroundColor Green
    Write-Host "🎯 IntelliSense should now work properly without false errors." -ForegroundColor Cyan
} else {
    Write-Host "❌ .vscode/settings.json not found!" -ForegroundColor Red
}
