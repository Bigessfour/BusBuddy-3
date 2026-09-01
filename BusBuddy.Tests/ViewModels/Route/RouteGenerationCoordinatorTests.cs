using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.WPF.ViewModels.Route;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.ViewModels.Route;

[TestFixture]
[Category("Unit")]
public class RouteGenerationCoordinatorTests
{
    [Test]
    public async Task GenerateAsync_NoPlanner_DoesNotInvoke()
    {
        var destinations = Mock.Of<IDestinationService>();

        var outcome = await RouteGenerationCoordinator.GenerateAsync(
            FleetKind.HomeToSchool,
            preferredSchoolName: null,
            preferSchoolWithStartTime: true,
            planner: null,
            destinations);

        Assert.That(outcome.Invoked, Is.False);
        Assert.That(outcome.StatusMessage, Does.Contain("unavailable"));
    }

    [Test]
    public async Task GenerateAsync_NoDestinations_DoesNotInvoke()
    {
        var planner = new Mock<IRouteDeterminationService>(MockBehavior.Strict);

        var outcome = await RouteGenerationCoordinator.GenerateAsync(
            FleetKind.HomeToSchool,
            preferredSchoolName: null,
            preferSchoolWithStartTime: true,
            planner.Object,
            destinations: null);

        Assert.That(outcome.Invoked, Is.False);
        Assert.That(outcome.StatusMessage, Does.Contain("Destination"));
        planner.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GenerateAsync_NoActiveSchools_DoesNotCallPlanner()
    {
        var destinations = new Mock<IDestinationService>();
        destinations
            .Setup(d => d.GetActiveSchoolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Destination>());
        var planner = new Mock<IRouteDeterminationService>(MockBehavior.Strict);

        var outcome = await RouteGenerationCoordinator.GenerateAsync(
            FleetKind.HomeToSchool,
            preferredSchoolName: "Oakridge",
            preferSchoolWithStartTime: true,
            planner.Object,
            destinations.Object);

        Assert.That(outcome.Invoked, Is.False);
        Assert.That(outcome.StatusMessage, Does.Contain("No active school"));
        planner.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GenerateAsync_PrefersNamedSchoolThenStartTime_CallsPlanner()
    {
        var withBell = new Destination
        {
            DestinationId = 2,
            Name = "Bell School",
            StartTime = TimeSpan.FromHours(8)
        };
        var named = new Destination
        {
            DestinationId = 7,
            Name = "Oakridge School"
        };
        var destinations = new Mock<IDestinationService>();
        destinations
            .Setup(d => d.GetActiveSchoolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { withBell, named });

        var planner = new Mock<IRouteDeterminationService>();
        planner
            .Setup(p => p.GenerateAndAssignAsync(
                7,
                RouteTimeSlotKind.Both,
                FleetKind.HomeToSchool,
                It.IsAny<RouteGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteGenerationResult
            {
                Success = true,
                Proposals = [new RouteProposalDto { SuggestedRouteName = "Draft-Oakridge-1" }],
                AssignedStudentCount = 3
            });

        var outcome = await RouteGenerationCoordinator.GenerateAsync(
            FleetKind.HomeToSchool,
            preferredSchoolName: "Oakridge School",
            preferSchoolWithStartTime: true,
            planner.Object,
            destinations.Object);

        Assert.That(outcome.Invoked, Is.True);
        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.StatusMessage, Does.Contain("3"));
        planner.Verify(
            p => p.GenerateAndAssignAsync(
                7,
                RouteTimeSlotKind.Both,
                FleetKind.HomeToSchool,
                It.IsAny<RouteGenerationOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
