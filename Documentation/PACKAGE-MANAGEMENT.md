# 📦 BusBuddy Package Management Guide

> **Comprehensive guide for NuGet package management, dependency scanning, and version control in the BusBuddy WPF application.**

## 📋 Table of Contents

- [Package Sources & Configuration](#package-sources--configuration)
- [Dependency Management](#dependency-management)
- [Syncfusion Package Management](#syncfusion-package-management)
- [Version Pinning Strategy](#version-pinning-strategy)
- [Security & Vulnerability Scanning](#security--vulnerability-scanning)
- [Automated Tasks](#automated-tasks)
- [Troubleshooting](#troubleshooting)

## 🔧 Package Sources & Configuration

### NuGet.config Overview

BusBuddy uses a centralized `NuGet.config` at the repository root to ensure consistent package restoration across all development environments.

**Key Features:**
- **Single Source**: Only uses `nuget.org` (no private feeds required)
- **Automatic Restore**: Packages restore automatically during build
- **No Local Packages**: Uses global packages folder (repository remains lightweight)
- **Syncfusion Compatible**: Follows [official Syncfusion guidelines](https://help.syncfusion.com/windowsforms/installation/install-nuget-packages)

### Configuration Details

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- All packages including Syncfusion available on nuget.org -->
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>

  <packageRestore>
    <add key="enabled" value="True" />
    <add key="automatic" value="True" />
  </packageRestore>

  <config>
    <add key="dependencyVersion" value="Highest" />
    <add key="signatureValidationMode" value="accept" />
  </config>
</configuration>
```

## 📊 Dependency Management

### Current Package Strategy

1. **Centralized Version Management**: `Directory.Build.props` defines all package versions
2. **Exact Version Pinning**: All packages use exact versions (no floating versions)
3. **Consistent Across Projects**: Both Core and WPF projects use identical versions

### Package Restoration Process

```bash
# Standard restoration (recommended)
dotnet restore

# Force clean restoration
dotnet restore --force --no-cache

# Verbose restoration for troubleshooting
dotnet restore --verbosity detailed
```

### Core Dependencies Overview

| Package Category | Primary Packages | Version Management |
|-----------------|------------------|-------------------|
| **UI Framework** | Syncfusion.WPF.* | Centralized in Directory.Build.props |
| **Data Access** | Microsoft.EntityFrameworkCore.* | Exact version pinning |
| **Logging** | Serilog.* | Compatible version ranges |
| **Testing** | Microsoft.NET.Test.Sdk | Latest stable versions |

## 🎨 Syncfusion Package Management

### Official Documentation Reference

Following [Syncfusion WPF Installation Guide](https://help.syncfusion.com/windowsforms/installation/install-nuget-packages):

### Current Syncfusion Configuration

- **Version**: `30.2.5` (pinned in Directory.Build.props)
- **Source**: `nuget.org` (official public feed)
- **License**: Community/Commercial (configured via environment variable)
- **Controls Used**: DockingManager, NavigationDrawer, SfDataGrid, Charts

### Syncfusion Package References

```xml
<!-- Centralized in Directory.Build.props -->
<PropertyGroup>
  <SyncfusionVersion>30.2.5</SyncfusionVersion>
</PropertyGroup>

<!-- Individual project references -->
<PackageReference Include="Syncfusion.SfChart.WPF" Version="$(SyncfusionVersion)" />
<PackageReference Include="Syncfusion.SfDataGrid.WPF" Version="$(SyncfusionVersion)" />
<PackageReference Include="Syncfusion.Tools.WPF" Version="$(SyncfusionVersion)" />
```

### Syncfusion License Management

```csharp
// App.xaml.cs - License registration before UI initialization
public partial class App : Application
{
    public App()
    {
        // Register Syncfusion license from environment variable
        var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrEmpty(licenseKey))
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
        }
    }
}
```

## 📌 Version Pinning Strategy

### Why Exact Version Pinning?

1. **Reproducible Builds**: Ensures identical packages across environments
2. **Security**: Prevents automatic updates to potentially vulnerable versions
3. **Stability**: Avoids breaking changes from minor version updates
4. **Team Consistency**: All developers use identical dependency versions

### Implementation Pattern

```xml
<!-- ✅ CORRECT: Exact version pinning -->
<PackageReference Include="Syncfusion.SfChart.WPF" Version="30.2.5" />

<!-- ❌ AVOID: Floating versions -->
<PackageReference Include="Syncfusion.SfChart.WPF" Version="30.*" />
<PackageReference Include="Syncfusion.SfChart.WPF" Version="[30.2.5,)" />
```

### Centralized Version Management

**Directory.Build.props Example:**
```xml
<Project>
  <PropertyGroup>
    <!-- Core Framework Versions -->
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>

    <!-- Package Versions -->
  <SyncfusionVersion>30.2.5</SyncfusionVersion>
    <EntityFrameworkVersion>8.0.0</EntityFrameworkVersion>
    <SerilogVersion>4.0.2</SerilogVersion>
  </PropertyGroup>
</Project>
```

## 🔒 Security & Vulnerability Scanning

### Automated Vulnerability Detection

BusBuddy includes automated scripts for regular security scanning:

```powershell
# PowerShell script for vulnerability scanning
.\dependency-management.ps1 -ScanVulnerabilities

# Direct dotnet command
dotnet list package --vulnerable --include-transitive
```

### Security Scanning Features

1. **Known Vulnerabilities**: Scans all packages for CVE entries
2. **Transitive Dependencies**: Includes indirect package vulnerabilities
3. **Severity Classification**: Categorizes vulnerabilities by risk level
4. **Automated Reporting**: Generates security reports for CI/CD

### Current Security Status

- **Last Scan**: Clean (no known vulnerabilities)
- **Syncfusion 30.2.5**: No security advisories
- **Entity Framework**: Latest stable versions
- **Regular Scanning**: Integrated into CI/CD pipeline

## 🤖 Automated Tasks

### VS Code Tasks Integration

BusBuddy includes dedicated VS Code tasks for package management:

```json
{
  "label": "🛡️ BB: Dependency Security Scan",
  "type": "shell",
  "command": "pwsh.exe",
  "args": [
    "-ExecutionPolicy", "Bypass",
    "-File", "${workspaceFolder}\\dependency-management.ps1",
    "-ScanVulnerabilities"
  ],
  "group": "test"
}
```

### Available Automated Tasks

1. **🛡️ Dependency Security Scan**: Check for vulnerabilities
2. **📊 Full Dependency Analysis**: Complete dependency report
3. **📌 Validate Version Pinning**: Ensure all versions are exact
4. **📦 Restore Packages**: Clean package restoration

### PowerShell Automation Scripts

**dependency-management.ps1** provides comprehensive automation:

```powershell
# Run security scan
.\dependency-management.ps1 -ScanVulnerabilities

# Full analysis with reporting
.\dependency-management.ps1 -Full

# Validate version consistency
.\dependency-management.ps1 -ValidateVersions
```

## 🚨 Troubleshooting

### Common Package Issues

#### 1. Package Restore Failures

**Symptoms:**
- Build errors about missing packages
- "Package not found" errors

**Solutions:**
```bash
# Clear NuGet caches
dotnet nuget locals all --clear

# Force restore with verbose output
dotnet restore --force --no-cache --verbosity detailed

# Check package sources
dotnet nuget list source
```

#### 2. Syncfusion License Issues

**Symptoms:**
- Syncfusion trial messages in application
- License validation errors

**Solutions:**
```bash
# Set environment variable (Windows)
setx SYNCFUSION_LICENSE_KEY "your_license_key_here"

# Verify license registration in App.xaml.cs
# Ensure license is set before any Syncfusion control instantiation
```

#### 3. Version Conflicts

**Symptoms:**
- Assembly binding errors
- Conflicting package versions

**Solutions:**
```bash
# Check package versions across projects
.\dependency-management.ps1 -ValidateVersions

# Review and update Directory.Build.props
# Ensure all projects use consistent versions
```

#### 4. Network/Proxy Issues

**Symptoms:**
- Package download timeouts
- Connection errors to nuget.org

**Solutions:**
```bash
# Configure proxy in NuGet.config if needed
# Check corporate firewall settings
# Verify DNS resolution for api.nuget.org
```

### Debug Commands

```bash
# Check current package references
dotnet list package

# Check for outdated packages
dotnet list package --outdated

# Check package sources
dotnet nuget list source

# Restore with maximum verbosity
dotnet restore --verbosity diagnostic
```

## 📈 Best Practices Summary

### ✅ Do's

1. **Use exact version pinning** for all packages
2. **Centralize version management** in Directory.Build.props
3. **Run regular vulnerability scans**
4. **Keep Syncfusion versions consistent** across all projects
5. **Use environment variables** for sensitive configuration
6. **Test package restoration** in clean environments

### ❌ Don'ts

1. **Don't use floating versions** (*, [1.0,), etc.)
2. **Don't commit packages folder** to source control
3. **Don't mix PackageReference and packages.config**
4. **Don't hardcode license keys** in source code
5. **Don't ignore security vulnerabilities**
6. **Don't skip dependency validation** in CI/CD

## 🔗 Related Documentation

- [Syncfusion WPF Installation Guide](https://help.syncfusion.com/windowsforms/installation/install-nuget-packages)
- [Microsoft PackageReference Documentation](https://docs.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
- [NuGet.config Reference](https://docs.microsoft.com/en-us/nuget/reference/nuget-config-file)
- [Dependency Scanning Best Practices](https://docs.github.com/en/code-security/supply-chain-security)

---

**Last Updated**: July 25, 2025
**Maintainer**: BusBuddy Development Team
**Review Cycle**: Monthly security scans, quarterly dependency updates
