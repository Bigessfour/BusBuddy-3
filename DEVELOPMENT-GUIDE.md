# 🛠️ BusBuddy Development Guide

**Comprehensive guide for BusBuddy development practices, patterns, and workflows**

## 🎯 **Development Philosophy**

### **MVP-First Approach**

BusBuddy follows a clean build, MVP-first development strategy:

- **Primary Goal**: Maintain 0 build errors at all times
- **MVP Focus**: Student management and route assignment core functionality
- **Quality Gates**: All changes must pass `bbMvpCheck` and `bbAntiRegression`

### **Technology Stack**

- **Framework**: .NET 9.0 WPF with C# 12
- **UI Library**: Syncfusion WPF 30.1.42 (FluentDark theme)
- **Database**: Entity Framework Core 9.0.7 with Azure SQL/LocalDB
- **Logging**: Serilog with structured logging
- **Automation**: PowerShell 7.5.2 with custom modules

## 🏗️ **Architecture Overview**

### **Project Structure**

```
BusBuddy/
├── BusBuddy.Core/           # Business logic and data access
│   ├── Models/              # Domain entities
│   ├── Services/            # Business services
│   ├── Data/                # EF Core contexts
│   └── Interfaces/          # Service contracts
├── BusBuddy.WPF/            # Presentation layer
│   ├── Views/               # XAML views
│   ├── ViewModels/          # MVVM view models
│   ├── Resources/           # Styles and themes
│   └── Utilities/           # UI helpers
├── BusBuddy.Tests/          # Test suite
└── PowerShell/              # Automation scripts
    └── Modules/BusBuddy/    # PowerShell commands
```

### **MVVM Pattern**

```csharp
// ViewModel Pattern
public class StudentsViewModel : BaseViewModel
{
    private readonly IStudentService _studentService;
    private ObservableCollection<Student> _students = new();

    public StudentsViewModel(IStudentService studentService)
    {
        _studentService = studentService;
        LoadStudentsCommand = new RelayCommand(async () => await LoadStudentsAsync());
    }

    public ObservableCollection<Student> Students
    {
        get => _students;
        set => SetProperty(ref _students, value);
    }

    public RelayCommand LoadStudentsCommand { get; }

    private async Task LoadStudentsAsync()
    {
        try
        {
            var students = await _studentService.GetStudentsAsync();
            Students.Clear();
            foreach (var student in students)
                Students.Add(student);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load students");
            ShowError($"Failed to load students: {ex.Message}");
        }
    }
}
```

## 🎨 **UI Development Standards**

### **Syncfusion Control Usage**

Always use Syncfusion controls over standard WPF controls:

```xml
<!-- ✅ CORRECT: Syncfusion SfDataGrid -->
<syncfusion:SfDataGrid ItemsSource="{Binding Students}"
                       SelectedItem="{Binding SelectedStudent}"
                       AutoGenerateColumns="False"
                       AllowSorting="True"
                       AllowFiltering="True">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn HeaderText="Student ID" MappingName="StudentNumber"/>
        <syncfusion:GridTextColumn HeaderText="Name" MappingName="StudentName"/>
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>

<!-- ❌ WRONG: Standard DataGrid -->
<DataGrid ItemsSource="{Binding Students}">
    <!-- Never use standard WPF controls -->
</DataGrid>
```

### **Theme Integration**

```xml
<!-- Resource Dictionary Integration -->
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <syncfusion:FluentDarkThemeSettings x:Key="FluentDarkTheme"/>
        <ResourceDictionary Source="/BusBuddy.WPF;component/Resources/Styles.xaml"/>
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
```

## 📝 **Coding Standards**

### **C# Standards**

```csharp
// ✅ Proper nullable handling
public async Task<Student?> GetStudentAsync(int? studentId)
{
    if (!studentId.HasValue)
        return null;

    return await _context.Students
        .FirstOrDefaultAsync(s => s.Id == studentId.Value);
}

// ✅ Structured logging with Serilog
private static readonly ILogger Logger = Log.ForContext<StudentService>();

public async Task CreateStudentAsync(CreateStudentRequest request)
{
    Logger.Information("Creating student {StudentName}", request.Name);

    try
    {
        var student = new Student
        {
            Name = request.Name,
            CreatedDate = DateTime.UtcNow
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        Logger.Information("Student {StudentName} created with ID {StudentId}",
            student.Name, student.Id);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to create student {StudentName}", request.Name);
        throw;
    }
}
```

### **PowerShell Standards**

```powershell
# ✅ CORRECT: Use proper output streams
function Get-BuildStatus {
    [CmdletBinding()]
    param()

    Write-Information "Checking build status..." -InformationAction Continue

    try {
        $result = dotnet build --no-restore
        if ($LASTEXITCODE -eq 0) {
            Write-Output "Build succeeded"
        } else {
            Write-Error "Build failed with exit code $LASTEXITCODE"
        }
    }
    catch {
        Write-Error "Build check failed: $($_.Exception.Message)"
    }
}

# ❌ WRONG: Using Write-Host
function Bad-Example {
    Write-Host "This violates PowerShell standards" # Never use Write-Host
}
```

