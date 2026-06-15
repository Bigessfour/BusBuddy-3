# Emergency Security Fix - Find Hardcoded API Key Source
# This script safely searches for the problematic line causing API key exposure

[CmdletBinding()]
param()

Write-Information "🚨 SECURITY AUDIT: Finding Source of API Key Exposure" -InformationAction Continue
Write-Information "=====================================================" -InformationAction Continue

# 1. Check all PowerShell files for problematic patterns
Write-Information "`n🔍 Checking PowerShell files for unsafe API key usage..." -InformationAction Continue

$problematicFiles = @()

Get-ChildItem -Recurse -File -Filter "*.ps1" | ForEach-Object {
    try {
        $content = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($content) {
            # Check for patterns that might cause API key to be executed
            $dangerousPatterns = @(
                '\$env:XAI_API_KEY\s*(?!\s*=|\s*\)|\s*\}|\s*\]|"|\s*\|)',  # Unquoted env var usage
                'if\s*\(\s*\$env:XAI_API_KEY',  # Direct if condition on env var
                '\$env:XAI_API_KEY\s*\(',  # Trying to execute as function
                'XAI_API_KEY\s*=\s*["\']?xai-'  # Assignment of hardcoded key - REMOVED ACTUAL KEY PART
            )

            foreach ($pattern in $dangerousPatterns) {
                if ($content -match $pattern) {
                    $problematicFiles += @{
                        File = $_.FullName
                        Pattern = $pattern
                        Match = $matches[0]
                    }
                }
            }
        }
    } catch {
        Write-Warning "Could not read file: $($_.FullName)"
    }
}

if ($problematicFiles.Count -gt 0) {
    Write-Information "⚠️ Found potentially problematic files:" -InformationAction Continue
    foreach ($issue in $problematicFiles) {
        Write-Information "  File: $($issue.File)" -InformationAction Continue
        Write-Information "  Pattern: $($issue.Pattern)" -InformationAction Continue
        Write-Information "  Match: $($issue.Match)" -InformationAction Continue
        Write-Information "" -InformationAction Continue
    }
} else {
    Write-Information "✅ No hardcoded API keys found in PowerShell files" -InformationAction Continue
}

# 2. Check for files that might have been created with the API key as filename
Write-Information "🔍 Checking for files with API key in filename..." -InformationAction Continue
$apiKeyFiles = Get-ChildItem -Recurse -File | Where-Object { $_.Name -match "xai-waXPwUxd" }
if ($apiKeyFiles) {
    Write-Information "⚠️ Files with API key in filename found:" -InformationAction Continue
    $apiKeyFiles | ForEach-Object { Write-Information "  $($_.FullName)" -InformationAction Continue }
} else {
    Write-Information "✅ No files with API key in filename" -InformationAction Continue
}

# 3. Check environment variable assignment safety
Write-Information "`n🔍 Checking environment variable assignment..." -InformationAction Continue
$envVar = $env:XAI_API_KEY
if ($envVar) {
    if ($envVar -match '^xai-[a-zA-Z0-9]{60,}$') {
        Write-Information "✅ Environment variable format appears correct" -InformationAction Continue
        Write-Information "   Length: $($envVar.Length) characters" -InformationAction Continue
        Write-Information "   Prefix: xai-[hidden]" -InformationAction Continue
    } else {
        Write-Information "⚠️ Environment variable format unexpected" -InformationAction Continue
        Write-Information "   Length: $($envVar.Length) characters" -InformationAction Continue
        Write-Information "   Starts with: $($envVar.Substring(0, [Math]::Min(10, $envVar.Length)))..." -InformationAction Continue
    }
} else {
    Write-Information "❌ XAI_API_KEY environment variable not set" -InformationAction Continue
}

# 4. Suggest immediate actions
Write-Information "`n🛠️ IMMEDIATE SECURITY ACTIONS REQUIRED:" -InformationAction Continue
Write-Information "=========================================" -InformationAction Continue
Write-Information "1. 🔒 Regenerate your xAI API key immediately at https://console.x.ai/" -InformationAction Continue
Write-Information "2. 🚫 Check git history for any commits with the exposed key" -InformationAction Continue
Write-Information "3. 🧹 Clear PowerShell history: Clear-History" -InformationAction Continue
Write-Information "4. 🔍 Review all recent script executions" -InformationAction Continue
Write-Information "5. ✅ Verify proper environment variable usage in all scripts" -InformationAction Continue

Write-Information "`n📋 RECOMMENDED SECURE PATTERNS:" -InformationAction Continue
Write-Information "================================" -InformationAction Continue
Write-Information '✅ Correct:   $apiKey = $env:XAI_API_KEY'  -InformationAction Continue
Write-Information '✅ Correct:   if ([string]::IsNullOrEmpty($env:XAI_API_KEY))'  -InformationAction Continue
Write-Information '✅ Correct:   $headers = @{ "Authorization" = "Bearer $env:XAI_API_KEY" }'  -InformationAction Continue
Write-Information '❌ WRONG:     if ($env:XAI_API_KEY)  # Can cause execution'  -InformationAction Continue
Write-Information '❌ WRONG:     $env:XAI_API_KEY()     # Tries to execute as function'  -InformationAction Continue

Write-Information "`n⚠️ API KEY SECURITY STATUS: COMPROMISED - REGENERATE IMMEDIATELY" -InformationAction Continue -BackgroundColor Yellow

