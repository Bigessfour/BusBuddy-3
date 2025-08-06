# 🔧 XML Standards for BusBuddy

## Core XML Principles

- **Well-Formed XML**: All XML must be well-formed with proper nesting and closing tags
- **XML Declaration**: Include XML declaration with version and encoding
- **Meaningful Element Names**: Use descriptive, clear element names
- **Attributes vs. Elements**: Use attributes for metadata, elements for data
- **Consistent Naming**: Use PascalCase for elements, camelCase for attributes
- **Namespace Usage**: Use XML namespaces for all WPF XAML files
- **Comment Documentation**: Include comprehensive comments for complex sections

## Tools and Validation

- **XMLSpy**: Advanced XML editing and validation
- **VS Code Extensions**: XML Tools, XML (Red Hat) for validation
- **PowerShell Validation**: Use `Test-BusBuddyXmlSyntax` script for batch validation
- **XML Linting**: Pre-commit validation with XML linting
- **Schema Validation**: Use XSD for schema validation where applicable

## XAML-Specific Standards

- **Resource Dictionary Organization**: Group similar resources in dedicated dictionaries
- **Namespace Declarations**: All XAML files must include proper namespaces
- **Syncfusion Integration**: Include Syncfusion namespace declarations consistently
- **WPF Binding Syntax**: Follow consistent pattern for bindings
- **Element Formatting**: One attribute per line for elements with multiple attributes
- **Style Definitions**: Use explicit keys for all styles
- **Resource Keys**: Use descriptive, hierarchical naming for resource keys

## XML File Organization

- **Indentation**: 2 or 4 spaces (consistent per file type)
- **Line Length**: Keep lines under 120 characters when practical
- **Element Ordering**: Consistent element ordering in similar files
- **Comments**: Use XML comments with meaningful descriptions

## Configuration Files

- **AppSettings**: Use hierarchical structure for app settings
- **Connection Strings**: Store connection strings in dedicated section
- **Environment Variables**: Use environment placeholders for sensitive values
- **Deployment Configuration**: Separate production vs development settings

## Common XML Patterns

```xml
<!-- Config File Pattern -->
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <!-- Application Settings -->
  <appSettings>
    <add key="Setting1" value="Value1" />
    <add key="Setting2" value="Value2" />
  </appSettings>
  
  <!-- Connection Strings -->
  <connectionStrings>
    <add name="DefaultConnection" 
         connectionString="Server=.;Database=BusBuddy;Trusted_Connection=True;" 
         providerName="System.Data.SqlClient" />
  </connectionStrings>
</configuration>
```

```xml
<!-- XAML Pattern -->
<Window x:Class="BusBuddy.WPF.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:BusBuddy.WPF"
        xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
        mc:Ignorable="d"
        Title="BusBuddy Transportation Management" 
        Height="600" 
        Width="800">
  
  <!-- Resource dictionaries -->
  <Window.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="/BusBuddy.WPF;component/Resources/FluentDarkTheme.xaml" />
        <ResourceDictionary Source="/BusBuddy.WPF;component/Resources/CommonStyles.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Window.Resources>
  
  <!-- Main content -->
  <Grid>
    <!-- Grid definitions with comments -->
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    
    <!-- Header content -->
    <StackPanel Grid.Row="0">
      <!-- Header elements -->
    </StackPanel>
    
    <!-- Main content area -->
    <syncfusion:SfDataGrid Grid.Row="1"
                           AutoGenerateColumns="False"
                           ItemsSource="{Binding GridData}">
      <!-- Column definitions -->
    </syncfusion:SfDataGrid>
    
    <!-- Footer status bar -->
    <StatusBar Grid.Row="2">
      <!-- Status items -->
    </StatusBar>
  </Grid>
</Window>
```

## Validation and Enforcement

- **Pre-commit Hooks**: Use Git hooks to validate XML before commit
- **Build Integration**: Include XML validation in build process
- **Documentation**: Include XML standards in developer onboarding
- **Schema Enforcement**: Use XSD schemas for validation where appropriate

## XML Security Considerations

- **Sensitive Data**: Never store passwords or keys in plain text
- **External Entities**: Disable XXE processing for security
- **Input Validation**: Always validate XML input from external sources
- **Error Handling**: Implement proper error handling for XML parsing
## 🔧 **XML Documentation Standards**

### **C# XML Documentation**
```xml
/// <summary>
/// Represents a school bus driver with associated information and qualifications.
/// </summary>
/// <remarks>
/// This class contains all necessary information for driver management including
/// licensing, certifications, and employment history.
/// </remarks>
public class Driver
{
    /// <summary>
    /// Gets or sets the unique identifier for the driver.
    /// </summary>
    /// <value>
    /// A positive integer that uniquely identifies the driver in the system.
    /// </value>
    public int DriverId { get; set; }

    /// <summary>
    /// Gets or sets the driver's full name.
    /// </summary>
    /// <value>
    /// The complete name of the driver, including first and last name.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the name is null or empty.
    /// </exception>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Validates the driver's license information.
    /// </summary>
    /// <param name="licenseNumber">The license number to validate.</param>
    /// <param name="expirationDate">The license expiration date.</param>
    /// <returns>
    /// <c>true</c> if the license is valid; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the license number format is invalid.
    /// </exception>
    public bool ValidateLicense(string licenseNumber, DateTime expirationDate)
    {
        // Implementation
    }
}
```

