# 🏗️ Build Configurations Reference - BusBuddy Foundation

**Purpose**: Centralized build configuration for consistent development across the BusBuddy solution.

**Source**: `Directory.Build.props` - MSBuild properties applied to all projects in the solution.

## 🎯 Core Framework Configuration

### Target Framework & Language
```xml
<!-- Modern .NET Foundation -->
<TargetFramework>net9.0-windows</TargetFramework>
<LangVersion>12</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<UseWPF>true</UseWPF>
```

**Copilot Context**: Modern C# 12 features, nullable reference types, WPF-specific patterns.

## 📦 Package Version Standards

### Core Technology Stack
| Package | Version | Purpose | Copilot Benefit |
|---------|---------|---------|-----------------|
| **Syncfusion WPF** | `30.2.4` | Professional UI controls | Rich XAML control completions |
| **Entity Framework Core** | `9.0.7` | Data access layer | Modern EF Core patterns |
| **Serilog** | `4.3.0` | Pure logging (no Microsoft.Extensions) | Structured logging patterns |

### MVVM & UI Support
```xml
<!-- MVVM and UI Enhancement Packages -->
<CommunityToolkitMvvmVersion>8.3.2</CommunityToolkitMvvmVersion>
<AutoMapperVersion>12.0.1</AutoMapperVersion>
<WebView2Version>1.0.2792.45</WebView2Version>
<XamlBehaviorsVersion>1.1.135</XamlBehaviorsVersion>
```

### Testing Framework
```xml
<!-- Comprehensive Testing Stack -->
<NUnitVersion>4.3.1</NUnitVersion>
<TestSdkVersion>17.12.0</TestSdkVersion>
<FluentAssertionsVersion>6.12.2</FluentAssertionsVersion>
<MoqVersion>4.20.72</MoqVersion>
<CoverletVersion>6.0.2</CoverletVersion>
```

## 🔍 Code Analysis Configuration

### Quality Standards
```xml
<!-- Industry Standard Code Quality -->
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisMode>Recommended</AnalysisMode>
<CodeAnalysisRuleSet>$(MSBuildThisFileDirectory)BusBuddy-Practical.ruleset</CodeAnalysisRuleSet>
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

### MVP-Friendly Suppressions
```xml
<!-- Practical suppressions for rapid development -->
<NoWarn>$(NoWarn);CA1305;CA1860;CA1848;CA1851;CA1304</NoWarn>
<!-- Nullable warnings suppressed for Phase 1 MVP -->
<NoWarn>$(NoWarn);CS8600;CS8601;CS8602;CS8603;CS8604</NoWarn>
```

**Copilot Context**: Follows Microsoft recommended analysis with practical adjustments for MVP development.

## ⚡ Performance Optimization

### Build Performance
```xml
<!-- Optimal Build Configuration -->
<UseSharedCompilation>true</UseSharedCompilation>
<BuildInParallel>true</BuildInParallel>
<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
<DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
```

### Test Project Optimization
```xml
<!-- Test-specific performance settings -->
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <UseSharedCompilation>false</UseSharedCompilation>
  <BuildInParallel>false</BuildInParallel>
  <GenerateDocumentationFile>false</GenerateDocumentationFile>
</PropertyGroup>
```

## 🌐 Globalization Support

### International Transportation
```xml
<!-- Global transportation system support -->
<InvariantGlobalization>false</InvariantGlobalization>
<SatelliteResourceLanguages>en-US</SatelliteResourceLanguages>
```

**Copilot Context**: Prepared for international school transportation management.

## 💡 Copilot Usage Examples

### Creating New Projects
```csharp
// Copilot Prompt: "Create new WPF project following BusBuddy standards"
// Result: Inherits all Directory.Build.props settings automatically
```

### Package References
```xml
<!-- Copilot Prompt: "Add Syncfusion chart control with version management" -->
<PackageReference Include="Syncfusion.SfChart.WPF" Version="$(SyncfusionVersion)" />
```

### Entity Framework Usage
```csharp
// Copilot Prompt: "Create EF Core DbContext following BusBuddy patterns"
// Result: Uses EntityFrameworkVersion for consistent package references
```

### Serilog Implementation
```csharp
// Copilot Prompt: "Implement Serilog structured logging"
// Result: Pure Serilog without Microsoft.Extensions.Logging conflicts
```

## 🔧 Project-Specific Overrides

### WPF Projects
```xml
<!-- Automatically applied to WPF projects -->
<UseWPF>true</UseWPF>
<OutputType>WinExe</OutputType>
<GenerateAssemblyInfo>true</GenerateAssemblyInfo>
```

### Core/Library Projects
```xml
<!-- Automatically applied to library projects -->
<OutputType>Library</OutputType>
<IsPackable>false</IsPackable>
```

### Test Projects
```xml
<!-- Automatically detected and configured -->
<IsTestProject>true</IsTestProject>
<IsPackable>false</IsPackable>
```

## 🚀 Advanced Features

### MSBuild Optimization
```xml
<!-- Prevents MSB4181 warnings -->
<MSBuildAllProjects Condition="'$(MSBuildAllProjects)' == ''">$(MSBuildThisFileFullPath)</MSBuildAllProjects>
<EnableDefaultItems>true</EnableDefaultItems>
```

### .NET 9 Specific Features
```xml
<!-- Latest C# and .NET features -->
<LangVersion>12</LangVersion>
<TargetFramework>net9.0-windows</TargetFramework>
```

## 🔄 Maintenance Commands

### Validate Configuration
```powershell
# Check build configuration
bb-health --check-build

# Validate package versions
dotnet list package --outdated

# Analyze code quality
dotnet build --verbosity minimal
```

### Update Packages
```powershell
# Update central package versions
# Edit Directory.Build.props versions
# Run solution-wide restore
dotnet restore --force
```

---
*Standardized for BusBuddy MVP with Microsoft best practices* 🚀