## 🚀 **Development Workflow**

### **Daily Development Session**

```powershell
# Start development environment
bbDevSession

# This automatically:
# 1. Loads PowerShell modules
# 2. Configures environment
# 3. Runs health checks
# 4. Sets up debugging
```

### **Feature Development Process**

1. **Start with Health Check**

    ```powershell
    bbHealth              # Verify system state
    bbMvpCheck           # Ensure MVP baseline
    ```

2. **Make Changes**
    - Follow Syncfusion-only UI patterns
    - Use proper nullable reference types
    - Implement structured logging

3. **Validate Changes**

    ```powershell
    bbBuild              # Verify build
    bbXamlValidate       # Check XAML compliance
    bbAntiRegression     # Prevent regressions
    bbTest               # Run tests
    ```

4. **Final Verification**
    ```powershell
    bbMvpCheck           # Must show "MVP READY!"
    ```

### **Commit Standards**

```bash
# Commit message format
git commit -m "feat: add student search functionality

- Implement search filter in StudentsViewModel
- Add search TextBox to StudentsView
- Update SfDataGrid with AllowFiltering=True
- Add structured logging for search operations

Fixes #123"
```

## 🔍 **Quality Assurance**

### **Anti-Regression Checks**

```powershell
# Mandatory checks before commits
bbAntiRegression     # Scans for:
                     # - Microsoft.Extensions.Logging usage (forbidden)
                     # - Standard WPF controls (use Syncfusion instead)
                     # - Write-Host in PowerShell (use proper streams)

bbXamlValidate       # Ensures:
                     # - Only Syncfusion controls in XAML
                     # - Proper namespace declarations
                     # - FluentDark theme consistency
```

### **Code Quality Gates**

1. **Build**: Must be 0 errors, warnings acceptable during MVP
2. **MVP Check**: Must pass all essential functionality tests
3. **Anti-Regression**: Must not introduce forbidden patterns
4. **XAML Validation**: Must use Syncfusion controls consistently

## 🧪 **Testing Practices**

### **Test Categories**

```csharp
// Unit Tests
[Test]
public async Task CreateStudent_ValidData_ReturnsStudent()
{
    // Arrange
    var service = new StudentService(_mockContext.Object, _mockLogger.Object);
    var request = new CreateStudentRequest { Name = "John Doe" };

    // Act
    var result = await service.CreateStudentAsync(request);

    // Assert
    Assert.That(result.Name, Is.EqualTo("John Doe"));
    Assert.That(result.Id, Is.GreaterThan(0));
}

// Integration Tests
[Test]
public async Task StudentsView_LoadData_DisplaysInGrid()
{
    // Test UI integration with real data
}
```

### **Running Tests**

```powershell
# Run tests (note: .NET 9 compatibility issues)
bbTest

# Alternative: Use VS Code NUnit Test Runner extension
# Or Visual Studio Test Explorer
```

## 📊 **Data Access Patterns**

### **Entity Framework Standards**

```csharp
// Service Pattern with EF Core
public class StudentService : IStudentService
{
    private readonly BusBuddyContext _context;
    private readonly ILogger<StudentService> _logger;

    public StudentService(BusBuddyContext context, ILogger<StudentService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<Student>> GetStudentsAsync()
    {
        try
        {
            return await _context.Students
                .Where(s => s.IsActive)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve students");
            throw;
        }
    }
}
```

### **Database Configuration**

```csharp
// Context Configuration
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    var provider = _configuration["DatabaseProvider"];
    var connectionString = provider switch
    {
        "Azure" => _configuration.GetConnectionString("BusBuddyDb"),
        "LocalDB" => _configuration.GetConnectionString("DefaultConnection"),
        _ => _configuration.GetConnectionString("DefaultConnection")
    };

    optionsBuilder.UseSqlServer(connectionString);
}
```

## 🐛 **Debugging and Diagnostics**

### **Debug Helper Integration**

```powershell
# Start debug monitoring
bbDebugStart

# Export debug data
bbDebugExport

# Monitor health in real-time
bbHealth --watch
```

### **Logging Configuration**

```csharp
// Serilog setup in Program.cs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/busbuddy-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://localhost:5341")
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .CreateLogger();
```

### **AI Configuration (xAI Grok-4)**

BusBuddy integrates with xAI Grok-4 for route optimization and intelligent analysis:

#### **API Key Setup**
```powershell
# Set machine environment variable (required for development)
$env:XAI_API_KEY = "your-xai-api-key-here"
[System.Environment]::SetEnvironmentVariable("XAI_API_KEY", "your-xai-api-key-here", "Machine")

# Verify configuration
Import-Module ".\PowerShell\Modules\grok-config.psm1" -Force
$apiKey = Get-ApiKeySecurely
Write-Information "API Key Length: $($apiKey.Length)"  # Should be 84
```

