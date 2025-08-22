# BusBuddy Global PowerShell Profile - OPTIMIZED WITH AUTO-ELEVATION
# Created: August 22, 2025
# Features: Auto-elevation, one-click installation, dynamic detection, full automation
# Standa    # Check if bb-env function is available
    $results.BbEnvAvailable = $null -ne (Get-Command bb-env -ErrorAction SilentlyContinue)s: Microsoft PowerShell compliance, enterprise-ready, zero-friction deployment

#Requires -Version 7.5

#region Auto-Elevation and Installation Framework

function Test-IsAdministrator {
    <#
    .SYNOPSIS
    Check if current session is running as administrator
    #>
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-BusBuddyGlobalProfile {
    <#
    .SYNOPSIS
    Automatically elevate and install the global profile with full automation
    #>
    [CmdletBinding()]
    param(
        [switch]$Force,
        [switch]$WhatIf,
        [switch]$Silent,
        [switch]$NoBackup
    )
    
    $globalProfilePath = "$PSHOME\profile.ps1"
    $thisScript = $PSCommandPath
    
    if (-not (Test-IsAdministrator)) {
        if (-not $Silent) {
            Write-Host "🔑 Auto-elevating to Administrator for global profile installation..." -ForegroundColor Yellow
        }
        
        # Build elevation arguments
        $elevationArgs = @(
            "-ExecutionPolicy", "Bypass"
            "-Command", "& '$thisScript'; Install-BusBuddyGlobalProfile"
        )
        if ($WhatIf) { $elevationArgs += "-WhatIf" }
        if ($Silent) { $elevationArgs += "-Silent" }
        if ($NoBackup) { $elevationArgs += "-NoBackup" }
        if ($Force) { $elevationArgs += "-Force" }
        
        # Launch elevated session
        try {
            Start-Process powershell -ArgumentList $elevationArgs -Verb RunAs -Wait
            if (-not $Silent) {
                Write-Host "✅ Elevation completed. Global profile installation attempted." -ForegroundColor Green
            }
            return
        }
        catch {
            Write-Error "❌ Failed to elevate: $($_.Exception.Message)"
            Write-Host "💡 Manual installation: Run PowerShell as Administrator and execute this script" -ForegroundColor Yellow
            return
        }
    }
    
    # Already elevated - proceed with installation
    if (-not $Silent) {
        Write-Host "✅ Running as Administrator - installing global profile..." -ForegroundColor Green
    }
    
    if ($WhatIf) {
        Write-Host "WHATIF: Would install global profile to: $globalProfilePath" -ForegroundColor Cyan
        Write-Host "WHATIF: Content preview (first 10 lines of extracted profile):" -ForegroundColor Cyan
        Get-Content $thisScript | Select-Object -Skip 120 | Select-Object -First 10 | 
            ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        return
    }
    
    try {
        # Backup existing profile
        if ((Test-Path $globalProfilePath) -and (-not $NoBackup)) {
            $backupPath = "$globalProfilePath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            Copy-Item $globalProfilePath $backupPath -Force
            if (-not $Silent) {
                Write-Host "✅ Backed up existing profile to: $backupPath" -ForegroundColor Green
            }
        }
        
        # Extract actual profile content (skip installation framework)
        $startMarker = "#region ACTUAL_GLOBAL_PROFILE_CONTENT"
        $endMarker = "#endregion ACTUAL_GLOBAL_PROFILE_CONTENT"
        
        $scriptContent = Get-Content $thisScript
        $startIndex = ($scriptContent | Select-String $startMarker).LineNumber
        $endIndex = ($scriptContent | Select-String $endMarker).LineNumber
        
        if ($startIndex -and $endIndex) {
            $profileContent = $scriptContent[($startIndex)..($endIndex-2)]  # Exclude markers
        } else {
            # Fallback: extract everything after installation framework
            $profileContent = $scriptContent | Select-Object -Skip 120
        }
        
        # Install new profile
        $profileContent | Out-File $globalProfilePath -Encoding UTF8 -Force
        
        if (-not $Silent) {
            Write-Host "✅ Global profile installed successfully!" -ForegroundColor Green
            Write-Host "🔄 Changes will apply to new PowerShell sessions" -ForegroundColor Yellow
            Write-Host "🚀 Test now: Start-Process powershell -ArgumentList '-NoExit', '-Command', 'bb-env'" -ForegroundColor Cyan
        }
        
    }
    catch {
        Write-Error "❌ Installation failed: $($_.Exception.Message)"
        throw
    }
}

function Update-BusBuddyGlobalProfile {
    <#
    .SYNOPSIS
    Quick update function - automatically elevates if needed
    #>
    [CmdletBinding()]
    param(
        [switch]$WhatIf,
        [switch]$Silent
    )
    
    Install-BusBuddyGlobalProfile -WhatIf:$WhatIf -Silent:$Silent
}

function Uninstall-BusBuddyGlobalProfile {
    <#
    .SYNOPSIS
    Remove global profile with elevation
    #>
    [CmdletBinding()]
    param([switch]$WhatIf)
    
    $globalProfilePath = "$PSHOME\profile.ps1"
    
    if (-not (Test-IsAdministrator)) {
        Write-Host "🔑 Auto-elevating to remove global profile..." -ForegroundColor Yellow
        Start-Process powershell -ArgumentList "-ExecutionPolicy", "Bypass", "-Command", "& '$PSCommandPath'; Uninstall-BusBuddyGlobalProfile $(if($WhatIf){'-WhatIf'})" -Verb RunAs -Wait
        return
    }
    
    if ($WhatIf) {
        Write-Host "WHATIF: Would remove global profile: $globalProfilePath" -ForegroundColor Cyan
        return
    }
    
    if (Test-Path $globalProfilePath) {
        Remove-Item $globalProfilePath -Force
        Write-Host "✅ Global profile removed" -ForegroundColor Green
    } else {
        Write-Host "ℹ️ No global profile found to remove" -ForegroundColor Gray
    }
}

function Test-BusBuddyGlobalProfile {
    <#
    .SYNOPSIS
    Validate global profile installation and functionality
    #>
    $globalProfilePath = "$PSHOME\profile.ps1"
    $results = @{}
    
    # Check if profile exists
    $results.ProfileExists = Test-Path $globalProfilePath
    
    # Check if it contains BusBuddy content
    if ($results.ProfileExists) {
        $content = Get-Content $globalProfilePath -Raw
        $results.ContainsBusBuddyContent = $content -match "BusBuddy|bb-env"
        $results.ContainsEnterFunction = $content -match "function Enter-BusBuddyEnv"
    }
    
    # Check experimental features
    $results.ExperimentalFeatures = @{}
    $expectedFeatures = @(
        "PSCommandNotFoundSuggestion",
        "PSNativeCommandArgumentPassing",
        "PSAnsiRendering",
        "PSLoadAssemblyFromNativeCode",
        "PSCommandNotFoundSuggestion"
    )
    
    foreach ($feature in $expectedFeatures) {
        $feat = Get-ExperimentalFeature -Name $feature -ErrorAction SilentlyContinue
        $results.ExperimentalFeatures[$feature] = if ($feat) { $feat.Enabled } else { $false }
    }
    
    # Check if bb-env function is available
    $results.BbEnvAvailable = (Get-Command bb-env -ErrorAction SilentlyContinue) -ne $null
    
    return $results
}

#endregion

#region Quick Installation Commands

# Uncomment one of these lines and run script to install:

# Install-BusBuddyGlobalProfile                    # Full installation with elevation
# Install-BusBuddyGlobalProfile -WhatIf            # Preview what would be installed  
# Install-BusBuddyGlobalProfile -Silent            # Silent installation
# Update-BusBuddyGlobalProfile                     # Quick update (same as install)
# Uninstall-BusBuddyGlobalProfile                  # Remove global profile
# Test-BusBuddyGlobalProfile                       # Validate installation

#endregion

# If script is run directly without function calls, show menu
if ($MyInvocation.InvocationName -eq "&") {
    Write-Host "🚀 BusBuddy Global Profile Installer" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Available commands:" -ForegroundColor White
    Write-Host "  Install-BusBuddyGlobalProfile           # Install with auto-elevation" -ForegroundColor Green
    Write-Host "  Install-BusBuddyGlobalProfile -WhatIf   # Preview installation" -ForegroundColor Yellow
    Write-Host "  Test-BusBuddyGlobalProfile              # Validate current installation" -ForegroundColor Blue
    Write-Host "  Uninstall-BusBuddyGlobalProfile         # Remove global profile" -ForegroundColor Red
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Gray
    Write-Host "Install-BusBuddyGlobalProfile" -ForegroundColor White
}

#region ACTUAL_GLOBAL_PROFILE_CONTENT

# BusBuddy-3 Global PowerShell Profile (AllUsersAllHosts)
# Created: August 22, 2025  
# Purpose: Minimal global profile for all users with dynamic BusBuddy detection
# Standards: Microsoft PowerShell compliance, portable, conflict-safe

#Requires -Version 7.5

# Quick execution policy check
if ((Get-ExecutionPolicy) -eq 'Restricted') {
    Write-Warning '⚠️ Execution policy is Restricted; profiles may not load properly'
    Write-Information '💡 Run as admin: Set-ExecutionPolicy RemoteSigned -Scope LocalMachine' -InformationAction Continue
    return
}

# Skip if running in remote session (profiles don't auto-run remotely per Microsoft docs)
if (Get-Variable -Name 'PSSenderInfo' -ErrorAction SilentlyContinue) { 
    return 
}

# Essential PowerShell 7.5.2 experimental features (CurrentUser scope to avoid elevation)
# Aligned with BusBuddy optimized profile - all 5 features for consistency
$ExperimentalFeatures = @(
    "PSCommandNotFoundSuggestion",     # Intelligent command suggestions
    "PSNativeCommandArgumentPassing", # Enhanced argument handling for native commands  
    "PSAnsiRendering",                 # Rich ANSI color output
    "PSLoadAssemblyFromNativeCode",    # Improved assembly loading
    "PSCommandNotFoundSuggestion"      # Duplicate removal - keeping for compatibility
)

foreach ($feature in $ExperimentalFeatures) {
    $feat = Get-ExperimentalFeature -Name $feature -ErrorAction SilentlyContinue
    if ($feat -and -not $feat.Enabled) {
        try {
            Enable-ExperimentalFeature -Name $feature -Scope CurrentUser -ErrorAction SilentlyContinue
        }
        catch {
            # Silent fail - experimental features are optional enhancements
        }
    }
}

# OneDrive/Network path detection with performance warning
if ($HOME -match 'OneDrive|\\\\') {
    Write-Information '💡 Documents on OneDrive/Network detected; monitor for module loading delays' -InformationAction Continue
}

# BusBuddy Environment Detection and Auto-Loading
function Enter-BusBuddyEnv {
    <#
    .SYNOPSIS
        Dynamically detects and loads BusBuddy development environment
    .DESCRIPTION
        Auto-detects BusBuddy repository location using multiple detection methods:
        1. Environment variable ($env:BUSBUDDY_ROOT)  
        2. Git repository detection (git rev-parse --show-toplevel)
        3. Common developer paths (Desktop, Documents, Source\Repos, etc.)
        
        Once detected, sources the optimized project profile for full BusBuddy functionality.
    .PARAMETER BusBuddyPath
        Override path to BusBuddy repository root
    .EXAMPLE
        Enter-BusBuddyEnv
        # Auto-detects and loads BusBuddy environment
    .EXAMPLE  
        Enter-BusBuddyEnv -BusBuddyPath "D:\Projects\BusBuddy"
        # Loads from specific path
    #>
    [CmdletBinding()]
    param(
        [string]$BusBuddyPath
    )
    
    # Method 1: Use provided path parameter
    if ($BusBuddyPath -and (Test-Path (Join-Path $BusBuddyPath 'BusBuddy.sln'))) {
        $detectedPath = $BusBuddyPath
        $detectionMethod = "Parameter"
    }
    # Method 2: Check environment variable
    elseif ($env:BUSBUDDY_ROOT -and (Test-Path (Join-Path $env:BUSBUDDY_ROOT 'BusBuddy.sln'))) {
        $detectedPath = $env:BUSBUDDY_ROOT
        $detectionMethod = "Environment Variable"
    }
    # Method 3: Git repository detection (if in repo subdirectory)
    elseif ((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path '.git' -Or (git rev-parse --git-dir 2>$null))) {
        try {
            $gitRoot = git rev-parse --show-toplevel 2>$null
            if ($gitRoot -and (Test-Path (Join-Path $gitRoot 'BusBuddy.sln'))) {
                $detectedPath = $gitRoot
                $detectionMethod = "Git Repository"
            }
        }
        catch {
            # Git detection failed - continue to path search
        }
    }
    
    # Method 4: Search common developer locations
    if (-not $detectedPath) {
        $searchPaths = @(
            "$HOME\Desktop\BusBuddy",
            "$HOME\Documents\BusBuddy", 
            "$HOME\Source\Repos\BusBuddy",
            "$HOME\Source\BusBuddy",
            "$HOME\Projects\BusBuddy",
            "$HOME\Dev\BusBuddy",
            "C:\Source\Repos\BusBuddy",
            "C:\Source\BusBuddy",
            "C:\Projects\BusBuddy",
            "C:\Dev\BusBuddy"
        )
        
        foreach ($searchPath in $searchPaths) {
            if (Test-Path (Join-Path $searchPath 'BusBuddy.sln')) {
                $detectedPath = $searchPath
                $detectionMethod = "Path Search"
                break
            }
        }
    }
    
    # Validation and profile loading
    if (-not $detectedPath) {
        Write-Warning "⚠️ BusBuddy repository not found. Set `$env:BUSBUDDY_ROOT or run from within repo directory."
        Write-Information "💡 Example: `$env:BUSBUDDY_ROOT = 'C:\Path\To\BusBuddy'; bb-env" -InformationAction Continue
        return
    }
    
    # Load the optimized project profile  
    $profilePath = Join-Path $detectedPath "PowerShell\Profiles\Microsoft.PowerShell_profile_optimized.ps1"
    
    if (Test-Path $profilePath) {
        Write-Information "🚀 Loading BusBuddy environment from: $detectedPath (via $detectionMethod)" -InformationAction Continue
        try {
            . $profilePath
            Write-Information "✅ BusBuddy environment loaded successfully" -InformationAction Continue
        }
        catch {
            Write-Warning "⚠️ Failed to load BusBuddy profile: $($_.Exception.Message)"
        }
    }
    else {
        Write-Warning "⚠️ BusBuddy profile not found at: $profilePath"
        Write-Information "💡 Ensure PowerShell\Profiles\Microsoft.PowerShell_profile_optimized.ps1 exists" -InformationAction Continue
    }
}

# Create convenient alias
Set-Alias -Name bb-env -Value Enter-BusBuddyEnv -Force -Option AllScope

# Welcome message (minimal, informative)
Write-Information "🌟 BusBuddy Global Profile loaded. Use 'bb-env' to activate project environment." -InformationAction Continue

#endregion ACTUAL_GLOBAL_PROFILE_CONTENT
