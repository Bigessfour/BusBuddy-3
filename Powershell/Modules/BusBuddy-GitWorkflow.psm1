#Requires -Version 7.5

<#
.SYNOPSIS
    BusBuddy Git Workflow Module - Microsoft PowerShell Standards Compliant
.DESCRIPTION
    Provides Microsoft-compliant Git workflow functions with automated fetchability index integration.
    Ensures Grok-4 always has up-to-date repository structure information before commits.
.NOTES
    File Name      : BusBuddy-GitWorkflow.psm1
    Prerequisite   : PowerShell 7.5+, Git installed
    Author         : BusBuddy Development Team
    Version        : 1.0.0
    Reference      : https://learn.microsoft.com/en-us/powershell/scripting/developer/module/writing-a-windows-powershell-module
.LINK
    https://github.com/Bigessfour/BusBuddy-3
#>

# Module metadata
$script:ModuleVersion = '1.0.0'
$script:ModuleName = 'BusBuddy-GitWorkflow'

# Module-level variables
$script:WorkspaceRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$script:FetchabilityScript = Join-Path $script:WorkspaceRoot "generate-fetchability-index.ps1"
$script:FetchabilityIndex = Join-Path $script:WorkspaceRoot "FETCHABILITY-INDEX.json"

<#
.SYNOPSIS
    Updates the fetchability index before Git operations.
.DESCRIPTION
    Regenerates FETCHABILITY-INDEX.json to ensure Grok-4 has current repository structure.
    Uses Microsoft PowerShell error handling patterns and structured output.
.PARAMETER Validate
    Run with validation to check file integrity.
.EXAMPLE
    Update-BusBuddyFetchabilityIndex
    Updates the fetchability index with default settings.
.EXAMPLE
    Update-BusBuddyFetchabilityIndex -Validate
    Updates and validates the fetchability index.
.NOTES
    Microsoft Reference: https://learn.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-exceptions
#>
function Update-BusBuddyFetchabilityIndex {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [switch]$Validate
    )

    begin {
        Write-Verbose "Starting fetchability index update process"
        $ErrorActionPreference = 'Stop'
    }

    process {
        try {
            Write-Information "🔄 Updating fetchability index for Grok-4..." -InformationAction Continue

            # Verify script exists
            if (-not (Test-Path $script:FetchabilityScript)) {
                throw "Fetchability script not found at: $script:FetchabilityScript"
            }

            # Build command arguments
            $scriptArgs = @()
            if ($Validate) {
                $scriptArgs += '-Validate'
            }

            # Execute the fetchability script with proper error handling
            if ($PSCmdlet.ShouldProcess("Fetchability index at $script:FetchabilityIndex", "Update fetchability index")) {
                $result = if ($scriptArgs.Count -gt 0) {
                    & $script:FetchabilityScript @scriptArgs
                } else {
                    & $script:FetchabilityScript
                }

                # Verify output file was created/updated
                if (Test-Path $script:FetchabilityIndex) {
                    $indexInfo = Get-Item $script:FetchabilityIndex
                    Write-Information "✅ Fetchability index updated successfully" -InformationAction Continue
                    Write-Information "📁 Size: $([math]::Round($indexInfo.Length / 1KB, 2)) KB" -InformationAction Continue
                    Write-Information "🕒 Modified: $($indexInfo.LastWriteTime)" -InformationAction Continue

                    return [PSCustomObject]@{
                        Success = $true
                        FilePath = $script:FetchabilityIndex
                        SizeKB = [math]::Round($indexInfo.Length / 1KB, 2)
                        LastModified = $indexInfo.LastWriteTime
                        ValidationRun = $Validate.IsPresent
                    }
                } else {
                    throw "Fetchability index file was not created at: $script:FetchabilityIndex"
                }
            } else {
                Write-Information "Operation cancelled by user" -InformationAction Continue
                return [PSCustomObject]@{
                    Success = $false
                    Error = "Operation cancelled by user"
                    ValidationRun = $Validate.IsPresent
                }
            }
        }
        catch {
            Write-Error "Failed to update fetchability index: $($_.Exception.Message)"
            return [PSCustomObject]@{
                Success = $false
                Error = $_.Exception.Message
                ValidationRun = $Validate.IsPresent
            }
        }
    }

    end {
        Write-Verbose "Fetchability index update process completed"
    }
}

