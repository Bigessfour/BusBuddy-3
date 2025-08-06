# 🚌 BusBuddy Master Standards Document

## 📋 **Overview**
This document defines the master standards for the BusBuddy school transportation management system, ensuring consistency, quality, and maintainability across all development practices.

## 🎯 **Project Standards**

### **PowerShell 7.5.2 Compliance** ✅
- **Microsoft Standards**: Full compliance with [PowerShell 7.5.2 guidelines](https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/creating-profiles)
- **Multi-Threading**: Synchronized hashtable patterns for thread-safe operations
- **Progress Tracking**: Native `Write-Progress` support across parallel operations
- **Module Structure**: Clean 5,434-line module with 40+ specialized functions

### **.NET 9.0 Framework Standards** ✅
- **Target Framework**: .NET 9.0-windows (latest stable)
- **Language Features**: C# 13.0 with latest language features
- **Nullable Reference Types**: Enabled throughout the codebase
- **Code Analysis**: BusBuddy-Practical.ruleset with zero tolerance for warnings

### **Architecture Standards** ✅
- **Pattern**: MVVM (Model-View-ViewModel) with proper separation of concerns
- **UI Framework**: WPF with Syncfusion Essential Studio 30.1.40
- **Database**: Entity Framework Core 9.0.7 with SQL Server LocalDB
- **Logging**: Serilog 4.0.2 with structured logging and enrichers

### **Code Quality Standards** ✅
- **Build Errors**: Zero tolerance - all builds must succeed without errors
- **Build Warnings**: Zero tolerance - all code analysis warnings must be resolved
- **Test Coverage**: Minimum 80% code coverage across all projects
- **Documentation**: XML documentation required for all public APIs

### **Security Standards** ✅
- **Secrets Management**: No hardcoded connection strings or sensitive data
- **Environment Variables**: Use for all sensitive configuration
- **Input Validation**: Comprehensive validation at all layers
- **SQL Injection Prevention**: Parameterized queries and EF Core patterns only

### **Performance Standards** ✅
- **Build Time**: Target under 3 seconds for full solution build
- **Async Operations**: Use async/await for all I/O operations
- **Memory Management**: Proper disposal patterns and resource management
- **Database Optimization**: Lazy loading and query optimization

## 📁 **Directory Structure Standards**

### **Solution Organization**
```
BusBuddy/
├── BusBuddy.Core/          # Business logic and data access
├── BusBuddy.WPF/           # User interface and ViewModels
├── BusBuddy.Tests/         # Unit and integration tests
├── BusBuddy.UITests/       # Automated UI testing
├── Standards/              # Project standards documentation
├── Documentation/          # Technical documentation
├── PowerShell/             # PowerShell 7.5.2 environment
└── Scripts/                # Build and automation scripts
```

### **Naming Conventions**
- **Classes**: PascalCase (e.g., `VehicleManagementService`)
- **Methods**: PascalCase (e.g., `GetDriverDetailsAsync`)
- **Properties**: PascalCase (e.g., `DriverName`)
- **Fields**: camelCase with underscore prefix (e.g., `_logger`)
- **Constants**: PascalCase (e.g., `MaxRetryAttempts`)

## 🧪 **Testing Standards**

### **Test Categories**
- **Unit Tests**: Component-level testing for business logic
- **Integration Tests**: Database and service integration validation
- **UI Tests**: Automated user interface testing with Syncfusion controls
- **Performance Tests**: Load testing and performance benchmarks

### **Test Naming**
- **Pattern**: `MethodName_StateUnderTest_ExpectedBehavior`
- **Example**: `GetDriver_WithValidId_ReturnsDriver`

## 🔧 **Development Workflow Standards**

### **Git Workflow**
- **Branching**: Feature branches with descriptive names
- **Commits**: Conventional commit format (`type(scope): description`)
- **Pull Requests**: Required for all changes to main branch
- **Code Review**: Mandatory peer review before merge

### **Build Process**
- **Continuous Integration**: GitHub Actions workflows for all branches
- **Quality Gates**: Build, test, security scan, and standards validation
- **Deployment**: Automated deployment to staging and production environments

## 📚 **Documentation Standards**

### **Required Documentation**
- **XML Documentation**: All public APIs and complex internal methods
- **README Files**: Comprehensive setup and usage instructions
- **Architecture Decisions**: Document significant architectural choices
- **API Documentation**: Auto-generated from XML comments

### **Documentation Format**
- **Markdown**: Primary format for all documentation
- **Code Comments**: Clear, concise explanations of complex logic
- **Inline Documentation**: JSDoc-style comments for important functions

## 🚀 **Deployment Standards**

### **Environment Management**
- **Development**: Local development with SQLite/LocalDB
- **Staging**: Azure environment with full production simulation
- **Production**: Secure Azure deployment with monitoring

### **Configuration Management**
- **Environment-Specific**: Separate configuration for each environment
- **Secret Management**: Azure Key Vault for production secrets
- **Feature Flags**: Configurable feature toggles for gradual rollouts

## 📊 **Monitoring and Logging Standards**

### **Logging Requirements**
- **Structured Logging**: Serilog with consistent message templates
- **Log Levels**: Appropriate use of Debug, Info, Warning, Error, Fatal
- **Correlation IDs**: Track requests across service boundaries
- **Performance Logging**: Monitor critical operation timings

### **Monitoring**
- **Application Insights**: Comprehensive telemetry and monitoring
- **Health Checks**: Automated health monitoring and alerting
- **Performance Metrics**: Track key performance indicators
- **Error Tracking**: Comprehensive error logging and analysis

---

**Document Version**: 1.0  
**Last Updated**: July 30, 2025  
**Next Review**: Monthly review cycle  
**Maintained By**: BusBuddy Development Team
