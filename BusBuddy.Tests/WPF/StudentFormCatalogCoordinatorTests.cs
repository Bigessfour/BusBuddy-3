using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
public class StudentFormCatalogCoordinatorTests
{
    [Test]
    public async Task RefreshPickupSuggestion_NearbyCatalog_HintsWithoutAssigning()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        await using var ctx = new BusBuddyDbContext(
            new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"hint_{System.Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        var student = new Student { Latitude = 38.0872m, Longitude = -102.6208m };
        var pickup = new Mock<IPickupStopService>();
        pickup
            .Setup(p => p.FindNearestAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PickupStop
            {
                PickupStopId = 7,
                Name = "Oak & 4th",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
            });

        var catalog = new StudentFormCatalogCoordinator(
            ctx,
            student,
            [],
            new ObservableCollection<string>(),
            new ObservableCollection<PickupStop>(),
            new ObservableCollection<Destination>(),
            _ => { },
            pickupStops: pickup.Object);

        await catalog.RefreshPickupSuggestionAsync();

        student.PickupStopId.Should().BeNull();
        catalog.PickupStopHint.Should().Contain("Oak & 4th");
        catalog.PickupStopHint.Should().Contain("Select it");
    }

    [Test]
    public async Task RefreshPickupSuggestion_NoCatalog_UsesHomeAndHintsCluster()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        await using var ctx = new BusBuddyDbContext(
            new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"home_{System.Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        var student = new Student { Latitude = 38.0872m, Longitude = -102.6208m, PickupStopId = 9 };
        var pickup = new Mock<IPickupStopService>();
        pickup
            .Setup(p => p.FindNearestAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PickupStop?)null);

        var students = new Mock<IStudentService>();
        students
            .Setup(s => s.GetNearbyHomePickupStudentsAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Student { StudentId = 2, Latitude = 38.0873m, Longitude = -102.6209m } });

        var catalog = new StudentFormCatalogCoordinator(
            ctx,
            student,
            [],
            new ObservableCollection<string>(),
            new ObservableCollection<PickupStop>(),
            new ObservableCollection<Destination>(),
            _ => { },
            students.Object,
            pickup.Object);

        await catalog.RefreshPickupSuggestionAsync();

        student.PickupStopId.Should().Be(9);
        catalog.UsesHomeAsPickupStop.Should().BeFalse();
        catalog.PickupStopHint.Should().Contain("using home pickup");
        catalog.PickupStopHint.Should().Contain("other home pickup");
    }

    [Test]
    public async Task LoadAll_WithValidatedHomeAndEmptyCatalog_HintsNoPublishedStops()
    {
        BusBuddyDbContext.SkipGlobalSeedData = true;
        await using var ctx = new BusBuddyDbContext(
            new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"load_{System.Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        var student = new Student { Latitude = 38.0901m, Longitude = -102.6189m };
        var pickup = new Mock<IPickupStopService>();
        pickup
            .Setup(p => p.GetActiveStopsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PickupStop>());
        pickup
            .Setup(p => p.FindNearestAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PickupStop?)null);

        var catalog = new StudentFormCatalogCoordinator(
            ctx,
            student,
            [],
            new ObservableCollection<string>(),
            new ObservableCollection<PickupStop>(),
            new ObservableCollection<Destination>(),
            _ => { },
            pickupStops: pickup.Object);

        await catalog.LoadAllAsync();

        catalog.PickupStopHint.Should().Contain("No published catalog stops");
        catalog.PickupStopHint.Should().Contain("Add a catalog stop");
    }
}
