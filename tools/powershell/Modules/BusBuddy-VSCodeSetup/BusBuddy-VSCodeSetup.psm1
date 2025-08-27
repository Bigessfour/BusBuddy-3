# 🚌 BusBuddy VS Code Extensions Installer
# Automated installation of VS Code extensions for PowerShell module development
# Supports both VS Code and VS Code Insiders

# cspell:ignore azureresourcegroups azurestorage azurefunctions azureappservice formulahendry eamodio LASTEXITCODE

#Requires -Version 7.0

<#
.SYNOPSIS
${1:Short description}

.DESCRIPTION
${2:Long description}

.PARAMETER UseInsiders
${3:Parameter description}

.PARAMETER Force
${4:Parameter description}

.PARAMETER ListOnly
${5:Parameter description}

.EXAMPLE
${6:An example}

.NOTES
${7:General notes}
#>
function Install-BusBuddyVSCodeExtension {
    [CmdletBinding()]
    param(
        [switch]$UseInsiders,
        [switch]$Force,
        [switch]$ListOnly
    )

    Write-Information "🚌 BusBuddy VS Code Extensions Installer" -InformationAction Continue

    # Detect VS Code installation
    $codeCommand = if ($UseInsiders) { "code-insiders" } else { "code" }

    try {
        & $codeCommand --version | Out-Null
        Write-Information "✅ Found $codeCommand installation" -InformationAction Continue
    } catch {
        Write-Error "❌ $codeCommand not found in PATH. Please install VS Code first."
        exit 1
    }

    # Extensions for PowerShell module development
    $extensionsForModules = @(
        # Core PowerShell Development
        @{ Id = "ms-vscode.powershell"; Description = "Core PowerShell 7.5.2 support" },
        @{ Id = "ms-vscode.powershell-preview"; Description = "Latest PowerShell features" },

        # Azure Development (for Az modules)
        @{ Id = "ms-azuretools.vscode-azureresourcegroups"; Description = "Azure resource management" },
        @{ Id = "ms-azuretools.vscode-azurestorage"; Description = "Azure Storage integration" },
        @{ Id = "ms-azuretools.vscode-azurefunctions"; Description = "Azure Functions development" },
        @{ Id = "ms-azuretools.vscode-azureappservice"; Description = "Azure App Service deployment" },

        # Database Development (for SqlServer module)
        @{ Id = "ms-mssql.mssql"; Description = "SQL Server connection and queries" },
        @{ Id = "ms-mssql.sql-database-projects-vscode"; Description = "SQL database projects" },
        @{ Id = "ms-azuretools.vscode-cosmosdb"; Description = "NoSQL database support" },

        # PowerShell Testing and Development
        @{ Id = "formulahendry.code-runner"; Description = "Execute PowerShell snippets" },
        @{ Id = "ms-vscode.remote-repositories"; Description = "Git repository integration" },

        # Quality and Productivity
        @{ Id = "aaron-bond.better-comments"; Description = "Enhanced code documentation" },
        @{ Id = "streetsidesoftware.code-spell-checker"; Description = "Documentation quality" },
        @{ Id = "eamodio.gitlens"; Description = "Advanced Git integration" }
    )

    if ($ListOnly) {
        Write-Output "`n📋 Extensions to be installed:"
        foreach ($ext in $extensionsForModules) {
            Write-Output "  • $($ext.Id) - $($ext.Description)"
        }
        Write-Output "`nTotal: $($extensionsForModules.Count) extensions"
        return
    }

    Write-Information "`n🔧 Installing VS Code extensions for PowerShell module development..." -InformationAction Continue

    $installed = @()
    $failed = @()
    $skipped = @()

    foreach ($extension in $extensionsForModules) {
        try {
            # Check if already installed
            $existingExtensions = & $codeCommand --list-extensions

            if ($existingExtensions -contains $extension.Id -and -not $Force) {
                Write-Information "⏭️  $($extension.Id) already installed" -InformationAction Continue
                $skipped += $extension.Id
                continue
            }

            Write-Information "📦 Installing $($extension.Id) - $($extension.Description)" -InformationAction Continue

            # Install extension
            $result = & $codeCommand --install-extension $extension.Id --force 2>&1

            if ($LASTEXITCODE -eq 0) {
                $installed += $extension.Id
                Write-Information "✅ $($extension.Id) installed successfully" -InformationAction Continue
            } else {
                $failed += $extension.Id
                Write-Warning "❌ Failed to install $($extension.Id): $result"
            }

        } catch {
            $failed += $extension.Id
            Write-Warning "❌ Exception installing $($extension.Id): $($_.Exception.Message)"
        }
    }

    # Summary
    Write-Output "`n📊 Extension Installation Summary:"
    Write-Output "✅ Successfully installed: $($installed.Count) extensions"
    Write-Output "⏭️  Already installed (skipped): $($skipped.Count) extensions"
    Write-Output "❌ Failed installations: $($failed.Count) extensions"

    if ($installed.Count -gt 0) {
        Write-Output "`n✅ Newly installed extensions:"
        foreach ($ext in $installed) {
            Write-Output "  • $ext"
        }
    }

    if ($failed.Count -gt 0) {
        Write-Warning "`n❌ Failed extensions:"
        foreach ($ext in $failed) {
            Write-Output "  • $ext"
        }
        Write-Output "💡 Try running VS Code as administrator or check network connectivity"
    }

    Write-Output "`n🔄 Restart VS Code to activate all extensions"
    Write-Output "💡 Run 'Developer: Reload Window' command in VS Code if extensions don't load"

    return @{
        Installed = $installed
        Skipped = $skipped
        Failed = $failed
        Total = $extensionsForModules.Count
    }

    Write-Information "🚌 VS Code extensions setup complete!" -InformationAction Continue
}

# Export the function
Export-ModuleMember -Function Install-BusBuddyVSCodeExtensions
