#Requires -Version 7.5

<#
.SYNOPSIS
    Quick BusBuddy Environment Setup Script

.DESCRIPTION
    Automates the setup of BusBuddy development environment on a new machine.
    Supports both Google Drive sync and git clone setups.

.PARAMETER SkipExtensions
    Skip VS Code extension installation

.PARAMETER GoogleDrivePath
    Specify custom Google Drive path if not in default location

.EXAMPLE
    pwsh -ExecutionPolicy Bypass -File "setup-environment.ps1"

.EXAMPLE
    pwsh -ExecutionPolicy Bypass -File "setup-environment.ps1" -GoogleDrivePath "G:\My Drive\BusBuddy"
#>

param(
    [switch]$SkipExtensions,
    [string]$GoogleDrivePath
)

Write-Host @"
🚌 ═══════════════════════════════════════════════════════════════════════
   BUSBUDDY ENVIRONMENT SETUP SCRIPT
   Automated configuration for new development machines
   Supports: Google Drive Sync | Git Clone | Fresh Setup
═══════════════════════════════════════════════════════════════════════
"@ -ForegroundColor Cyan

# Detect if we're in a Google Drive synced folder
$isGoogleDrive = $false
$currentPath = Get-Location
if ($GoogleDrivePath) {
    $isGoogleDrive = $true
    Write-SetupStatus "Using specified Google Drive path: $GoogleDrivePath" -Level Info
}
elseif ($currentPath.Path -match "Google Drive|My Drive") {
    $isGoogleDrive = $true
    Write-SetupStatus "Detected Google Drive sync location" -Level Success
}
else {
    Write-SetupStatus "Standard git clone setup detected" -Level Info
}

# Helper function for status messages
function Write-SetupStatus {
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error")]$Level = "Info"
    )

    $colors = @{ Info = "Cyan"; Success = "Green"; Warning = "Yellow"; Error = "Red" }
    $prefixes = @{ Info = "ℹ️"; Success = "✅"; Warning = "⚠️"; Error = "❌" }

    Write-Host "$($prefixes[$Level]) $Message" -ForegroundColor $colors[$Level]
}# Check prerequisites
Write-SetupStatus "Checking prerequisites..." -Level Info

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7 -or ($PSVersionTable.PSVersion.Major -eq 7 -and $PSVersionTable.PSVersion.Minor -lt 5)) {
    Write-SetupStatus "PowerShell 7.5+ required. Current: $($PSVersionTable.PSVersion)" -Level Error
    Write-SetupStatus "Download from: https://github.com/PowerShell/PowerShell/releases/tag/v7.5.2" -Level Info
    exit 1
}
else {
    Write-SetupStatus "PowerShell version: $($PSVersionTable.PSVersion) ✓" -Level Success
}

# Check .NET version
try {
    $dotnetVersion = & dotnet --version 2>$null
    if (-not $dotnetVersion -or $dotnetVersion -notmatch "^8\.") {
        Write-SetupStatus ".NET 8.0+ required. Current: $dotnetVersion" -Level Error
        Write-SetupStatus "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -Level Info
        exit 1
    }
    else {
        Write-SetupStatus ".NET version: $dotnetVersion ✓" -Level Success
    }
}
catch {
    Write-SetupStatus ".NET SDK not found in PATH" -Level Error
    Write-SetupStatus "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -Level Info
    exit 1
}

# Check VS Code
$vsCode = Get-Command code, code-insiders -ErrorAction SilentlyContinue | Select-Object -First 1
if ($vsCode) {
    Write-SetupStatus "VS Code found: $($vsCode.Name) ✓" -Level Success
}
else {
    Write-SetupStatus "VS Code not found in PATH" -Level Warning
    Write-SetupStatus "Download from: https://code.visualstudio.com/" -Level Info
}

# Check project structure
Write-SetupStatus "Verifying project structure..." -Level Info

$requiredPaths = @(
    "BusBuddy.sln",
    "AI-Assistant\Scripts\load-bus-buddy-profile.ps1",
    "Tools\Scripts\BusBuddy-File-Debugger.ps1",
    ".vscode\tasks.json"
)

$missingFiles = @()
foreach ($path in $requiredPaths) {
    if (Test-Path $path) {
        Write-SetupStatus "Found: $path ✓" -Level Success
    }
    else {
        Write-SetupStatus "Missing: $path" -Level Error
        $missingFiles += $path
    }
}

if ($missingFiles.Count -gt 0) {
    Write-SetupStatus "Critical files missing. Please ensure complete repository clone." -Level Error
    exit 1
}

