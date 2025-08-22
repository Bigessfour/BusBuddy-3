# 🚌 BusBuddy PowerShell Dependencies
# PSDepend manifest for automated module management
# Usage: Invoke-PSDepend -Path dependencies.psd1 -Install

@{
    # Core PowerShell Development Modules
    PSScriptAnalyzer = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    # Security and Secret Management
    'Microsoft.PowerShell.SecretManagement' = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    'Microsoft.PowerShell.SecretStore' = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    # Azure Integration Modules
    'Az.Storage' = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    'Az.KeyVault' = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    'Az.Monitor' = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    # Database and Testing
    SqlServer = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    Pester = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = '5.6.1'  # Specific version for compatibility
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    # Development Workflow
    PSDepend = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    Plaster = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    PSFramework = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
    
    # NuGet Integration
    'NuGet.PackageManagement' = @{
        DependencyType = 'PSGalleryModule'
        Target = 'CurrentUser'
        Version = 'latest'
        Parameters = @{
            SkipPublisherCheck = $true
            AllowClobber = $true
        }
    }
}
