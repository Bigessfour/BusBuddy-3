# 🚌 BusBuddy Dependency Management Script
# Local dependency validation and Syncfusion license checking
# Usage: .\Scripts\Validate-Dependencies.ps1

[CmdletBinding()]
param(
    [switch]$CheckOutdated,
    [switch]$CheckVulnerabilities,
    [switch]$ValidateLicense,
    [switch]$UpdatePackages,
    [switch]$GenerateReport,
    [string]$OutputPath = "dependency-report.json"
)

# Error handling
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Script information
$ScriptInfo = @{
    Name = "BusBuddy Dependency Manager"
    Version = "1.0.0"
    Description = "Validates dependencies and Syncfusion licensing"
    LastModified = Get-Date -Format "yyyy-MM-dd"
}

Write-Host "🚌 $($ScriptInfo.Name) v$($ScriptInfo.Version)" -ForegroundColor Blue
Write-Host "📋 $($ScriptInfo.Description)" -ForegroundColor Gray

# Initialize report data
$Report = @{
    Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    SolutionPath = "BusBuddy.sln"
    Results = @{}
    Issues = @()
    Recommendations = @()
}

function Test-SyncfusionLicense {
    [CmdletBinding()]
    param()
    
    Write-Host "`n🔐 Validating Syncfusion License Configuration..." -ForegroundColor Yellow
    
    $licenseValidation = @{
        EnvironmentVariable = $false
        CodeRegistration = $false
        Issues = @()
        Status = "Unknown"
    }
    
    # Check environment variable
    $licenseKey = $env:SYNCFUSION_LICENSE_KEY
    if ([string]::IsNullOrEmpty($licenseKey)) {
        $licenseValidation.Issues += "SYNCFUSION_LICENSE_KEY environment variable not set"
        Write-Warning "❌ Syncfusion license key not found in environment variables"
    } else {
        $licenseValidation.EnvironmentVariable = $true
        Write-Host "✅ Syncfusion license key found in environment" -ForegroundColor Green
    }
    
    # Check for license registration in code
    $registrationFound = $false
    $codeFiles = Get-ChildItem -Recurse -Filter "*.cs" -Path @("BusBuddy.Core", "BusBuddy.WPF") -ErrorAction SilentlyContinue
    
    foreach ($file in $codeFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -and $content.Contains("RegisterLicense")) {
            $registrationFound = $true
            Write-Host "✅ License registration found in: $($file.Name)" -ForegroundColor Green
            break
        }
    }
    
    if (-not $registrationFound) {
        $licenseValidation.Issues += "No license registration calls found in code"
        Write-Warning "❌ No Syncfusion license registration found in code"
    } else {
        $licenseValidation.CodeRegistration = $true
    }
    
    # Determine overall status
    if ($licenseValidation.EnvironmentVariable -and $licenseValidation.CodeRegistration) {
        $licenseValidation.Status = "Valid"
        Write-Host "✅ Syncfusion license configuration is valid" -ForegroundColor Green
    } elseif ($licenseValidation.EnvironmentVariable -or $licenseValidation.CodeRegistration) {
        $licenseValidation.Status = "Partial"
        Write-Warning "⚠️ Syncfusion license configuration is incomplete"
    } else {
        $licenseValidation.Status = "Invalid"
        Write-Error "❌ Syncfusion license configuration is invalid" -ErrorAction Continue
    }
    
    return $licenseValidation
}

function Test-PackageVulnerabilities {
    [CmdletBinding()]
    param()
    
    Write-Host "`n🔍 Checking for vulnerable packages..." -ForegroundColor Yellow
    
    try {
        $vulnerableOutput = dotnet list package --vulnerable --include-transitive 2>&1
        $vulnerabilities = @()
        
        if ($vulnerableOutput -like "*No vulnerable packages found*") {
            Write-Host "✅ No vulnerable packages found" -ForegroundColor Green
        } else {
            Write-Warning "⚠️ Vulnerable packages detected"
            $vulnerabilities = $vulnerableOutput | Where-Object { $_ -like "*>*" }
            foreach ($vuln in $vulnerabilities) {
                Write-Warning "  $vuln"
            }
        }
        
        return @{
            HasVulnerabilities = $vulnerabilities.Count -gt 0
            Vulnerabilities = $vulnerabilities
            Output = $vulnerableOutput
        }
    } catch {
        Write-Error "Failed to check vulnerabilities: $($_.Exception.Message)"
        return @{
            HasVulnerabilities = $null
            Vulnerabilities = @()
            Output = $_.Exception.Message
        }
    }
}

