# 🚀 BusBuddy Global Coding Standards & Settings Hierarchy

## **Settings Hierarchy (Priority Order)**

### 1. **Primary: Custom Instructions** (Highest Priority)

**Location**: `.github/copilot-instructions.md`
**Purpose**: Project-specific guidance for GitHub Copilot
**Key Standards**:

- ✅ Phase 1: Functional over perfect
- ✅ Incremental fixes over rewrites
- ✅ .NET 9.0 + WPF + Syncfusion 30.2.6
- ✅ Serilog structured logging ONLY

# 🚀 BusBuddy Global Coding Standards & Settings Hierarchy

## **Settings Hierarchy (Priority Order)**

### 1. **Primary: Custom Instructions** (Highest Priority)

**Location**: `.github/copilot-instructions.md`
**Purpose**: Project-specific guidance for GitHub Copilot
**Key Standards**:

- ✅ Phase 1: Functional over perfect
- ✅ Incremental fixes over rewrites
- ✅ .NET 9.0 + WPF + Syncfusion 30.2.6
- ✅ Serilog structured logging ONLY

### 2. **Official Microsoft C# 12.0 Standards** ⭐ **ENFORCED**

**Source**: [Microsoft Learn C# 12.0 Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)
**Purpose**: Official C# 12.0 language features and best practices
**Key Standards**:

- ✅ **Primary Constructors**: `public class Person(string firstName, string lastName)`
- ✅ **Collection Expressions**: `int[] row = [1, 2, 3, 4, 5]` and spread operator `..`
- ✅ **Ref Readonly Parameters**: `ref readonly` for large struct parameters
- ✅ **Default Lambda Parameters**: `var addOne = (int x = 1) => x + 1`
- ✅ **Using Aliases for Any Type**: `using Point = (int x, int y)`
- ✅ **Inline Arrays**: For high-performance scenarios with `[System.Runtime.CompilerServices.InlineArray]`
- ✅ **Experimental Attribute**: `[Experimental]` for preview features
- ✅ **Nullable Reference Types**: Enabled throughout project

### 3. **Official Microsoft XAML/WPF Standards** ⭐ **ENFORCED**

**Source**: [Microsoft Learn WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
**Purpose**: Official WPF/XAML patterns and best practices
**Key Standards**:

- ✅ **Data Binding**: `OneWay`, `TwoWay`, `OneWayToSource`, `OneTime` modes
- ✅ **DataContext**: Establish clear hierarchy with inheritance
- ✅ **UpdateSourceTrigger**: `PropertyChanged` for immediate, `LostFocus` for text
- ✅ **Validation**: `ValidationRules` with `ExceptionValidationRule`, `DataErrorValidationRule`
- ✅ **Converters**: `IValueConverter` for type conversions, `IMultiValueConverter` for complex
- ✅ **Case Sensitivity**: XAML is case-sensitive, follow exact casing
- ✅ **Attribute vs Property Element**: Use attribute syntax when possible
- ✅ **Naming**: Use `x:Name` for identification, prefer framework `Name` when available
- ✅ **Content Properties**: Leverage to reduce markup verbosity
- ✅ **Attached Properties**: `OwnerType.PropertyName` syntax (e.g., `DockPanel.Dock`)
- ✅ **Markup Extensions**: `{Binding}`, `{StaticResource}`, `{DynamicResource}` syntax
- ✅ **Collections**: Omit explicit collection tags when parser can infer

### 4. **PowerShell 7.5 Standards** ⭐ **VALIDATED**

**Location**: `.powershell-profile-75-standards.ps1` + `PSScriptAnalyzerSettings.psd1`
**Purpose**: PowerShell 7.5 modern syntax and best practices
**Key Standards**:

- ✅ Modern operators: `??`, `&&`, `||`, ternary `?:`
- ✅ ForEach-Object -Parallel for performance
- ✅ $"string {interpolation}" syntax
- ✅ ErrorAction Stop as default
- ✅ ProgressAction SilentlyContinue

### 5. **VS Code Integration**

**Location**: `.vscode/settings.json` + `tasks.json`
**Purpose**: Development environment consistency
**Key Standards**:

- ✅ PowerShell 7.5.2 as default terminal
- ✅ Task Explorer for build/run operations
- ✅ Enhanced task monitoring with logging

## **Official Microsoft Standards Compliance**

### ✅ **C# 12.0 COMPLIANCE STATUS**:

- **Primary Constructors**: Ready for implementation in new classes
- **Collection Expressions**: Can replace array/list initialization syntax
- **Ref Readonly Parameters**: Available for high-performance scenarios
- **Default Lambda Parameters**: Simplifies functional programming patterns
- **Using Aliases**: Enables type aliasing for complex generic types
- **Inline Arrays**: For performance-critical array operations
- **Experimental Features**: Marked with `[Experimental]` attribute

### ✅ **XAML/WPF COMPLIANCE STATUS**:

- **Data Binding**: All binding modes and patterns validated
- **Validation**: Built-in and custom validation rules supported
- **Converters**: Type conversion patterns established
- **Syntax**: Case sensitivity and markup extension rules enforced
- **Performance**: Collection inference and content property optimization
- **Architecture**: MVVM patterns with proper data context management

### ✅ **ALIGNED with PS 7.5 Standards**:

- Modern operator support (`??`, `&&`, `||`)
- Enhanced error handling patterns
- Performance optimizations (parallel processing)
- String interpolation capabilities
- Compatible syntax validation

### 🔧 **ENHANCED for BusBuddy**:

- Allows Write-Host for user feedback (utility scripts)
- Allows global variables for configuration
- Practical over perfectionist rules
- Structured logging integration

## **Validation Commands**

```powershell
# Test PowerShell 7.5 compliance
. .\.powershell-profile-75-standards.ps1
Test-PowerShell75Standards -ScriptPath "your-script.ps1"

# Run PSScriptAnalyzer with BusBuddy settings
Invoke-ScriptAnalyzer -Path "your-script.ps1" -Settings "PSScriptAnalyzerSettings.psd1"

# Validate .NET/C# 12.0 code standards
dotnet build BusBuddy.sln --verbosity normal

# Test XAML validation (via build process)
dotnet build BusBuddy.WPF/BusBuddy.WPF.csproj --verbosity normal
```

## **References**

- **C# 12.0 Official**: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12
- **WPF Data Binding**: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/
- **XAML Overview**: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/xaml/
- **PowerShell 7.5**: https://learn.microsoft.com/en-us/powershell/scripting/whats-new/what-s-new-in-powershell-75
- **PSScriptAnalyzer**: https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/using-scriptanalyzer
- **EditorConfig**: https://editorconfig.org/
- **MSBuild Directory.Build.props**: https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build

## **🗂️ Extended Standards Organization**

**Comprehensive Language Standards**: See `Standards/` directory for detailed specifications:

- **📋 LANGUAGE-INVENTORY.md**: Complete technology inventory (17 languages/technologies)
- **📚 Standards/Languages/**: Individual language standards with official documentation
- **⚙️ Standards/Configurations/**: Configuration file standards and patterns
- **🛠️ Standards/Tools/**: Development tool configuration and best practices
- **📖 Standards/MASTER-STANDARDS.md**: Directory structure and integration guide

**Key Extended Standards Available**:

- 📋 **JSON Standards**: RFC 8259 compliance, security patterns, validation
- 🏗️ **XML Standards**: W3C XML 1.0, MSBuild patterns, project structure
- 🌊 **YAML Standards**: YAML 1.2.2, GitHub Actions CI/CD patterns
- 🗃️ **SQL Standards**: T-SQL patterns, Entity Framework conventions (pending)
- 📝 **Documentation Standards**: Markdown, comments, naming conventions (pending)

---

**Last Updated**: July 25, 2025
**PowerShell Version**: 7.5.2
**Target Framework**: .NET 8.0
