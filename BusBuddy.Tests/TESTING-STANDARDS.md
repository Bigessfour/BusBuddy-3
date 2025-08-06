# 🧪 BusBuddy Phase 2 Testing Standards

## **Global Testing Framework: NUnit + FluentAssertions**

This document establishes the **Phase 2 testing standards** for the BusBuddy project using **NUnit** and **FluentAssertions** exclusively.

## 🎯 **Testing Architecture**

### **Test Project Structure**
```
BusBuddy.Tests/              # Unit & Integration Tests (NUnit + FluentAssertions)
├── Core/                    # Business logic tests
├── ViewModels/             # ViewModel tests
├── Services/               # Service layer tests
├── Integration/            # Database & API integration tests
└── Utilities/              # Test utilities and helpers

BusBuddy.UITests/           # UI Automation Tests (NUnit + FluentAssertions + FlaUI)
├── Tests/                  # UI test implementations
├── PageObjects/            # Page object pattern
├── Builders/               # Test data builders
└── Utilities/              # UI test helpers
```

## 🔧 **NUnit Test Framework Standards**

### **Test Class Structure**
```csharp
using NUnit.Framework;
using FluentAssertions;

[TestFixture]
public class ExampleViewModelTests
{
    private ExampleViewModel _viewModel;
    private Mock<IExampleService> _mockService;

    [SetUp]
    public void SetUp()
    {
        // Arrange - Setup test dependencies
        _mockService = new Mock<IExampleService>();
        _viewModel = new ExampleViewModel(_mockService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // Cleanup - Dispose resources
        _viewModel?.Dispose();
    }

    [Test]
    public async Task LoadData_ShouldLoadDataSuccessfully()
    {
        // Arrange
        var expectedData = new List<ExampleModel> { /* test data */ };
        _mockService.Setup(s => s.GetDataAsync()).ReturnsAsync(expectedData);

        // Act
        await _viewModel.LoadDataAsync();

        // Assert
        _viewModel.Data.Should().BeEquivalentTo(expectedData);
        _viewModel.IsLoading.Should().BeFalse();
    }
}
```

### **Test Attributes (NUnit)**
- `[TestFixture]` - Test class marker
- `[Test]` - Individual test method
- `[SetUp]` - Runs before each test
- `[TearDown]` - Runs after each test
- `[OneTimeSetUp]` - Runs once before all tests in fixture
- `[OneTimeTearDown]` - Runs once after all tests in fixture
- `[Category("CategoryName")]` - Test categorization

## 💎 **FluentAssertions Standards**

### **Assertion Patterns**
```csharp
// Collections
result.Should().NotBeEmpty("because data should be loaded");
result.Should().HaveCount(5, "because we expect 5 items");
result.Should().Contain(x => x.Id == expectedId);

// Objects
result.Should().NotBeNull();
result.Should().BeOfType<ExpectedType>();
result.Should().BeEquivalentTo(expectedObject);

// Strings
result.Should().NotBeNullOrEmpty();
result.Should().StartWith("Expected");
result.Should().Contain("substring");

// Booleans
result.Should().BeTrue("because the operation should succeed");
result.Should().BeFalse("because validation should fail");

// Exceptions
var act = () => service.ThrowingMethod();
act.Should().Throw<ArgumentException>()
   .WithMessage("*expected message*");

// Async Operations
await act.Should().ThrowAsync<InvalidOperationException>();
```

### **Readable Test Messages**
Always include **because** clauses for clarity:
```csharp
// ✅ Good - Clear intention
result.Should().NotBeEmpty("because the dashboard should display driver data");

// ❌ Avoid - No context
result.Should().NotBeEmpty();
```

## 🏗️ **Test Categories & Organization**

### **Test Categories**
```csharp
[Test, Category("Unit")]
[Test, Category("Integration")]
[Test, Category("UI")]
[Test, Category("Performance")]
[Test, Category("Phase1")]
[Test, Category("Phase2")]
```

### **Test Method Naming**
Use **descriptive names** that explain the scenario:
```csharp
[Test]
public async Task LoadDrivers_WithValidDatabase_ShouldReturnDriverList()

[Test]
public async Task SaveDriver_WithInvalidData_ShouldThrowValidationException()

[Test]
public void Navigate_FromDashboardToDrivers_ShouldUpdateCurrentView()
```

## 🔄 **Test Data Management**

### **Test Builders Pattern**
```csharp
public class DriverTestBuilder
{
    private Driver _driver = new();

    public DriverTestBuilder WithName(string name)
    {
        _driver.Name = name;
        return this;
    }

    public DriverTestBuilder WithLicense(string license)
    {
        _driver.LicenseNumber = license;
        return this;
    }

    public Driver Build() => _driver;
}

// Usage
var driver = new DriverTestBuilder()
    .WithName("John Doe")
    .WithLicense("DL123456")
    .Build();
```

