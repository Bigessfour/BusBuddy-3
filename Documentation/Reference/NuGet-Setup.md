# 📦 NuGet Setup Reference - BusBuddy Package Management

**Purpose**: Centralized NuGet configuration ensuring consistent package restoration across development environments.

**Source**: `NuGet.config` - Solution-level NuGet configuration for reliable dependency management.

## 🎯 Package Sources Configuration

### Primary Source

```xml
<packageSources>
  <clear />
  <!-- Single, reliable source for all packages -->
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
</packageSources>
```

**Key Benefits**:

- **Syncfusion packages**: Available directly from NuGet.org (no separate feed needed)
- **Official Microsoft packages**: Entity Framework, WPF, testing frameworks
- **Community packages**: AutoMapper, FluentAssertions, etc.
- **Simplified setup**: Single source reduces configuration complexity

**Copilot Context**: All package references in BusBuddy resolve from the official NuGet gallery.

## ⚙️ Package Restore Settings

### Automatic Restoration

```xml
<packageRestore>
  <add key="enabled" value="True" />
  <add key="automatic" value="True" />
</packageRestore>
```

**Benefits**:

- **Zero-touch setup**: Packages restore automatically on build
- **CI/CD friendly**: Works seamlessly in GitHub Actions
- **Developer experience**: No manual package restore commands needed

## 🔗 Binding Redirects

### Assembly Conflict Resolution

```xml
<bindingRedirects>
  <add key="skip" value="False" />
</bindingRedirects>
```

**Purpose**: Automatically generates binding redirects for version conflicts, especially important for Syncfusion and WPF assemblies.

## 🎛️ Package Management

### Modern PackageReference Format

```xml
<packageManagement>
  <add key="format" value="1" />
  <add key="disabled" value="False" />
</packageManagement>
```

**Modern Standards**:

- **PackageReference**: Modern package management (not packages.config)
- **Transitive dependencies**: Automatic resolution
- **Central version management**: Via Directory.Build.props

## 🔧 Advanced Configuration

### Dependency Behavior

```xml
<config>
  <add key="dependencyVersion" value="Highest" />
  <add key="signatureValidationMode" value="accept" />
  <add key="defaultPushSource" value="https://api.nuget.org/v3/index.json" />
</config>
```

**Settings Explained**:

- **dependencyVersion**: Always gets latest compatible versions
- **signatureValidationMode**: Accepts packages for development speed
- **defaultPushSource**: Standard NuGet.org for any publishing

## 💡 Copilot Usage Examples

### Adding Syncfusion Packages

```xml
<!-- Copilot Prompt: "Add Syncfusion chart control with centralized versioning" -->
<PackageReference Include="Syncfusion.SfChart.WPF" Version="$(SyncfusionVersion)" />
```

### Entity Framework Packages

```xml
<!-- Copilot Prompt: "Add EF Core with SQL Server support" -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="$(EntityFrameworkVersion)" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="$(EntityFrameworkVersion)" />
```

### Testing Packages

```xml
<!-- Copilot Prompt: "Add NUnit testing framework with FluentAssertions" -->
<PackageReference Include="NUnit" Version="$(NUnitVersion)" />
<PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
```

## 🚀 BusBuddy-Specific Benefits

### Syncfusion Integration

- **No custom feeds**: All Syncfusion packages available from NuGet.org
- **Version consistency**: Managed centrally in Directory.Build.props
- **Licensing**: Handled via environment variables, not package sources

### Development Workflow

- **Fresh clone**: `git clone` + `dotnet restore` = ready to build
- **Clean builds**: `dotnet clean && dotnet restore --force`
- **CI/CD ready**: Works in GitHub Actions without additional configuration

## 🔍 Package Source Strategy

### Why Single Source?

```xml
<!-- Simplified, reliable package resolution -->
<clear />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
```

**Advantages**:

- **Reliability**: Official Microsoft-managed infrastructure
- **Speed**: Global CDN for fast package downloads
- **Security**: Signed packages with malware scanning
- **Compatibility**: All BusBuddy dependencies available

**Avoids Common Issues**:

- ❌ Custom feed authentication problems
- ❌ Package source ordering conflicts
- ❌ Network proxy complications
- ❌ Mirror synchronization delays

## 🛠️ Troubleshooting Commands

### Clear and Restore

```powershell
# Clear all NuGet caches
dotnet nuget locals all --clear

# Force restore with clean slate
dotnet restore --force --no-cache

# Verify package sources
dotnet nuget list source
```

### Validate Configuration

```powershell
# Check NuGet configuration
bb-health --check-nuget

# Validate package restoration
bb-build --clean

# Test Syncfusion package availability
bb-validate-syncfusion
```

## 🔄 Integration with Build System

### Directory.Build.props Coordination

```xml
<!-- Central version management -->
<SyncfusionVersion>30.1.42</SyncfusionVersion>
<EntityFrameworkVersion>9.0.7</EntityFrameworkVersion>

<!-- Referenced in project files -->
<PackageReference Include="Syncfusion.Tools.WPF" Version="$(SyncfusionVersion)" />
```

### Global.json Compatibility

```json
{
  "sdk": {
    "version": "9.0.303",
    "rollForward": "latestMinor"
  }
}
```

## 📋 Package Categories in BusBuddy

### Core Framework

- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.AspNetCore.Components
- Microsoft.Extensions.Configuration

### UI Framework

- Syncfusion.Tools.WPF
- Syncfusion.Themes.FluentDark.WPF
- Microsoft.Toolkit.Mvvm

### Testing Framework

- NUnit
- FluentAssertions
- Moq
- Coverlet.collector

### External APIs

- System.Net.Http.Json
- Azure.Identity
- Polly

---

_Streamlined package management for consistent BusBuddy development_ 🚀
