#Requires -RunAsAdministrator
Write-Host "Completing PowerShell reset for BusBuddy..." -ForegroundColor Green

# Stop PowerShell processes
Stop-Process -Name "powershell", "pwsh" -Force -ErrorAction SilentlyContinue

# Remove old modules
$winPSModulePath = "$env:ProgramFiles\WindowsPowerShell\Modules"
$ps7ModulePath = "$env:USERPROFILE\Documents\PowerShell\Modules"
if (Test-Path $winPSModulePath) { Remove-Item -Path $winPSModulePath -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $ps7ModulePath) { Remove-Item -Path $ps7ModulePath -Recurse -Force -ErrorAction SilentlyContinue }

# Verify PowerShell 7.5.2
$psVersion = pwsh -Command { $PSVersionTable.PSVersion.ToString() }
if ($psVersion -ne "7.5.2") {
    winget install --id Microsoft.PowerShell --version 7.5.2 --source winget --silent
}

# Install modules from PSGallery
Set-PSRepository -Name PSGallery -InstallationPolicy Trusted
Install-Module -Name Microsoft.WinGet.Client, Pester -Force -Scope CurrentUser -Repository PSGallery -ErrorAction Stop

# Reinstall VS Code extensions
code --install-extension ms-vscode.powershell --force
code --install-extension ms-vscode.powershell-preview --force

# Clean up temporary files
Remove-Item -Path "C:\Users\biges\Desktop\BusBuddy\BusBuddy.WPF\obj\*.csproj.nuget.g.props" -Force -ErrorAction SilentlyContinue

# Verify BusBuddy setup
Set-Location -Path "C:\Users\biges\Desktop\BusBuddy"
Import-Module .\PowerShell\BusBuddy.psm1
Test-BusBuddyHealth

Write-Host "PowerShell reset complete! Run 'bb-build' and 'bb-run' to test BusBuddy." -ForegroundColor Green