# Set execution policy if needed
Write-SetupStatus "Checking PowerShell execution policy..." -Level Info
$currentPolicy = Get-ExecutionPolicy -Scope CurrentUser
if ($currentPolicy -eq "Restricted") {
    Write-SetupStatus "Setting execution policy to RemoteSigned..." -Level Info
    try {
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
        Write-SetupStatus "Execution policy updated ✓" -Level Success
    }
    catch {
        Write-SetupStatus "Failed to set execution policy: $($_.Exception.Message)" -Level Error
        Write-SetupStatus "Run manually: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -Level Info
    }
}
else {
    Write-SetupStatus "Execution policy: $currentPolicy ✓" -Level Success
}

# Install VS Code extensions
if (-not $SkipExtensions -and $vsCode) {
    Write-SetupStatus "Installing VS Code extensions..." -Level Info

    $extensions = @(
        "ms-vscode.powershell",
        "ms-dotnettools.csharp",
        "spmeesseman.vscode-taskexplorer",
        "ms-vscode.vscode-xml"
    )

    foreach ($ext in $extensions) {
        try {
            Write-SetupStatus "Installing: $ext" -Level Info
            & $vsCode.Name --install-extension $ext 2>$null
            Write-SetupStatus "Installed: $ext ✓" -Level Success
        }
        catch {
            Write-SetupStatus "Failed to install: $ext" -Level Warning
        }
    }
}

# Test profile loading
Write-SetupStatus "Testing enhanced profile..." -Level Info
try {
    & ".\AI-Assistant\Scripts\load-bus-buddy-profile.ps1" -Quiet -LoadModulesOnly
    Write-SetupStatus "Enhanced profile loads successfully ✓" -Level Success
}
catch {
    Write-SetupStatus "Profile loading failed: $($_.Exception.Message)" -Level Error
    Write-SetupStatus "Check: AI-Assistant\Scripts\load-bus-buddy-profile.ps1" -Level Info
}

# Test build system
Write-SetupStatus "Testing build system..." -Level Info
try {
    $buildResult = & dotnet build "BusBuddy.sln" --verbosity quiet --nologo 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-SetupStatus "Build system working ✓" -Level Success
    }
    else {
        Write-SetupStatus "Build system needs attention" -Level Warning
        Write-SetupStatus "Run: dotnet restore BusBuddy.sln" -Level Info
    }
}
catch {
    Write-SetupStatus "Build test failed: $($_.Exception.Message)" -Level Warning
}

# Create VS Code settings if missing
$settingsPath = ".vscode\settings.json"
if (-not (Test-Path $settingsPath)) {
    Write-SetupStatus "Creating VS Code settings..." -Level Info

    $settingsContent = @"
{
  "terminal.integrated.profiles.windows": {
    "PowerShell 7.5.2": {
      "path": "pwsh.exe",
      "args": ["-NoProfile", "-NoExit", "-Command",
        "& '.\\AI-Assistant\\Scripts\\load-bus-buddy-profile.ps1';"]
    }
  },
  "terminal.integrated.defaultProfile.windows": "PowerShell 7.5.2",
  "powershell.scriptAnalysis.enable": true,
  "powershell.codeFormatting.preset": "OTBS",
  "files.autoSave": "afterDelay",
  "files.autoSaveDelay": 1000
}
"@

    try {
        $settingsContent | Out-File -FilePath $settingsPath -Encoding UTF8
        Write-SetupStatus "VS Code settings created ✓" -Level Success
    }
    catch {
        Write-SetupStatus "Failed to create VS Code settings" -Level Warning
    }
}
else {
    Write-SetupStatus "VS Code settings exist ✓" -Level Success
}

# Final summary
Write-Host @"

🎉 SETUP COMPLETE!
═══════════════════════════════════════════════════════════════════════

$(if ($isGoogleDrive) { "🔄 Google Drive Sync Setup" } else { "📁 Git Clone Setup" })

✅ Next Steps:
1. Open VS Code in this directory: code .
2. Open a new terminal (should auto-load profile)
3. Run: Test-BusBuddyHealth (to verify everything works)
4. Run: bb-help (to see all available commands)

🚀 Quick Test Commands:
    Test-BusBuddyHealth          # Comprehensive health check
   bb-debug-files -AutoFix      # Test file debugging tool
   bb-build -FormatFirst        # Test enhanced build system

📚 Documentation:
   - ENHANCED-PROFILE-GUIDE.md  # Detailed usage guide
   - ENVIRONMENT-SETUP-GUIDE.md # Complete setup reference

$(if ($isGoogleDrive) { @"
🔄 Google Drive Benefits:
   • All configuration files synced automatically
   • Enhanced profiles ready to use immediately
   • VS Code settings pre-configured
   • All development tools included
"@ })

═══════════════════════════════════════════════════════════════════════
"@ -ForegroundColor Green

Write-SetupStatus "Environment setup completed successfully!" -Level Success

if ($isGoogleDrive) {
    Write-SetupStatus "Google Drive sync detected - you should be ready to go immediately!" -Level Success
}
else {
    Write-SetupStatus "You can now continue development as if on the original machine." -Level Info
}
