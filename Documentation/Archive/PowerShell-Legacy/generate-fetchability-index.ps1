# 🚌 BusBuddy FETCHABILITY-INDEX.json Generator (Git Integrated)
# Generates machine-readable fetchability index for AI assistants
# Optimized for pre-commit hook integration

[CmdletBinding()]
param(
    [switch]$Validate,
    [switch]$GitIntegrated,
    [string]$OutputPath = "FETCHABILITY-INDEX.json"
)

# Suppress progress for automated runs
$ProgressPreference = 'SilentlyContinue'

if (-not $GitIntegrated) {
    Write-Information "Generating fetchability index for $Scope" -InformationAction Continue
}

# Repository information (auto-detect from git)
try {
    $repoUrl = (git config --get remote.origin.url) -replace '\.git$', ''
    $currentBranch = git branch --show-current
    $latestCommit = git rev-parse HEAD
} catch {
    $repoUrl = "https://github.com/Bigessfour/BusBuddy-3"
    $currentBranch = "master"
    $latestCommit = "latest"
}

$baseBranch = "$repoUrl/raw/$currentBranch"
$baseSha = "$repoUrl/raw/$latestCommit"

# Enhanced file filtering with performance optimization
$excludePatterns = @(
    '\.git', '\\bin\\', '\\obj\\', '\\node_modules\\', '\\TestResults\\',
    '\\\.vs\\', '\\artifacts\\', '\.tmp$', '\.cache$', '\.user$', '\.suo$',
    'FETCHABILITY-INDEX.*\.json$', 'generate.*fetchability.*\.ps1$'
)

$allFiles = Get-ChildItem -Recurse -File | Where-Object {
    $path = $_.FullName
    -not ($excludePatterns | Where-Object { $path -match $_ })
} | Sort-Object FullName

if (-not $GitIntegrated) {
    Write-Information "📊 Processing $($allFiles.Count) files..." -InformationAction Continue
}

# Enhanced categorization with AI/ML focus
function Get-FileCategory($file) {
    $ext = $file.Extension.ToLower()
    $name = $file.Name.ToLower()
    $path = $file.FullName.ToLower()

    # Priority order for better AI understanding
    if ($path -match '\\\.vscode\\|\\\.github\\' -or $name -like '*.json' -and $path -match 'config|setting') {
        return "vscode_github_config"
    }
    elseif ($path -match '\\documentation\\|\\docs\\' -or $ext -in @('.md', '.txt') -or $name -like 'readme*') {
        return "documentation"
    }
    elseif ($ext -eq '.cs' -and $path -match '\\viewmodels\\|\\views\\') {
        return "wpf_ui_code"
    }
    elseif ($ext -eq '.cs' -and $path -match '\\services\\|\\core\\') {
        return "business_logic"
    }
    elseif ($ext -eq '.xaml') {
        return "wpf_ui_markup"
    }
    elseif ($ext -in @('.cs', '.vb', '.fs')) {
        return "dotnet_source"
    }
    elseif ($ext -eq '.ps1' -or $ext -eq '.psm1' -or $ext -eq '.psd1') {
        return "powershell_automation"
    }
    elseif ($ext -in @('.json', '.xml', '.yml', '.yaml', '.config', '.props', '.targets')) {
        return "configuration"
    }
    elseif ($path -match '\\tests\\|\\test\\' -or $name -like '*test*' -or $name -like '*spec*') {
        return "tests"
    }
    elseif ($ext -in @('.png', '.jpg', '.jpeg', '.gif', '.svg', '.ico')) {
        return "assets"
    }
    elseif ($ext -in @('.sql', '.db', '.sqlite')) {
        return "database"
    }
    else {
        return "other"
    }
}

# Enhanced type detection
function Get-FileType($file) {
    $ext = $file.Extension.ToLower()
    if ($ext) {
        return $ext.Substring(1)
    }
    return "unknown"
}

# Build enhanced files array with metadata
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
        size_kb = [math]::Round($file.Length / 1KB, 2)
        last_modified = $file.LastWriteTime.ToString("yyyy-MM-dd")
        url_branch = "$baseBranch/$relativePath"
        url_sha = "$baseSha/$relativePath"
    }

    # Add AI-relevant metadata for specific file types
    if ($category -eq "wpf_ui_code" -or $category -eq "business_logic") {
        $fileEntry.ai_priority = "high"
    }
    elseif ($category -eq "documentation" -or $category -eq "vscode_github_config") {
        $fileEntry.ai_priority = "medium"
    }
    else {
        $fileEntry.ai_priority = "low"
    }

    $filesArray += $fileEntry
}

# Create comprehensive index with AI optimization
$categoryStats = $filesArray | Group-Object category | ForEach-Object {
    @{
        category = $_.Name
        count = $_.Count
        total_size_kb = [math]::Round(($_.Group | Measure-Object size_kb -Sum).Sum, 2)
    }
}

$index = @{
    meta = @{
        generated = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        description = "AI-optimized machine-readable fetchability index for BusBuddy project"
        repository = $repoUrl
        branch = $currentBranch
        commit = $latestCommit
        base_url_branch = $baseBranch
        base_url_sha = $baseSha
        total_files = $filesArray.Count
        total_size_kb = [math]::Round(($filesArray | Measure-Object size_kb -Sum).Sum, 2)
        ai_optimization = @{
            high_priority_files = ($filesArray | Where-Object { $_.ai_priority -eq "high" }).Count
            categories_optimized = "wpf_ui_code, business_logic, documentation prioritized"
            last_updated = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        }
        categories = ($categoryStats | Sort-Object count -Descending)
    }
    files = ($filesArray | Sort-Object @{Expression={if($_.ai_priority -eq "high"){1}elseif($_.ai_priority -eq "medium"){2}else{3}}}, path)
}

# Convert to JSON with optimization
$jsonOutput = $index | ConvertTo-Json -Depth 10 -Compress:$GitIntegrated

# Save with UTF-8 encoding
$jsonOutput | Out-File -FilePath $OutputPath -Encoding UTF8 -NoNewline

if (-not $GitIntegrated) {
    Write-Information "✅ FETCHABILITY-INDEX.json generated!" -InformationAction Continue
    Write-Information "📄 File: $OutputPath" -InformationAction Continue
    Write-Information "📊 Total files indexed: $($filesArray.Count)" -InformationAction Continue
    Write-Information "🤖 AI-optimized with priority classification" -InformationAction Continue

    # Show category breakdown
    Write-Information "`n📁 Files by category:"  -InformationAction Continue-ForegroundColor Cyan
    foreach ($stat in ($categoryStats | Sort-Object count -Descending)) {
        Write-Information "   $($stat.category): $($stat.count) files ($($stat.total_size_kb) KB)"  -InformationAction Continue-ForegroundColor White
    }
}

if ($Validate) {
    # Validate JSON structure
    try {
        $testParse = Get-Content $OutputPath | ConvertFrom-Json
        Write-Information "✅ JSON validation passed"  -InformationAction Continue-ForegroundColor Green
        return $true
    } catch {
        Write-Information "❌ JSON validation failed: $($_.Exception.Message)"  -InformationAction Continue-ForegroundColor Red
        return $false
    }
}