<#
.SYNOPSIS
    Commits changes with automated fetchability index update.
.DESCRIPTION
    Performs a Git commit with automatic fetchability index regeneration to ensure
    Grok-4 has current repository information. Follows Microsoft PowerShell standards.
.PARAMETER Message
    The commit message (required).
.PARAMETER IncludeUntracked
    Include untracked files in the commit (git add .).
.PARAMETER SkipFetchabilityUpdate
    Skip the fetchability index update (not recommended).
.EXAMPLE
    Invoke-BusBuddyCommit -Message "feat: add new student management features"
    Commits with fetchability index update.
.EXAMPLE
    Invoke-BusBuddyCommit -Message "fix: resolve validation errors" -IncludeUntracked
    Commits all files including untracked ones.
.NOTES
    Microsoft Reference: https://learn.microsoft.com/en-us/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines
#>
function Invoke-BusBuddyCommit {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Message,

        [switch]$IncludeUntracked,

        [switch]$SkipFetchabilityUpdate
    )

    begin {
        Write-Verbose "Starting BusBuddy commit process"
        $ErrorActionPreference = 'Stop'

        # Validate we're in a Git repository
        if (-not (Test-Path '.git')) {
            throw "Not in a Git repository. Please run from the repository root."
        }
    }

    process {
        try {
            # Step 1: Update fetchability index (unless skipped)
            if (-not $SkipFetchabilityUpdate) {
                Write-Information "🔍 Step 1: Updating fetchability index for Grok-4..." -InformationAction Continue
                $indexResult = Update-BusBuddyFetchabilityIndex -Validate

                if (-not $indexResult.Success) {
                    throw "Fetchability index update failed: $($indexResult.Error)"
                }

                Write-Information "✅ Fetchability index ready for Grok-4" -InformationAction Continue
            } else {
                Write-Warning "⚠️ Skipping fetchability index update (not recommended for Grok-4 workflows)"
            }

            # Step 2: Stage changes
            Write-Information "📝 Step 2: Staging changes..." -InformationAction Continue

            if ($IncludeUntracked) {
                if ($PSCmdlet.ShouldProcess("All files (including untracked)", "git add")) {
                    git add .
                    Write-Information "📁 All files staged (including untracked)" -InformationAction Continue
                }
            } else {
                if ($PSCmdlet.ShouldProcess("Modified and deleted files", "git add")) {
                    git add -u
                    Write-Information "📁 Modified and deleted files staged" -InformationAction Continue
                }
            }

            # Always ensure fetchability index is staged if it was updated
            if (-not $SkipFetchabilityUpdate -and (Test-Path $script:FetchabilityIndex)) {
                git add $script:FetchabilityIndex
                Write-Information "📋 Fetchability index staged for commit" -InformationAction Continue
            }

            # Step 3: Check if there are changes to commit
            $stagingStatus = git diff --cached --name-only
            if (-not $stagingStatus) {
                Write-Warning "⚠️ No changes staged for commit"
                return [PSCustomObject]@{
                    Success = $false
                    Reason = "No changes to commit"
                    FetchabilityUpdated = -not $SkipFetchabilityUpdate
                }
            }

            # Step 4: Commit changes
            Write-Information "💾 Step 3: Committing changes..." -InformationAction Continue

            if ($PSCmdlet.ShouldProcess($Message, "git commit")) {
                git commit -m $Message

                if ($LASTEXITCODE -eq 0) {
                    $commitHash = git rev-parse --short HEAD
                    Write-Information "✅ Commit successful: $commitHash" -InformationAction Continue
                    Write-Information "📝 Message: $Message" -InformationAction Continue

                    return [PSCustomObject]@{
                        Success = $true
                        CommitHash = $commitHash
                        Message = $Message
                        FetchabilityUpdated = -not $SkipFetchabilityUpdate
                        FilesStaged = $stagingStatus.Count
                    }
                } else {
                    throw "Git commit failed with exit code: $LASTEXITCODE"
                }
            }
        }
        catch {
            Write-Error "Commit failed: $($_.Exception.Message)"
            return [PSCustomObject]@{
                Success = $false
                Error = $_.Exception.Message
                FetchabilityUpdated = -not $SkipFetchabilityUpdate
            }
        }
    }

    end {
        Write-Verbose "BusBuddy commit process completed"
    }
}

<#
.SYNOPSIS
    Pushes commits to remote repository with pre-push validation.
