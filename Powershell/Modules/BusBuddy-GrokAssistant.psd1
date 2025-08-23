@{
    # Module metadata
    RootModule = 'BusBuddy-GrokAssistant.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'a8b9c3d4-e5f6-7890-1234-567890abcdef'
    Author = 'BusBuddy Development Team'
    CompanyName = 'BusBuddy'
    Copyright = '© 2024 BusBuddy. All rights reserved.'
    Description = 'AI-powered assistant for BusBuddy school bus transportation management analysis using xAI Grok-4'

    # Version requirements
    PowerShellVersion = '7.0'
    DotNetFrameworkVersion = '8.0'

    # Functions to export from this module
    FunctionsToExport = @(
        'Invoke-GrokCIAnalysis',
        'Invoke-GrokRouteOptimization',
        'Invoke-GrokMaintenancePrediction',
        'Get-GrokInsights',
        'Test-GrokConnection',
        'Get-GrokConfig',
        'Get-GrokAPILog',
        'Invoke-GrokCodeReview',
        'Invoke-GrokPerformanceAnalysis',
        'Invoke-GrokSecurityAudit',
        'Invoke-GrokArchitectureReview',
        'Invoke-GrokTestOptimization',
        'Invoke-GrokDependencyAnalysis',
        'Invoke-GrokDatabaseOptimization',
        'Invoke-GrokUIUXReview',
        'Start-GrokDevelopmentSession',
        'Get-GrokProjectInsights'
    )

    # Cmdlets to export from this module
    CmdletsToExport = @()

    # Variables to export from this module
    VariablesToExport = @('GrokConfig')

    # Aliases to export from this module
    AliasesToExport = @(
        'grok-ci',
        'grok-routes',
        'grok-maintenance',
        'grok-insights',
        'grok-test',
        'grok-config',
        'grok-log',
        'grok-review',
        'grok-perf',
        'grok-security',
        'grok-arch',
        'grok-tests',
        'grok-deps',
        'grok-db',
        'grok-ui',
        'grok-session',
        'grok-project'
    )

    # List of all files packaged with this module
    FileList = @(
        'BusBuddy-GrokAssistant.psm1',
        'BusBuddy-GrokAssistant.psd1',
        'grok-config.ps1',
        '.grok-config.json',
        'grok-assistant.settings.json',
        'GROK-ASSISTANT-CONFIG.md',
        'grok-busbuddy-assistant.ps1'
    )

    # Private data to pass to the module specified in RootModule/ModuleToProcess
    PrivateData = @{
        PSData = @{
            # Tags applied to this module
            Tags = @('BusBuddy', 'Grok', 'AI', 'xAI', 'CI-CD', 'Route-Optimization', 'Maintenance', 'PowerShell')

            # License URI
            LicenseUri = 'https://github.com/yourusername/busbuddy/blob/main/LICENSE'

            # Project URI
            ProjectUri = 'https://github.com/yourusername/busbuddy'

            # Release notes
            ReleaseNotes = @'
v1.0.0 - Initial Release
- CI/CD failure analysis using Grok-4 AI
- Route optimization recommendations
- Maintenance prediction capabilities
- Integration with BusBuddy ecosystem
- PowerShell 7.0+ support
- Comprehensive configuration system
'@

            # Prerequisites
            ExternalModuleDependencies = @()

            # Icon URI
            IconUri = ''
        }

        # BusBuddy specific configuration
        BusBuddy = @{
            ModuleType = 'AI-Assistant'
            IntegrationLevel = 'Core'
            RequiresApiKey = $true
            SupportedPlatforms = @('Windows', 'Linux', 'macOS')
            ConfigurationFiles = @(
                'grok-config.ps1',
                '.grok-config.json',
                'grok-assistant.settings.json'
            )
        }
    }

    # Help Info URI
    HelpInfoURI = 'https://github.com/yourusername/busbuddy/blob/main/docs/grok-assistant.md'

    # Default prefix for commands imported from this module
    DefaultCommandPrefix = ''
}
