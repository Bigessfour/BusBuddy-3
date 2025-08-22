# GitHub Authentication Troubleshooting Guide

## 🔐 **WebAuthn Not Supported Issues**

### **Problem Description**

When accessing GitHub with two-factor authentication, you may encounter:

- "This browser or device is reporting partial passkey support"
- "WebAuthn isn't supported. Use a different browser or device to use your passkey"
- "Authentication failed"

### **Immediate Solutions**

#### **Option 1: Use GitHub Personal Access Token (Recommended)**

```powershell
# Configure Git to use Personal Access Token
git config --global credential.helper manager-core
git config --global user.name "Your GitHub Username"
git config --global user.email "your.email@example.com"

# When prompted for password, use your Personal Access Token instead
```

**Creating a Personal Access Token:**

1. Go to GitHub.com → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Generate new token with these scopes:
    - `repo` (Full control of private repositories)
    - `workflow` (Update GitHub Action workflows)
    - `write:packages` (Upload packages to GitHub Package Registry)
3. Copy the token and use it as your password when Git prompts

#### **Option 2: GitHub CLI Authentication**

```powershell
# Install GitHub CLI if not already installed
winget install --id GitHub.cli

# Authenticate via GitHub CLI
gh auth login

# Configure Git to use GitHub CLI
gh auth setup-git
```

#### **Option 3: SSH Key Authentication**

```powershell
# Generate SSH key
ssh-keygen -t ed25519 -C "your.email@example.com"

# Add SSH key to ssh-agent
ssh-add ~/.ssh/id_ed25519

# Copy public key to clipboard
Get-Content ~/.ssh/id_ed25519.pub | Set-Clipboard

# Add to GitHub: Settings → SSH and GPG keys → New SSH key
# Paste the copied key

# Test SSH connection
ssh -T git@github.com

# Update remote URL to use SSH
git remote set-url origin git@github.com:Bigessfour/BusBuddy-3.git
```

### **Browser-Specific Fixes**

#### **Chrome/Edge Issues**

```powershell
# Clear browser data for GitHub
# Chrome: Settings → Privacy and security → Clear browsing data
# Select "Cookies and other site data" and "Cached images and files"
# Time range: "All time"

# Disable hardware acceleration temporarily
# Chrome: Settings → Advanced → System → Use hardware acceleration when available (OFF)
```

#### **Firefox Issues**

```powershell
# Enable WebAuthn in Firefox
# Navigate to: about:config
# Search for: security.webauth.webauthn
# Set to: true

# Also check: security.webauth.webauthn_enable_softtoken = true
```

### **VS Code Git Integration**

#### **Configure VS Code for Token Authentication**

```json
// .vscode/settings.json
{
    "git.autofetch": true,
    "git.confirmSync": false,
    "git.enableSmartCommit": true,
    "git.postCommitCommand": "none",
    "terminal.integrated.env.windows": {
        "GIT_ASKPASS": "code --wait"
    }
}
```

#### **PowerShell Profile Integration**

```powershell
# Add to PowerShell profile for seamless authentication
function Set-GitCredentials {
    param(
        [string]$Username,
        [string]$Token
    )

    git config --global user.name $Username
    git config --global credential.helper manager-core

    Write-Information "Git configured for token authentication" -InformationAction Continue
    Write-Warning "Use your Personal Access Token as password when prompted"
}

# Usage: Set-GitCredentials "YourUsername"
```

### **Security Best Practices**

#### **Token Management**

- **Scope Limitation**: Only grant necessary permissions to tokens
- **Expiration**: Set reasonable expiration dates (90 days recommended)
- **Storage**: Never store tokens in code or plain text files
- **Rotation**: Regularly rotate tokens for security

#### **Environment Variables**

```powershell
# Store token securely in environment variable
[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "your_token_here", "User")

# Use in scripts
$token = $env:GITHUB_TOKEN
git config --global credential.helper "!f() { echo username=token; echo password=$token; }; f"
```

### **Development Workflow Integration**

#### **PowerShell Commands for BusBuddy**

```powershell
function Push-BusBuddyChanges {
    param([string]$CommitMessage)

    try {
        git add -A
        git commit -m $CommitMessage
        git push
        Write-Host "✅ Changes pushed successfully" -ForegroundColor Green
    }
    catch {
        Write-Warning "Authentication may be required. Use Personal Access Token as password."
        Write-Host "Or run: gh auth login" -ForegroundColor Yellow
    }
}

function Sync-BusBuddyRepo {
    git fetch origin
    git pull origin master
    Write-Host "✅ Repository synchronized" -ForegroundColor Green
}
```

#### **Automated Authentication Check**

```powershell
function Test-GitHubAuthentication {
    try {
        $result = git ls-remote origin HEAD 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ GitHub authentication working" -ForegroundColor Green
            return $true
        }
        else {
            Write-Warning "❌ GitHub authentication failed"
            Write-Host "Run one of these solutions:" -ForegroundColor Yellow
            Write-Host "1. gh auth login" -ForegroundColor Cyan
            Write-Host "2. Configure Personal Access Token" -ForegroundColor Cyan
            Write-Host "3. Setup SSH keys" -ForegroundColor Cyan
            return $false
        }
    }
    catch {
        Write-Error "Git authentication test failed: $_"
        return $false
    }
}
```

### **Quick Recovery Commands**

```powershell
# Quick authentication status check
function Get-GitAuthStatus {
    Write-Host "🔍 Checking Git authentication..." -ForegroundColor Blue

    # Check if GitHub CLI is authenticated
    $ghStatus = gh auth status 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GitHub CLI authenticated" -ForegroundColor Green
    }
    else {
        Write-Host "❌ GitHub CLI not authenticated" -ForegroundColor Red
    }

    # Check Git config
    $username = git config user.name
    $email = git config user.email

    Write-Host "Git User: $username" -ForegroundColor Cyan
    Write-Host "Git Email: $email" -ForegroundColor Cyan

    # Test repository access
    Test-GitHubAuthentication
}

# Emergency fallback
function Reset-GitAuthentication {
    Write-Warning "Resetting Git authentication..."

    # Clear stored credentials
    git config --global --unset credential.helper

    # Reconfigure with manager-core
    git config --global credential.helper manager-core

    Write-Host "Run 'gh auth login' or configure Personal Access Token" -ForegroundColor Yellow
}
```

## 🚀 **Integration with BusBuddy Development**

### **PowerShell Profile Integration**

Add these functions to your BusBuddy PowerShell profile:

```powershell
# Add to PowerShell/Profiles/Microsoft.PowerShell_profile.ps1

# Quick Git authentication functions
function bb-git-status { Get-GitAuthStatus }
function bb-git-reset { Reset-GitAuthentication }
function bb-git-test { Test-GitHubAuthentication }

# Enhanced push with authentication handling
function bb-push {
    param([string]$Message = "Update: $(Get-Date -Format 'yyyy-MM-dd HH:mm')")

    if (-not (Test-GitHubAuthentication)) {
        Write-Warning "Fix authentication first with: bb-git-reset"
        return
    }

    Push-BusBuddyChanges $Message
}
```

### **VS Code Integration**

```json
// .vscode/tasks.json - Add authentication check task
{
    "label": "Check Git Authentication",
    "type": "shell",
    "command": "powershell",
    "args": ["-Command", "Test-GitHubAuthentication"],
    "group": "test",
    "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
    }
}
```

---

**Status**: Use Personal Access Token or GitHub CLI for immediate resolution. SSH keys provide the most reliable long-term solution.

**Quick Fix**: Run `gh auth login` in terminal for immediate access to GitHub operations.
