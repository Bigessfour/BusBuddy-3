# 🧪 BusBuddy Testing Standards

## **Global Testing Framework: NUnit + FluentAssertions**

This document establishes the unified testing standards for the BusBuddy project using **NUnit** and **FluentAssertions** exclusively.

**Official Documentation References:**
- NUnit Framework: https://docs.nunit.org/
- .NET Testing: https://learn.microsoft.com/en-us/dotnet/core/testing/
- FluentAssertions: https://fluentassertions.com/

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
[Test, Category("AzureIntegration")]
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

### **SQLite Database Testing** (Replaces InMemory for Integration)

     ```csharp
     [SetUp]
     public void SetUp()
     {
         var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
             .UseSqlite("Filename=:memory:")  // Or "Filename=test.db" for file-based
             .Options;

         _context = new BusBuddyDbContext(options);
         _context.Database.OpenConnection();  // Required for in-memory SQLite transactions
         _context.Database.EnsureCreated();   // Create schema
         SeedTestData();
     }

     [TearDown]
     public void TearDown()
     {
         _context.Database.CloseConnection();  // Clean up
         _context.Dispose();
     }
     ```
     - **Why SQLite?**: Supports transactions (e.g., `using var transaction = _context.Database.BeginTransaction();`), relationships, and raw SQL—removing InMemory restrictions. For Azure SQL-specific tests, add a conditional to use a real connection string via secrets.
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

### **Azure SQL Integration**

```csharp
[TestFixture, Category("AzureIntegration")]
public class AzureSqlIntegrationTests
{
    private BusBuddyDbContext _context;
    private SqlConnection _connection;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Use Azure SQL Database for integration tests
        var connectionString = "Your Azure SQL connection string here";
        _connection = new SqlConnection(connectionString);
        _connection.Open();

        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseSqlServer(_connection)
            .Options;

        _context = new BusBuddyDbContext(options);
        _context.Database.EnsureCreated();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _context.Database.EnsureDeleted();
        _connection.Close();
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

## 🎯 **Testing Priorities**

Focus on:

1. **MVVM Pattern Validation** - ViewModels, Commands, Data Binding
2. **Business Logic Testing** - Core services and domain logic
3. **Data Access Testing** - Repository patterns and Entity Framework
4. **UI Workflow Testing** - Navigation and user interactions
5. **Integration Testing** - End-to-end scenarios
6. **Performance Testing** - Load times and responsiveness

Coverage Goals:

- **Unit Tests**: 80%+ coverage on business logic (target)
- **Integration Tests**: All critical user workflows
- **UI Tests**: Primary navigation and data entry flows
- **Performance Tests**: Critical data loading operations

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
dotnet test --filter Category=AzureIntegration

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### **Continuous Integration**

- All tests must pass before merge
- Coverage reports generated on each build
- Performance regression detection
- UI test execution in headless mode

### **Parallel Testing Guidelines**

- Use `[NonParallelizable]` on tests with shared state (e.g., file-based SQLite).
- Monitor for flakiness in Syncfusion UI tests; fallback to sequential if needed.
- In ci.yml, add `--blame` for diagnostics.

## 🔄 **Flaky Test Handling**

### **Flaky Test Definition**

Flaky tests are tests that **pass and fail intermittently** without code changes, often due to:

- Race conditions
- Timing issues
- External dependencies
- Resource contention
- Non-deterministic behavior

### **Flaky Test Detection**

#### **Test Configuration**

```xml
<!-- testsettings.runsettings -->
<FlakyTestSettings>
  <Enabled>true</Enabled>
  <MaxRetries>3</MaxRetries>
  <RetryDelay>1000</RetryDelay>
  <DetectPatterns>
    <Pattern>TimeoutException</Pattern>
    <Pattern>ThreadAbortException</Pattern>
    <Pattern>IOException</Pattern>
    <Pattern>WebException</Pattern>
  </DetectPatterns>
  <ExcludeCategories>
    <Category>IntegrationTest</Category>
    <Category>DatabaseTest</Category>
  </ExcludeCategories>
</FlakyTestSettings>
```

#### **Retry Attributes**

```csharp
[Test]
[Retry(3)] // NUnit Retry attribute
public async Task UnstableNetworkCall_ShouldRetryOnFailure()
{
    // Test that might be flaky due to network issues
}