.DESCRIPTION
    Pushes commits to the remote repository with optional pre-push checks.
    Ensures fetchability index is current for Grok-4 analysis.
.PARAMETER RemoteName
    The remote name (default: origin).
.PARAMETER BranchName
    The branch name (default: current branch).
.PARAMETER Force
    Force push (use with caution).
.EXAMPLE
    Invoke-BusBuddyPush
    Pushes current branch to origin.
.EXAMPLE
    Invoke-BusBuddyPush -RemoteName upstream -BranchName feature/new-functionality
    Pushes specific branch to upstream remote.
.NOTES
    Microsoft Reference: https://learn.microsoft.com/en-us/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines
#>
function Invoke-BusBuddyPush {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [string]$RemoteName = 'origin',

        [string]$BranchName,

        [switch]$Force
    )

    begin {
        Write-Verbose "Starting BusBuddy push process"
        $ErrorActionPreference = 'Stop'

        # Validate we're in a Git repository
        if (-not (Test-Path '.git')) {
            throw "Not in a Git repository. Please run from the repository root."
        }
    }

    process {
        try {
            # Get current branch if not specified
            if (-not $BranchName) {
                $BranchName = git branch --show-current
                if (-not $BranchName) {
                    throw "Could not determine current branch name"
                }
            }

            Write-Information "🚀 Pushing to $RemoteName/$BranchName..." -InformationAction Continue

            # Pre-push validation
            Write-Information "🔍 Running pre-push validation..." -InformationAction Continue

            # Check if fetchability index is current
            if (Test-Path $script:FetchabilityIndex) {
                $indexAge = (Get-Date) - (Get-Item $script:FetchabilityIndex).LastWriteTime
                if ($indexAge.TotalMinutes -gt 30) {
                    Write-Warning "⚠️ Fetchability index is $([math]::Round($indexAge.TotalMinutes, 1)) minutes old. Consider updating for Grok-4."
                } else {
                    Write-Information "✅ Fetchability index is current ($([math]::Round($indexAge.TotalMinutes, 1)) minutes old)" -InformationAction Continue
                }
            } else {
                Write-Warning "⚠️ Fetchability index not found. Grok-4 may not have current repository structure."
            }

            # Check for unpushed commits
            $unpushedCommits = git log --oneline "$RemoteName/$BranchName..HEAD" 2>$null
            if ($unpushedCommits) {
                $commitCount = ($unpushedCommits | Measure-Object).Count
                Write-Information "📦 $commitCount commit(s) ready to push" -InformationAction Continue
            } else {
                Write-Information "ℹ️ No new commits to push" -InformationAction Continue
                return [PSCustomObject]@{
                    Success = $true
                    Reason = "No commits to push"
                    Remote = $RemoteName
                    Branch = $BranchName
                }
            }

            # Build push command
            $pushArgs = @($RemoteName, $BranchName)
            if ($Force) {
                $pushArgs += '--force'
                Write-Warning "⚠️ Force push enabled - this can overwrite remote history!"
            }

            # Execute push
            if ($PSCmdlet.ShouldProcess("$RemoteName/$BranchName", "git push")) {
                git push @pushArgs

                if ($LASTEXITCODE -eq 0) {
                    Write-Information "✅ Push successful to $RemoteName/$BranchName" -InformationAction Continue
                    Write-Information "🎯 Repository ready for Grok-4 analysis" -InformationAction Continue

                    return [PSCustomObject]@{
                        Success = $true
                        Remote = $RemoteName
                        Branch = $BranchName
                        CommitsPushed = $commitCount
                        ForcePush = $Force.IsPresent
                    }
                } else {
                    throw "Git push failed with exit code: $LASTEXITCODE"
                }
            }
        }
        catch {
            Write-Error "Push failed: $($_.Exception.Message)"
            return [PSCustomObject]@{
                Success = $false
                Error = $_.Exception.Message
                Remote = $RemoteName
                Branch = $BranchName
            }
        }
    }

    end {
        Write-Verbose "BusBuddy push process completed"
    }
}

<#
.SYNOPSIS
    Complete Git workflow: commit and push with fetchability index integration.
.DESCRIPTION
    Combines commit and push operations with automated fetchability index updates
    to ensure Grok-4 has the most current repository information.
.PARAMETER Message
    The commit message (required).
.PARAMETER RemoteName
    The remote name (default: origin).
