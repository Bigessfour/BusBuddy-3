#Requires -Version 7.0

<#
.SYNOPSIS
    BusBuddy GitHub automation with API key security validation
.DESCRIPTION
    Comprehensive GitHub workflow automation with pre-push API key scanning
    and security validation for BusBuddy project
.EXAMPLE
    Invoke-CompleteGitHubWorkflow -ValidateSecrets -GenerateCommitMessage
#>

[CmdletBinding()]
param(
    [string]$WorkspaceRoot = (Get-Location),
    [switch]$ValidateSecrets,
    [switch]$GenerateCommitMessage,
    [switch]$WaitForCompletion,
    [switch]$AnalyzeResults,
    [switch]$AutoFix,
    [switch]$InteractiveMode,
    [switch]$DryRun
)

# Import required modules and set error handling
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Configuration
$script:Config = @{
    WorkspaceRoot   = $WorkspaceRoot
    LogFile         = Join-Path $WorkspaceRoot "logs\github-automation-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
    SecretPatterns  = @{
        'XAI_API_KEY'       = @{
            Pattern     = '(xai-[a-zA-Z0-9]{40,}|sk-[a-zA-Z0-9]{40,})'
            Description = 'X.AI Grok API Key'
            Critical    = $true
        }
        'OPENAI_API_KEY'    = @{
            Pattern     = 'sk-[a-zA-Z0-9]{48,}'
            Description = 'OpenAI API Key'
            Critical    = $true
        }
        'AZURE_KEY'         = @{
            Pattern     = '[a-f0-9]{32}'
            Description = 'Azure Service Key'
            Critical    = $true
        }
        'CONNECTION_STRING' = @{
            Pattern     = '(Server=.*?Password=.*?;|Data Source=.*?Password=.*?;)'
            Description = 'Database Connection String with Password'
            Critical    = $true
        }
        'PRIVATE_KEY'       = @{
            Pattern     = '-----BEGIN (RSA )?PRIVATE KEY-----'
            Description = 'Private Key'
            Critical    = $true
        }
        'JWT_SECRET'        = @{
            Pattern     = '(jwt_secret|JWT_SECRET)[\s]*=[\s]*[''"]?[a-zA-Z0-9+/]{20,}[''"]?'
            Description = 'JWT Secret'
            Critical    = $false
        }
    }
    ExcludePatterns = @(
        '*.log'
        '*.tmp'
        'bin/*'
        'obj/*'
        'TestResults/*'
        '.vs/*'
        'node_modules/*'
        '.git/*'
        'logs/*'
    )
}

#region Logging Functions

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Warning', 'Error', 'Success')]$Level = 'Info'
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logEntry = "[$timestamp] [$Level] $Message"

    # Ensure log directory exists
    $logDir = Split-Path $script:Config.LogFile -Parent
    if (-not (Test-Path $logDir)) {
        New-Item -Path $logDir -ItemType Directory -Force | Out-Null
    }

    # Write to log file
    Add-Content -Path $script:Config.LogFile -Value $logEntry

    # Write to console with colors
    switch ($Level) {
        'Info' { Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
        'Warning' { Write-Host "⚠️  $Message" -ForegroundColor Yellow }
        'Error' { Write-Host "❌ $Message" -ForegroundColor Red }
        'Success' { Write-Host "✅ $Message" -ForegroundColor Green }
    }
}

#endregion

#region Secret Detection Functions

function Test-FileForSecrets {
    param(
        [string]$FilePath
    )

    $detectedSecrets = @()

    try {
        $content = Get-Content -Path $FilePath -Raw -ErrorAction SilentlyContinue
        if (-not $content) {
            return $detectedSecrets
        }

        foreach ($secretType in $script:Config.SecretPatterns.Keys) {
            $pattern = $script:Config.SecretPatterns[$secretType]

            if ($content -match $pattern.Pattern) {
                $matches = [regex]::Matches($content, $pattern.Pattern)
                foreach ($match in $matches) {
                    $detectedSecrets += @{
                        File        = $FilePath
                        Type        = $secretType
                        Description = $pattern.Description
                        Critical    = $pattern.Critical
                        LineNumber  = ($content.Substring(0, $match.Index) -split "`n").Count
                        Match       = $match.Value.Substring(0, [Math]::Min(20, $match.Value.Length)) + "..."
                    }
                }
            }
        }
    }
    catch {
        Write-Log "Error scanning file $FilePath`: $_" -Level Warning
    }

    return $detectedSecrets
}