[Test]
[NonParallelizable] // Prevent parallel execution
[Category("Flaky")]
public void DatabaseOperation_ShouldNotInterfereWithOtherTests()
{
    // Test that modifies shared database state
}
```

### **Flaky Test Mitigation Strategies**

#### **1. Stabilize Test Environment**

```csharp
[SetUp]
public void SetUp()
{
    // Ensure clean state for each test
    ResetDatabase();
    ClearCache();
    ResetMocks();
}

[TearDown]
public void TearDown()
{
    // Clean up after each test
    DisposeResources();
    ResetGlobalState();
}
```

#### **2. Handle Timing Issues**

```csharp
[Test]
public async Task AsyncOperation_ShouldCompleteWithinTimeout()
{
    // Use proper timeouts
    var timeout = TimeSpan.FromSeconds(30);
    var result = await _service.ProcessAsync()
        .WaitAsync(timeout);

    result.Should().NotBeNull();
}

[Test]
public void UIUpdate_ShouldEventuallyReflectChanges()
{
    // Wait for UI to update
    var maxAttempts = 10;
    for (int i = 0; i < maxAttempts; i++)
    {
        if (_viewModel.IsLoading == false) break;
        Thread.Sleep(100);
    }

    _viewModel.IsLoading.Should().BeFalse();
}
```

#### **3. Isolate External Dependencies**

```csharp
[Test]
public async Task ExternalServiceCall_ShouldHandleNetworkFailures()
{
    // Mock external services
    _mockHttpMessageHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ThrowsAsync(new HttpRequestException("Network error"))
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

    // Test retry logic
    await _service.CallExternalServiceAsync();
}
```

#### **4. Database Test Isolation**

```csharp
[Test]
[NonParallelizable]
public async Task DatabaseOperation_ShouldNotAffectOtherTests()
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        // Perform database operations
        var driver = new Driver { Name = "Test Driver" };
        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();

        // Assert
        driver.Id.Should().BeGreaterThan(0);
    }
    finally
    {
        await transaction.RollbackAsync();
    }
}
```

### **CI/CD Flaky Test Handling**

#### **Retry Configuration in CI**

```yaml
- name: Run Tests with Retry
  run: |
      $attempts = 2
      $success = $false
      for ($i = 1; $i -le $attempts; $i++) {
        Write-Output "Test attempt $i of $attempts"
        dotnet test --logger "trx;LogFileName=test-results.trx"
        if ($LASTEXITCODE -eq 0) {
          $success = $true
          break
        }
        Start-Sleep -Seconds 10
      }
```

#### **Flaky Test Reporting**

```yaml
- name: Analyze Test Flakiness
  if: always()
  run: |
      # Parse test results for patterns
      $testResults = Get-ChildItem -Recurse "*.trx"
      foreach ($result in $testResults) {
        $content = Get-Content $result.FullName -Raw
        $failedTests = [regex]::Matches($content, 'testName="([^"]*)"[^>]*outcome="Failed"')
        if ($failedTests.Count -gt 0) {
          Write-Host "⚠️ Failed tests detected - potential flakiness"
          foreach ($match in $failedTests) {
            Write-Host "  - $($match.Groups[1].Value)"
          }
        }
      }
```

### **Flaky Test Best Practices**

#### **Avoid Common Causes**

- **Race Conditions**: Use proper synchronization
- **Timing Dependencies**: Use event-driven approaches
- **Shared State**: Isolate test data and resources
- **External Services**: Mock or stub external dependencies
- **UI Timing**: Wait for UI elements properly

#### **Test Categories for Flakiness**

```csharp
[Test, Category("Flaky")]
public void NetworkDependentTest_ShouldRetryOnFailure()
{
    // Tests that may be flaky due to network issues
}

[Test, Category("Stable")]
public void PureLogicTest_ShouldAlwaysPass()
{
    // Tests that should never be flaky
}
```

#### **Monitoring and Alerting**

- Track flaky test frequency
- Set up alerts for increased flakiness
- Review flaky tests regularly
- Consider quarantining consistently flaky tests

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