function Test-OutdatedPackages {
    [CmdletBinding()]
    param()
    
    Write-Host "`n📊 Checking for outdated packages..." -ForegroundColor Yellow
    
    try {
        $outdatedOutput = dotnet list package --outdated 2>&1
        $outdatedPackages = @()
        
        if ($outdatedOutput -like "*No outdated packages found*") {
            Write-Host "✅ All packages are up to date" -ForegroundColor Green
        } else {
            Write-Information "📦 Outdated packages found"
            $outdatedPackages = $outdatedOutput | Where-Object { $_ -like "*>*" }
            foreach ($pkg in $outdatedPackages) {
                Write-Information "  $pkg"
            }
        }
        
        return @{
            HasOutdated = $outdatedPackages.Count -gt 0
            OutdatedPackages = $outdatedPackages
            Output = $outdatedOutput
        }
    } catch {
        Write-Error "Failed to check outdated packages: $($_.Exception.Message)"
        return @{
            HasOutdated = $null
            OutdatedPackages = @()
            Output = $_.Exception.Message
        }
    }
}

function Test-SyncfusionVersionConsistency {
    [CmdletBinding()]
    param()
    
    Write-Host "`n🔄 Checking Syncfusion version consistency..." -ForegroundColor Yellow
    
    $syncfusionVersions = @()
    
    # Check Directory.Build.props
    $propsFile = "Directory.Build.props"
    if (Test-Path $propsFile) {
        $propsContent = Get-Content $propsFile -Raw
        if ($propsContent -match "<SyncfusionVersion>(.*?)</SyncfusionVersion>") {
            $syncfusionVersions += @{
                Source = "Directory.Build.props"
                Version = $Matches[1]
            }
        }
    }
    
    # Check project files for direct Syncfusion references
    $projectFiles = Get-ChildItem -Recurse -Filter "*.csproj"
    foreach ($projFile in $projectFiles) {
        $projContent = Get-Content $projFile.FullName -Raw
        $matches = [regex]::Matches($projContent, 'PackageReference Include="Syncfusion[^"]*"[^>]*Version="([^"]*)"')
        foreach ($match in $matches) {
            $syncfusionVersions += @{
                Source = $projFile.Name
                Version = $match.Groups[1].Value
            }
        }
    }
    
    $uniqueVersions = $syncfusionVersions | Group-Object Version
    
    if ($uniqueVersions.Count -eq 1) {
        Write-Host "✅ Syncfusion versions are consistent: $($uniqueVersions[0].Name)" -ForegroundColor Green
        return @{
            IsConsistent = $true
            Version = $uniqueVersions[0].Name
            Sources = $syncfusionVersions
        }
    } else {
        Write-Warning "⚠️ Syncfusion version inconsistency detected"
        foreach ($version in $uniqueVersions) {
            Write-Warning "  Version $($version.Name) found in:"
            foreach ($source in $version.Group) {
                Write-Warning "    - $($source.Source)"
            }
        }
        return @{
            IsConsistent = $false
            Version = $null
            Sources = $syncfusionVersions
        }
    }
}

function Update-DependencyPackages {
    [CmdletBinding()]
    param()
    
    Write-Host "`n📦 Updating packages..." -ForegroundColor Yellow
    
    if (-not $UpdatePackages) {
        Write-Information "Use -UpdatePackages switch to actually update packages"
        return
    }
    
    try {
        # Clear package cache
        Write-Host "🧹 Clearing package cache..." -ForegroundColor Gray
        dotnet nuget locals all --clear
        
        # Restore packages
        Write-Host "📥 Restoring packages..." -ForegroundColor Gray
        dotnet restore --force --no-cache
        
        Write-Host "✅ Package update completed" -ForegroundColor Green
    } catch {
        Write-Error "Failed to update packages: $($_.Exception.Message)"
    }
}

