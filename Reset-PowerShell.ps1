# Reset-PowerShell.ps1
# Script to remove all PowerShell installations and modules, then reinstall from PowerShell Gallery

# Requires Administrator privileges
#Requires -RunAsAdministrator

Write-Host "Starting PowerShell reset process..." -ForegroundColor Green

# Step 1: Stop PowerShell processes
Write-Host "Stopping all PowerShell processes..."
Stop-Process -Name "powershell", "pwsh" -Force -ErrorAction SilentlyContinue

# Step 2: Uninstall PowerShell 7.x
Write-Host "Uninstalling PowerShell 7.x..."
$pwshApp = Get-CimInstance -ClassName Win32_Product | Where-Object { $_.Name -like "PowerShell*7*" }
if ($pwshApp) {
    $pwshApp | ForEach-Object { msiexec.exe /x $_.IdentifyingNumber /quiet }
} else {
    Write-Host "No PowerShell 7.x installation found."
}

# Step 3: Remove Windows PowerShell modules
Write-Host "Removing Windows PowerShell modules..."
$winPSModulePath = "$env:ProgramFiles\WindowsPowerShell\Modules"
if (Test-Path $winPSModulePath) {
    Remove-Item -Path $winPSModulePath -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "No Windows PowerShell modules found."
}

# Step 4: Remove PowerShell 7.x modules
Write-Host "Removing PowerShell 7.x modules..."
$ps7ModulePath = "$env:USERPROFILE\Documents\PowerShell\Modules"
if (Test-Path $ps7ModulePath) {
    Remove-Item -Path $ps7ModulePath -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "No PowerShell 7.x modules found."
}

# Step 5: Remove PowerShell profiles
Write-Host "Removing PowerShell profiles..."
$profiles = @(
    "$env:USERPROFILE\Documents\WindowsPowerShell\profile.ps1",
    "$env:USERPROFILE\Documents\PowerShell\profile.ps1",
    "$env:USERPROFILE\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1",
    "$env:USERPROFILE\Documents\PowerShell\Microsoft.PowerShell_profile.ps1"
)
foreach ($profile in $profiles) {
    if (Test-Path $profile) {
        Remove-Item -Path $profile -Force -ErrorAction SilentlyContinue
    }
}

# Step 6: Install PowerShell 7.5.2 from PowerShell Gallery
Write-Host "Installing PowerShell 7.5.2..."
try {
    winget install --id Microsoft.PowerShell --version 7.5.2 --source winget --silent
    Write-Host "PowerShell 7.5.2 installed successfully."
} catch {
    Write-Host "Failed to install PowerShell 7.5.2. Ensure Winget is functional." -ForegroundColor Red
    Write-Host "Error: $_"
    exit 1
}

# Step 7: Install NuGet provider
Write-Host "Installing NuGet provider..."
Install-PackageProvider -Name NuGet -Force -ErrorAction Stop

# Step 8: Set PSGallery as trusted repository
Write-Host "Setting PSGallery as trusted repository..."
Set-PSRepository -Name PSGallery -InstallationPolicy Trusted

# Step 9: Install required PowerShell modules for BusBuddy
Write-Host "Installing required PowerShell modules..."
$requiredModules = @(
    "Microsoft.WinGet.Client",
    "Pester" # For testing, as implied by BusBuddy's bb-test command
)
foreach ($module in $requiredModules) {
    Write-Host "Installing module: $module"
    Install-Module -Name $module -Force -Scope CurrentUser -Repository PSGallery -ErrorAction Continue
}

# Step 10: Verify installation
Write-Host "Verifying PowerShell installation..."
$psVersion = pwsh -Command { $PSVersionTable.PSVersion.ToString() }
Write-Host "Installed PowerShell version: $psVersion"

# Step 11: Reinstall VS Code PowerShell extensions
Write-Host "Reinstalling VS Code PowerShell extensions..."
$vsCodeExtensions = @(
    "ms-vscode.powershell",
    "ms-vscode.powershell-preview"
)
foreach ($extension in $vsCodeExtensions) {
    code --install-extension $extension --force
}

Write-Host "PowerShell reset complete! Run 'pwsh' to start PowerShell 7.5.2." -ForegroundColor Green
Write-Host "Next steps: Import BusBuddy module and run 'Test-BusBuddyHealth' to validate setup."
