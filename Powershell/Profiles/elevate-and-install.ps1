# Auto-Elevate and Install Global Profile
# This script will automatically request admin privileges and install the global profile

param(
    [switch]$Install,
    [switch]$Test
)

# Check if running as administrator
function Test-IsAdmin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
    Write-Host "🔑 Requesting Administrator privileges..." -ForegroundColor Yellow
    
    # Prepare elevation arguments
    $scriptPath = $MyInvocation.MyCommand.Path
    $arguments = "-ExecutionPolicy Bypass -File `"$scriptPath`" -Install"
    
    try {
        # Launch elevated PowerShell
        Start-Process PowerShell -ArgumentList $arguments -Verb RunAs -Wait
        Write-Host "✅ Elevation completed" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Failed to elevate: $($_.Exception.Message)"
        Write-Host "💡 Please run PowerShell as Administrator manually" -ForegroundColor Yellow
    }
    
    return
}

# Now running as administrator
Write-Host "✅ Running as Administrator" -ForegroundColor Green

if ($Install) {
    Write-Host "📝 Installing global profile..." -ForegroundColor Cyan
    
    $globalProfilePath = "$PSHOME\profile.ps1"
    $sourceProfilePath = Join-Path $PSScriptRoot "global-profile-fix.ps1"
    
    if (-not (Test-Path $sourceProfilePath)) {
        Write-Error "❌ Source profile not found: $sourceProfilePath"
        Read-Host "Press Enter to continue"
        return
    }
    
    try {
        # Backup existing profile if it exists
        if (Test-Path $globalProfilePath) {
            $backupPath = "$globalProfilePath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            Copy-Item $globalProfilePath $backupPath -Force
            Write-Host "✅ Backed up existing profile to: $backupPath" -ForegroundColor Green
        }
        
        # Copy the profile content
        Copy-Item $sourceProfilePath $globalProfilePath -Force
        Write-Host "✅ Global profile installed successfully!" -ForegroundColor Green
        Write-Host "📍 Installed to: $globalProfilePath" -ForegroundColor Gray
        
        # Test the installation
        Write-Host "🧪 Testing installation..." -ForegroundColor Cyan
        
        if (Test-Path $globalProfilePath) {
            $content = Get-Content $globalProfilePath -Raw
            if ($content -match "BusBuddy|bb-env") {
                Write-Host "✅ Profile contains BusBuddy content" -ForegroundColor Green
            } else {
                Write-Warning "⚠️ Profile doesn't contain expected BusBuddy content"
            }
        }
        
        Write-Host ""
        Write-Host "🚀 Installation complete!" -ForegroundColor Green
        Write-Host "🔄 Please restart PowerShell to apply changes" -ForegroundColor Yellow
        Write-Host "🧪 Test with: Start a new PowerShell session and run 'bb-env'" -ForegroundColor Cyan
        
    }
    catch {
        Write-Error "❌ Installation failed: $($_.Exception.Message)"
    }
    
    Write-Host ""
    Write-Host "Press Enter to continue..." -ForegroundColor Gray
    Read-Host
}
else {
    Write-Host "💡 Usage: Run this script to auto-elevate and install global profile" -ForegroundColor Yellow
    Write-Host "Example: .\elevate-and-install.ps1" -ForegroundColor Gray
}