function Invoke-SecretScan {
    param(
        [string[]]$Files
    )

    Write-Log "🔍 Starting security scan for API keys and secrets..."

    $allSecrets = @()
    $scannedFiles = 0

    foreach ($file in $Files) {
        $fullPath = Join-Path $script:Config.WorkspaceRoot $file

        # Skip excluded patterns
        $skip = $false
        foreach ($pattern in $script:Config.ExcludePatterns) {
            if ($file -like $pattern) {
                $skip = $true
                break
            }
        }

        if ($skip) {
            continue
        }

        if (Test-Path $fullPath -PathType Leaf) {
            $secrets = Test-FileForSecrets -FilePath $fullPath
            $allSecrets += $secrets
            $scannedFiles++
        }
    }

    Write-Log "Scanned $scannedFiles files for secrets"

    if ($allSecrets.Count -gt 0) {
        Write-Log "🚨 SECURITY ALERT: Found $($allSecrets.Count) potential secrets!" -Level Error

        foreach ($secret in $allSecrets) {
            $level = if ($secret.Critical) { 'Error' } else { 'Warning' }
            Write-Log "$($secret.Type) detected in $($secret.File):$($secret.LineNumber) - $($secret.Description)" -Level $level
        }

        $criticalSecrets = $allSecrets | Where-Object { $_.Critical }
        if ($criticalSecrets.Count -gt 0) {
            Write-Log "❌ CRITICAL: $($criticalSecrets.Count) critical secrets found. Push BLOCKED!" -Level Error
            return $false
        }
        else {
            Write-Log "⚠️  Non-critical secrets detected. Review recommended." -Level Warning
            if ($InteractiveMode) {
                $response = Read-Host "Continue with push? (y/N)"
                return $response -eq 'y' -or $response -eq 'Y'
            }
            return $true
        }
    }
    else {
        Write-Log "✅ No secrets detected. Safe to push." -Level Success
        return $true
    }
}

#endregion

#region Environment Validation

function Test-APIKeyEnvironment {
    Write-Log "🔑 Validating API key environment..."

    $issues = @()

    # Check XAI_API_KEY
    if (-not $env:XAI_API_KEY) {
        $issues += "XAI_API_KEY environment variable not set"
    }
    elseif ($env:XAI_API_KEY.Length -lt 20) {
        $issues += "XAI_API_KEY appears to be invalid (too short)"
    }
    else {
        Write-Log "✅ XAI_API_KEY environment variable is set" -Level Success
    }

    # Check for common misconfigurations
    $commonVars = @('OPENAI_API_KEY', 'AZURE_OPENAI_KEY', 'ANTHROPIC_API_KEY')
    foreach ($var in $commonVars) {
        if (Get-ChildItem Env: | Where-Object Name -eq $var) {
            Write-Log "ℹ️  Found $var environment variable" -Level Info
        }
    }

    if ($issues.Count -gt 0) {
        foreach ($issue in $issues) {
            Write-Log $issue -Level Warning
        }
        return $false
    }

    return $true
}

#endregion

#region Git Operations

function Get-StagedFiles {
    try {
        $stagedFiles = git diff --cached --name-only 2>$null
        if ($LASTEXITCODE -eq 0) {
            return $stagedFiles | Where-Object { $_ -and $_.Trim() }
        }
        return @()
    }
    catch {
        Write-Log "Error getting staged files: $_" -Level Error
        return @()
    }
}

function Invoke-SmartGitStaging {
    param(
        [switch]$InteractiveMode
    )

    Write-Log "📦 Analyzing workspace for changes..."

    # Get all modified files
    $modifiedFiles = git status --porcelain 2>$null | Where-Object { $_ } | ForEach-Object {
        $_.Substring(3)
    }

    if (-not $modifiedFiles) {
        Write-Log "No changes detected" -Level Info
        return @()
    }

    Write-Log "Found $($modifiedFiles.Count) modified files"

    if ($InteractiveMode) {
        Write-Host "`n📋 Modified files:" -ForegroundColor Cyan
        for ($i = 0; $i -lt $modifiedFiles.Count; $i++) {
            Write-Host "  [$($i+1)] $($modifiedFiles[$i])" -ForegroundColor White
        }

        $response = Read-Host "`nStage all files? (Y/n)"
        if ($response -eq 'n' -or $response -eq 'N') {
            Write-Log "Staging cancelled by user" -Level Info
            return @()
        }
    }

    # Stage files
    git add . 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Log "✅ Successfully staged $($modifiedFiles.Count) files" -Level Success
        return $modifiedFiles
    }
    else {
        Write-Log "❌ Failed to stage files" -Level Error
        return @()
    }
}