# Main execution
try {
    # Validate Syncfusion license
    if ($ValidateLicense -or $PSCmdlet.ParameterSetName -eq "__AllParameterSets") {
        $Report.Results.SyncfusionLicense = Test-SyncfusionLicense
    }
    
    # Check vulnerabilities
    if ($CheckVulnerabilities -or $PSCmdlet.ParameterSetName -eq "__AllParameterSets") {
        $Report.Results.Vulnerabilities = Test-PackageVulnerabilities
    }
    
    # Check outdated packages
    if ($CheckOutdated -or $PSCmdlet.ParameterSetName -eq "__AllParameterSets") {
        $Report.Results.OutdatedPackages = Test-OutdatedPackages
    }
    
    # Check Syncfusion version consistency
    $Report.Results.SyncfusionVersions = Test-SyncfusionVersionConsistency
    
    # Update packages if requested
    if ($UpdatePackages) {
        Update-DependencyPackages
    }
    
    # Generate recommendations
    if ($Report.Results.SyncfusionLicense.Status -ne "Valid") {
        $Report.Recommendations += "Configure Syncfusion license key and registration"
    }
    
    if ($Report.Results.Vulnerabilities.HasVulnerabilities) {
        $Report.Recommendations += "Update vulnerable packages immediately"
    }
    
    if ($Report.Results.OutdatedPackages.HasOutdated) {
        $Report.Recommendations += "Consider updating outdated packages"
    }
    
    if (-not $Report.Results.SyncfusionVersions.IsConsistent) {
        $Report.Recommendations += "Standardize Syncfusion package versions"
    }
    
    # Generate report
    if ($GenerateReport) {
        $reportJson = $Report | ConvertTo-Json -Depth 10
        $reportJson | Out-File -FilePath $OutputPath -Encoding UTF8
        Write-Host "`n📄 Report saved to: $OutputPath" -ForegroundColor Green
    }
    
    # Summary
    Write-Host "`n📋 Dependency Validation Summary:" -ForegroundColor Blue
    Write-Host "  Syncfusion License: $($Report.Results.SyncfusionLicense.Status)" -ForegroundColor $(if ($Report.Results.SyncfusionLicense.Status -eq "Valid") { "Green" } else { "Red" })
    
    if ($Report.Results.Vulnerabilities) {
        $vulnColor = if ($Report.Results.Vulnerabilities.HasVulnerabilities) { "Red" } else { "Green" }
        Write-Host "  Vulnerabilities: $(if ($Report.Results.Vulnerabilities.HasVulnerabilities) { "Found" } else { "None" })" -ForegroundColor $vulnColor
    }
    
    if ($Report.Results.OutdatedPackages) {
        $outdatedColor = if ($Report.Results.OutdatedPackages.HasOutdated) { "Yellow" } else { "Green" }
        Write-Host "  Outdated Packages: $(if ($Report.Results.OutdatedPackages.HasOutdated) { "Found" } else { "None" })" -ForegroundColor $outdatedColor
    }
    
    $consistencyColor = if ($Report.Results.SyncfusionVersions.IsConsistent) { "Green" } else { "Red" }
    Write-Host "  Version Consistency: $(if ($Report.Results.SyncfusionVersions.IsConsistent) { "Valid" } else { "Invalid" })" -ForegroundColor $consistencyColor
    
    if ($Report.Recommendations.Count -gt 0) {
        Write-Host "`n💡 Recommendations:" -ForegroundColor Yellow
        foreach ($rec in $Report.Recommendations) {
            Write-Host "  • $rec" -ForegroundColor Yellow
        }
    }
    
} catch {
    Write-Error "Script execution failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "`n✅ Dependency validation completed" -ForegroundColor Green
