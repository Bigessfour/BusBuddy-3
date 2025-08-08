# 🚌 BusBuddy - School Transportation Management System

> **Modern WPF application for comprehensive school bus fleet management, built with .NET 9.0 and Syncfusion controls.**

[![Build Status](https://img.shields.io/badge/build-✅%20passing-brightgreen)](https://github.com/Bigessfour/BusBuddy-3)
[![.NET](https://img.shields.io/badge/.NET-9.0.304-blue)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.5.2-blue)](https://github.com/PowerShell/PowerShell)
[![Syncfusion](https://img.shields.io/badge/Syncfusion-30.1.42%20✅%20Licensed-orange)](https://www.syncfusion.com/wpf-controls)
[![MVP Status](https://img.shields.io/badge/MVP-⚠️%20Development-yellow)](https://github.com/Bigessfour/BusBuddy-3)
[![Production](https://img.shields.io/badge/Production-�%20In%20Progress-yellow)](https://github.com/Bigessfour/BusBuddy-3)

## 🎯 **Project Vision**

BusBuddy streamlines school transportation operations through intelligent route management, vehicle tracking, and student assignment systems. Built with modern .NET technologies and professional-grade UI components.

### **Core Features**
- 🚌 **Fleet Management**: Vehicle tracking, maintenance scheduling, driver assignments
- 📍 **Route Optimization**: Google Earth Engine integration for efficient route planning
- 👥 **Student Management**: Student enrollment, route assignments, pickup/dropoff tracking
- 📊 **Analytics Dashboard**: Real-time metrics, performance reports, operational insights
- 🔧 **Maintenance Tracking**: Scheduled maintenance, repair history, compliance monitoring

## 🚀 **Quick Start**

### **Prerequisites**
- **Windows 10/11** (for WPF)
- **.NET 9.0 SDK** (9.0.303 or later)
- **PowerShell 7.5.2+** (for automation)
- **Visual Studio Code** (recommended) or Visual Studio 2022

### **Installation & Setup**
```bash
# Clone the repository
git clone https://github.com/Bigessfour/BusBuddy-3.git
cd BusBuddy

# Load PowerShell automation
Import-Module .\PowerShell\Modules\BusBuddy\BusBuddy.psm1

# Build and run (MVP development version)
bbBuild && bbRun

# Check for known issues before deployment
bbHealth
```

**⚠️ Note**: Before production deployment, please review the "Known Risks" section below.

### **📋 Current Build Status**
**MVP functionality operational with ongoing improvements:**
- ✅ **Build Status**: Clean build with 0 errors (warnings addressed)
- ✅ **Package Integrity**: Version conflicts resolved
- ✅ **Syncfusion License**: Configured and operational
- ✅ **MVP Validation**: Core functionality confirmed working
- ⚠️ **Environment**: .NET 9.0.304, PowerShell 7.5.2 operational
- 🔄 **Production Readiness**: In progress (see Known Risks section)

### **Development Setup**
```powershell
# Complete development environment setup
bbDevSession

# Verify system health
bbHealth

# Run tests
bbTest
```

## 📊 **Current Status (August 8, 2025)**

### **🎉 MVP Core Features Ready**
- ✅ **Build Status**: Clean build (0 errors, warnings documented)
- ✅ **MVP Verification**: `bbMvpCheck` confirms core functionality working
- ✅ **Core Features**: Student management and route assignment functional
- ✅ **Documentation**: Command references updated and standardized
- ✅ **PowerShell Automation**: 20+ working commands with enterprise-grade tooling
- ⚠️ **Production Status**: See Known Risks section below

### **Recent Achievements (Commit: 29b7dc1)**
- ✅ **Command Standardization**: Updated all commands from `bb-*` to `bb*` format
- ✅ **PowerShell Refactoring**: Fixed 49 Write-Host violations (5.4% compliance improvement)
- ✅ **Professional Tooling**: Created automated refactoring and analysis tools
- ✅ **Code Quality**: Fixed nullable reference warnings, maintained clean build
- ✅ **Comprehensive Documentation**: Updated all guides and reference materials

### **Available Commands**
```powershell
# Core Development
bbBuild               # Build solution (24.36s clean build)
bbRun                 # Run application
bbTest                # Execute tests (.NET 9 compatibility handled)
bbHealth              # System health check
bbClean               # Clean build artifacts
bbRestore             # Restore packages

# Development Workflow
bbDevSession          # Start complete development environment
bbInfo                # Show module information
bbCommands            # List all available commands

# Quality Assurance
bbXamlValidate        # Validate XAML files
bbAntiRegression      # Run compliance checks
bbMvpCheck            # Verify MVP readiness

# Route Optimization
bbRoutes              # XAI route optimization system
bbRouteDemo           # Demo with sample data
bbRouteStatus         # Check optimization status
```

## ⚠️ **Known Risks & Issues**

### **Database & Migration Concerns**
- **Migration History**: Potential migration history out of sync between environments
- **Seeding Issues**: Database seeding shows "0 records" despite "Already seeded" messages
- **Schema Changes**: Possible regression in UI data binding due to recent schema modifications
- **LocalDB vs Production**: Differences between LocalDB development and production SQL Server behavior

### **UI & Data Binding Risks**
- **Syncfusion Migration**: Ongoing migration from standard WPF to Syncfusion controls may introduce temporary inconsistencies
- **Data Binding**: Schema changes may affect existing MVVM data binding patterns
- **Performance**: Large datasets may impact UI responsiveness during initial load
- **Theme Consistency**: FluentDark/FluentLight theme application may be incomplete across all controls

### **PowerShell & Automation**
- **Module Compliance**: BusBuddy.psm1 has 45% compliance with Microsoft PowerShell standards
- **Write-Host Usage**: 50+ Write-Host violations need remediation for enterprise deployment
- **Command Standardization**: Recent `bb-*` to `bb*` format changes may require workflow adjustments
- **Environment Dependencies**: PowerShell 7.5.2+ requirement may limit deployment environments

### **External Dependencies**
- **Syncfusion Licensing**: Community license limitations in production environments
- **Google Earth Engine**: API rate limits and authentication dependencies
- **Azure SQL**: Network connectivity and firewall configuration requirements
- **Package Versions**: .NET 9.0.304 and associated package dependencies still stabilizing

### **Production Readiness Gaps**
- **Error Handling**: Exception handling patterns need standardization across modules
- **Logging**: Transition from multiple logging frameworks to Serilog-only not fully complete
- **Security**: Production security review and hardening pending
- **Performance Testing**: Load testing with realistic data volumes not yet conducted
- **Backup & Recovery**: Database backup and disaster recovery procedures not implemented

### **Recommended Actions Before Production**
1. **Database Audit**: Verify migration state and seeding integrity across all environments
2. **UI Testing**: Comprehensive testing of all Syncfusion controls with real data
3. **PowerShell Remediation**: Address Write-Host violations and improve module compliance
4. **Security Review**: Conduct security assessment and implement recommended hardening
5. **Performance Testing**: Test with production-scale data volumes
6. **Documentation**: Complete deployment and operational procedures documentation

## 🏗️ **Architecture**

### **Technology Stack**
| Component | Technology | Version |
|-----------|------------|---------|
| **Framework** | .NET | 9.0.303 |
| **UI Framework** | WPF | Built-in |
| **UI Controls** | Syncfusion Essential Studio | 30.1.42 |
| **Data Access** | Entity Framework Core | 9.0.7 |
| **Database** | SQL Server / LocalDB | Latest |
| **Logging** | Serilog | 4.3.0 |
| **Testing** | NUnit | 4.3.1 |
| **MVVM** | CommunityToolkit.MVVM | 8.3.2 |

### **Project Structure**
```
BusBuddy/
├── 🏢 BusBuddy.Core/           # Business logic, data access, services
├── 🎨 BusBuddy.WPF/            # WPF UI layer with Syncfusion controls
├── 🧪 BusBuddy.Tests/          # Unit and integration tests
├── 🔧 PowerShell/              # Build automation and utilities
├── 📚 Documentation/           # Project documentation
└── 📋 Standards/               # Development standards and guidelines
```

### **Design Patterns**
- **MVVM**: Model-View-ViewModel for clean separation of concerns
- **Dependency Injection**: Built-in .NET DI container
- **Repository Pattern**: Data access abstraction
- **Command Pattern**: User actions through ICommand interface

## 🔧 **Development**

### **PowerShell Automation**
BusBuddy includes a comprehensive PowerShell module for development tasks:

```powershell
# Essential commands (Updated August 8, 2025)
bbBuild               # Build the solution
bbRun                 # Run the application
bbTest                # Execute all tests
bbHealth              # System diagnostics
bbDevSession          # Complete development setup

# Advanced commands
bb-xaml-validate      # Validate XAML files
bb-catch-errors       # Execute with exception capture
bb-commands           # List all available commands
```

### **Building**
```bash
# Standard .NET CLI
dotnet restore
dotnet build BusBuddy.sln
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj

# Or use PowerShell automation
bb-build && bb-run
```

### **Testing**
```bash
# Run all tests
dotnet test BusBuddy.sln

# Or use PowerShell
bb-test
```

## 🎨 **UI Design**

BusBuddy uses Syncfusion Essential Studio for WPF to provide a modern, professional user interface:

### **Key UI Components**
- **SfDataGrid**: Advanced data grid with sorting, filtering, and editing
- **DockingManager**: Professional layout management
- **SfChart**: Rich charting and visualization
- **NavigationDrawer**: Modern navigation patterns
- **SfScheduler**: Calendar and scheduling interface

### **Themes**
- **FluentDark**: Modern dark theme
- **FluentLight**: Clean light theme
- **Custom**: BusBuddy-specific styling

## 📊 **Features**

### **Core Modules**

#### **🚌 Vehicle Management**
- Fleet inventory and specifications
- Maintenance scheduling and tracking
- Driver assignments and qualifications
- Fuel consumption monitoring
- Inspection compliance

#### **📍 Route Management**
- Interactive route planning with Google Earth Engine
- Stop optimization and timing
- Student assignment to routes
- Real-time tracking and updates
- Performance analytics

#### **👥 Student Management**
- Student enrollment and profiles
- Route assignments and pickup locations
- Attendance tracking
- Parent communication
- Special needs accommodation

#### **📈 Analytics & Reporting**
- Operational dashboards
- Performance metrics
- Cost analysis
- Compliance reporting
- Predictive maintenance

## 🛠️ **Configuration**

### **Database Setup**
```bash
# Create/update database
dotnet ef database update

# Add new migration
dotnet ef migrations add NewMigrationName
```

### **Environment Variables**
- `SYNCFUSION_LICENSE_KEY`: **Community License key** (required for production)
  - **Community License**: Free for individual developers and small teams
  - **Setup**: Set your actual license key from Syncfusion account
  - **Format**: Long alphanumeric string (200+ characters)
  - **Example**: `[System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "YOUR_ACTUAL_LICENSE_KEY", "User")`
- `ConnectionStrings__DefaultConnection`: Database connection string
- `GoogleEarthEngine__ApiKey`: Google Earth Engine API key

### **Syncfusion Community License Setup**
1. **Get License**: Visit [Syncfusion Community License](https://www.syncfusion.com/products/communitylicense) 
2. **Generate Key**: Login to your Syncfusion account and generate license key
3. **Set Environment**: Replace placeholder with your actual key (NOT "TRIAL_MODE")
4. **Verify**: Application should start without trial dialogs

### **Development Settings**
Configuration is managed through `appsettings.json` files in each project:
- `BusBuddy.Core/appsettings.json`: Core configuration
- `BusBuddy.WPF/appsettings.json`: UI-specific settings

## 🧪 **Testing** - Advanced NUnit Integration

### **PowerShell Testing Module** ✨ **NEW**
BusBuddy includes a comprehensive testing infrastructure with VS Code NUnit Test Runner integration:

```powershell
# Load advanced testing module
Import-Module ".\PowerShell\Modules\BusBuddy.Testing\BusBuddy.Testing.psd1"

# Quick testing commands
bb-test                    # Run all tests
bb-test -TestSuite Unit    # Run unit tests only
bb-test-watch              # Continuous testing with file monitoring
bb-test-report             # Generate comprehensive markdown report
bb-test-status             # Check current test status
```

### **Test Structure**
- **Unit Tests**: `BusBuddy.Tests/Core/` - Core business logic validation
- **Integration Tests**: `BusBuddy.Tests/Phase3Tests/` - Database and service interactions
- **Validation Tests**: `BusBuddy.Tests/ValidationTests/` - Input validation and error handling

### **Test Categories**
| Category | Filter | Description |
|----------|--------|-------------|
| **All** | No filter | Complete test suite |
| **Unit** | `Category=Unit` | Core business logic |
| **Integration** | `Category=Integration` | Database/service interactions |
| **Validation** | `Category=Validation` | Input validation & error handling |
| **Core** | `TestName~Core` | BusBuddy.Core project tests |
| **WPF** | `TestName~WPF` | UI and presentation layer |

### **VS Code Integration**
- **NUnit Test Runner Extension**: Automatic test discovery and execution
- **Task Explorer**: Run "🧪 BB: Phase 4 Modular Tests" for comprehensive testing
- **Watch Mode**: Use "🔄 BB: Phase 4 Test Watch" for continuous testing

### **Advanced Features**
- **Watch Mode**: Monitors `*.cs` and `*.xaml` files for changes, auto-runs tests
- **Detailed Reporting**: Markdown reports with metrics, environment details, and recommendations
- **Category-Based Testing**: Focus on specific test types during development
- **Microsoft Compliance**: PowerShell 7.5.2 standards-compliant automation

### **Coverage**
Test coverage reports are generated in `TestResults/` directory with detailed TRX files and comprehensive markdown reports in `Documentation/Reports/`.

## 📚 **Documentation**

### **For Developers**
- [**System Architecture**](Documentation/PHASE-2-IMPLEMENTATION-PLAN.md)
- [**Development Standards**](Standards/MASTER-STANDARDS.md)
- [**PowerShell Reference**](Documentation/PowerShell-7.5.2-Reference.md)
- [**Phase 4 Status**](Documentation/Phase4-Milestone-Report.md)

### **For AI Assistants**
- [**AI Assistant Guide**](Grok%20Resources/GROK-README.md)
- [**Repository Navigation**](Grok%20Resources/AI-ASSISTANT-GUIDE.md)

## 🤝 **Contributing**

### **Development Workflow**
1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Follow the [development standards](Standards/MASTER-STANDARDS.md)
4. Commit changes: `git commit -m 'Add amazing feature'`
5. Push to branch: `git push origin feature/amazing-feature`
6. Open a Pull Request

### **Standards**
- ✅ **Syncfusion controls only** (no standard WPF controls)
- ✅ **Serilog logging** (no Microsoft.Extensions.Logging)
- ✅ **PowerShell 7.5.2 compliance** (no Write-Host)
- ✅ **.NET 9.0 target framework**
- ✅ **MVVM pattern** with proper separation of concerns

### **Code Quality**
- All tests must pass: `bb-test`
- Code analysis must pass: `bb-build`
- XAML validation: `bb-xaml-validate`
- PowerShell compliance: Follow Microsoft standards

## 📈 **Project Status**

### **Current Phase**: MVP Development (Phase 4 Complete)
- ✅ **Foundation**: Complete (.NET 9, Syncfusion, EF Core)
- ✅ **Testing Infrastructure**: Operational (NUnit, coverage reporting)
- ✅ **PowerShell Automation**: Core functionality stable (compliance improvements needed)
- 🟡 **UI Migration**: Completing Syncfusion control migration (some inconsistencies remain)
- 🟡 **Student Management**: Core features functional (production hardening in progress)
- 🔄 **Production Readiness**: Requires addressing known risks listed above
- 🎯 **Route Optimization**: Next major milestone

### **Quality Metrics**
- **Build Status**: ✅ Passing (0 errors, warnings documented)
- **Test Coverage**: 75%+ achieved (85% target)
- **Code Quality**: Meets development standards (production review pending)
- **Documentation**: Comprehensive for development (operational docs needed)
- **PowerShell Compliance**: 45% (Microsoft standards remediation in progress)

## 🌟 **Roadmap**

### **Upcoming Features**
- 🎯 **Student Entry System**: Complete student-to-route assignment
- 📱 **Mobile Companion**: Driver mobile app
- 🔔 **Real-time Notifications**: Parent and administrator alerts
- 🤖 **AI Route Optimization**: Machine learning for route efficiency
- 📊 **Advanced Analytics**: Predictive maintenance and cost optimization

### **Long-term Vision**
- Integration with state transportation reporting systems
- Multi-district support
- IoT device integration for real-time tracking
- Environmental impact tracking and reporting

## 📞 **Support**

### **Getting Help**
- 📚 **Documentation**: Start with this README and linked guides
- 🔧 **PowerShell**: Use `bbHealth` for system diagnostics
- 🐛 **Issues**: Create GitHub issues for bugs or feature requests
- 💬 **Discussions**: Use GitHub discussions for questions

### **Troubleshooting**
```powershell
# System health check
bbHealth

# Build diagnostics
bb-build 2>&1 | tee build-output.log

# XAML validation
bb-xaml-validate
```

## 📄 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 **Acknowledgments**

- **Syncfusion**: Professional WPF controls
- **Microsoft**: .NET platform and development tools
- **PowerShell Team**: Advanced automation capabilities
- **NUnit Team**: Comprehensive testing framework

---

**Built with ❤️ for school transportation professionals**

*Last Updated: August 8, 2025*