function New-IntelligentCommit {
    param(
        [string[]]$StagedFiles,
        [switch]$GenerateMessage
    )

    if (-not $StagedFiles -or $StagedFiles.Count -eq 0) {
        Write-Log "No staged files for commit" -Level Warning
        return $false
    }

    $commitMessage = ""

    if ($GenerateMessage) {
        Write-Log "🤖 Generating intelligent commit message..."

        # Analyze file types and changes
        $fileTypes = @{}
        foreach ($file in $StagedFiles) {
            $ext = [System.IO.Path]::GetExtension($file).ToLower()
            if (-not $fileTypes.ContainsKey($ext)) {
                $fileTypes[$ext] = 0
            }
            $fileTypes[$ext]++
        }

        # Generate contextual message
        $categories = @()
        if ($fileTypes['.cs']) { $categories += "code" }
        if ($fileTypes['.xaml']) { $categories += "UI" }
        if ($fileTypes['.ps1']) { $categories += "scripts" }
        if ($fileTypes['.md']) { $categories += "docs" }
        if ($fileTypes['.json'] -or $fileTypes['.config']) { $categories += "config" }

        if ($categories.Count -gt 0) {
            $commitMessage = "Update $($categories -join ', ') - $($StagedFiles.Count) files"
        }
        else {
            $commitMessage = "Update project files - $($StagedFiles.Count) files"
        }

        Write-Log "Generated commit message: $commitMessage" -Level Info
    }
    else {
        $commitMessage = Read-Host "Enter commit message"
        if (-not $commitMessage) {
            Write-Log "Commit cancelled - no message provided" -Level Warning
            return $false
        }
    }

    # Perform commit
    git commit -m $commitMessage 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Log "✅ Successfully committed changes" -Level Success
        return $true
    }
    else {
        Write-Log "❌ Failed to commit changes" -Level Error
        return $false
    }
}

#endregion

#region Main Workflow Functions

function Invoke-PrePushValidation {
    Write-Log "🔒 Starting pre-push security validation..."

    # Get files to be pushed
    $stagedFiles = Get-StagedFiles
    if (-not $stagedFiles) {
        Write-Log "No staged files to validate" -Level Info
        return $true
    }

    # Validate environment
    $envValid = Test-APIKeyEnvironment
    if (-not $envValid -and -not $DryRun) {
        Write-Log "Environment validation failed" -Level Error
        return $false
    }

    # Scan for secrets
    $secretScanPassed = Invoke-SecretScan -Files $stagedFiles
    if (-not $secretScanPassed) {
        Write-Log "Secret scan failed - push blocked" -Level Error
        return $false
    }

    Write-Log "✅ Pre-push validation completed successfully" -Level Success
    return $true
}

function Invoke-CompleteGitHubWorkflow {
    param(
        [switch]$ValidateSecrets,
        [switch]$GenerateCommitMessage,
        [switch]$WaitForCompletion,
        [switch]$AnalyzeResults,
        [switch]$AutoFix
    )

    Write-Log "🚀 Starting complete GitHub workflow..."

    try {
        # Stage files
        $stagedFiles = Invoke-SmartGitStaging -InteractiveMode:$InteractiveMode
        if (-not $stagedFiles -or $stagedFiles.Count -eq 0) {
            Write-Log "No files to process" -Level Warning
            return
        }

        # Validate secrets if requested
        if ($ValidateSecrets) {
            $validationPassed = Invoke-PrePushValidation
            if (-not $validationPassed) {
                Write-Log "❌ Workflow stopped due to validation failure" -Level Error
                return
            }
        }

        # Commit changes
        $commitSuccess = New-IntelligentCommit -StagedFiles $stagedFiles -GenerateMessage:$GenerateCommitMessage
        if (-not $commitSuccess) {
            Write-Log "❌ Commit failed" -Level Error
            return
        }

        # Push to remote
        if (-not $DryRun) {
            Write-Log "📤 Pushing to remote repository..."
            git push 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Log "✅ Successfully pushed to remote" -Level Success
            }
            else {
                Write-Log "❌ Failed to push to remote" -Level Error
                return
            }
        }
        else {
            Write-Log "🔍 DRY RUN: Would push to remote repository" -Level Info
        }

        Write-Log "✅ GitHub workflow completed successfully" -Level Success

    }
    catch {
        Write-Log "❌ Workflow failed: $_" -Level Error
    }
}

#endregion

#region Utility Functions

function Show-Help {
    Write-Host @"
🚌 BusBuddy GitHub Automation Tool

USAGE:
    .\BusBuddy-GitHub-Automation.ps1 [OPTIONS]

EXAMPLES:
    # Complete workflow with secret validation
    Invoke-CompleteGitHubWorkflow -ValidateSecrets -GenerateCommitMessage

    # Interactive staging and commit
    Invoke-SmartGitStaging -InteractiveMode

    # Security scan only
    Invoke-PrePushValidation

    # Dry run mode
    .\BusBuddy-GitHub-Automation.ps1 -DryRun -ValidateSecrets

FUNCTIONS:
    Invoke-CompleteGitHubWorkflow  - Full automation workflow
    Invoke-SmartGitStaging         - Intelligent file staging
    New-IntelligentCommit          - Generate contextual commits
    Invoke-PrePushValidation       - Security validation
    Test-APIKeyEnvironment         - Environment validation
    Invoke-SecretScan              - Scan for exposed secrets

"@ -ForegroundColor Cyan
}

#endregion

# Main execution if run directly
if ($MyInvocation.InvocationName -ne '.') {
    Write-Log "🚌 BusBuddy GitHub Automation Started" -Level Info

    if ($args -contains '-help' -or $args -contains '--help' -or $args -contains '-h') {
        Show-Help
        exit 0
    }

    # Run validation by default
    Invoke-PrePushValidation
}
