# 🚌 BusBuddy PowerShell 7.5.2 Validation Script
# Validates PowerShell modules for compliance with modern syntax and best practices

#Requires -Version 7.5
#Requires -Module PSScriptAnalyzer

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ModulePath = "$PSScriptRoot\Modules",
    
    [Parameter()]
    [string]$SettingsPath = "$PSScriptRoot\..\PSScriptAnalyzerSettings.psd1",
    
    [Parameter()]
    [switch]$Fix,
    
    [Parameter()]
    [switch]$Detailed
)

begin {
    Write-Information "🔍 BusBuddy PowerShell 7.5.2 Compliance Validation" -InformationAction Continue
    Write-Information "Module Path: $ModulePath" -InformationAction Continue
    Write-Information "Settings: $SettingsPath" -InformationAction Continue
}

process {
    try {
        # Get all PowerShell files
        $psFiles = Get-ChildItem -Path $ModulePath -Filter "*.ps*" -Recurse -File
        Write-Information "Found $($psFiles.Count) PowerShell files to analyze" -InformationAction Continue
        
        $analysisResults = @()
        $totalIssues = 0
        
        foreach ($file in $psFiles) {
            Write-Information "Analyzing: $($file.Name)" -InformationAction Continue
            
            # Run PSScriptAnalyzer
            $issues = Invoke-ScriptAnalyzer -Path $file.FullName -Settings $SettingsPath
            
            if ($issues) {
                $totalIssues += $issues.Count
                $analysisResults += [PSCustomObject]@{
                    File = $file.Name
                    Path = $file.FullName
                    Issues = $issues
                    IssueCount = $issues.Count
                }
                
                if ($Detailed) {
                    Write-Warning "Issues in $($file.Name):"
                    foreach ($issue in $issues) {
                        Write-Warning "  Line $($issue.Line): $($issue.RuleName) - $($issue.Message)"
                    }
                }
            }
        }
        
        # Summary
        Write-Output "`n📊 PowerShell 7.5.2 Compliance Report:"
        Write-Output "Files analyzed: $($psFiles.Count)"
        Write-Output "Files with issues: $($analysisResults.Count)"
        Write-Output "Total issues found: $totalIssues"
        
        if ($totalIssues -eq 0) {
            Write-Information "✅ All PowerShell files are compliant with 7.5.2 standards!" -InformationAction Continue
        } else {
            Write-Warning "❌ Found $totalIssues compliance issues that need attention"
        }
        
        # Auto-fix if requested
        if ($Fix -and $totalIssues -gt 0) {
            Write-Information "🔧 Attempting to auto-fix issues..." -InformationAction Continue
            
            foreach ($result in $analysisResults) {
                $fixableIssues = $result.Issues | Where-Object { $_.SuggestedCorrections }
                
                if ($fixableIssues) {
                    Write-Information "Fixing $($fixableIssues.Count) issues in $($result.File)" -InformationAction Continue
                    Invoke-ScriptAnalyzer -Path $result.Path -Settings $SettingsPath -Fix
                }
            }
        }
        
        return $analysisResults
        
    } catch {
        Write-Error "Failed to validate PowerShell compliance: $($_.Exception.Message)"
        throw
    }
}

end {
    Write-Information "🏁 PowerShell validation complete" -InformationAction Continue
}