### **In-Memory Database Testing**
```csharp
[SetUp]
public void SetUp()
{
    var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;

    _context = new BusBuddyDbContext(options);
    SeedTestData();
}
```

## 🎭 **Mocking with Moq**

### **Service Mocking Standards**
```csharp
[SetUp]
public void SetUp()
{
    _mockService = new Mock<IDriverService>();
    _mockLogger = new Mock<ILogger<DriversViewModel>>();

    // Setup common mock behaviors
    _mockService.Setup(s => s.GetAllAsync())
               .ReturnsAsync(new List<Driver>());
}

[Test]
public async Task LoadDrivers_ShouldCallServiceOnce()
{
    // Act
    await _viewModel.LoadDriversAsync();

    // Assert
    _mockService.Verify(s => s.GetAllAsync(), Times.Once);
}
```

## 🚀 **Performance Testing**

### **Performance Assertions**
```csharp
[Test, Category("Performance")]
public async Task LoadLargeDataSet_ShouldCompleteWithinTimeLimit()
{
    // Arrange
    var stopwatch = Stopwatch.StartNew();

    // Act
    await _viewModel.LoadLargeDataSetAsync();

    // Assert
    stopwatch.Stop();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
        "because loading should complete within 2 seconds");
}
```

## 🔌 **Integration Testing**

### **Database Integration**
```csharp
[TestFixture, Category("Integration")]
public class DriverServiceIntegrationTests
{
    private BusBuddyDbContext _context;
    private DriverService _service;

    [SetUp]
    public void SetUp()
    {
        // Use real database or containerized test database
        _context = CreateTestContext();
        _service = new DriverService(_context);
    }
}
```

## 📱 **UI Testing with FlaUI**

### **UI Test Structure**
```csharp
[TestFixture, Category("UI")]
public class DashboardUITests
{
    private Application _app;
    private Window _mainWindow;

    [SetUp]
    public void SetUp()
    {
        _app = Application.Launch("BusBuddy.WPF.exe");
        _mainWindow = _app.GetMainWindow(Automation);
    }

    [Test]
    public void ClickDriversButton_ShouldNavigateToDriversView()
    {
        // Act
        var driversButton = _mainWindow.FindFirstDescendant(cf => cf.ByName("Drivers"));
        driversButton.Click();

        // Assert
        var driversView = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DriversView"));
        driversView.Should().NotBeNull("because the drivers view should be displayed");
    }
}
```

## 🎯 **Phase 2 Testing Priorities**

### **Testing Focus Areas**
1. **MVVM Pattern Validation** - ViewModels, Commands, Data Binding
2. **Business Logic Testing** - Core services and domain logic
3. **Data Access Testing** - Repository patterns and Entity Framework
4. **UI Workflow Testing** - Navigation and user interactions
5. **Integration Testing** - End-to-end scenarios
6. **Performance Testing** - Load times and responsiveness

### **Test Coverage Goals**
- **Unit Tests**: 80%+ coverage on business logic
- **Integration Tests**: All critical user workflows
- **UI Tests**: Primary navigation and data entry flows
- **Performance Tests**: All data loading operations

## 🛠️ **Testing Utilities**

### **Common Test Helpers**
```csharp
public static class TestHelpers
{
    public static BusBuddyDbContext CreateInMemoryContext(string dbName = null)
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new BusBuddyDbContext(options);
    }

    public static void SeedTestData(BusBuddyDbContext context)
    {
        // Add standard test data
        context.Drivers.AddRange(CreateTestDrivers());
        context.Vehicles.AddRange(CreateTestVehicles());
        context.SaveChanges();
    }
}
```

## 📋 **Test Execution Standards**

### **Running Tests**
```bash
# Run all tests
dotnet test

# Run specific categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
dotnet test --filter Category=Phase2

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### **Continuous Integration**
- All tests must pass before merge
- Coverage reports generated on each build
- Performance regression detection
- UI test execution in headless mode

---

## 🔄 **Migration from MSTest**

### **Attribute Mapping**
```csharp
// MSTest → NUnit
[TestClass]      → [TestFixture]
[TestMethod]     → [Test]
[TestInitialize] → [SetUp]
[TestCleanup]    → [TearDown]
[TestCategory]   → [Category]

// Assertions: MSTest → FluentAssertions
Assert.IsTrue(condition)        → condition.Should().BeTrue()
Assert.AreEqual(expected, actual) → actual.Should().Be(expected)
Assert.IsNotNull(obj)           → obj.Should().NotBeNull()
CollectionAssert.Contains(collection, item) → collection.Should().Contain(item)
```

This comprehensive testing standard ensures **consistent, maintainable, and readable tests** throughout the BusBuddy Phase 2 development process.
