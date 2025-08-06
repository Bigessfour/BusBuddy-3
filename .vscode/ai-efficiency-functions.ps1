# 🤖 AI Assistant Simple Efficiency Helpers
# ==========================================
# Simple, safe functions that don't interfere with existing systems

function Show-AIQuickReference {
    <#
    .SYNOPSIS
    Displays simple AI efficiency reference
    .DESCRIPTION
    Shows Windows PowerShell alternatives and efficiency tips without complex enforcement
    #>

    Write-Host "🤖 AI Assistant Quick Reference" -ForegroundColor Cyan
    Write-Host "=" * 40 -ForegroundColor Cyan

    Write-Host "`n✅ Windows PowerShell Commands:" -ForegroundColor Green
    Write-Host "   Get-Content file.txt | Select-Object -First 20    # Not: head -n 20" -ForegroundColor Gray
    Write-Host "   Select-String 'pattern' file.txt                  # Not: grep 'pattern'" -ForegroundColor Gray
    Write-Host "   Get-ChildItem -Force                               # Not: ls -la" -ForegroundColor Gray

    Write-Host "`n⚡ Quick Efficiency Check:" -ForegroundColor Yellow
    Write-Host "   1. Use existing bb-* functions first" -ForegroundColor Gray
    Write-Host "   2. Group similar fixes (batch operations)" -ForegroundColor Gray
    Write-Host "   3. Simple commands, avoid complex pipes" -ForegroundColor Gray
    Write-Host "   4. Validate with: dotnet build --verbosity quiet" -ForegroundColor Gray

    Write-Host "`n📖 Full reference: .vscode/ai-quick-reference.md" -ForegroundColor Blue
}

function Test-SimpleCommand {
    <#
    .SYNOPSIS
    Simple check for Unix commands on Windows
    .DESCRIPTION
    Basic validation without complex enforcement
    #>
    param([string]$Command)

    $unixCommands = @("head", "tail", "grep", "uniq", "sed", "awk", "ls", "cat")
    $hasUnixCommand = $false

    foreach ($unix in $unixCommands) {
        if ($Command -match "\b$unix\b") {
            Write-Host "💡 Consider PowerShell alternative for: $unix" -ForegroundColor Yellow
            $hasUnixCommand = $true
        }
    }

    if (-not $hasUnixCommand) {
        Write-Host "✅ Command looks Windows PowerShell compatible" -ForegroundColor Green
    }
}

# Simple aliases - no complex module exports
Set-Alias -Name "ai-ref" -Value "Show-AIQuickReference" -ErrorAction SilentlyContinue
Set-Alias -Name "ai-check-cmd" -Value "Test-SimpleCommand" -ErrorAction SilentlyContinue

Write-Host "🤖 Simple AI helpers loaded: ai-ref, ai-check-cmd" -ForegroundColor Green
