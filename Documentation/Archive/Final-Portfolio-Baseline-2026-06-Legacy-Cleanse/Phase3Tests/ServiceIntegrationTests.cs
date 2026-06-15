using NUnit.Framework;
using FluentAssertions;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Services; // Add missing WPF service namespace
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Tests.IntegrationTests;

/// <summary>
/// Integration tests for service layer functionality
/// Tests real service interactions and data flow
/// </summary>
[TestFixture]
public class ServiceIntegrationTests : IDisposable
{
    private IDataIntegrityService? _dataIntegrityService;
    private XAIChatService? _xaiChatService;
    private BusBuddyDbContext? _dbContext;

    [SetUp]
    public void SetUp()
    {
        // Setup in-memory EF Core context for integration tests
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new BusBuddyDbContext(options);

        // Create mock services for DataIntegrityService
        var mockRouteService = new Mock<IRouteService>();
        var mockDriverService = new Mock<IDriverService>();
        var mockBusService = new Mock<IBusService>();
        var mockActivityService = new Mock<IActivityService>();
        var mockStudentService = new Mock<IStudentService>();

        _dataIntegrityService = new DataIntegrityService(
            mockRouteService.Object,
            mockDriverService.Object,
            mockBusService.Object,
            mockActivityService.Object,
            mockStudentService.Object);

        _xaiChatService = new XAIChatService();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    [Category("Integration")]
    [Category("DataFlow")]
    public async Task Services_ShouldIntegrateWithDatabaseContext()
    {
        // Arrange
        _dbContext.Should().NotBeNull("Test context should be available");
        var initialDriverCount = _dbContext!.Drivers.Count();

        // Act - Add new driver through context
        var newDriver = new Driver
        {
            DriverId = 999,
            DriverName = "Integration Test Driver",
            Status = "Active",
            LicenseNumber = "INT999"
        };

        _dbContext.Drivers.Add(newDriver);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updatedCount = _dbContext.Drivers.Count();
        updatedCount.Should().Be(initialDriverCount + 1, "Driver count should increase");

        var retrievedDriver = _dbContext.Drivers.Find(999);
        retrievedDriver.Should().NotBeNull("New driver should be retrievable");
        retrievedDriver!.DriverName.Should().Be("Integration Test Driver");
    }

    [Test]
    [Category("Integration")]
    [Category("XAIChat")]
    public async Task XAIChatService_ShouldIntegrateWithSystemProperly()
    {
        // Act
        await _xaiChatService!.InitializeAsync();
        var isAvailable = await _xaiChatService.IsAvailableAsync();

        // Assert
        isAvailable.Should().BeTrue("XAI Chat service should be available");

        // Test various query types
        var fleetResponse = await _xaiChatService.GetResponseAsync("Show fleet status");
        var driverResponse = await _xaiChatService.GetResponseAsync("How many drivers are active?");
        var emergencyResponse = await _xaiChatService.GetResponseAsync("Emergency situation!");

        fleetResponse.Should().Contain("fleet", "Should provide fleet information");
        driverResponse.Should().Contain("driver", "Should provide driver information");
        emergencyResponse.Should().Contain("Emergency", "Should handle emergency queries");
    }

    [Test]
    [Category("Integration")]
    [Category("Performance")]
    public async Task ServiceOperations_ShouldCompleteWithinPerformanceTargets()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var operations = new List<Task>();

        // Act - Execute multiple service operations concurrently
        operations.Add(_xaiChatService!.GetResponseAsync("Fleet status"));
        operations.Add(_xaiChatService.GetResponseAsync("Driver information"));
        operations.Add(_xaiChatService.GetResponseAsync("Vehicle data"));

        await Task.WhenAll(operations);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            $"All operations should complete within 10 seconds. Actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    [Category("Integration")]
    [Category("ErrorHandling")]
    public async Task Services_ShouldHandleErrorsGracefully()
    {
        // Act & Assert - XAI Chat Service error handling
        var nullResponse = await _xaiChatService!.GetResponseAsync(null!);
        nullResponse.Should().NotBeNullOrEmpty("Should handle null input gracefully");
        nullResponse.Should().Contain("trouble", "Should indicate processing issue");

        var emptyResponse = await _xaiChatService.GetResponseAsync(string.Empty);
        emptyResponse.Should().NotBeNullOrEmpty("Should handle empty input gracefully");
    }

    [Test]
    [Category("Integration")]
    [Category("BusinessLogic")]
    public async Task Services_ShouldEnforceBusinessRules()
    {
        // Arrange - Create driver with expiring license
        var expiringDriver = new Driver
        {
            DriverId = 888,
            DriverName = "Expiring License Driver",
            Status = "Active",
            LicenseNumber = "EXP888",
            LicenseExpiryDate = DateTime.Now.AddDays(15) // Expires soon
        };

        _dbContext!.Drivers.Add(expiringDriver);
        await _dbContext.SaveChangesAsync();

        // Act - Query for drivers with expiring licenses
        var driversWithExpiringLicenses = _dbContext.Drivers
            .Where(d => d.LicenseExpiryDate < DateTime.Now.AddDays(30))
            .ToList();

        // Assert
        driversWithExpiringLicenses.Should().Contain(d => d.DriverId == 888,
            "Should identify driver with expiring license");
    }

    [Test]
    [Category("Integration")]
    [Category("DataValidation")]
    public async Task Services_ShouldValidateDataIntegrity()
    {
        // Arrange - Create data with integrity issues
        var uniqueId = 99999; // Use high ID to avoid conflicts with seeded data
        var driverWithoutName = new Driver
        {
            DriverId = uniqueId,
            DriverName = "", // This will trigger auto-generation in the model
            Status = "Active",
            LicenseNumber = $"BAD{uniqueId}"
        };

        // Act - Attempt to add data with originally empty name
        _dbContext!.Drivers.Add(driverWithoutName);
        await _dbContext.SaveChangesAsync();

        // Assert - Validate business logic behavior
        var savedDriver = _dbContext.Drivers.Find(uniqueId);
        savedDriver.Should().NotBeNull("Driver should be in database");

        // The Driver model automatically generates a name when empty is provided
        savedDriver!.DriverName.Should().Be($"Driver-{uniqueId}",
            "Driver model should auto-generate name for empty values");

        // Test that the original intent (empty name) was handled by business logic
        var wasOriginallyEmpty = savedDriver.DriverName.StartsWith("Driver-");
        wasOriginallyEmpty.Should().BeTrue("Auto-generated name indicates original was empty");
    }

    [Test]
    [Category("Integration")]
    [Category("ConcurrentAccess")]
    public async Task Services_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var tasks = new List<Task<string>>();

        // Act - Make concurrent XAI Chat requests
        for (int i = 0; i < 5; i++)
        {
            var query = $"Query {i}: Fleet status";
            tasks.Add(_xaiChatService!.GetResponseAsync(query));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(5, "Should handle all concurrent requests");
        responses.Should().OnlyContain(r => !string.IsNullOrEmpty(r), "All responses should be valid");
    }

    [Test]
    [Category("Integration")]
    [Category("ServiceLifecycle")]
    public async Task Services_ShouldMaintainStateCorrectly()
    {
        // Act - Initialize service
        await _xaiChatService!.InitializeAsync();
        var initialAvailability = await _xaiChatService.IsAvailableAsync();

        // Make some requests
        await _xaiChatService.GetResponseAsync("Test query 1");
        await _xaiChatService.GetResponseAsync("Test query 2");

        // Check availability again
        var finalAvailability = await _xaiChatService.IsAvailableAsync();

        // Assert
        initialAvailability.Should().BeTrue("Service should be available after initialization");
        finalAvailability.Should().BeTrue("Service should remain available after requests");
    }

    [Test]
    [Category("Integration")]
    [Category("ResourceManagement")]
    public async Task Services_ShouldManageResourcesProperly()
    {
        // This test ensures services don't leak resources or cause memory issues

        // Arrange
        var memoryBefore = GC.GetTotalMemory(false);

        // Act - Perform multiple operations
        for (int i = 0; i < 10; i++)
        {
            await _xaiChatService!.GetResponseAsync($"Memory test query {i}");
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(true);

        // Assert - Memory usage should not grow excessively
        var memoryGrowth = memoryAfter - memoryBefore;
        memoryGrowth.Should().BeLessThan(10_000_000, // 10MB threshold
            $"Memory growth should be reasonable. Growth: {memoryGrowth:N0} bytes");
    }

    [Test]
    [Category("Integration")]
    [Category("EndToEnd")]
    public async Task Services_ShouldSupportCompleteWorkflow()
    {
        // This test simulates a complete user workflow

        // Step 1: Initialize services
        await _xaiChatService!.InitializeAsync();

        // Step 2: Create a driver
        var newDriver = new Driver
        {
            DriverId = 555,
            DriverName = "Workflow Test Driver",
            Status = "Active",
            LicenseNumber = "WF555",
            DriverEmail = "workflow@test.com"
        };

        _dbContext!.Drivers.Add(newDriver);
        await _dbContext.SaveChangesAsync();

        // Step 3: Query about drivers through AI
        var aiResponse = await _xaiChatService.GetResponseAsync("How many drivers do we have?");

        // Step 4: Verify the workflow completed successfully
        var savedDriver = _dbContext.Drivers.Find(555);
        savedDriver.Should().NotBeNull("Driver should be saved successfully");

        aiResponse.Should().NotBeNullOrEmpty("AI should provide driver information");
        aiResponse.Should().Contain("driver", "AI response should mention drivers");

        // Step 5: Clean up
        _dbContext.Drivers.Remove(savedDriver!);
        await _dbContext.SaveChangesAsync();
    }
}