.PARAMETER BranchName
    The branch name (default: current branch).
.PARAMETER IncludeUntracked
    Include untracked files in the commit.
.PARAMETER SkipPush
    Only commit, don't push to remote.
.EXAMPLE
    Invoke-BusBuddyCommitAndPush -Message "feat: implement route optimization algorithm"
    Complete workflow with fetchability index update.
.EXAMPLE
    Invoke-BusBuddyCommitAndPush -Message "fix: resolve database connection issues" -IncludeUntracked -SkipPush
    Commit with untracked files but don't push.
.NOTES
    This is the primary function for BusBuddy Git workflows with Grok-4 integration.
#>
function Invoke-BusBuddyCommitAndPush {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Message,

        [string]$RemoteName = 'origin',

        [string]$BranchName,

        [switch]$IncludeUntracked,

        [switch]$SkipPush
    )

    begin {
        Write-Verbose "Starting complete BusBuddy Git workflow"
        Write-Information "🚀 BusBuddy Git Workflow: Commit & Push with Grok-4 Integration" -InformationAction Continue
    }

    process {
        try {
            # Step 1: Commit with fetchability index update
            Write-Information "📝 Phase 1: Committing changes..." -InformationAction Continue
            $commitResult = Invoke-BusBuddyCommit -Message $Message -IncludeUntracked:$IncludeUntracked

            if (-not $commitResult.Success) {
                throw "Commit failed: $($commitResult.Error -or $commitResult.Reason)"
            }

            Write-Information "✅ Commit completed: $($commitResult.CommitHash)" -InformationAction Continue

            # Step 2: Push to remote (unless skipped)
            if (-not $SkipPush) {
                Write-Information "🚀 Phase 2: Pushing to remote..." -InformationAction Continue
                $pushResult = Invoke-BusBuddyPush -RemoteName $RemoteName -BranchName $BranchName

                if (-not $pushResult.Success) {
                    throw "Push failed: $($pushResult.Error)"
                }

                Write-Information "✅ Push completed to $($pushResult.Remote)/$($pushResult.Branch)" -InformationAction Continue
            } else {
                Write-Information "⏭️ Skipping push as requested" -InformationAction Continue
                $pushResult = [PSCustomObject]@{ Success = $true; Skipped = $true }
            }

            # Summary
            Write-Information "🎉 BusBuddy Git workflow completed successfully!" -InformationAction Continue
            Write-Information "📋 Fetchability index updated for Grok-4 analysis" -InformationAction Continue

            return [PSCustomObject]@{
                Success = $true
                Commit = $commitResult
                Push = $pushResult
                WorkflowCompleted = Get-Date
            }
        }
        catch {
            Write-Error "Git workflow failed: $($_.Exception.Message)"
            return [PSCustomObject]@{
                Success = $false
                Error = $_.Exception.Message
                WorkflowCompleted = Get-Date
            }
        }
    }

    end {
        Write-Verbose "Complete BusBuddy Git workflow finished"
    }
}

<#
.SYNOPSIS
    Performs comprehensive security scan to prevent accidental exposure of secrets.
.DESCRIPTION
    Scans the repository for potential security issues including hardcoded API keys,
    exposed secrets, and unsafe patterns before Git operations. Uses Microsoft
    PowerShell security best practices.
.PARAMETER Path
    Root path to scan. Defaults to workspace root.
.PARAMETER IncludeGitHistory
    Include Git history scan for previously committed secrets.
.EXAMPLE
    Invoke-BusBuddySecurityScan
    Performs security scan on the current workspace.
.EXAMPLE
    Invoke-BusBuddySecurityScan -IncludeGitHistory
    Performs security scan including Git history check.
.NOTES
    Microsoft Reference: https://learn.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-exceptions
