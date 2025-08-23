# BusBuddy Grok Assistant PowerShell Module
# Integrates xAI Grok-4 API for intelligent analysis and optimization

#requires -Version 7.0

[CmdletBinding()]
param()

# Import the main assistant functionality
. "$PSScriptRoot\grok-busbuddy-assistant.ps1"

# Module metadata
$ModuleInfo = @{
    Name = 'BusBuddy-GrokAssistant'
    Version = '1.0.0'
    Description = 'AI-powered assistant for BusBuddy school bus transportation management analysis'
    Author = 'BusBuddy Development Team'
    CompanyName = 'BusBuddy'
    Copyright = '© 2024 BusBuddy. All rights reserved.'
    PowerShellVersion = '7.0'
    RequiredModules = @()
    FunctionsToExport = @(
        'Invoke-GrokCIAnalysis',
        'Invoke-GrokRouteOptimization',
        'Invoke-GrokMaintenancePrediction',
        'Get-GrokInsights',
        'Test-GrokConnection',
        'Get-GrokConfig',
        'Get-GrokAPILog'
    )
    AliasesToExport = @(
        'grok-ci',
        'grok-routes',
        'grok-maintenance',
        'grok-insights',
        'grok-test',
        'grok-config',
        'grok-log'
    )
    VariablesToExport = @('GrokConfig')
}

# Export module information for manifest generation
$script:ModuleInfo = $ModuleInfo
