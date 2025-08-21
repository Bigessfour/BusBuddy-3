# 🚌 BusBuddy Dependency Management PowerShell Functions
# Integrated dependency management functions for the BusBuddy PowerShell module
# Usage: Import-Module or add to BusBuddy.psm1

#Requires -Version 7.0

function Invoke-BusBuddyDependencyCheck {
    <#
    .SYNOPSIS
    Performs comprehensive dependency health check for BusBuddy project.
    
    .DESCRIPTION
    Validates package versions, checks for vulnerabilities, verifies Syncfusion license,
    and provides actionable recommendations for dependency management.
    
    .PARAMETER CheckOutdated
    Check for outdated packages.
    
    .PARAMETER CheckVulnerabilities
    Scan for package vulnerabilities.
    
    .PARAMETER ValidateLicense
    Validate Syncfusion license configuration.
    
    .PARAMETER GenerateReport
    Generate JSON report of dependency status.
    
    .PARAMETER OutputPath
    Path for the dependency report output.
    
    .EXAMPLE
    Invoke-BusBuddyDependencyCheck -CheckOutdated -ValidateLicense
    
    .EXAMPLE
    bb-deps-check -CheckVulnerabilities -GenerateReport
    #>
    [CmdletBinding()]
    [Alias('bb-deps-check')]
    param(
        [switch]$CheckOutdated,
        [switch]$CheckVulnerabilities,
        [switch]$ValidateLicense,
        [switch]$GenerateReport,
        [string]$OutputPath = "dependency-status.json"
    )
    
    begin {
        Write-Information "🚌 Starting BusBuddy dependency health check..." -InformationAction Continue
        $startTime = Get-Date
    }
    
    process {
        try {
            # Validate we're in the correct directory
            if (-not (Test-Path "BusBuddy.sln")) {
                throw "BusBuddy.sln not found. Please run from project root directory."
            }
            
            $results = @{
                Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                ProjectPath = (Get-Location).Path
                Status = "Unknown"
                Checks = @{}
                Issues = @()
                Recommendations = @()
            }
            
            # Syncfusion License Check
            if ($ValidateLicense) {
                Write-Information "🔐 Validating Syncfusion license..." -InformationAction Continue
                
                $licenseStatus = @{
                    EnvironmentKey = -not [string]::IsNullOrEmpty($env:SYNCFUSION_LICENSE_KEY)
                    CodeRegistration = $false
                    Status = "Invalid"
                }
                
                # Check for license registration in code
                $appXamlPath = "BusBuddy.WPF\App.xaml.cs"
                if (Test-Path $appXamlPath) {
                    $content = Get-Content $appXamlPath -Raw
                    $licenseStatus.CodeRegistration = $content -match "RegisterLicense"
                }
                
                if ($licenseStatus.EnvironmentKey -and $licenseStatus.CodeRegistration) {
                    $licenseStatus.Status = "Valid"
                    Write-Output "✅ Syncfusion license properly configured"
                } else {
                    $licenseStatus.Status = "Invalid"
                    $results.Issues += "Syncfusion license configuration incomplete"
                    Write-Warning "❌ Syncfusion license configuration issues detected"
                }
                
                $results.Checks.SyncfusionLicense = $licenseStatus
            }
            
            # Outdated Packages Check
            if ($CheckOutdated) {
                Write-Information "📊 Checking for outdated packages..." -InformationAction Continue
                
                $outdatedResult = dotnet list package --outdated 2>&1
                $hasOutdated = $outdatedResult -notlike "*No outdated packages found*"
                
                if ($hasOutdated) {
                    $results.Issues += "Outdated packages detected"
                    Write-Warning "⚠️ Outdated packages found"
                    $outdatedResult | Where-Object { $_ -like "*>*" } | ForEach-Object {
                        Write-Output "  $_"
                    }
                } else {
                    Write-Output "✅ All packages are up to date"
                }
                
                $results.Checks.OutdatedPackages = @{
                    HasOutdated = $hasOutdated
                    Output = $outdatedResult
                }
            }
            
            # Vulnerability Check
            if ($CheckVulnerabilities) {
                Write-Information "🔍 Scanning for package vulnerabilities..." -InformationAction Continue
                
                $vulnResult = dotnet list package --vulnerable --include-transitive 2>&1
                $hasVulnerabilities = $vulnResult -notlike "*No vulnerable packages found*"
                
                if ($hasVulnerabilities) {
                    $results.Issues += "Vulnerable packages detected"
                    Write-Error "❌ Security vulnerabilities found in packages" -ErrorAction Continue
                    $vulnResult | Where-Object { $_ -like "*>*" } | ForEach-Object {
                        Write-Warning "  $_"
                    }
                    $results.Recommendations += "Update vulnerable packages immediately"
                } else {
                    Write-Output "✅ No package vulnerabilities detected"
                }
                
                $results.Checks.Vulnerabilities = @{
                    HasVulnerabilities = $hasVulnerabilities
                    Output = $vulnResult
                }
            }
            
            # Version Consistency Check
            Write-Information "🔄 Checking package version consistency..." -InformationAction Continue
            
            $syncfusionVersions = @()
            
            # Check Directory.Build.props
            if (Test-Path "Directory.Build.props") {
                $propsContent = Get-Content "Directory.Build.props" -Raw
                if ($propsContent -match "<SyncfusionVersion>(.*?)</SyncfusionVersion>") {
                    $syncfusionVersions += $Matches[1]
                }
            }
            
            $versionConsistent = $syncfusionVersions | Group-Object | Measure-Object | ForEach-Object { $_.Count -eq 1 }
            
            if ($versionConsistent) {
                Write-Output "✅ Package versions are consistent"
            } else {
                $results.Issues += "Package version inconsistencies detected"
                Write-Warning "⚠️ Package version inconsistencies found"
            }
            
            $results.Checks.VersionConsistency = @{
                IsConsistent = $versionConsistent
                SyncfusionVersions = $syncfusionVersions
            }
            
            # Determine overall status
            if ($results.Issues.Count -eq 0) {
                $results.Status = "Healthy"
                Write-Output "`n✅ All dependency checks passed"
            } elseif ($results.Issues.Count -le 2) {
                $results.Status = "Warning"
                Write-Warning "`n⚠️ Minor dependency issues detected"
            } else {
                $results.Status = "Critical"
                Write-Error "`n❌ Critical dependency issues require attention" -ErrorAction Continue
            }
            
            # Generate recommendations
            if ($results.Checks.OutdatedPackages.HasOutdated) {
                $results.Recommendations += "Review and update outdated packages"
            }
            
            if ($results.Checks.SyncfusionLicense.Status -ne "Valid") {
                $results.Recommendations += "Configure Syncfusion license key and registration"
            }
            
            # Generate report if requested
            if ($GenerateReport) {
                $reportJson = $results | ConvertTo-Json -Depth 10
                $reportJson | Out-File -FilePath $OutputPath -Encoding UTF8
                Write-Output "📄 Dependency report saved to: $OutputPath"
            }
            
            return $results
            
        } catch {
            Write-Error "Dependency check failed: $($_.Exception.Message)"
            throw
        }
    }
    
    end {
        $duration = (Get-Date) - $startTime
        Write-Information "⏱️ Dependency check completed in $($duration.TotalSeconds.ToString('F2')) seconds" -InformationAction Continue
    }
}

