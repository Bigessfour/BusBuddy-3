# 🚌 BusBuddy - School Transportation Management System

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Phase 4](https://img.shields.io/badge/Phase-4%20Complete-brightgreen.svg)](Documentation/Phase4-Milestone-Report.md)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.5.2-blue.svg)](https://github.com/PowerShell/PowerShell)
[![Testing](https://img.shields.io/badge/Testing-NUnit%20%2B%20VS%20Code-brightgreen.svg)](#testing-infrastructure)

A modern, professional school transportation management system built with WPF, .NET 8, and Syncfusion controls. Features comprehensive route management, driver scheduling, vehicle tracking, and activity coordination with enhanced testing infrastructure.

## 🎯 **Current Status: Phase 4 Testing Infrastructure Complete (August 2, 2025)**

### ✅ **Recent Major Achievements**
- **🧪 Phase 4 Complete**: Comprehensive testing and validation infrastructure operational
- **📦 PowerShell Module Reorganization**: Microsoft 7.5.2 compliant modular structure
- **🔧 BusBuddy.Testing Module**: Complete NUnit integration with VS Code Test Runner
- **🏗️ Enhanced Development Tools**: bb-* commands for streamlined workflows
- **📚 Documentation Enhancement**: Comprehensive project documentation and standards

### 🚀 **Quick Start**

```powershell
# Clone and setup
git clone https://github.com/Bigessfour/BusBuddy-2.git
cd BusBuddy-2

# Build and run
dotnet restore
dotnet build BusBuddy.sln
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj

# Or use PowerShell automation
Import-Module .\PowerShell\Modules\BusBuddy\BusBuddy.psm1
bb-build && bb-run
```

## 🏗️ **Comprehensive Project Architecture**

### **Core Technology Stack**
- **Framework**: .NET 8.0-windows (WPF Desktop Application)
- **UI Library**: Syncfusion Essential Studio for WPF v30.1.42
- **Data Access**: Entity Framework Core 9.0.7 with SQL Server/LocalDB
- **Logging**: Serilog 4.3.0 with structured logging
- **Testing**: NUnit 4.0.1 with VS Code Test Runner integration
- **Automation**: PowerShell 7.5.2 with Microsoft compliance standards

### **Application Architecture Layers**
```
┌─────────────────────────────────────────────────────────┐
│                    BusBuddy.WPF                        │
│              Presentation Layer (MVVM)                 │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐      │
│  │    Views    │ │ ViewModels  │ │  Services   │      │
│  │   (XAML)    │ │   (C#)      │ │ (UI Logic)  │      │
│  └─────────────┘ └─────────────┘ └─────────────┘      │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                   BusBuddy.Core                        │
│               Business Logic Layer                      │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐      │
│  │   Models    │ │  Services   │ │    Data     │      │
│  │ (Entities)  │ │ (Business)  │ │   (EF Core) │      │
│  └─────────────┘ └─────────────┘ └─────────────┘      │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│                 Testing & Automation                    │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐      │
│  │BusBuddy.Tests│ │  PowerShell │ │   VS Code   │      │
│  │   (NUnit)   │ │  (bb-tools) │ │ Integration │      │
│  └─────────────┘ └─────────────┘ └─────────────┘      │
└─────────────────────────────────────────────────────────┘
```

## 📁 **Detailed File Structure (Fetchability Guide)**

### **🎯 Root Directory**
```
BusBuddy/
├── 📄 README.md                          # This comprehensive project guide
├── 📄 GROK-README.md                     # AI assistant navigation guide
├── 📄 CONTRIBUTING.md                    # Development contribution guidelines
├── 📄 LICENSE                           # Project license information
├── 📄 BusBuddy.sln                      # Visual Studio solution file
├── 📄 global.json                       # .NET SDK version configuration
├── 📄 Directory.Build.props             # MSBuild common properties
├── 📄 Directory.Packages.props          # NuGet package version management
├── 📄 NuGet.config                      # NuGet package source configuration
├── 📄 BusBuddy-Practical.ruleset       # Code analysis rules for development
├── 📄 JSON-STANDARDS.md                 # JSON formatting and usage standards
├── 📄 XML-STANDARDS.md                  # XML/XAML formatting standards
├── 📄 YAML-STANDARDS.md                 # YAML configuration standards
├── 📄 BusBuddy.db                       # SQLite database (development)
└── 📄 mcp.json                          # Model Context Protocol configuration
```

### **🏗️ Core Business Logic (BusBuddy.Core/)**
```
BusBuddy.Core/
├── 📄 BusBuddy.Core.csproj              # Core project configuration
├── 📄 BusBuddyDbContext.cs              # Entity Framework database context
├── 📄 appsettings.json                  # Core application configuration
├── 📄 packages.lock.json                # NuGet package lock file
│
├── 📂 Configuration/                     # Application configuration services
│   ├── 📄 ConfigurationService.cs       # Centralized configuration management
│   └── 📄 EnvironmentHelper.cs          # Environment-specific logic
│
├── 📂 Data/                             # Data access and database operations
│   ├── 📄 Configurations/               # Entity Framework configurations
│   │   ├── 📄 DriverConfiguration.cs    # Driver entity configuration
│   │   ├── 📄 BusConfiguration.cs       # Bus entity configuration
│   │   ├── 📄 ActivityConfiguration.cs  # Activity entity configuration
│   │   └── 📄 StudentConfiguration.cs   # Student entity configuration
│   │
│   ├── 📂 Services/                     # Data layer services
│   │   ├── 📄 SeedDataService.cs        # Database seeding for development
│   │   ├── 📄 Phase2DataSeederService.cs # Phase 2 specific data seeding
│   │   └── 📄 DataIntegrityService.cs   # Data validation and integrity
│   │
│   └── 📂 Migrations/                   # Entity Framework migrations
│       ├── 📄 20250801_InitialCreate.cs # Initial database schema
│       ├── 📄 20250801_AddStudents.cs   # Student entity migration
│       ├── 📄 20250802_AddActivities.cs # Activity system migration
│       └── 📄 BusBuddyContextModelSnapshot.cs # EF model snapshot
│
├── 📂 Extensions/                       # Core extension methods
│   ├── 📄 DatabaseExtensions.cs         # Database operation extensions
│   ├── 📄 ServiceCollectionExtensions.cs # DI container extensions
│   └── 📄 ValidationExtensions.cs       # Data validation extensions
│
├── 📂 Interceptors/                     # Entity Framework interceptors
│   ├── 📄 LoggingInterceptor.cs         # Database operation logging
│   ├── 📄 PerformanceInterceptor.cs     # Query performance monitoring
│   └── 📄 ValidationInterceptor.cs      # Data validation interception
│
├── 📂 Logging/                          # Logging configuration and enrichers
│   ├── 📄 LoggingConfiguration.cs       # Serilog configuration setup
│   ├── 📄 PerformanceEnricher.cs        # Performance metrics enrichment
│   └── 📄 ContextEnricher.cs            # Application context enrichment
│
├── 📂 Models/                           # Domain models and entities
│   ├── 📄 Driver.cs                     # Driver entity (license, contact, availability)
│   ├── 📄 Bus.cs                        # Bus entity (unified vehicle model)
│   ├── 📄 Activity.cs                   # Activity entity (scheduling, routes)
│   ├── 📄 Student.cs                    # Student entity (transportation needs)
│   ├── 📄 Route.cs                      # Route entity (geographic planning)
│   ├── 📄 ActivitySchedule.cs           # Activity scheduling entity
│   ├── 📄 SportsEvent.cs                # Sports event entity
│   ├── 📄 RecurrenceType.cs             # Activity recurrence enumeration
│   └── 📄 Enums/                        # Domain-specific enumerations
│       ├── 📄 ActivityType.cs           # Activity classification types
│       ├── 📄 VehicleStatus.cs          # Vehicle operational status
│       ├── 📄 DriverStatus.cs           # Driver availability status
│       └── 📄 RouteStatus.cs            # Route operational status
│
├── 📂 Services/                         # Business logic services
│   ├── 📄 Interfaces/                   # Service interface contracts
│   │   ├── 📄 IDriverService.cs         # Driver management service interface
│   │   ├── 📄 IBusService.cs            # Bus management service interface
│   │   ├── 📄 IActivityService.cs       # Activity coordination service interface
│   │   ├── 📄 IRouteService.cs          # Route planning service interface
│   │   ├── 📄 ISchedulingService.cs     # Scheduling coordination interface
│   │   ├── 📄 ISportsSchedulingService.cs # Sports event scheduling interface
│   │   └── 📄 IGeoDataService.cs        # Geographic data service interface
│   │
│   ├── 📄 DriverService.cs              # Driver management implementation
│   ├── 📄 BusService.cs                 # Bus management implementation
│   ├── 📄 ActivityService.cs            # Activity coordination implementation
│   ├── 📄 RouteService.cs               # Route planning implementation
│   ├── 📄 SchedulingService.cs          # Scheduling coordination implementation
│   ├── 📄 SportsSchedulingService.cs    # Sports event scheduling implementation
│   └── 📄 GeoDataService.cs             # Geographic data service implementation
│
└── 📂 Utilities/                        # Core utility classes
    ├── 📄 DateTimeHelper.cs             # Date/time manipulation utilities
    ├── 📄 ValidationHelper.cs           # Data validation utilities
    ├── 📄 ConfigurationHelper.cs        # Configuration management utilities
    └── 📄 SecurityHelper.cs             # Security and authorization utilities
```

### **🎨 WPF Presentation Layer (BusBuddy.WPF/)**
```
BusBuddy.WPF/
├── 📄 BusBuddy.WPF.csproj               # WPF project configuration
├── 📄 App.xaml                         # Application-level XAML definitions
├── 📄 App.xaml.cs                      # Application startup and DI configuration
├── 📄 AssemblyInfo.cs                  # Assembly metadata and versioning
├── 📄 appsettings.json                 # WPF-specific configuration
├── 📄 app.config                       # Legacy configuration support
├── 📄 RelayCommand.cs                  # MVVM command implementation
├── 📄 packages.lock.json               # NuGet package lock file
│
├── 📂 Assets/                          # Static application resources
│   ├── 📂 Images/                      # Image resources and icons
│   │   ├── 📄 bus-icon.png            # Application icon assets
│   │   ├── 📄 driver-avatar.png       # Default driver images
│   │   └── 📄 logos/                  # Company/application logos
│   │
│   ├── 📂 Fonts/                      # Custom font resources
│   │   └── 📄 BusDisplay.ttf          # Custom transportation fonts
│   │
│   └── 📂 Data/                       # Sample data files
│       ├── 📄 SampleDrivers.json     # Development driver data
│       ├── 📄 SampleRoutes.json      # Development route data
│       └── 📄 SampleActivities.json  # Development activity data
│
├── 📂 Commands/                        # WPF-specific command implementations
│   ├── 📄 NavigationCommands.cs        # Navigation command implementations
│   ├── 📄 DataCommands.cs              # Data operation commands
│   └── 📄 UICommands.cs                # UI interaction commands
│
├── 📂 Controls/                        # Custom user controls
│   ├── 📄 LoadingSpinner.xaml          # Loading indicator control
│   ├── 📄 LoadingSpinner.xaml.cs       # Loading spinner code-behind
│   ├── 📄 StatusBar.xaml               # Application status bar
│   ├── 📄 StatusBar.xaml.cs            # Status bar code-behind
│   ├── 📄 AddressValidationControl.xaml # Address validation UI
│   ├── 📄 AddressValidationControl.xaml.cs # Address validation logic
│   └── 📄 NavigationPanel.xaml         # Navigation control panel
│
├── 📂 Converters/                      # Value converters for data binding
│   ├── 📄 BooleanToVisibilityConverter.cs # Boolean to visibility conversion
│   ├── 📄 DateTimeConverter.cs         # Date/time formatting conversion
│   ├── 📄 StatusToColorConverter.cs    # Status to color mapping
│   ├── 📄 EnumToStringConverter.cs     # Enumeration display conversion
│   └── 📄 NullToVisibilityConverter.cs # Null value visibility handling
│
├── 📂 Extensions/                      # WPF-specific extension methods
│   ├── 📄 DependencyObjectExtensions.cs # WPF dependency object extensions
│   ├── 📄 FrameworkElementExtensions.cs # UI element extensions
│   └── 📄 DataGridExtensions.cs        # Syncfusion DataGrid extensions
│
├── 📂 Logging/                         # WPF-specific logging configuration
│   ├── 📄 UILoggingConfiguration.cs    # UI-specific logging setup
│   ├── 📄 PerformanceLogger.cs         # UI performance monitoring
│   └── 📄 UserActionLogger.cs          # User interaction logging
│
├── 📂 Mapping/                         # Data mapping between layers
│   ├── 📄 ViewModelMappings.cs         # Model to ViewModel mapping
│   ├── 📄 DTOMappings.cs               # Data transfer object mapping
│   └── 📄 EntityMappings.cs            # Entity to display model mapping
│
├── 📂 Models/                          # UI-specific model classes
│   ├── 📄 DriverViewModel.cs           # Driver display model
│   ├── 📄 BusViewModel.cs              # Bus display model
│   ├── 📄 ActivityViewModel.cs         # Activity display model
│   ├── 📄 RouteViewModel.cs            # Route display model
│   ├── 📄 NavigationItem.cs            # Navigation menu model
│   └── 📄 DashboardMetrics.cs          # Dashboard statistics model
│
├── 📂 Resources/                       # XAML resource dictionaries
│   ├── 📄 Themes/                      # Application themes
│   │   ├── 📄 FluentDark.xaml         # Syncfusion FluentDark theme
│   │   ├── 📄 FluentLight.xaml        # Syncfusion FluentLight theme
│   │   └── 📄 Custom.xaml             # Custom theme modifications
│   │
│   ├── 📄 Styles/                      # Control styles
│   │   ├── 📄 ButtonStyles.xaml       # Button styling definitions
│   │   ├── 📄 DataGridStyles.xaml     # DataGrid styling definitions
│   │   ├── 📄 TextBlockStyles.xaml    # Text styling definitions
│   │   └── 📄 SyncfusionStyles.xaml   # Syncfusion control customizations
│   │
│   ├── 📄 Templates/                   # Data templates
│   │   ├── 📄 DriverTemplates.xaml    # Driver display templates
│   │   ├── 📄 BusTemplates.xaml       # Bus display templates
│   │   ├── 📄 ActivityTemplates.xaml  # Activity display templates
│   │   └── 📄 ListTemplates.xaml      # Generic list templates
│   │
│   └── 📄 Dictionaries/                # Resource dictionaries
│       ├── 📄 Colors.xaml             # Color definitions
│       ├── 📄 Brushes.xaml            # Brush resources
│       ├── 📄 Icons.xaml              # Icon resources
│       └── 📄 Animations.xaml         # Animation resources
│
├── 📂 Services/                        # UI-specific services
│   ├── 📄 NavigationService.cs         # View navigation service
│   ├── 📄 DialogService.cs             # Modal dialog service
│   ├── 📄 NotificationService.cs       # User notification service
│   ├── 📄 ThemeService.cs              # Theme management service
│   ├── 📄 PrintService.cs              # Report printing service
│   └── 📄 ExportService.cs             # Data export service
│
├── 📂 Testing/                         # UI testing utilities
│   ├── 📄 MockServices.cs              # Mock service implementations
│   ├── 📄 TestDataProviders.cs         # Test data generation
│   └── 📄 UITestHelpers.cs             # UI testing helper methods
│
├── 📂 Utilities/                       # WPF utility classes
│   ├── 📄 DebugHelper.cs               # Debug information and monitoring
│   ├── 📄 UIHelper.cs                  # UI manipulation utilities
│   ├── 📄 PrintHelper.cs               # Printing and export utilities
│   ├── 📄 ValidationHelper.cs          # UI validation utilities
│   └── 📄 PerformanceHelper.cs         # UI performance monitoring
│
├── 📂 ViewModels/                      # MVVM ViewModels organized by feature
│   ├── 📄 BaseViewModel.cs             # Base ViewModel with INotifyPropertyChanged
│   ├── 📄 MainViewModel.cs             # Main window ViewModel
│   ├── 📄 DashboardViewModel.cs        # Dashboard metrics ViewModel
│   │
│   ├── 📂 Driver/                      # Driver management ViewModels
│   │   ├── 📄 DriversViewModel.cs      # Driver list management
│   │   ├── 📄 DriverFormViewModel.cs   # Driver form operations
│   │   └── 📄 DriverDetailsViewModel.cs # Driver details display
│   │
│   ├── 📂 Vehicle/                     # Vehicle management ViewModels
│   │   ├── 📄 VehiclesViewModel.cs     # Vehicle list management (legacy)
│   │   ├── 📄 VehicleViewModel.cs      # Single vehicle operations (legacy)
│   │   ├── 📄 BusViewModel.cs          # Bus management (current)
│   │   └── 📄 BusFormViewModel.cs      # Bus form operations
│   │
│   ├── 📂 Activity/                    # Activity management ViewModels
│   │   ├── 📄 ActivitiesViewModel.cs   # Activity list management
│   │   ├── 📄 ActivityFormViewModel.cs # Activity form operations
│   │   ├── 📄 SchedulingViewModel.cs   # Activity scheduling
│   │   └── 📄 SportsViewModel.cs       # Sports event management
│   │
│   ├── 📂 Route/                       # Route management ViewModels
│   │   ├── 📄 RoutesViewModel.cs       # Route list management
│   │   ├── 📄 RouteFormViewModel.cs    # Route planning forms
│   │   └── 📄 RouteMapViewModel.cs     # Geographic route visualization
│   │
│   ├── 📂 Student/                     # Student management ViewModels
│   │   ├── 📄 StudentsViewModel.cs     # Student list management
│   │   ├── 📄 StudentFormViewModel.cs  # Student registration forms
│   │   └── 📄 StudentRouteViewModel.cs # Student route assignment
│   │
│   └── 📂 GoogleEarth/                 # Google Earth integration ViewModels
│       ├── 📄 GoogleEarthViewModel.cs  # Map visualization ViewModel
│       ├── 📄 RouteVisualizationViewModel.cs # Route mapping
│       └── 📄 GeoDataViewModel.cs      # Geographic data management
│
└── 📂 Views/                           # XAML Views organized by feature
    ├── 📄 MainWindow.xaml              # Main application window
    ├── 📄 MainWindow.xaml.cs           # Main window code-behind
    │
    ├── 📂 Dashboard/                   # Dashboard and main views
    │   ├── 📄 DashboardView.xaml       # Main dashboard display
    │   ├── 📄 DashboardView.xaml.cs    # Dashboard code-behind
    │   └── 📄 MetricsPanel.xaml        # Metrics display panel
    │
    ├── 📂 Driver/                      # Driver management views
    │   ├── 📄 DriversView.xaml         # Driver list view
    │   ├── 📄 DriversView.xaml.cs      # Driver list code-behind
    │   ├── 📄 DriverForm.xaml          # Driver registration/edit form
    │   ├── 📄 DriverForm.xaml.cs       # Driver form code-behind
    │   ├── 📄 DriverManagementView.xaml # Driver management interface
    │   └── 📄 DriverManagementView.xaml.cs # Driver management code-behind
    │
    ├── 📂 Vehicle/                     # Vehicle/Bus management views
    │   ├── 📄 VehiclesView.xaml        # Vehicle list view (legacy)
    │   ├── 📄 VehiclesView.xaml.cs     # Vehicle list code-behind (legacy)
    │   ├── 📄 VehicleForm.xaml         # Vehicle form (legacy)
    │   ├── 📄 VehicleForm.xaml.cs      # Vehicle form code-behind (legacy)
    │   ├── 📄 VehicleManagementView.xaml # Vehicle management interface
    │   ├── 📄 VehicleManagementView.xaml.cs # Vehicle management code-behind
    │   ├── 📄 BusView.xaml             # Bus list view (current)
    │   ├── 📄 BusView.xaml.cs          # Bus list code-behind (current)
    │   ├── 📄 BusForm.xaml             # Bus registration/edit form
    │   └── 📄 BusForm.xaml.cs          # Bus form code-behind
    │
    ├── 📂 Activity/                    # Activity management views
    │   ├── 📄 ActivitiesView.xaml      # Activity list view
    │   ├── 📄 ActivitiesView.xaml.cs   # Activity list code-behind
    │   ├── 📄 ActivityForm.xaml        # Activity creation/edit form
    │   ├── 📄 ActivityForm.xaml.cs     # Activity form code-behind
    │   ├── 📄 ActivityScheduleView.xaml # Activity scheduling interface
    │   ├── 📄 ActivityScheduleView.xaml.cs # Activity scheduling code-behind
    │   ├── 📄 ActivityScheduleEditDialog.xaml # Schedule edit dialog
    │   ├── 📄 ActivityScheduleEditDialog.xaml.cs # Schedule edit code-behind
    │   ├── 📄 ActivityManagementView.xaml # Activity management interface
    │   ├── 📄 ActivityManagementView.xaml.cs # Activity management code-behind
    │   └── 📄 SportsEventView.xaml     # Sports event management
    │
    ├── 📂 Route/                       # Route management views
    │   ├── 📄 RoutesView.xaml          # Route list view
    │   ├── 📄 RoutesView.xaml.cs       # Route list code-behind
    │   ├── 📄 RouteForm.xaml           # Route planning form
    │   ├── 📄 RouteForm.xaml.cs        # Route form code-behind
    │   ├── 📄 RouteManagementView.xaml # Route management interface
    │   └── 📄 RouteManagementView.xaml.cs # Route management code-behind
    │
    ├── 📂 Student/                     # Student management views
    │   ├── 📄 StudentsView.xaml        # Student list view
    │   ├── 📄 StudentsView.xaml.cs     # Student list code-behind
    │   ├── 📄 StudentForm.xaml         # Student registration form
    │   └── 📄 StudentForm.xaml.cs      # Student form code-behind
    │
    ├── 📂 Settings/                    # Application settings views
    │   ├── 📄 SettingsView.xaml        # Application settings interface
    │   ├── 📄 SettingsView.xaml.cs     # Settings view code-behind
    │   └── 📄 Settings.xaml            # Legacy settings view
    │
    ├── 📂 Analytics/                   # Analytics and reporting views
    │   ├── 📄 AnalyticsDashboardView.xaml # Analytics dashboard
    │   ├── 📄 AnalyticsDashboardView.xaml.cs # Analytics code-behind
    │   └── 📄 ReportsView.xaml         # Report generation interface
    │
    ├── 📂 Fuel/                        # Fuel management views
    │   ├── 📄 FuelDialog.xaml          # Fuel entry dialog
    │   ├── 📄 FuelDialog.xaml.cs       # Fuel dialog code-behind
    │   ├── 📄 FuelReconciliationDialog.xaml # Fuel reconciliation
    │   └── 📄 FuelReconciliationDialog.xaml.cs # Fuel reconciliation code-behind
    │
    ├── 📂 Bus/                         # Bus-specific views
    │   ├── 📄 NotificationWindow.xaml  # Bus notification system
    │   └── 📄 NotificationWindow.xaml.cs # Notification code-behind
    │
    └── 📂 GoogleEarth/                 # Google Earth integration views
        ├── 📄 GoogleEarthView.xaml     # Google Earth visualization
        ├── 📄 GoogleEarthView.xaml.cs  # Google Earth code-behind
        └── 📄 MapControls.xaml         # Map control interface
```

### **🧪 Testing Infrastructure (BusBuddy.Tests/ & Testing Tools)**
```
BusBuddy.Tests/
├── 📄 BusBuddy.Tests.csproj            # Test project configuration
├── 📄 packages.lock.json               # NuGet test packages lock
├── 📄 TESTING-STANDARDS.md             # Testing guidelines and standards
│
├── 📂 Core/                            # Core business logic tests
│   ├── 📄 DataLayerTests.cs            # Entity Framework CRUD tests ✅
│   ├── 📄 ServiceTests.cs              # Business service layer tests
│   ├── 📄 ModelTests.cs                # Domain model validation tests
│   └── 📄 IntegrationTests.cs          # Cross-service integration tests
│
├── 📂 ValidationTests/                 # Model and business rule validation
│   ├── 📄 ModelValidationTests.cs      # Entity validation tests ✅ (11 tests)
│   ├── 📄 BusinessRuleTests.cs         # Business logic validation
│   └── 📄 DataIntegrityTests.cs        # Data consistency validation
│
├── 📂 Phase3Tests/                     # Advanced feature tests (Phase 3)
│   ├── 📄 XAIChatServiceTests.cs       # AI integration tests
│   └── 📄 ServiceIntegrationTests.cs   # Advanced service integration
│
└── 📂 Utilities/                       # Test utilities and helpers
    ├── 📄 BaseTestFixture.cs           # Base test class with common setup
    ├── 📄 MockDataProvider.cs          # Test data generation utilities
    └── 📄 TestDatabaseFactory.cs       # In-memory database for testing

BusBuddy.UITests/                       # UI automation testing (separate project)
├── 📄 BusBuddy.UITests.csproj         # UI test project configuration
├── 📂 Tests/                          # UI automation tests
│   ├── 📄 DriversTests.cs             # Driver UI interaction tests
│   ├── 📄 VehicleModelTests.cs        # Vehicle form validation tests
│   ├── 📄 ActivityModelTests.cs       # Activity scheduling UI tests
│   ├── 📄 DashboardTests.cs           # Dashboard interaction tests
│   └── 📄 DataIntegrityServiceTests.cs # UI data consistency tests
└── 📂 PageObjects/                    # Page object pattern implementation
    ├── 📄 DriversPage.cs              # Drivers view page object
    ├── 📄 VehiclesPage.cs             # Vehicles view page object
    └── 📄 DashboardPage.cs            # Dashboard page object
```

### **🔧 PowerShell Development Automation**
```
PowerShell/
├── 📄 IMPLEMENTATION-STRATEGY.md       # PowerShell development strategy
├── 📄 DIRECTORY-STRUCTURE.md           # PowerShell organization guide
├── 📄 ORGANIZATION-COMPLETE.md         # Organization completion status
├── 📄 POWERSHELL-7.5.2-CLASSIFICATION.md # PowerShell version compliance
├── 📄 BusBuddy.settings.ini           # PowerShell module settings
├── 📄 organize-files.ps1              # File organization script
│
├── 📂 Modules/                         # PowerShell modules (Microsoft 7.5.2 compliant)
│   ├── 📂 BusBuddy/                   # Main BusBuddy PowerShell module
│   │   ├── 📄 BusBuddy.psd1           # Module manifest with metadata
│   │   ├── 📄 BusBuddy.psm1           # Main module implementation (8812 lines)
│   │   └── 📄 Functions/              # Function categories (organized structure)
│   │
│   ├── 📂 BusBuddy.Testing/           # Phase 4 Testing infrastructure module ✅
│   │   ├── 📄 BusBuddy.Testing.psd1   # Testing module manifest
│   │   ├── 📄 BusBuddy.Testing.psm1   # Testing module implementation
│   │   ├── 📄 Initialize-BusBuddyTesting.ps1 # Module initialization
│   │   └── 📄 Profile-Integration.ps1  # PowerShell profile integration
│   │
│   ├── 📂 BusBuddy.ExceptionCapture/  # Exception handling module
│   │   ├── 📄 BusBuddy.ExceptionCapture.psd1 # Exception module manifest
│   │   └── 📄 BusBuddy.ExceptionCapture.psm1 # Exception handling implementation
│   │
│   └── 📄 XamlValidation.psm1         # XAML validation utilities
│
├── 📂 Testing/                        # Testing automation scripts ✅
│   ├── 📄 BusBuddy.Testing.psd1       # Testing module manifest (Phase 4)
│   ├── 📄 BusBuddy.Testing.psm1       # Testing module implementation (Phase 4)
│   ├── 📄 Run-Phase4-NUnitTests.ps1   # NUnit test automation
│   ├── 📄 Run-Phase4-NUnitTests-Modular.ps1 # Modular test execution
│   ├── 📄 Test-BusBuddyModularSetup.ps1 # Module setup validation
│   ├── 📄 Test-TerminalErrorCapture-Testing.ps1 # Error capture testing
│   ├── 📄 Test-XmlSyntax.ps1          # XML/XAML syntax validation
│   ├── 📄 Validate-Phase4-Completion.ps1 # Phase 4 completion validation
│   ├── 📄 validate-powershell-comprehensive.ps1 # PowerShell compliance validation
│   ├── 📄 validate-xml-files.ps1      # XML file validation
│   ├── 📄 verify-phase5-implementation.ps1 # Phase 5 preparation
│   ├── 📄 verify-terminal-setup.ps1   # Terminal configuration validation
│   └── 📄 Invoke-Phase4-TestIntegration.ps1 # Test integration automation
│
├── 📂 Build/                          # Build and deployment scripts
│   ├── 📄 Enhanced-Build-Workflow.ps1 # Advanced build automation ✅
│   ├── 📄 Self-Resolving-Build.ps1    # Self-resolving build script ✅
│   ├── 📄 build-busbuddy-simple.ps1   # Simple build script
│   ├── 📄 clean-and-restore.ps1       # Clean and restore operations
│   ├── 📄 deploy-azure-sql.ps1        # Azure SQL deployment
│   └── 📄 dotnet-install.ps1          # .NET SDK installation
│
├── 📂 Setup/                          # Environment setup scripts
│   ├── 📄 setup-localdb-standardized.ps1 # LocalDB setup (Microsoft compliant) ✅
│   ├── 📄 setup-localdb.ps1           # Legacy LocalDB setup
│   ├── 📄 setup-nodejs-and-mcp.ps1    # Node.js and MCP server setup
│   └── 📄 setup-working-mcp.ps1       # Working MCP configuration
│
├── 📂 Functions/                      # Categorized PowerShell functions
│   ├── 📂 Database/                   # Database management functions
│   │   ├── 📄 Database-Standardized.ps1 # Database operations ✅
│   │   └── 📄 DatabaseFunctions.ps1   # Additional database utilities
│   │
│   ├── 📂 Development/                # Development workflow functions
│   │   ├── 📄 Development-Standardized.ps1 # Development operations ✅
│   │   └── 📄 DevEnvironment.ps1      # Development environment setup
│   │
│   ├── 📂 Diagnostics/               # Diagnostic and monitoring functions
│   │   ├── 📄 Diagnostics-Standardized.ps1 # Diagnostic operations ✅
│   │   └── 📄 HealthCheck.ps1         # System health monitoring
│   │
│   └── 📂 Utilities/                 # General utility functions
│       ├── 📄 Utilities-Standardized.ps1 # Utility operations ✅
│       └── 📄 CommonUtilities.ps1     # Common helper functions
│
├── 📂 GitHub/                         # GitHub integration scripts
│   ├── 📄 BusBuddy-GitHub-Automation.ps1 # GitHub workflow automation
│   └── 📄 git-review.ps1             # Git review automation
│
├── 📂 Validation/                     # Validation and quality scripts
│   ├── 📄 Invoke-BusBuddyXamlValidation.ps1 # XAML validation ✅
│   └── 📄 Test-BusBuddyXamlComplete.ps1 # Complete XAML testing
│
├── 📂 Monitoring/                     # System monitoring scripts
│   ├── 📄 BusBuddy-Terminal-Flow-Monitor.ps1 # Terminal flow monitoring
│   └── 📄 run-with-error-capture.ps1  # Error capture monitoring
│
├── 📂 Analysis/                       # Code analysis and metrics
│   ├── 📄 analyze-duplicates.ps1      # Duplicate code analysis
│   └── 📄 PowerShell-Profile-Analysis-*.json # Profile analysis results
│
├── 📂 Tools/                          # Development tools and utilities
│   ├── 📄 smart-consolidate.ps1       # Smart file consolidation
│   └── 📄 Fix-File-Locks.ps1         # File lock resolution
│
├── 📂 Settings/                       # Configuration and settings
│   ├── 📄 Set-BusBuddyDatabaseProvider.ps1 # Database provider configuration
│   └── 📄 Initialize-BusBuddyConfiguration.ps1 # Configuration initialization
│
├── 📂 Maintenance/                    # Maintenance and cleanup scripts
│   ├── 📄 cleanup-temporary-files.ps1 # Temporary file cleanup
│   └── 📄 archive-old-logs.ps1       # Log file archival
│
├── 📂 Config/                         # Configuration files
│   ├── 📄 PesterConfig.xml           # Pester testing configuration
│   └── 📄 PowerShellSettings.json    # PowerShell environment settings
│
├── 📂 Documentation/                  # PowerShell documentation
│   ├── 📄 README.md                  # PowerShell usage guide
│   └── 📄 STANDARDIZATION-SUMMARY.md # Standardization completion summary
│
└── 📂 Core/                          # Core PowerShell modules (legacy structure)
    ├── 📄 BusBuddy-Main.psm1         # Main module initialization
    └── 📄 BusBuddy.ExceptionCapture.psm1 # Exception capture utilities
```

### **📚 Documentation & Standards**
```
Documentation/
├── 📄 README.md                       # Documentation overview
├── 📄 ACCESSIBILITY-STANDARDS.md      # Accessibility compliance guidelines
├── 📄 DATABASE-CONFIGURATION.md       # Database setup and configuration
├── 📄 FILE-FETCHABILITY-GUIDE.md      # File organization guide
├── 📄 GROK-4-UPGRADE-SUMMARY.md       # AI integration upgrade summary
├── 📄 MSB3027-File-Lock-Resolution-Guide.md # Build issue resolution
├── 📄 NUGET-CONFIG-REFERENCE.md       # NuGet configuration reference
├── 📄 ORGANIZATION-SUMMARY.md         # Project organization overview
├── 📄 PACKAGE-MANAGEMENT.md           # Package management guidelines
├── 📄 PHASE-2-IMPLEMENTATION-PLAN.md  # Phase 2 development plan
├── 📄 Phase2-Validation-Report.md     # Phase 2 validation results
├── 📄 Phase4-Implementation-Complete.md # Phase 4 completion documentation ✅
├── 📄 Phase4-Milestone-Report.md      # Phase 4 milestone achievements ✅
├── 📄 POWERSHELL-7.5-FEATURES.md      # PowerShell 7.5 feature usage
├── 📄 PowerShell-7.5.2-Reference.md   # PowerShell 7.5.2 comprehensive reference
├── 📄 PowerShell-Paging-Fix-Complete.md # PowerShell paging issue resolution
├── 📄 PowerShell-Profile-File-Lock-Management.md # Profile file management
└── 📄 tavily-api-usage-guide.md       # External API integration guide

Data/                                   # Application data and configuration
├── 📄 SampleData.sql                  # Sample database data
├── 📄 TestData.json                   # Test data for development
└── 📄 Configuration.json              # Application configuration templates

Archive/                                # Archived and legacy files
├── 📂 Legacy-Scripts/                 # Deprecated PowerShell scripts
├── 📂 Migration-Backups/             # Migration backup files
└── 📂 Old-Documentation/             # Archived documentation versions

Standards/                              # Development standards (moved to root)
└── 📂 Languages/                      # Language-specific standards (moved to root)
```

### **⚙️ Development Configuration**
```
.vscode/
├── 📄 settings.json                   # VS Code workspace settings
├── 📄 settings_backup.json           # VS Code settings backup
├── 📄 tasks.json                     # VS Code task automation
├── 📄 launch.json                    # Debug configuration
└── 📄 extensions.json                # Recommended VS Code extensions

TestResults/                           # Test execution results
├── 📄 *.trx                          # NUnit test result files
├── 📄 coverage.xml                   # Code coverage reports
└── 📄 Phase4-Test-Report.md          # Phase 4 testing reports

logs/                                  # Application log files
├── 📄 application-*.log               # Serilog application logs
├── 📄 error-*.log                    # Error-specific log files
└── 📄 performance-*.log              # Performance monitoring logs

mcp-servers/                           # Model Context Protocol servers
├── 📄 configuration.json             # MCP server configuration
└── 📄 server-implementations/        # Custom MCP implementations

tools/                                 # External development tools
├── 📄 database-tools/                # Database management utilities
├── 📄 build-tools/                   # Build automation tools
└── 📄 testing-tools/                 # Testing and validation tools

nuget/                                 # NuGet package management
└── 📄 nuget.exe                      # NuGet package manager executable
```

## 🚀 **Essential Commands**

### **Basic Development Commands**
```powershell
bb-health          # Project health check
bb-build           # Build solution
bb-run             # Run application
bb-clean           # Clean build artifacts
bb-dev-session     # Complete development session
```

### **Advanced NUnit Test Runner Integration** ✅ **NEW**
```powershell
# Basic Testing
bb-test                              # Run all tests
bb-test -TestSuite Unit              # Run unit tests only
bb-test -TestSuite Integration       # Run integration tests
bb-test -TestSuite Core              # Run core project tests

# Advanced Testing Features
bb-test-watch                        # Continuous testing with file monitoring
bb-test-watch -TestSuite Unit        # Watch mode for unit tests
bb-test-report                       # Generate comprehensive test report
bb-test-status                       # Check current test status
bb-test-init                         # Initialize test environment
bb-test-compliance                   # Validate PowerShell standards

# VS Code Integration
# Use Task Explorer: "🧪 BB: Phase 4 Modular Tests"
# Use Task Explorer: "🔄 BB: Phase 4 Test Watch"
```

### **PowerShell Module Functions** (BusBuddy.Testing)
```powershell
Start-BusBuddyTest -TestSuite All -Detailed
Start-BusBuddyTestWatch -TestSuite Core
New-BusBuddyTestReport
Get-BusBuddyTestStatus
Initialize-BusBuddyTestEnvironment
Test-BusBuddyCompliance
```

## Google Earth Integration

The project includes Google Earth Engine integration for route visualization:
- Geographic data visualization
- Route mapping and analysis
- Real-time map updates

## Contributing

Please follow the established coding standards and use the PowerShell development tools for consistency.

## License

[Add license information here]