#>
function Invoke-BusBuddySecurityScan {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$Path = $script:WorkspaceRoot,

        [Parameter()]
        [switch]$IncludeGitHistory
    )

    begin {
        Write-Verbose "Starting BusBuddy security scan"
        Write-Information "🔐 SECURITY SCAN: Checking for exposed secrets" -InformationAction Continue
        Write-Information "=================================================" -InformationAction Continue
    }

    process {
        try {
            $securityIssues = @()
            $warnings = @()
            $recommendations = @()

            # 1. Scan for hardcoded API keys and secrets
            Write-Information "🔍 Scanning files for hardcoded secrets..." -InformationAction Continue

            $dangerousPatterns = @{
                'xAI_API_Key_Hardcoded' = 'xai-[a-zA-Z0-9]{60,}'
                'OpenAI_API_Key_Hardcoded' = 'sk-[a-zA-Z0-9]{48,}'
                'Azure_Connection_String' = 'DefaultEndpointsProtocol=https;AccountName=[^;]*;AccountKey=[^;]*'
                'SQL_Connection_Password' = 'Password\s*=\s*["''][^"'']{8,}["'']'
                'Unsafe_Env_Var_Usage' = '\$env:[A-Z_]*API_KEY\s*(?!\s*=|\s*\)|\s*\}|\s*\]|"|\s*\|)'
                'Bearer_Token_Hardcoded' = 'Bearer\s+[a-zA-Z0-9]{20,}'
                'GitHub_Token_Hardcoded' = 'ghp_[a-zA-Z0-9]{36}'
                'Private_Key_Content' = '-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----'
            }

            $filesToScan = Get-ChildItem -Path $Path -Recurse -File -Include "*.ps1", "*.psm1", "*.psd1", "*.cs", "*.json", "*.xml", "*.yaml", "*.yml", "*.config" |
                Where-Object {
                    param($FileItem)
                    $FileItem.DirectoryName -notmatch '\\(bin|obj|\.git|\.vs|TestResults|packages)\\'
                }

            foreach ($file in $filesToScan) {
                try {
                    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
                    if ($content) {
                        foreach ($patternName in $dangerousPatterns.Keys) {
                            $pattern = $dangerousPatterns[$patternName]
                            if ($content -match $pattern) {
                                $securityIssues += [PSCustomObject]@{
                                    Severity = "CRITICAL"
                                    Type = $patternName
                                    File = $file.FullName.Replace($Path, ".")
                                    Pattern = $pattern
                                    LineNumber = (($content -split "`n") | Select-String $pattern | Select-Object -First 1).LineNumber
                                }
                            }
                        }
                    }
                } catch {
                    Write-Warning "Could not scan file: $($file.FullName) - $($_.Exception.Message)"
                }
            }

            # 2. Check environment variable safety
            Write-Information "🔍 Checking environment variable usage..." -InformationAction Continue

            $sensitiveEnvVars = @('XAI_API_KEY', 'OPENAI_API_KEY', 'AZURE_CLIENT_SECRET', 'GITHUB_TOKEN')
            foreach ($envVar in $sensitiveEnvVars) {
                $value = [Environment]::GetEnvironmentVariable($envVar)
                if ($value) {
                    if ($value.Length -lt 20) {
                        $warnings += "⚠️ Environment variable $envVar appears too short (${value.Length} chars)"
                    } elseif ($value -notmatch '^[a-zA-Z0-9\-_]+$') {
                        $warnings += "⚠️ Environment variable $envVar contains unusual characters"
                    } else {
                        Write-Information "✅ Environment variable $envVar format appears correct" -InformationAction Continue
                    }
                }
            }

            # 3. Git history scan (if requested)
            if ($IncludeGitHistory) {
                Write-Information "🔍 Scanning Git history for leaked secrets..." -InformationAction Continue

                try {
                    $gitLogOutput = git log --all --full-history --grep="api.key\|secret\|password\|token" --oneline 2>$null
                    if ($gitLogOutput) {
                        foreach ($line in $gitLogOutput) {
                            $warnings += "⚠️ Potential sensitive commit found: $line"
                        }
                    }
                } catch {
                    Write-Warning "Could not scan Git history: $($_.Exception.Message)"
                }
            }

            # 4. Check for files with secrets in filename
            Write-Information "🔍 Checking for files with secrets in filename..." -InformationAction Continue

            $suspiciousFiles = Get-ChildItem -Path $Path -Recurse -File |
                Where-Object {
                    param($FileItem)
                    $FileItem.Name -match "(api.key|secret|password|token|credential)" -and
                    $FileItem.Extension -notin @('.md', '.txt', '.gitignore')
                }

            foreach ($file in $suspiciousFiles) {
                $warnings += "⚠️ Suspicious filename: $($file.FullName.Replace($Path, '.'))"
            }

            # 5. Generate recommendations
            $recommendations += "🔒 Regenerate any exposed API keys immediately"
            $recommendations += "🚫 Never commit secrets - use environment variables"
            $recommendations += "🧹 Clear PowerShell history if secrets were displayed"
            $recommendations += "📋 Use .gitignore to exclude sensitive files"
            $recommendations += "🔍 Run security scan before every commit"

            # Generate report
            $scanResult = [PSCustomObject]@{
                Timestamp = Get-Date
                SecurityIssues = $securityIssues
                Warnings = $warnings
                Recommendations = $recommendations
                TotalIssues = $securityIssues.Count
                TotalWarnings = $warnings.Count
                ScanPath = $Path
                FilesScanned = $filesToScan.Count
            }

            # Output results
            if ($securityIssues.Count -gt 0) {
                Write-Information "🚨 CRITICAL SECURITY ISSUES FOUND:" -InformationAction Continue
                foreach ($issue in $securityIssues) {
                    Write-Information "   ❌ $($issue.Type) in $($issue.File):$($issue.LineNumber)" -InformationAction Continue
                }
                Write-Information "🛑 DO NOT COMMIT - FIX SECURITY ISSUES FIRST!" -InformationAction Continue
            } else {
                Write-Information "✅ No critical security issues found" -InformationAction Continue
            }

            if ($warnings.Count -gt 0) {
                Write-Information "`n⚠️ SECURITY WARNINGS:" -InformationAction Continue
                foreach ($warning in $warnings) {
                    Write-Information "   $warning" -InformationAction Continue
                }
            }

            Write-Information "`n📋 SECURITY RECOMMENDATIONS:" -InformationAction Continue
            foreach ($recommendation in $recommendations) {
                Write-Information "   $recommendation" -InformationAction Continue
            }

            return $scanResult
        }
        catch {
            Write-Error "Security scan failed: $($_.Exception.Message)"
            throw
        }
    }

    end {
        Write-Verbose "Security scan completed"
    }
}

