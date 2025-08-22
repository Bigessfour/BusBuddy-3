# 🚌 BusBuddy PowerShell Modules Installation Script
# Complete installation of all PowerShell modules for BusBuddy development
# Run this script in a fresh PowerShell session (close VS Code first)

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$SkipAzure,
    [switch]$ListOnly,
    [switch]$Restart
)

begin {
    Write-Information "🚌 BusBuddy PowerShell Modules Installation" -InformationAction Continue
    Write-Information "==========================================" -InformationAction Continue
    
    if ($Restart) {
        Write-Information "🔄 Restarting PowerShell session to clear module locks..." -InformationAction Continue
        Start-Process pwsh -ArgumentList "-NoProfile", "-Command", "& '$PSCommandPath' -Force"
        exit
    }
    
    # Check if running in clean session
    $loadedModules = Get-Module | Where-Object { $_.Name -match "(Az\.|Pester|SqlServer)" }
    if ($loadedModules -and -not $Force) {
        Write-Warning "⚠️ PowerShell modules are currently loaded that may block installation:"
        $loadedModules | ForEach-Object { Write-Output "  • $($_.Name) v$($_.Version)" }
        Write-Information "`n💡 Solutions:" -InformationAction Continue
        Write-Output "  1. Close VS Code and all PowerShell windows, then run this script again"
        Write-Output "  2. Run: $PSCommandPath -Restart"
        Write-Output "  3. Run: $PSCommandPath -Force (may fail for locked modules)"
        return
    }
    
    # All PowerShell modules for BusBuddy development
    $allModules = @(
        # Phase 1: Security and Foundation
        @{ Name = 'PSScriptAnalyzer'; Description = 'PowerShell code analysis and linting'; Priority = 1 },
        @{ Name = 'Microsoft.PowerShell.SecretManagement'; Description = 'Secure secret storage framework'; Priority = 1 },
        @{ Name = 'Microsoft.PowerShell.SecretStore'; Description = 'Local secret store provider'; Priority = 1 },
        
        # Phase 2: Azure Integration
        @{ Name = 'Az.Storage'; Description = 'Azure Storage account operations'; Priority = 2; SkipIf = 'SkipAzure' },
        @{ Name = 'Az.KeyVault'; Description = 'Azure Key Vault secrets management'; Priority = 2; SkipIf = 'SkipAzure' },
        @{ Name = 'Az.Monitor'; Description = 'Azure monitoring and diagnostics'; Priority = 2; SkipIf = 'SkipAzure' },
        
        # Phase 3: Database and Testing
        @{ Name = 'SqlServer'; Description = 'SQL Server management cmdlets'; Priority = 3 },
        @{ Name = 'Pester'; Description = 'PowerShell testing framework'; Priority = 3; Version = '5.6.1' },
        
        # Phase 4: Development Workflow
        @{ Name = 'PSDepend'; Description = 'PowerShell dependency management'; Priority = 4 },
        @{ Name = 'Plaster'; Description = 'PowerShell template and scaffolding engine'; Priority = 4 },
        @{ Name = 'PSFramework'; Description = 'PowerShell development framework'; Priority = 4 },
        
        # Phase 5: Advanced Features
        @{ Name = 'NuGet.PackageManagement'; Description = 'NuGet package management integration'; Priority = 5 }
    )
    
    # Filter modules based on parameters
    $modulesToInstall = $allModules | Where-Object {
        if ($SkipAzure -and $_.SkipIf -eq 'SkipAzure') {
            return $false
        }
        return $true
    }
}

