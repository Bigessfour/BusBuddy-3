using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
public class PickupStopFormViewModelTests
{
    [Test]
    public async Task Save_HintsNearbyHomePickupsWithoutAssigningThem()
    {
        var stops = new Mock<IPickupStopService>();
        stops
            .Setup(s => s.AddStopAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PickupStop
            {
                PickupStopId = 11,
                Name = "Oak & 4th",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
            });

        var students = new Mock<IStudentService>();
        students
            .Setup(s => s.GetNearbyHomePickupStudentsAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Student { StudentId = 1, StudentName = "TEST_NEAR_A" },
                new Student { StudentId = 2, StudentName = "TEST_NEAR_B" },
            });

        var vm = new PickupStopFormViewModel(stops.Object, students.Object)
        {
            Name = "Oak & 4th"
        };
        vm.ApplyMapClick(38.0872, -102.6208);

        if (vm.SaveCommand is IAsyncRelayCommand save)
        {
            await save.ExecuteAsync(null);
        }
        else
        {
            Assert.Fail("SaveCommand should be IAsyncRelayCommand");
        }

        vm.SavedPickupStopId.Should().Be(11);
        vm.NearbyHomePickupHint.Should().Contain("2 home pickup");
        vm.NearbyHomePickupHint.Should().Contain("Assign those students");
        students.Verify(
            s => s.GetNearbyHomePickupStudentsAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public void ApplyMapClick_DoesNotRecenterTheCamera()
    {
        var stops = new Mock<IPickupStopService>();
        var vm = new PickupStopFormViewModel(stops.Object);
        var camera = vm.MapCenter;

        vm.ApplyMapClick(38.1535, -102.7195);

        vm.LatitudeValue.Should().BeApproximately(38.1535, 0.000001);
        vm.LongitudeValue.Should().BeApproximately(-102.7195, 0.000001);
        vm.HasMapPick.Should().BeTrue();
        vm.MapCenter.Should().Be(camera);
        vm.MapMarkers.Should().HaveCount(1);
    }

    [Test]
    public async Task Save_SuggestsNameFromAddressWhenNameBoxIsEmpty()
    {
        string? savedName = null;
        var stops = new Mock<IPickupStopService>();
        stops
            .Setup(s => s.AddStopAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string?, decimal, decimal, string, string?, CancellationToken>(
                (name, _, _, _, _, _, _) => savedName = name)
            .ReturnsAsync(new PickupStop
            {
                PickupStopId = 12,
                Name = "Oak Ave & 4th St",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
            });

        var vm = new PickupStopFormViewModel(stops.Object)
        {
            Address = "Oak Ave & 4th St, Wiley, CO 81092, USA"
        };
        vm.ApplyMapClick(38.0872, -102.6208);

        await ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

        savedName.Should().Be("Oak Ave & 4th St");
        vm.SavedPickupStopId.Should().Be(12);
        vm.Name.Should().Be("Oak Ave & 4th St");
        vm.ValidationMessage.Should().BeEmpty();
    }

    [Test]
    public async Task SuggestNameFromMap_UsesReverseGeocodeWhenNameIsEmpty()
    {
        var stops = new Mock<IPickupStopService>();
        var maps = new Mock<IMapsGeoService>();
        maps.SetupGet(m => m.IsConfigured).Returns(true);
        maps
            .Setup(m => m.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MapsGeocodeResult
            {
                Ok = true,
                FormattedAddress = "Oak Avenue & 4th Street, Wiley, CO 81092, USA"
            });

        var vm = new PickupStopFormViewModel(stops.Object, mapsGeo: maps.Object);
        vm.ApplyMapClick(38.0872, -102.6208);
        await vm.SuggestNameFromMapAsync();

        vm.Name.Should().Be("Oak Avenue & 4th Street");
        vm.Address.Should().Contain("Oak Avenue");
        maps.Verify(
            m => m.ReverseGeocodeAsync(38.0872, -102.6208, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public void TrySuggestName_DoesNotOverwriteATypedName()
    {
        var vm = new PickupStopFormViewModel(new Mock<IPickupStopService>().Object)
        {
            Name = "Barn corner"
        };

        vm.TrySuggestName("Oak & 4th", "Oak & 4th, Wiley, CO").Should().BeFalse();
        vm.Name.Should().Be("Barn corner");
    }
}
