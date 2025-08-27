# BusBuddy Project Structure Index

## 📋 **Microsoft-Compliant File Organization**

This document outlines the Microsoft-recommended file structure for **WPF .NET 9** applications with **Model Context Protocol (MCP)** integration, following official Microsoft best practices.

---

## 🏗️ **Root Directory Structure**

```
📁 BusBuddy/
├── 📄 README.md                    # Main project documentation
├── 📄 LICENSE                      # MIT license
├── 📄 STRUCTURE-INDEX.md           # This file - project organization guide
├── 📄 .gitignore                   # Git ignore patterns
├── 📁 build/                       # Build system & artifacts
├── 📁 config/                      # Configuration management
├── 📁 mcp/                         # Model Context Protocol integration
├── 📁 tools/                       # Development tools & scripts
├── 📁 tmp/                         # Temporary files
├── 📁 logs/                        # Application logs
├── 📁 BusBuddy.Core/              # Core business logic (.NET 9)
├── 📁 BusBuddy.WPF/               # WPF presentation layer
├── 📁 BusBuddy.Tests/             # Test suite
├── 📁 Documentation/              # Technical documentation
└── 📁 FusionCore/                 # Strategic planning & architecture
```

---

## 📁 **Detailed Directory Breakdown**

### **🔧 /build/** - Build System & Artifacts

```
build/
├── Directory.Build.props          # MSBuild properties (.NET 9)
├── Directory.Build.targets        # MSBuild targets
├── global.json                    # .NET SDK version (9.0.303)
├── NuGet.config                   # Package sources & Syncfusion feeds
├── artifacts/                     # Build outputs
└── TestResults/                   # Test execution results
```

**Purpose**: Centralized build configuration following Microsoft .NET project standards.

---

### **⚙️ /config/** - Configuration Management

```
config/
├── appsettings.json               # Default app configuration
├── appsettings.azure.json         # Azure-specific settings
├── appsettings.staging.json       # Staging environment
├── dependencies.psd1              # PowerShell dependencies
├── grok-assistant.settings.json   # AI assistant configuration
└── environment/
    ├── .env.example               # Environment template
    └── dev.env                    # Development environment
```

**Purpose**: Environment-specific configuration following Microsoft configuration patterns.

---

### **🤖 /mcp/** - Model Context Protocol Integration

```
mcp/
├── servers/                       # MCP server implementations
│   └── git-mcp-server.js         # Custom Git MCP server
├── tools/                         # MCP tool definitions
│   ├── azure-tools/              # Azure resource tools
│   ├── syncfusion-tools/         # Syncfusion component tools
│   └── database-tools/           # SQL database tools
└── config/
    └── mcp.json                   # MCP server configuration
```

**Purpose**: Microsoft MCP integration following official MCP server organization patterns.

**Key Features**:

- **Azure MCP Server**: Azure resource management
- **GitHub MCP Server**: Repository operations
- **Brave Search MCP**: Syncfusion documentation search
- **Microsoft Docs MCP**: Official Microsoft documentation
- **Custom BusBuddy Tools**: Project-specific operations

---

### **🛠️ /tools/** - Development Tools

```
tools/
├── powershell/                    # PowerShell modules & profiles
│   ├── Scripts/                   # Development scripts
│   ├── Profiles/                  # PowerShell profiles
│   └── Modules/                   # Custom modules
└── scripts/                       # Legacy scripts
```

**Purpose**: Development automation and productivity tools.

---

### **📊 /BusBuddy.Core/** - Business Logic (.NET 9)

```
BusBuddy.Core/
├── Configuration/                 # App configuration classes
├── Data/                         # Entity Framework contexts
├── Extensions/                   # Extension methods
├── Interceptors/                 # EF Core interceptors
├── Logging/                      # Serilog configuration
├── Migrations/                   # Database migrations
├── Models/                       # Domain entities
├── Services/                     # Business services
└── Utilities/                    # Core utilities
```

**Purpose**: Microsoft .NET 9 architecture with Entity Framework Core, following Clean Architecture principles.

---

### **🖥️ /BusBuddy.WPF/** - WPF Presentation (.NET 9)

```
BusBuddy.WPF/
├── Assets/                       # Images, fonts, resources
├── Commands/                     # MVVM commands
├── Controls/                     # Custom WPF controls
├── Converters/                   # Value converters
├── Extensions/                   # UI extensions
├── Logging/                      # UI-specific logging
├── Mapping/                      # DTO mappings
├── Messages/                     # MVVM messaging
├── Models/                       # UI models & DTOs
├── Resources/                    # XAML resources & styles
├── Services/                     # UI services (navigation, dialogs)
├── Utilities/                    # UI utilities
├── Validation/                   # Input validation
├── ViewModels/                   # MVVM ViewModels
└── Views/                        # XAML views
```

**Purpose**: Microsoft WPF MVVM architecture with **Syncfusion WPF 30.2.6** controls.

**Key Technologies**:

- **CommunityToolkit.Mvvm**: Microsoft's MVVM framework
- **Syncfusion Essential WPF**: Professional UI controls
- **Serilog**: Structured logging
- **.NET 9 WPF**: Latest Microsoft WPF framework