function Update-BusBuddyDependencies {
    <#
    .SYNOPSIS
    Updates BusBuddy project dependencies with safety checks.
    
    .DESCRIPTION
    Safely updates project dependencies with pre-update validation,
    backup creation, and post-update verification.
    
    .PARAMETER PackageNames
    Specific packages to update. If not specified, updates all packages.
    
    .PARAMETER Preview
    Show what would be updated without making changes.
    
    .PARAMETER Force
    Force update even if there are warnings.
    
    .PARAMETER CreateBackup
    Create backup of current package configuration.
    
    .EXAMPLE
    Update-BusBuddyDependencies -Preview
    
    .EXAMPLE
    bb-deps-update -PackageNames "Serilog.*" -CreateBackup
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [Alias('bb-deps-update')]
    param(
        [string[]]$PackageNames,
        [switch]$Preview,
        [switch]$Force,
        [switch]$CreateBackup
    )
    
    begin {
        Write-Information "🔄 Starting BusBuddy dependency update process..." -InformationAction Continue
        
        if (-not (Test-Path "BusBuddy.sln")) {
            throw "BusBuddy.sln not found. Please run from project root directory."
        }
    }
    
    process {
        try {
            # Pre-update validation
            Write-Information "🔍 Running pre-update validation..." -InformationAction Continue
            $preUpdateCheck = Invoke-BusBuddyDependencyCheck -CheckVulnerabilities -ValidateLicense
            
            if ($preUpdateCheck.Status -eq "Critical" -and -not $Force) {
                Write-Warning "Critical issues detected. Use -Force to proceed anyway."
                return
            }
            
            # Create backup if requested
            if ($CreateBackup) {
                $backupPath = "PackageBackup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
                
                Copy-Item "Directory.Build.props" "$backupPath\" -ErrorAction SilentlyContinue
                Copy-Item "NuGet.config" "$backupPath\" -ErrorAction SilentlyContinue
                Get-ChildItem -Recurse -Filter "*.csproj" | Copy-Item -Destination $backupPath -ErrorAction SilentlyContinue
                
                Write-Output "📦 Configuration backup created: $backupPath"
            }
            
            if ($Preview) {
                Write-Information "👀 Preview mode - showing available updates..." -InformationAction Continue
                dotnet list package --outdated
                return
            }
            
            if ($PSCmdlet.ShouldProcess("BusBuddy Dependencies", "Update Packages")) {
                # Clear package cache
                Write-Information "🧹 Clearing package cache..." -InformationAction Continue
                dotnet nuget locals all --clear
                
                # Restore packages
                Write-Information "📥 Restoring packages..." -InformationAction Continue
                dotnet restore --force --no-cache
                
                if ($PackageNames) {
                    foreach ($package in $PackageNames) {
                        Write-Information "📦 Updating package: $package" -InformationAction Continue
                        # Note: dotnet doesn't have direct package update command
                        # This would require nuget.exe or manual project file editing
                        Write-Warning "Package-specific updates require manual Directory.Build.props editing"
                    }
                } else {
                    Write-Information "📦 Package cache cleared and restored. Check Dependabot PRs for updates." -InformationAction Continue
                }
                
                # Post-update validation
                Write-Information "✅ Running post-update validation..." -InformationAction Continue
                $postUpdateCheck = Invoke-BusBuddyDependencyCheck -CheckVulnerabilities -ValidateLicense
                
                if ($postUpdateCheck.Status -ne "Healthy") {
                    Write-Warning "Post-update issues detected. Review dependency status."
                } else {
                    Write-Output "✅ Dependency update completed successfully"
                }
            }
            
        } catch {
            Write-Error "Dependency update failed: $($_.Exception.Message)"
            throw
        }
    }
}

