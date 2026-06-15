using NUnit.Framework;
using FluentAssertions;
using BusBuddy.WPF.Services;
using Serilog;
using Moq;

namespace BusBuddy.Tests.ServiceTests;

/// <summary>
/// Tests for XAIChatService functionality
/// Validates AI chat responses and service behavior
/// </summary>
[TestFixture]
public class XAIChatServiceTests
{
    private XAIChatService? _xaiChatService;

    [SetUp]
    public void SetUp()
    {
        _xaiChatService = new XAIChatService();
    }

    [Test]
    [Category("XAIChat")]
    [Category("Initialization")]
    public async Task InitializeAsync_ShouldCompleteSuccessfully()
    {
        // Act
        await _xaiChatService!.InitializeAsync();

        // Assert
        var isAvailable = await _xaiChatService.IsAvailableAsync();
        isAvailable.Should().BeTrue("service should be available after initialization");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ServiceAvailability")]
    public async Task IsAvailableAsync_ShouldReturnTrueWhenServiceIsRunning()
    {
        // Act
        var isAvailable = await _xaiChatService!.IsAvailableAsync();

        // Assert
        isAvailable.Should().BeTrue("mock service should always be available");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseGeneration")]
    public async Task GetResponseAsync_ShouldReturnFleetStatusResponse()
    {
        // Arrange
        var fleetQuery = "What is the current fleet status?";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(fleetQuery);

        // Assert
        response.Should().NotBeNullOrEmpty("should return a response");
        response.Should().Contain("fleet", "response should mention fleet");
        response.Should().Contain("buses", "response should mention buses");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseGeneration")]
    public async Task GetResponseAsync_ShouldReturnBusLocationResponse()
    {
        // Arrange
        var busQuery = "Find bus 101";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(busQuery);

        // Assert
        response.Should().NotBeNullOrEmpty("should return a response");
        response.Should().Contain("bus", "response should mention bus");
        response.Should().Contain("locate", "response should mention location capability");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseGeneration")]
    public async Task GetResponseAsync_ShouldReturnRouteInformationResponse()
    {
        // Arrange
        var routeQuery = "Tell me about route information";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(routeQuery);

        // Assert
        response.Should().NotBeNullOrEmpty("should return a response");
        response.Should().Contain("route", "response should mention routes");
        response.Should().Contain("network", "response should mention transportation network");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseGeneration")]
    public async Task GetResponseAsync_ShouldReturnEmergencyResponse()
    {
        // Arrange
        var emergencyQuery = "Emergency help needed!";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(emergencyQuery);

        // Assert
        response.Should().NotBeNullOrEmpty("should return a response");
        response.Should().Contain("Emergency", "response should acknowledge emergency");
        response.Should().Contain("911", "response should mention emergency contact");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseGeneration")]
    public async Task GetResponseAsync_ShouldReturnGreetingResponse()
    {
        // Arrange
        var greeting = "Hello there!";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(greeting);

        // Assert
        response.Should().NotBeNullOrEmpty("should return a response");
        response.Should().Contain("Hello", "response should acknowledge greeting");
        response.Should().Contain("AI assistant", "response should identify as AI assistant");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseGeneration")]
    public async Task GetResponseAsync_ShouldReturnDefaultResponseForUnknownQuery()
    {
        // Arrange
        var unknownQuery = "Something completely unrelated to transportation";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(unknownQuery);

        // Assert
        response.Should().NotBeNullOrEmpty("should return a response");
        response.Should().Contain("transportation management", "response should guide back to transportation topics");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ErrorHandling")]
    public async Task GetResponseAsync_ShouldHandleNullInput()
    {
        // Act
        var response = await _xaiChatService!.GetResponseAsync(null!);

        // Assert
        response.Should().NotBeNullOrEmpty("should return error response for null input");
        response.Should().Contain("trouble", "should indicate processing trouble");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ErrorHandling")]
    public async Task GetResponseAsync_ShouldHandleEmptyInput()
    {
        // Act
        var response = await _xaiChatService!.GetResponseAsync(string.Empty);

        // Assert
        response.Should().NotBeNullOrEmpty("should return response for empty input");
    }

    [Test]
    [Category("XAIChat")]
    [Category("Performance")]
    public async Task GetResponseAsync_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var query = "How many drivers are active?";

        // Act
        var response = await _xaiChatService!.GetResponseAsync(query);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000,
            $"Response should complete within 3 seconds. Actual: {stopwatch.ElapsedMilliseconds}ms");
        response.Should().NotBeNullOrEmpty("should return valid response");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ResponseVariety")]
    public async Task GetResponseAsync_ShouldProvideVariedResponsesForDifferentTopics()
    {
        // Arrange
        var queries = new[]
        {
            "student transportation",
            "driver management",
            "maintenance schedule",
            "fuel efficiency"
        };

        var responses = new List<string>();

        // Act
        foreach (var query in queries)
        {
            var response = await _xaiChatService!.GetResponseAsync(query);
            responses.Add(response);
        }

        // Assert
        responses.Should().HaveCount(4, "should generate response for each query");
        responses.Should().OnlyHaveUniqueItems("should provide varied responses for different topics");

        responses[0].Should().Contain("student", "student query should mention students");
        responses[1].Should().Contain("driver", "driver query should mention drivers");
        responses[2].Should().Contain("maintenance", "maintenance query should mention maintenance");
        responses[3].Should().Contain("fuel", "fuel query should mention fuel");
    }

    [Test]
    [Category("XAIChat")]
    [Category("ServiceIntegration")]
    public async Task XAIChatService_ShouldIntegrateWithLoggingProperly()
    {
        // This test validates that the service works without throwing exceptions
        // when logging is called (even though we can't easily verify log output in this context)

        // Arrange
        var query = "Test logging integration";

        // Act & Assert
        var response = await _xaiChatService!.GetResponseAsync(query);
        response.Should().NotBeNullOrEmpty("service should work with logging integration");

        // Verify multiple operations work
        await _xaiChatService.InitializeAsync();
        var isAvailable = await _xaiChatService.IsAvailableAsync();
        isAvailable.Should().BeTrue("service should remain available");
    }
}
