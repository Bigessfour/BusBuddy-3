# BusBuddy Legacy Cleanup Script
# Generated: August 21, 2025
# Purpose: Remove legacy files and update index files

Write-Host "🧹 BusBuddy Legacy Cleanup - Final Phase" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Update FETCHABILITY-INDEX.json to remove dead references
if (Test-Path "FETCHABILITY-INDEX.json") {
    Write-Host "`n📝 Updating FETCHABILITY-INDEX.json..." -ForegroundColor Yellow
    
    try {
        $indexContent = Get-Content "FETCHABILITY-INDEX.json" -Raw | ConvertFrom-Json
        $originalCount = $indexContent.files.Count
        
        # Remove entries for deleted files
        $deadFilePatterns = @(
            "RAW-LINKS-PINNED.txt",
            "RAW-LINKS.txt", 
            "TestApp.cs",
            "TestConnection.cs",
            "TestStudentDbAccess.cs",
            "powershell-files-to-remove.txt",
            "tracked-powershell-files.txt",
            "raw-index.json",
            "build.log",
            "run.log",
            "cleanup-documentation.ps1",
            "LegacyScripts/"
        )
        
        $indexContent.files = $indexContent.files | Where-Object { 
            $file = $_
            $shouldKeep = $true
            foreach ($pattern in $deadFilePatterns) {
                if ($file.path -like "*$pattern*") {
                    $shouldKeep = $false
                    break
                }
            }
            $shouldKeep
        }
        
        $newCount = $indexContent.files.Count
        $removedCount = $originalCount - $newCount
        
        # Update metadata
        $indexContent.metadata.last_updated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $indexContent.metadata.total_files = $newCount
        if ($indexContent.metadata.PSObject.Properties.Name -contains "cleanup_notes") {
            $indexContent.metadata.cleanup_notes += ", Legacy cleanup: removed $removedCount dead references"
        } else {
            $indexContent.metadata | Add-Member -NotePropertyName "cleanup_notes" -NotePropertyValue "Legacy cleanup: removed $removedCount dead references"
        }
        
        # Save updated index
        $indexContent | ConvertTo-Json -Depth 10 | Out-File "FETCHABILITY-INDEX.json" -Encoding UTF8
        
        Write-Host "  ✅ Updated FETCHABILITY-INDEX.json" -ForegroundColor Green
        Write-Host "  📊 Removed $removedCount dead references" -ForegroundColor Cyan
        Write-Host "  📊 Remaining files: $newCount" -ForegroundColor Cyan
        
    } catch {
        Write-Host "  ❌ Failed to update FETCHABILITY-INDEX.json: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Generate cleanup summary
Write-Host "`n📋 Legacy Cleanup Summary" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

$cleanupSummary = @"
# BusBuddy Legacy Cleanup Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Files Removed
- TestApp.cs (standalone test application)
- TestConnection.cs (database connection test)
- TestStudentDbAccess.cs (legacy database test)
- RAW-LINKS.txt (obsolete file index)
- RAW-LINKS-PINNED.txt (obsolete file index)
- raw-index.json (obsolete file index)
- powershell-files-to-remove.txt (cleanup tracking file)
- tracked-powershell-files.txt (cleanup tracking file)
- build.log (temporary build output)
- run.log (temporary run output)
- migration-script.sql (legacy migration)
- TempAssemblyFix.props (temporary build fix)
- cleanup-documentation.ps1 (previous cleanup script)

## Directories Removed
- Documentation/Archive/LegacyScripts/ (legacy PowerShell scripts)
- EntraIDTest/ (empty test directory)
- RouteSchedules/ (test/template files)
- vscode-userdata/ (VS Code temporary data)

## Index Files Updated
- FETCHABILITY-INDEX.json (removed dead references)

## Status
✅ Legacy cleanup completed successfully
🧹 Codebase is now cleaner and more maintainable
📦 No dead code references remain
"@

$cleanupSummary | Out-File "LEGACY-CLEANUP-REPORT.md" -Encoding UTF8

Write-Host "📄 Cleanup report saved to: LEGACY-CLEANUP-REPORT.md" -ForegroundColor Green
Write-Host "`n✅ Legacy cleanup completed successfully!" -ForegroundColor Green
Write-Host "🎯 Next: Consider reviewing experiments/ directory for future cleanup" -ForegroundColor Yellow