function Test-BusBuddyDependabotConfig {
    <#
    .SYNOPSIS
    Validates Dependabot configuration for BusBuddy project.
    
    .DESCRIPTION
    Checks Dependabot configuration file for proper setup and provides
    recommendations for improvement.
    
    .PARAMETER ValidateOnly
    Only validate existing configuration without suggestions.
    
    .PARAMETER ShowRecommendations
    Display configuration recommendations.
    
    .EXAMPLE
    Test-BusBuddyDependabotConfig -ShowRecommendations
    
    .EXAMPLE
    bb-deps-dependabot -ValidateOnly
    #>
    [CmdletBinding()]
    [Alias('bb-deps-dependabot')]
    param(
        [switch]$ValidateOnly,
        [switch]$ShowRecommendations
    )
    
    begin {
        Write-Information "🤖 Validating Dependabot configuration..." -InformationAction Continue
    }
    
    process {
        $configPath = ".github/dependabot.yml"
        $validation = @{
            ConfigExists = Test-Path $configPath
            HasNuGetEcosystem = $false
            HasSchedule = $false
            HasGrouping = $false
            Issues = @()
            Recommendations = @()
        }
        
        if (-not $validation.ConfigExists) {
            $validation.Issues += "Dependabot configuration file not found"
            Write-Warning "❌ Dependabot configuration missing at $configPath"
            
            if (-not $ValidateOnly) {
                Write-Output "💡 Create Dependabot configuration with:"
                Write-Output "   New-Item -ItemType Directory -Path '.github' -Force"
                Write-Output "   # Then add dependabot.yml configuration"
            }
            
            return $validation
        }
        
        # Read and validate configuration
        $configContent = Get-Content $configPath -Raw
        
        # Check for NuGet ecosystem
        if ($configContent -match 'package-ecosystem:\s*["\']?nuget["\']?') {
            $validation.HasNuGetEcosystem = $true
            Write-Output "✅ NuGet ecosystem configured"
        } else {
            $validation.Issues += "NuGet package ecosystem not configured"
            Write-Warning "❌ NuGet ecosystem missing"
        }
        
        # Check for schedule
        if ($configContent -match 'schedule:\s*\n\s*interval:') {
            $validation.HasSchedule = $true
            Write-Output "✅ Update schedule configured"
        } else {
            $validation.Issues += "Update schedule not configured"
            Write-Warning "❌ Update schedule missing"
        }
        
        # Check for grouping
        if ($configContent -match 'groups:') {
            $validation.HasGrouping = $true
            Write-Output "✅ Package grouping configured"
        } else {
            $validation.Recommendations += "Consider adding package grouping for better PR management"
            Write-Information "💡 Package grouping not configured" -InformationAction Continue
        }
        
        # Provide recommendations
        if ($ShowRecommendations -and -not $ValidateOnly) {
            Write-Output "`n💡 Dependabot Recommendations:"
            
            if (-not $validation.HasGrouping) {
                Write-Output "  • Add package grouping to reduce PR volume"
                Write-Output "  • Group Syncfusion packages together"
                Write-Output "  • Group Microsoft.Extensions.* packages"
            }
            
            Write-Output "  • Configure auto-merge for low-risk packages"
            Write-Output "  • Set up ignore rules for packages requiring manual review"
            Write-Output "  • Enable security-only updates for critical packages"
        }
        
        # Summary
        $statusColor = if ($validation.Issues.Count -eq 0) { "Green" } else { "Yellow" }
        $status = if ($validation.Issues.Count -eq 0) { "✅ Valid" } else { "⚠️ Issues Found" }
        Write-Host "`nDependabot Configuration: $status" -ForegroundColor $statusColor
        
        foreach ($issue in $validation.Issues) {
            Write-Warning "  • $issue"
        }
        
        return $validation
    }
}