process {
    if ($ListOnly) {
        Write-Information "`n📋 PowerShell Modules for BusBuddy Development:" -InformationAction Continue
        Write-Information "================================================" -InformationAction Continue
        
        for ($phase = 1; $phase -le 5; $phase++) {
            $phaseModules = $modulesToInstall | Where-Object Priority -EQ $phase
            if ($phaseModules) {
                Write-Information "`nPhase $phase:" -InformationAction Continue
                $phaseModules | ForEach-Object {
                    $status = if (Get-Module -ListAvailable -Name $_.Name -ErrorAction SilentlyContinue) { "✅" } else { "❌" }
                    Write-Output "  $status $($_.Name) - $($_.Description)"
                }
            }
        }
        
        Write-Information "`n💡 Usage:" -InformationAction Continue
        Write-Output "  Install all: $PSCommandPath"
        Write-Output "  Skip Azure: $PSCommandPath -SkipAzure"
        Write-Output "  Force install: $PSCommandPath -Force"
        return
    }
    
    Write-Information "`n🔧 Installing PowerShell modules for BusBuddy..." -InformationAction Continue
    
    $results = @{
        Installed = @()
        Updated = @()
        Failed = @()
        Skipped = @()
        AlreadyInstalled = @()
    }
    
    # Install by phases
    for ($phase = 1; $phase -le 5; $phase++) {
        $phaseModules = $modulesToInstall | Where-Object Priority -EQ $phase
        
        if ($phaseModules) {
            Write-Information "`n📦 Phase $phase Installation:" -InformationAction Continue
            
            foreach ($moduleInfo in $phaseModules) {
                try {
                    $existing = Get-Module -ListAvailable -Name $moduleInfo.Name -ErrorAction SilentlyContinue
                    
                    if ($existing -and -not $Force) {
                        Write-Information "  ⏭️  $($moduleInfo.Name) v$($existing[0].Version) already installed" -InformationAction Continue
                        $results.AlreadyInstalled += $moduleInfo.Name
                        continue
                    }
                    
                    Write-Verbose "  📦 Installing $($moduleInfo.Name)..."
                    
                    $installParams = @{
                        Name = $moduleInfo.Name
                        Scope = 'CurrentUser'
                        Force = $true
                        AllowClobber = $true
                        SkipPublisherCheck = $true
                    }
                    
                    if ($moduleInfo.Version) {
                        $installParams.RequiredVersion = $moduleInfo.Version
                    }
                    
                    Install-Module @installParams -ErrorAction Stop
                    
                    if ($existing) {
                        $results.Updated += $moduleInfo.Name
                        Write-Information "  ✅ $($moduleInfo.Name) updated successfully" -InformationAction Continue
                    } else {
                        $results.Installed += $moduleInfo.Name
                        Write-Information "  ✅ $($moduleInfo.Name) installed successfully" -InformationAction Continue
                    }
                    
                } catch {
                    $results.Failed += $moduleInfo.Name
                    Write-Error "  ❌ Failed to install $($moduleInfo.Name): $($_.Exception.Message)"
                    
                    # Try alternative installation for some modules
                    if ($moduleInfo.Name -eq 'PSRule.Azure') {
                        Write-Information "  💡 PSRule.Azure may require different repository or manual installation" -InformationAction Continue
                    }
                }
            }
        }
    }
}

end {
    # Installation Summary
    Write-Information "`n📊 Installation Summary:" -InformationAction Continue
    Write-Information "========================" -InformationAction Continue
    Write-Output "✅ Newly installed: $($results.Installed.Count)"
    Write-Output "🔄 Updated: $($results.Updated.Count)"
    Write-Output "⏭️  Already installed: $($results.AlreadyInstalled.Count)"
    Write-Output "❌ Failed: $($results.Failed.Count)"
    
    if ($results.Installed.Count -gt 0) {
        Write-Information "`n✅ Newly installed modules:" -InformationAction Continue
        $results.Installed | ForEach-Object { Write-Output "  • $_" }
    }
    
    if ($results.Updated.Count -gt 0) {
        Write-Information "`n🔄 Updated modules:" -InformationAction Continue
        $results.Updated | ForEach-Object { Write-Output "  • $_" }
    }
    
    if ($results.Failed.Count -gt 0) {
        Write-Information "`n❌ Failed installations:" -InformationAction Continue
        $results.Failed | ForEach-Object { Write-Output "  • $_" }
        
        Write-Information "`n💡 Troubleshooting tips:" -InformationAction Continue
        Write-Output "  • Ensure internet connectivity"
        Write-Output "  • Try running PowerShell as Administrator"
        Write-Output "  • Check PowerShell Gallery: Get-PSRepository"
        Write-Output "  • For Azure modules, ensure Az.Accounts is installed first"
    }
    
    # Next steps
    Write-Information "`n🚀 Next Steps:" -InformationAction Continue
    Write-Output "1. Restart PowerShell or VS Code to load new modules"
    Write-Output "2. Run: Import-Module BusBuddy-DependencyManagement -Force"
    Write-Output "3. Test installation: bb-deps-check -ValidateLicense"
    Write-Output "4. Install VS Code extensions: .\Scripts\Install-VSCodeExtensions.ps1"
    
    # Create import script for easy testing
    $importScript = @"
# 🚌 BusBuddy Module Import Test
# Test all installed PowerShell modules

Write-Information "🧪 Testing BusBuddy PowerShell modules..." -InformationAction Continue

# Test installations
@(
$(($modulesToInstall | ForEach-Object { "    '$($_.Name)'" }) -join ",`n")
) | ForEach-Object {
    try {
        `$module = Get-Module -ListAvailable -Name `$_ -ErrorAction SilentlyContinue
        if (`$module) {
            Write-Output "✅ `$_ v`$(`$module[0].Version) available"
        } else {
            Write-Output "❌ `$_ not found"
        }
    } catch {
        Write-Warning "⚠️  `$_ error: `$(`$_.Exception.Message)"
    }
}

Write-Information "`n🔧 BusBuddy module test complete!" -InformationAction Continue
"@
    
    $importScript | Out-File -FilePath ".\Test-BusBuddyModules.ps1" -Encoding UTF8
    Write-Output "5. Run module test: .\Test-BusBuddyModules.ps1"
    
    Write-Information "`n🚌 BusBuddy PowerShell environment setup complete!" -InformationAction Continue
}