### **XML Documentation Tags**
- **`<summary>`**: Brief description of the element
- **`<remarks>`**: Additional detailed information
- **`<param>`**: Parameter description
- **`<returns>`**: Return value description
- **`<exception>`**: Exceptions that may be thrown
- **`<example>`**: Usage examples
- **`<see>`**: Cross-references to other elements
- **`<value>`**: Property value description

## 🛡️ **Security Standards**

### **Sensitive Data in XML**
```xml
<!-- ❌ NEVER DO THIS -->
<connectionStrings>
  <add name="Production" 
       connectionString="Server=prod;Database=BusBuddy;User=admin;Password=secret123;" />
</connectionStrings>

<!-- ✅ DO THIS INSTEAD -->
<connectionStrings>
  <add name="Production" 
       connectionString="Server=${DB_SERVER};Database=${DB_NAME};Integrated Security=true;" />
</connectionStrings>
```

### **Configuration Transformation**
```xml
<!-- appsettings.Production.config -->
<?xml version="1.0" encoding="utf-8"?>
<configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
  
  <connectionStrings>
    <add name="DefaultConnection" 
         connectionString="${AZURE_SQL_CONNECTION_STRING}"
         xdt:Transform="SetAttributes" 
         xdt:Locator="Match(name)" />
  </connectionStrings>
  
  <appSettings>
    <add key="Environment" value="Production" xdt:Transform="SetAttributes" xdt:Locator="Match(key)" />
  </appSettings>
  
</configuration>
```

## 📊 **Validation Standards**

### **Schema Validation**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <!-- Content with proper namespace -->
</Project>
```

### **Well-Formed XML Requirements**
- **Single Root Element**: Only one root element
- **Proper Nesting**: All elements properly nested
- **Attribute Quotes**: All attribute values quoted
- **Case Sensitivity**: Consistent element and attribute casing
- **End Tags**: All elements properly closed

## 🔍 **Formatting Standards**

### **Indentation Rules**
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Serilog" Version="4.0.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.7" />
  </ItemGroup>
</Project>
```

### **Attribute Formatting**
```xml
<!-- Short attributes on same line -->
<PackageReference Include="Serilog" Version="4.0.2" />

<!-- Long attributes on separate lines -->
<PackageReference 
    Include="Microsoft.EntityFrameworkCore.SqlServer" 
    Version="9.0.7" 
    PrivateAssets="all" />
```

## 📁 **File Organization**

### **Project Structure**
```
BusBuddy/
├── Directory.Build.props           # Global MSBuild properties
├── BusBuddy-Practical.ruleset     # Code analysis rules
├── BusBuddy.sln                   # Solution file
├── BusBuddy.Core/
│   ├── BusBuddy.Core.csproj       # Core project file
│   └── app.config                 # Core configuration
├── BusBuddy.WPF/
│   ├── BusBuddy.WPF.csproj        # WPF project file
│   └── app.config                 # WPF configuration
└── BusBuddy.Tests/
    └── BusBuddy.Tests.csproj      # Test project file
```

### **Naming Conventions**
- **Project Files**: `{ProjectName}.csproj`
- **Configuration**: `app.config`, `web.config`
- **Properties**: `Directory.Build.props`, `Directory.Build.targets`
- **Rules**: `{ProjectName}.ruleset`

## ✅ **Quality Checklist**

### **Before Committing XML Files**
- [ ] Valid XML syntax (well-formed)
- [ ] Consistent 2-space indentation
- [ ] Proper namespace declarations
- [ ] No sensitive data hardcoded
- [ ] Comments for complex configurations
- [ ] Schema validation passes
- [ ] Appropriate file encoding (UTF-8)

### **MSBuild Specific Checklist**
- [ ] SDK-style project format used
- [ ] Target framework explicitly specified
- [ ] Package versions pinned
- [ ] No unnecessary ItemGroups or PropertyGroups
- [ ] Project references use relative paths
- [ ] Documentation generation enabled

## 🛠️ **Tools and Integration**

### **Recommended Tools**
- **VS Code**: XML extension for validation and IntelliSense
- **Visual Studio**: Built-in MSBuild support
- **XMLSpy**: Advanced XML editing and validation
- **MSBuild**: Command-line build tool

### **Build Integration**
- **Schema Validation**: Automatic XML schema validation
- **Format Checking**: Automated formatting verification
- **Security Scanning**: Check for hardcoded secrets

---

**Document Version**: 1.0  
**Last Updated**: July 30, 2025  
**Applies To**: All XML files in BusBuddy project  
**Next Review**: Monthly standards review