<#
.SYNOPSIS
    Enhanced commit function with mandatory security scanning.
.DESCRIPTION
    Extends the basic commit functionality with automatic security scanning
    to prevent accidental exposure of secrets in Git commits.
.PARAMETER Message
    Commit message following conventional commit standards.
.PARAMETER SkipSecurityScan
    Skip security scan (NOT RECOMMENDED - use only for emergency fixes).
.EXAMPLE
    Invoke-BusBuddySecureCommit -Message "feat: add new feature"
    Commits with security scan verification.
.NOTES
    This function performs security scanning before allowing commits.
#>
function Invoke-BusBuddySecureCommit {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [switch]$SkipSecurityScan
    )

    begin {
        Write-Verbose "Starting secure commit process"
    }

    process {
        try {
            # Security scan (unless explicitly skipped)
            if (-not $SkipSecurityScan) {
                Write-Information "🔐 Running mandatory security scan..." -InformationAction Continue
                $securityResult = Invoke-BusBuddySecurityScan

                if ($securityResult.TotalIssues -gt 0) {
                    Write-Error "🛑 COMMIT BLOCKED: Security issues found. Fix issues before committing."
                    Write-Information "Run 'Invoke-BusBuddySecurityScan' for detailed information." -InformationAction Continue
                    return $false
                }

                if ($securityResult.TotalWarnings -gt 0) {
                    Write-Information "⚠️ Security warnings found. Review before proceeding." -InformationAction Continue
                    $continue = Read-Host "Continue with commit? (y/N)"
                    if ($continue -ne 'y' -and $continue -ne 'Y') {
                        Write-Information "Commit cancelled by user." -InformationAction Continue
                        return $false
                    }
                }
            } else {
                Write-Warning "⚠️ Security scan skipped - USE WITH CAUTION!"
            }

            # Update fetchability index
            Update-BusBuddyFetchabilityIndex

            # Perform commit
            return Invoke-BusBuddyCommit -Message $Message
        }
        catch {
            Write-Error "Secure commit failed: $($_.Exception.Message)"
            throw
        }
    }

    end {
        Write-Verbose "Secure commit process completed"
    }
}

# Export public functions following Microsoft module standards
Export-ModuleMember -Function @(
    'Update-BusBuddyFetchabilityIndex',
    'Invoke-BusBuddyCommit',
    'Invoke-BusBuddyPush',
    'Invoke-BusBuddyCommitAndPush',
    'Invoke-BusBuddySecurityScan',
    'Invoke-BusBuddySecureCommit'
)