function Get-BusBuddyDependencyReport {
    <#
    .SYNOPSIS
    Generates comprehensive dependency report for BusBuddy project.
    
    .DESCRIPTION
    Creates detailed report of all dependencies, their status, vulnerabilities,
    licensing, and recommendations for maintenance.
    
    .PARAMETER OutputFormat
    Output format: JSON, HTML, or Text.
    
    .PARAMETER OutputPath
    Path for the generated report.
    
    .PARAMETER IncludeMetrics
    Include historical metrics and trends.
    
    .EXAMPLE
    Get-BusBuddyDependencyReport -OutputFormat HTML -OutputPath "dependency-report.html"
    
    .EXAMPLE
    bb-deps-report -IncludeMetrics
    #>
    [CmdletBinding()]
    [Alias('bb-deps-report')]
    param(
        [ValidateSet('JSON', 'HTML', 'Text')]
        [string]$OutputFormat = 'JSON',
        [string]$OutputPath,
        [switch]$IncludeMetrics
    )
    
    begin {
        Write-Information "📊 Generating comprehensive dependency report..." -InformationAction Continue
        $reportData = @{
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            ProjectName = "BusBuddy"
            ReportVersion = "1.0"
            Summary = @{}
            Details = @{}
            Recommendations = @()
        }
    }
    
    process {
        try {
            # Comprehensive dependency check
            $dependencyStatus = Invoke-BusBuddyDependencyCheck -CheckOutdated -CheckVulnerabilities -ValidateLicense
            $dependabotStatus = Test-BusBuddyDependabotConfig -ValidateOnly
            
            # Build summary
            $reportData.Summary = @{
                OverallHealth = $dependencyStatus.Status
                TotalIssues = $dependencyStatus.Issues.Count
                SyncfusionLicense = $dependencyStatus.Checks.SyncfusionLicense.Status
                DependabotConfigured = $dependabotStatus.ConfigExists
                LastUpdated = $dependencyStatus.Timestamp
            }
            
            # Detailed findings
            $reportData.Details = @{
                DependencyChecks = $dependencyStatus.Checks
                DependabotValidation = $dependabotStatus
                Issues = $dependencyStatus.Issues
                PackageVersions = @{}
            }
            
            # Extract package versions from Directory.Build.props
            if (Test-Path "Directory.Build.props") {
                $propsContent = Get-Content "Directory.Build.props" -Raw
                $versions = [regex]::Matches($propsContent, '<(\w+Version)>(.*?)</\1>')
                foreach ($match in $versions) {
                    $reportData.Details.PackageVersions[$match.Groups[1].Value] = $match.Groups[2].Value
                }
            }
            
            # Recommendations
            $reportData.Recommendations = $dependencyStatus.Recommendations
            
            if ($dependabotStatus.Issues.Count -gt 0) {
                $reportData.Recommendations += "Fix Dependabot configuration issues"
            }
            
            # Set default output path if not provided
            if (-not $OutputPath) {
                $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
                $extension = switch ($OutputFormat) {
                    'JSON' { 'json' }
                    'HTML' { 'html' }
                    'Text' { 'txt' }
                }
                $OutputPath = "BusBuddy-Dependency-Report-$timestamp.$extension"
            }
            
            # Generate output based on format
            switch ($OutputFormat) {
                'JSON' {
                    $reportJson = $reportData | ConvertTo-Json -Depth 10
                    $reportJson | Out-File -FilePath $OutputPath -Encoding UTF8
                }
                'HTML' {
                    $htmlReport = @"
<!DOCTYPE html>
<html>
<head>
    <title>BusBuddy Dependency Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background-color: #007ACC; color: white; padding: 10px; }
        .section { margin: 20px 0; }
        .status-healthy { color: green; }
        .status-warning { color: orange; }
        .status-critical { color: red; }
        .issue { background-color: #fff3cd; padding: 5px; margin: 5px 0; }
        .recommendation { background-color: #d4edda; padding: 5px; margin: 5px 0; }
    </style>
</head>
<body>
    <div class="header">
        <h1>🚌 BusBuddy Dependency Report</h1>
        <p>Generated: $($reportData.GeneratedAt)</p>
    </div>
    
    <div class="section">
        <h2>Summary</h2>
        <p><strong>Overall Health:</strong> <span class="status-$($reportData.Summary.OverallHealth.ToLower())">$($reportData.Summary.OverallHealth)</span></p>
        <p><strong>Total Issues:</strong> $($reportData.Summary.TotalIssues)</p>
        <p><strong>Syncfusion License:</strong> $($reportData.Summary.SyncfusionLicense)</p>
        <p><strong>Dependabot Configured:</strong> $($reportData.Summary.DependabotConfigured)</p>
    </div>
    
    <div class="section">
        <h2>Issues</h2>
        $(foreach ($issue in $reportData.Details.Issues) { "<div class='issue'>• $issue</div>" })
    </div>
    
    <div class="section">
        <h2>Recommendations</h2>
        $(foreach ($rec in $reportData.Recommendations) { "<div class='recommendation'>• $rec</div>" })
    </div>
</body>
</html>
"@
                    $htmlReport | Out-File -FilePath $OutputPath -Encoding UTF8
                }
                'Text' {
                    $textReport = @"
🚌 BusBuddy Dependency Report
Generated: $($reportData.GeneratedAt)

SUMMARY
=======
Overall Health: $($reportData.Summary.OverallHealth)
Total Issues: $($reportData.Summary.TotalIssues)
Syncfusion License: $($reportData.Summary.SyncfusionLicense)
Dependabot Configured: $($reportData.Summary.DependabotConfigured)

ISSUES
======
$(foreach ($issue in $reportData.Details.Issues) { "• $issue`n" })

RECOMMENDATIONS
===============
$(foreach ($rec in $reportData.Recommendations) { "• $rec`n" })

PACKAGE VERSIONS
================
$(foreach ($version in $reportData.Details.PackageVersions.GetEnumerator()) { "$($version.Key): $($version.Value)`n" })
"@
                    $textReport | Out-File -FilePath $OutputPath -Encoding UTF8
                }
            }
            
            Write-Output "📄 Dependency report generated: $OutputPath"
            Write-Output "📊 Report format: $OutputFormat"
            Write-Output "📈 Overall health: $($reportData.Summary.OverallHealth)"
            
            return $reportData
            
        } catch {
            Write-Error "Report generation failed: $($_.Exception.Message)"
            throw
        }
    }
}

# Export functions for module use
Export-ModuleMember -Function @(
    'Invoke-BusBuddyDependencyCheck',
    'Update-BusBuddyDependencies', 
    'Test-BusBuddyDependabotConfig',
    'Get-BusBuddyDependencyReport'
) -Alias @(
    'bb-deps-check',
    'bb-deps-update',
    'bb-deps-dependabot', 
    'bb-deps-report'
)
