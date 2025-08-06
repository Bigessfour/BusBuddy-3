# 🏗️ XML Standards for BusBuddy

## **Official XML Standards**
- **Core Specification**: [W3C XML 1.0 (Fifth Edition)](https://www.w3.org/TR/xml/)
- **Schema Specification**: [W3C XML Schema 1.1](https://www.w3.org/TR/xmlschema11-1/)
- **Namespaces**: [W3C XML Namespaces 1.0](https://www.w3.org/TR/xml-names/)
- **MSBuild Schema**: [MSBuild Reference](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reference)

## **XML Usage in BusBuddy**

### **MSBuild Files**
- `*.csproj` - Project files
- `Directory.Build.props` - Global build properties
- `*.targets` - Build target definitions
- `*.ruleset` - Code analysis rules

### **Configuration Files**
- `app.config` - Application configuration
- `testsettings.runsettings.xml` - Test execution settings

### **Resource Files**
- `*.resx` - Localization resources (if used)

## **XML Standards Enforcement**

### ✅ **Required XML Standards**

#### **1. Basic Syntax (W3C XML 1.0)**
```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- Comments use double-dash syntax -->
<RootElement>
  <ChildElement attribute="value">
    <GrandChildElement>Text content</GrandChildElement>
  </ChildElement>
</RootElement>
```

#### **2. MSBuild Project Standards**
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Property Groups: Logical organization -->
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <!-- Item Groups: Dependencies and references -->
  <ItemGroup>
    <PackageReference Include="Syncfusion.SfDataGrid.WPF" Version="$(SyncfusionVersion)" />
  </ItemGroup>

</Project>
```

#### **3. Naming Conventions**
- **Elements**: Use `PascalCase` for element names
- **Attributes**: Use `camelCase` for attribute names (MSBuild exception: PascalCase)
- **Properties**: Use descriptive, hierarchical names
- **Namespaces**: Use meaningful namespace prefixes

#### **4. Indentation and Formatting**
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <OutputType>WinExe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Serilog" Version="4.0.2" />
  </ItemGroup>
</Project>
```

#### **5. XML Comments**
```xml
<!-- 🚌 BusBuddy Core Configuration -->
<!-- This section defines the core project properties -->
<PropertyGroup Label="Core Configuration">
  <!-- Target the latest .NET 8.0 LTS framework -->
  <TargetFramework>net9.0-windows</TargetFramework>
</PropertyGroup>
```

### ✅ **MSBuild-Specific Standards**

#### **1. Property Organization**
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- 🏗️ CORE PROJECT CONFIGURATION -->
  <PropertyGroup Label="Framework and Language">
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- 📦 VERSION MANAGEMENT -->
  <PropertyGroup Label="Version Control">
    <SyncfusionVersion>30.1.40</SyncfusionVersion>
    <EntityFrameworkVersion>9.0.7</EntityFrameworkVersion>
  </PropertyGroup>

  <!-- 🔧 BUILD CONFIGURATION -->
  <PropertyGroup Label="Build Settings">
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
  </PropertyGroup>

</Project>
```

#### **2. Package Reference Standards**
```xml
<ItemGroup Label="Core Framework">
  <!-- Use version variables for consistency -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="$(EntityFrameworkVersion)" />
  <PackageReference Include="Syncfusion.SfDataGrid.WPF" Version="$(SyncfusionVersion)" />
</ItemGroup>

<ItemGroup Label="Logging and Utilities">
  <!-- Explicit version numbers for stable dependencies -->
  <PackageReference Include="Serilog" Version="4.0.2" />
  <PackageReference Include="AutoMapper" Version="12.0.1" />
</ItemGroup>
```

#### **3. Conditional Properties**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <DefineConstants>DEBUG;TRACE</DefineConstants>
  <DebugType>full</DebugType>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>TRACE</DefineConstants>
  <DebugType>portable</DebugType>
  <Optimize>true</Optimize>
</PropertyGroup>
```

### ✅ **Configuration File Standards**

#### **1. App.config Structure**
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>

  <appSettings>
    <add key="Environment" value="Development" />
    <add key="LogLevel" value="Information" />
  </appSettings>

  <connectionStrings>
    <add name="DefaultConnection"
         connectionString="Server=localhost;Database=BusBuddy;Trusted_Connection=true;"
         providerName="System.Data.SqlClient" />
  </connectionStrings>

</configuration>
```

#### **2. Test Configuration**
```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>

  <!-- Test execution configuration -->
  <RunConfiguration>
    <MaxCpuCount>0</MaxCpuCount>
    <ResultsDirectory>.\TestResults</ResultsDirectory>
  </RunConfiguration>

  <!-- Data collection configuration -->
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="Code Coverage" uri="datacollector://Microsoft/CodeCoverage/2.0">
        <Configuration>
          <CodeCoverage>
            <ModulePaths>
              <Include>
                <ModulePath>.*BusBuddy.*\.dll$</ModulePath>
              </Include>
            </ModulePaths>
          </CodeCoverage>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>

</RunSettings>
```

### ✅ **Security and Performance Standards**

#### **1. Security Considerations**
- **No Hardcoded Secrets**: Use environment variables or secure configuration
- **Input Validation**: Validate all XML input using XSD schemas
- **Entity Resolution**: Disable external entity resolution to prevent XXE attacks

#### **2. Performance Guidelines**
- **File Size**: Keep project files under 500KB
- **Conditional Logic**: Minimize complex conditions in MSBuild files
- **Import Optimization**: Use targeted imports, avoid wildcards

#### **3. Schema Validation**
```xml
<!-- Always declare encoding -->
<?xml version="1.0" encoding="utf-8"?>

<!-- Use appropriate schema declarations -->
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <!-- Project content -->
</Project>
```

## **BusBuddy-Specific XML Patterns**

### **Project Structure Template**
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- 🚌 BusBuddy [PROJECT_NAME] - Greenfield Foundation -->
  <PropertyGroup Label="Project Identity">
    <ProjectName>BusBuddy.[ProjectName]</ProjectName>
    <Description>BusBuddy [component description]</Description>
  </PropertyGroup>

  <!-- 🏗️ FRAMEWORK CONFIGURATION -->
  <PropertyGroup Label="Framework Settings">
<<<<<<< HEAD
    <TargetFramework>net8.0-windows</TargetFramework>
=======
    <TargetFramework>net9.0-windows</TargetFramework>
>>>>>>> df2d18d (chore: stage and commit all changes after migration to BusBuddy-3 repo (CRLF to LF warnings acknowledged))
    <UseWPF Condition="'$(IsWPFProject)' == 'true'">true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <!-- 📦 DEPENDENCIES -->
  <ItemGroup Label="Framework Dependencies">
    <!-- Core dependencies here -->
  </ItemGroup>

</Project>
```

## **Validation Commands**

```powershell
# Validate XML syntax
[xml]$xml = Get-Content "file.xml"
$xml -ne $null  # Returns true if valid

# Format XML files
$xml = [xml](Get-Content "file.xml")
$xml.Save("formatted-file.xml")

# Validate MSBuild files
dotnet build --verbosity normal  # Will catch MSBuild XML errors

# XML Schema validation (if schema available)
$schema = [xml](Get-Content "schema.xsd")
$xml.Schemas.Add($null, "schema.xsd")
$xml.Validate($null)
```

## **Tools and Extensions**

### **VS Code Extensions**
- **XML Language Support**: Built-in XML support
- **MSBuild Project Tools**: MSBuild IntelliSense
- **XML Tools**: XML formatting and validation

### **.NET Tools**
- **System.Xml**: Built-in XML processing
- **XDocument/XElement**: LINQ to XML
- **MSBuild API**: Programmatic MSBuild file manipulation

## **Common Pitfalls and Solutions**

### ❌ **Common Mistakes**
```xml
<!-- DON'T: Inconsistent casing -->
<packageReference include="Library" version="1.0.0" />

<!-- DON'T: Missing namespace declarations -->
<Project>
  <PropertyGroup>
    <targetFramework>net9.0</targetFramework>
  </PropertyGroup>
</Project>

<!-- DON'T: Hardcoded paths -->
<Reference Include="C:\absolute\path\to\library.dll" />
```

### ✅ **Correct Patterns**
```xml
<!-- DO: Consistent PascalCase for MSBuild -->
<PackageReference Include="Library" Version="1.0.0" />

<!-- DO: Proper MSBuild SDK -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>

<!-- DO: Relative or variable paths -->
<Reference Include="$(MSBuildProjectDirectory)\lib\library.dll" />
```

## **References**
- **W3C XML 1.0**: [XML Core Specification](https://www.w3.org/TR/xml/)
- **XML Schema**: [W3C XML Schema](https://www.w3.org/TR/xmlschema11-1/)
- **MSBuild Reference**: [Microsoft MSBuild Documentation](https://learn.microsoft.com/en-us/visualstudio/msbuild/)
- **XML Security**: [OWASP XML Security](https://cheatsheetseries.owasp.org/cheatsheets/XML_Security_Cheat_Sheet.html)

---
**Last Updated**: July 25, 2025
**XML Version**: 1.0 (Fifth Edition)
**MSBuild Version**: 17.11.31
