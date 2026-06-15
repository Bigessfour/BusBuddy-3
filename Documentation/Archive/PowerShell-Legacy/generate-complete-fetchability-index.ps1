# 🚌 BusBuddy Complete FETCHABILITY-INDEX.json Generator
# Generates comprehensive fetchability index for ALL files in the codebase

Write-Information "Generating complete fetchability index..." -InformationAction Continue

# Repository information
$repoUrl = "https://github.com/Bigessfour/BusBuddy-3"
$baseBranch = "https://raw.githubusercontent.com/Bigessfour/BusBuddy-3/master"
$baseSha = "https://raw.githubusercontent.com/Bigessfour/BusBuddy-3/7f9dc1b9a7fd6733198abf11320082bca121a94c"

# Get all files recursively, excluding build artifacts and system directories
$allFiles = Get-ChildItem -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\\.git\\' -and
    $_.FullName -notmatch '\\bin\\' -and
    $_.FullName -notmatch '\\obj\\' -and
    $_.FullName -notmatch '\\node_modules\\' -and
    $_.FullName -notmatch '\\TestResults\\' -and
    $_.FullName -notmatch '\\\.vs\\' -and
    $_.FullName -notmatch '\\artifacts\\' -and
    $_.Name -notlike '*.tmp' -and
    $_.Name -notlike '*.cache' -and
    $_.Name -notlike '*.user' -and
    $_.Name -notlike '*.suo' -and
    $_.Name -ne 'generate-complete-fetchability-index.ps1'
} | Sort-Object FullName

Write-Information "📊 Processing $($allFiles.Count) files..." -InformationAction Continue

# Function to determine file category
function Get-FileCategory($file) {
    $ext = $file.Extension.ToLower()
    $name = $file.Name.ToLower()
    $path = $file.FullName.ToLower()

    if ($path -match '\\documentation\\|\\docs\\' -or $ext -in @('.md', '.txt') -or $name -like 'readme*' -or $name -like 'changelog*') {
        return "documentation"
    }
    elseif ($ext -in @('.cs', '.xaml', '.vb', '.fs', '.ts', '.js', '.py', '.java', '.cpp', '.c', '.h')) {
        return "source_code"
    }
    elseif ($ext -in @('.json', '.xml', '.yml', '.yaml', '.config', '.props', '.targets', '.settings', '.editorconfig') -or $name -like '*.config') {
        return "configuration"
    }
    elseif ($path -match '\\tests\\|\\test\\' -or $name -like '*test*' -or $name -like '*spec*') {
        return "tests"
    }
    elseif ($ext -in @('.png', '.jpg', '.jpeg', '.gif', '.svg', '.ico', '.bmp')) {
        return "assets"
    }
    elseif ($ext -in @('.log', '.db', '.sqlite', '.mdf', '.ldf')) {
        return "build_artifacts"
    }
    else {
        return "other"
    }
}

# Function to determine file type
function Get-FileType($file) {
    $ext = $file.Extension.ToLower()
    if ($ext) {
        return $ext.Substring(1)  # Remove the dot
    }
    return "unknown"
}

# Build the files array
$filesArray = @()
$workspaceRoot = (Get-Location).Path

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($workspaceRoot.Length + 1).Replace('\', '/')
    $category = Get-FileCategory $file
    $type = Get-FileType $file

    $fileEntry = @{
        path = $relativePath
        category = $category
        type = $type
        url_branch = "$baseBranch/$relativePath"
        url_sha = "$baseSha/$relativePath"
    }

    $filesArray += $fileEntry
}

# Create the complete index structure
$index = @{
    meta = @{
        generated = (Get-Date -Format "yyyy-MM-dd")
        description = "Complete machine-readable fetchability index for BusBuddy project - ALL FILES"
        repository = $repoUrl
        base_url_branch = $baseBranch
        base_url_sha = $baseSha
        total_files = $filesArray.Count
        categories = @("documentation", "source_code", "configuration", "tests", "assets", "build_artifacts", "other")
        cleanup_notes = @(
            "Legacy files removed in August 2025 cleanup",
            "PowerShell dependency management module added",
            "All .vscode and .github files included",
            "Complete file inventory - ALL FILES INCLUDED"
        )
    }
    files = $filesArray
}

# Convert to JSON and save
$jsonOutput = $index | ConvertTo-Json -Depth 10
$jsonOutput | Out-File -FilePath "FETCHABILITY-INDEX-COMPLETE.json" -Encoding UTF8

Write-Information "✅ Complete FETCHABILITY -InformationAction Continue-INDEX generated!" -ForegroundColor Green
Write-Information "📄 File: FETCHABILITY -InformationAction Continue-INDEX-COMPLETE.json" -ForegroundColor White
Write-Information "📊 Total files indexed: $($filesArray.Count)"  -InformationAction Continue-ForegroundColor White

# Show category breakdown
$categoryBreakdown = $filesArray | Group-Object category | Sort-Object Count -Descending
Write-Information "`n📁 Files by category:"  -InformationAction Continue-ForegroundColor Cyan
foreach ($cat in $categoryBreakdown) {
    Write-Information "   $($cat.Name): $($cat.Count) files"  -InformationAction Continue-ForegroundColor White
}

Write-Information "`n🔄 To replace current index, run:"  -InformationAction Continue-ForegroundColor Yellow
Write-Information "   Move -InformationAction Continue-Item FETCHABILITY-INDEX-COMPLETE.json FETCHABILITY-INDEX.json -Force" -ForegroundColor White