---

### **🧪 /BusBuddy.Tests/** - Test Suite

```
BusBuddy.Tests/
├── Core/                         # Core business logic tests
├── ViewModels/                   # ViewModel tests
├── ValidationTests/              # Validation logic tests
├── Phase3Tests/                  # Feature tests
└── Helpers/                      # Test utilities
```

**Purpose**: Comprehensive testing following Microsoft testing best practices.

---

### **📚 /Documentation/** - Technical Documentation

```
Documentation/
├── SETUP-GUIDE.md               # Project setup instructions
├── AZURE-SQL-SETUP.md           # Database configuration
├── SYNCFUSION-UPGRADE-30.2.6.md # UI framework upgrade
├── GROK-INTEGRATION-REVIEW-COMPLETE.md # AI integration
├── Development/                  # Development guides
├── Deployment/                   # Deployment procedures
├── Reference/                    # API references
└── Samples/                      # Code examples
```

**Purpose**: Technical documentation and development guides.

---

### **🚀 /FusionCore/** - Strategic Architecture

```
FusionCore/
├── README.md                     # FusionCore overview
├── DEVELOPMENT-GUIDE.md          # Architecture guidelines
├── Architecture-Blueprints/      # System architecture
├── Implementation-Guides/        # Technical implementations
├── Strategic-Planning/           # Business planning
│   ├── INDEX.md                 # Strategic planning index
│   └── MVP-Phase-1.md           # Phase 1 roadmap
├── MVP-Phases/                   # Development phases
└── Troubleshooting/              # Issue resolution
```

**Purpose**: Strategic planning and high-level architecture documentation.

---

## 🎯 **Microsoft Standards Compliance**

### **WPF Architecture (Microsoft MVVM Pattern)**

- ✅ **Model-View-ViewModel**: Clean separation of concerns
- ✅ **CommunityToolkit.Mvvm**: Microsoft's official MVVM framework
- ✅ **Data Binding**: Declarative XAML with minimal code-behind
- ✅ **Dependency Injection**: .NET 9 DI container integration
- ✅ **Async/Await**: Proper asynchronous programming patterns

### **MCP Integration (Microsoft MCP Standards)**

- ✅ **Standardized Configuration**: `.vscode/mcp.json` for VS Code integration
- ✅ **Server Organization**: Dedicated `/mcp/servers/` directory
- ✅ **Tool Definitions**: Organized tool categories
- ✅ **Environment Management**: Proper credential handling

### **.NET 9 Framework Standards**

- ✅ **Project Structure**: Microsoft-recommended folder organization
- ✅ **Build System**: Directory.Build.props for centralized configuration
- ✅ **Package Management**: NuGet.config with official feeds
- ✅ **Testing**: Microsoft testing frameworks and patterns

---

## 🔧 **Key Technologies & Versions**

| Technology                | Version | Purpose                  |
| ------------------------- | ------- | ------------------------ |
| **.NET Framework**        | 9.0     | Core runtime platform    |
| **WPF**                   | .NET 9  | Desktop UI framework     |
| **Syncfusion WPF**        | 30.2.6  | Professional UI controls |
| **Entity Framework Core** | 9.0+    | Data access layer        |
| **CommunityToolkit.Mvvm** | Latest  | Microsoft MVVM framework |
| **Serilog**               | 4.3.0+  | Structured logging       |
| **PowerShell**            | 7.5.2+  | Development automation   |
| **Azure SQL Database**    | Latest  | Cloud database           |

---

## 📖 **Usage Guidelines**

### **For Developers**

1. **Follow MVVM**: Use ViewModels for business logic, Views for UI
2. **Use Syncfusion**: Leverage professional WPF controls
3. **Configure MCP**: Add new tools to `/mcp/tools/`
4. **Document Changes**: Update relevant documentation

### **For System Administrators**

1. **Environment Configuration**: Modify files in `/config/environment/`
2. **Build Settings**: Adjust `/build/` configuration files
3. **Logging**: Monitor `/logs/` directory

### **For Contributors**

1. **Read Documentation**: Start with `/Documentation/SETUP-GUIDE.md`
2. **Follow Standards**: Maintain Microsoft compliance
3. **Test Changes**: Run comprehensive test suite
4. **Update Documentation**: Keep guides current

---

## 🚀 **Quick Start**

```powershell
# Clone and setup
git clone <repository>
cd BusBuddy

# Restore dependencies
dotnet restore BusBuddy.sln

# Build solution
dotnet build BusBuddy.sln

# Run application
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj
```

---

## 📞 **Support Resources**

- **Documentation**: `/Documentation/` directory
- **Setup Guide**: `/Documentation/SETUP-GUIDE.md`
- **Architecture**: `/FusionCore/README.md`
- **API Reference**: Generated XML documentation
- **Troubleshooting**: `/FusionCore/Troubleshooting/`

---

_This structure follows Microsoft's official recommendations for WPF applications, MCP integration, and .NET 9 project organization. For detailed guidance, reference the official Microsoft documentation linked throughout this project._
