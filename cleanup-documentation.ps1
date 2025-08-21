# BusBuddy Documentation Cleanup Script
# Generated: August 21, 2025
# Purpose: Remove 28 obsolete documentation files after PowerShell infrastructure removal

Write-Host "🧹 Starting BusBuddy Documentation Cleanup..." -ForegroundColor Cyan

# Define files to delete (28 obsolete files identified in audit)
$filesToDelete = @(
    # PowerShell-Related Documentation (Legacy)
    'Documentation/PowerShell-Refactoring-Plan.md',
    'Documentation/POWERSHELL-STANDARDS.md',
    'Documentation/Command-Refactoring-Status.md',
    'Documentation/BusBuddy-Route-Commands-Refactored.md',
    'Documentation/Update-Summary-Route-Commands-2025-08-08.md',
    
    # Phase-Specific Documentation (Outdated)
    'Documentation/Archive/Phase4-Milestone-Report.md',
    'Documentation/Archive/PHASE-3A-COMPLETION-REPORT.md',
    'Documentation/Archive/Phase4-Implementation-Complete.md',
    'Documentation/Archive/Phase2-Validation-Report.md',
    
    # Superseded Implementation Guides
    'Documentation/Archive/PHASE-2-IMPLEMENTATION-PLAN.md',
    'Documentation/Archive/INTEGRATION-GUIDE.md',
    'Documentation/Archive/ENHANCED-PROFILE-GUIDE.md',
    'Documentation/Archive/ENVIRONMENT-SETUP-GUIDE.md',
    'Documentation/Archive/STREAMLINED-WORKFLOW-GUIDE.md',
    
    # Legacy Feature Documentation
    'Documentation/xAI-Enhancement-Plan.md',
    'Documentation/Archive/CSV-Student-Seeding-Integration-Checklist.md',
    'Documentation/Archive/WILEY-DATA-SEEDING-SUMMARY.md',
    'Documentation/Archive/Button-vs-SfButton-Analysis.md',
    'Documentation/Archive/DockingManager-Standardization-Guide.md',
    
    # Build/Tool Configuration (Outdated)
    'Documentation/Archive/MSB3027-File-Lock-Resolution-Guide.md',
    'Documentation/Archive/DEV-KIT-USAGE-GUIDE.md',
    'Documentation/Archive/PDF-Conversion-Status-Report.md',
    'Documentation/Archive/DEVELOPMENT-PROCESS-MONITORING.md',
    
    # Report Files (Historical Data)
    'Documentation/Reports/TestResults-20250803-083336.md',
    'Documentation/Reports/TestResults-20250808-213009.md',
    'Documentation/Reports/TestResults-20250808-213028.md',
    'Documentation/Write-Host-Analysis-20250808-060702.json',
    'Documentation/Reports/COMPLETE-TOOLS-REVIEW-REPORT.md'
)

# Track deletion results
$deletedFiles = @()
$notFoundFiles = @()
$errorFiles = @()

Write-Host "📋 Processing $($filesToDelete.Count) files for deletion..." -ForegroundColor Yellow

foreach ($file in $filesToDelete) {
    try {
        if (Test-Path $file) {
            Remove-Item $file -Force
            $deletedFiles += $file
            Write-Host "✅ Deleted: $file" -ForegroundColor Green
        } else {
            $notFoundFiles += $file
            Write-Host "⚠️  Not found: $file" -ForegroundColor Yellow
        }
    } catch {
        $errorFiles += $file
        Write-Host "❌ Error deleting: $file - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Summary report
Write-Host "`n📊 Cleanup Summary:" -ForegroundColor Cyan
Write-Host "✅ Successfully deleted: $($deletedFiles.Count) files" -ForegroundColor Green
Write-Host "⚠️  Files not found: $($notFoundFiles.Count) files" -ForegroundColor Yellow
Write-Host "❌ Errors encountered: $($errorFiles.Count) files" -ForegroundColor Red

if ($deletedFiles.Count -gt 0) {
    Write-Host "`n🗑️  Deleted Files:" -ForegroundColor Green
    $deletedFiles | ForEach-Object { Write-Host "   - $_" -ForegroundColor Gray }
}

if ($notFoundFiles.Count -gt 0) {
    Write-Host "`n⚠️  Files Not Found (may have been already removed):" -ForegroundColor Yellow
    $notFoundFiles | ForEach-Object { Write-Host "   - $_" -ForegroundColor Gray }
}

if ($errorFiles.Count -gt 0) {
    Write-Host "`n❌ Files with Errors:" -ForegroundColor Red
    $errorFiles | ForEach-Object { Write-Host "   - $_" -ForegroundColor Gray }
}

Write-Host "`n🎯 Cleanup Complete! Documentation is now cleaner and more focused." -ForegroundColor Green
Write-Host "📝 Next step: Update remaining 19 files to remove PowerShell references" -ForegroundColor Cyan
