# 🚌 BusBuddy Complete Module Installation Script
# Installs all modules from the original comprehensive environment setup
# Based on Azure SQL PowerShell best practices and Syncfusion WPF guidelines

#Requires -Version 7.5

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Force,
    
    [Parameter()]
    [switch]$SkipOptional
)

begin {
    Write-Information "🚌 Installing BusBuddy Complete PowerShell Environment" -InformationAction Continue
    Write-Information "This will install all modules from the original comprehensive setup" -InformationAction Continue
}

process {
    # High-impact modules (required for full functionality)
    $RequiredModules = @(
        @{ Name = 'Az.Accounts'; Description = 'Azure authentication and account management' },
        @{ Name = 'Az.Resources'; Description = 'Azure resource management' },
        @{ Name = 'Az.Sql'; Description = 'Azure SQL database management' },
        @{ Name = 'Az.Storage'; Description = 'Azure storage account operations' },
        @{ Name = 'Az.KeyVault'; Description = 'Azure Key Vault secrets management' },
        @{ Name = 'Az.Monitor'; Description = 'Azure monitoring and diagnostics' },
        @{ Name = 'Az.Security'; Description = 'Azure Security Center operations' },
        @{ Name = 'SqlServer'; Description = 'SQL Server management cmdlets' },
        @{ Name = 'PSScriptAnalyzer'; Description = 'PowerShell code analysis and linting' },
        @{ Name = 'Microsoft.PowerShell.SecretManagement'; Description = 'Secure secret storage' },
        @{ Name = 'Microsoft.PowerShell.SecretStore'; Description = 'Local secret store provider' },
        @{ Name = 'Pester'; Description = 'PowerShell testing framework'; Version = '5.6.1' },
        @{ Name = 'PSDepend'; Description = 'PowerShell dependency management' },
        @{ Name = 'PSRule.Rules.Azure'; Description = 'Azure compliance and best practices validation' },
        @{ Name = 'Plaster'; Description = 'PowerShell project scaffolding' },
        @{ Name = 'PSFramework'; Description = 'PowerShell framework for advanced development' },
        @{ Name = 'PackageManagement'; Description = 'Built-in package management (should be available)' }
    )
    
    # Nice-to-have modules (optional but recommended)
    $OptionalModules = @(
        @{ Name = 'dbatools'; Description = 'SQL Server database administration' },
        @{ Name = 'ImportExcel'; Description = 'Excel file manipulation' },
        @{ Name = 'PSGraph'; Description = 'Graph visualization for PowerShell' }
    )
    
    $allModules = $RequiredModules
    if (-not $SkipOptional) {
        $allModules += $OptionalModules
    }
    
    $installed = @()
    $failed = @()
    $totalModules = $allModules.Count
    $currentModule = 0
    
    foreach ($module in $allModules) {
        $currentModule++
        Write-Progress -Activity "Installing PowerShell Modules" -Status "Installing $($module.Name)" -PercentComplete (($currentModule / $totalModules) * 100)
        
        try {
            # Check if already installed
            $existing = Get-Module -ListAvailable -Name $module.Name -ErrorAction SilentlyContinue
            
            if ($existing -and -not $Force) {
                Write-Information "✅ $($module.Name) already installed (version: $($existing[0].Version))" -InformationAction Continue
                $installed += $module.Name
                continue
            }
            
            Write-Information "📦 Installing $($module.Name) - $($module.Description)" -InformationAction Continue
            
            $installParams = @{
                Name = $module.Name
                Scope = 'CurrentUser'
                Force = $true
                AllowClobber = $true
                SkipPublisherCheck = $true
                ErrorAction = 'Stop'
            }
            
            if ($module.Version) {
                $installParams.RequiredVersion = $module.Version
            }
            
            Install-Module @installParams
            $installed += $module.Name
            Write-Information "✅ $($module.Name) installed successfully" -InformationAction Continue
            
        } catch {
            $failed += $module.Name
            Write-Warning "❌ Failed to install $($module.Name): $($_.Exception.Message)"
        }
    }
    
    Write-Progress -Activity "Installing PowerShell Modules" -Completed
    
    # Summary
    Write-Output "`n📊 BusBuddy Complete Module Installation Summary:"
    Write-Output "✅ Successfully installed: $($installed.Count) modules"
    Write-Output "❌ Failed installations: $($failed.Count) modules"
    
    if ($installed.Count -gt 0) {
        Write-Output "`n✅ Installed modules:"
        $installed | ForEach-Object { Write-Output "  - $_" }
    }
    
    if ($failed.Count -gt 0) {
        Write-Output "`n❌ Failed modules:"
        $failed | ForEach-Object { Write-Output "  - $_" }
        Write-Warning "Run with -Force to retry failed installations"
    }
    
    if ($failed.Count -eq 0) {
        Write-Information "`n🎉 All modules installed successfully!" -InformationAction Continue
        Write-Information "💡 Restart your PowerShell session or run: . `$PROFILE" -InformationAction Continue
        Write-Information "🔧 Test with: bb-deps-check" -InformationAction Continue
    }
}

end {
    Write-Information "🏁 BusBuddy module installation complete" -InformationAction Continue
}