#### **Model Configuration**
```powershell
# Current production settings (August 2025)
$GrokConfig = @{
    DefaultModel = "grok-4-0709"       # Exact model ID required
    BaseUrl = "https://api.x.ai/v1"    # xAI API endpoint  
    MaxTokens = 4000                   # Response token limit
    Temperature = 0.3                  # Balanced creativity/consistency
    TimeoutSeconds = 60                # Request timeout
}
```

#### **Testing AI Integration**
```powershell
# Test API connectivity
Test-GrokConnection -Verbose
# Expected: "✅ Grok API connection successful."

# Test route analysis
grok-route-analysis -RouteData $routeData -OptimizationGoal "minimize-time"

# Test maintenance predictions  
grok-maintenance-forecast -VehicleData $vehicleData -PredictionWindow "30-days"
```

#### **Troubleshooting AI Issues**
```powershell
# Diagnostic check for common issues
$apiKey = Get-ApiKeySecurely
if ($apiKey.Length -ne 84) {
    Write-Warning "API key length incorrect: $($apiKey.Length). Expected: 84"
}

$config = grok-config
if ($config.DefaultModel -ne "grok-4-0709") {
    Write-Warning "Incorrect model: $($config.DefaultModel). Expected: grok-4-0709"
}

# Fix common vault sync issues
if ($env:XAI_API_KEY -ne $apiKey) {
    Set-Secret -Name "XAI_API_KEY" -Secret $env:XAI_API_KEY -Vault GlobalApiSecrets
    Write-Information "✅ Vault updated with environment key"
}
```

## 🔧 **Performance Optimization**

### **UI Performance**

- Use data virtualization for large lists
- Implement async data loading
- Use background threads for heavy operations

### **Database Performance**

- Use EF Core query optimization
- Implement proper indexing
- Use projection for large datasets

## 📚 **Documentation Standards**

### **Code Documentation**

```csharp
/// <summary>
/// Creates a new student record in the database
/// </summary>
/// <param name="request">Student creation data including name, grade, etc.</param>
/// <returns>The created student with assigned ID</returns>
/// <exception cref="ArgumentNullException">Thrown when request is null</exception>
public async Task<Student> CreateStudentAsync(CreateStudentRequest request)
```

### **PowerShell Documentation**

```powershell
<#
.SYNOPSIS
    Validates the current build state and MVP readiness

.DESCRIPTION
    Performs comprehensive checks including:
    - Build compilation status
    - Essential functionality tests
    - Data access validation
    - UI component verification

.EXAMPLE
    bbMvpCheck

.NOTES
    Must return "MVP READY! You can ship this!" for deployment
#>
function Invoke-MvpCheck {
    # Implementation
}
```

## 🚦 **Error Handling Patterns**

### **Service Layer Errors**

```csharp
public async Task<Result<Student>> CreateStudentAsync(CreateStudentRequest request)
{
    try
    {
        // Validation
        if (request == null)
            return Result<Student>.Failure("Request cannot be null");

        // Implementation
        var student = new Student { Name = request.Name };
        await _context.SaveChangesAsync();

        return Result<Student>.Success(student);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create student");
        return Result<Student>.Failure($"Creation failed: {ex.Message}");
    }
}
```

## 🎯 **MVP Requirements**

### **Core Functionality**

- ✅ Student management (CRUD operations)
- ✅ Route assignment
- ✅ Basic dashboard
- ✅ Data persistence

### **Deferred Features**

- XAI integration
- Google Earth Engine
- Advanced reporting
- Vehicle management
- Driver scheduling
- Maintenance tracking

## 🛠️ **Available Commands**

### **Core Development**

- `bbBuild` - Build solution
- `bbRun` - Run application
- `bbTest` - Execute tests
- `bbHealth` - System health check
- `bbClean` - Clean build artifacts

### **Quality Assurance**

- `bbXamlValidate` - Validate XAML files
- `bbAntiRegression` - Run compliance checks
- `bbMvpCheck` - Verify MVP readiness

### **Advanced Workflows**

- `bbDevSession` - Complete dev environment setup
- `bbQuickTest` - Rapid build-test cycle
- `bbDiagnostic` - Comprehensive system analysis
- `bbReport` - Generate project status report

## 🆘 **Troubleshooting**

### **Common Issues**

1. **Build Failures**: Run `bbHealth` for diagnostics
2. **Test Issues**: Use VS Code NUnit Test Runner for .NET 9
3. **UI Problems**: Verify Syncfusion namespace declarations
4. **Database Errors**: Check connection strings and migrations

### **Getting Help**

- Run `bbCommands` for available automation
- Check `GROK-README.md` for current status
- Use `bbHealth` for diagnostic information
- Review logs in `logs/` directory
