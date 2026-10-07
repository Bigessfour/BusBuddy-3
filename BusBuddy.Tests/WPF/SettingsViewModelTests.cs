using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Settings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class SettingsViewModelTests
{
    [Test]
    public async Task SaveSettingsAsync_PersistsAllKeysAndUpdatesStatus()
    {
        var settings = new Mock<IUserSettingsService>();
        settings.SetupGet(s => s.EnableActivityLogging).Returns(true);
        settings.SetupGet(s => s.ShowDashboardOnStartup).Returns(true);
        settings.SetupGet(s => s.CachedTheme).Returns("FluentDark");
        settings.Setup(s => s.LoadSettingsAsync()).Returns(Task.CompletedTask);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.Theme, It.IsAny<string>()))
            .ReturnsAsync("FluentDark");
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.SaveSettingsAsync()).ReturnsAsync(true);

        var skin = new Mock<ISkinManagerService>();
        var vm = new SettingsViewModel(settings.Object, skin.Object);

        await Task.Delay(100);
        vm.SelectedTheme = "FluentLight";
        vm.EnableActivityLogging = false;
        vm.ShowDashboardOnStartup = false;

        await vm.SaveCommand.ExecuteAsync(null);

        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.Theme, "FluentLight"), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.EnableActivityLogging, false), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, false), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLatitude, It.IsAny<string>()), Times.Once);
        settings.Verify(s => s.SaveSettingsAsync(), Times.Once);
        skin.Verify(s => s.ApplyTheme("FluentLight"), Times.Once);
        vm.StatusMessage.Should().Be("Settings saved");
    }

    [Test]
    public async Task SaveSettingsAsync_WritesDistrictAndSyncsMap_NotUsCentroid()
    {
        var settings = CreateSettingsMock();
        settings.Setup(s => s.SaveSettingsAsync()).ReturnsAsync(true);

        var accessor = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings()));
        var mapSync = new Mock<IDistrictMapSync>();
        mapSync.Setup(m => m.ApplyDistrictGeographyAsync(default)).Returns(Task.CompletedTask);

        var vm = new SettingsViewModel(
            settings.Object,
            new Mock<ISkinManagerService>().Object,
            accessor,
            districtMapSync: mapSync.Object);

        await Task.Delay(100);
        vm.DepotName = "Hop Settings Depot";
        vm.DepotLatitudeText = "38.0872";
        vm.DepotLongitudeText = "-102.6208";
        vm.BoundingBoxMinLatText = "38.05";
        vm.BoundingBoxMinLonText = "-102.80";
        vm.BoundingBoxMaxLatText = "38.25";
        vm.BoundingBoxMaxLonText = "-102.40";

        await vm.SaveCommand.ExecuteAsync(null);

        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLatitude, "38.0872"), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLongitude, "-102.6208"), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictBoundingBoxMinLat, "38.0500"), Times.Once);

        Assert.That(DistrictDepot.TryGetCoordinates(accessor.Current, out var lat, out var lon), Is.True);
        Assert.That(lat, Is.EqualTo(38.0872).Within(0.0001));
        Assert.That(lon, Is.EqualTo(-102.6208).Within(0.0001));

        var camera = DistrictMapAnchor.ResolveCamera(accessor.Current);
        Assert.That(camera.Latitude, Is.EqualTo(38.0872).Within(0.0001));
        Assert.That(camera.Longitude, Is.EqualTo(-102.6208).Within(0.0001));
        Assert.That(camera.ZoomLevel, Is.EqualTo(MapDefaults.DistrictZoomLevel));
        Assert.That(camera.ZoomLevel, Is.Not.EqualTo(MapDefaults.UnconfiguredZoomLevel));
        Assert.That(Math.Abs(camera.Latitude - MapDefaults.UnconfiguredLatitude), Is.GreaterThan(0.5));

        mapSync.Verify(m => m.ApplyDistrictGeographyAsync(default), Times.Once);
        vm.StatusMessage.Should().Be("Settings saved");
    }

    [Test]
    public async Task SaveSettingsAsync_DoesNotSyncMapWhenPersistFails()
    {
        var settings = CreateSettingsMock();
        settings.Setup(s => s.SaveSettingsAsync()).ReturnsAsync(false);
        var mapSync = new Mock<IDistrictMapSync>();

        var vm = new SettingsViewModel(
            settings.Object,
            new Mock<ISkinManagerService>().Object,
            new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings())),
            districtMapSync: mapSync.Object);

        await Task.Delay(100);
        vm.DepotLatitudeText = "38.0872";
        vm.DepotLongitudeText = "-102.6208";

        await vm.SaveCommand.ExecuteAsync(null);

        mapSync.Verify(m => m.ApplyDistrictGeographyAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        vm.StatusMessage.Should().Be("Failed to save settings");
    }

    [Test]
    public void ApplyDepotAddress_FillsCityStateZipAndCoordinates()
    {
        var vm = new SettingsViewModel(CreateSettingsMock().Object, new Mock<ISkinManagerService>().Object);
        vm.ApplyDepotAddress(new PlaceAddressApplier.AppliedAddress(
            Street: "210 West Pearl",
            City: "Lamar",
            State: "CO",
            Zip: "81052",
            Latitude: 38.0872,
            Longitude: -102.6208,
            FormattedAddress: "210 West Pearl, Lamar, CO 81052",
            PlaceId: "test"));

        vm.DepotAddress.Should().Contain("210 West Pearl");
        vm.DepotCity.Should().Be("Lamar");
        vm.DepotState.Should().Be("CO");
        vm.DepotZipCode.Should().Be("81052");
        vm.DepotLatitudeText.Should().Contain("38.0872");
        vm.DepotLongitudeText.Should().Contain("-102.6208");
    }

    [Test]
    public async Task SaveSettingsAsync_AfterApplyDepotAddress_PersistsCoordinates()
    {
        var settings = CreateSettingsMock();
        settings.Setup(s => s.SaveSettingsAsync()).ReturnsAsync(true);
        var accessor = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings()));
        var vm = new SettingsViewModel(
            settings.Object,
            new Mock<ISkinManagerService>().Object,
            accessor);

        await Task.Delay(100);
        vm.ApplyDepotAddress(new PlaceAddressApplier.AppliedAddress(
            Street: "210 West Pearl",
            City: "Lamar",
            State: "CO",
            Zip: "81052",
            Latitude: 38.0872,
            Longitude: -102.6208,
            FormattedAddress: "210 West Pearl, Lamar, CO 81052",
            PlaceId: "test"));

        await vm.SaveCommand.ExecuteAsync(null);

        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLatitude, "38.0872"), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLongitude, "-102.6208"), Times.Once);
        Assert.That(DistrictDepot.TryGetCoordinates(accessor.Current, out var lat, out var lon), Is.True);
        Assert.That(lat, Is.EqualTo(38.0872).Within(0.0001));
    }

    [Test]
    public async Task LoadSettingsAsync_DoesNotOverwritePlacesApply()
    {
        var gate = new TaskCompletionSource<bool>();
        var settings = CreateSettingsMock();
        settings.Setup(s => s.LoadSettingsAsync()).Returns(gate.Task);
        var vm = new SettingsViewModel(settings.Object, new Mock<ISkinManagerService>().Object);

        vm.ApplyDepotAddress(new PlaceAddressApplier.AppliedAddress(
            Street: "210 West Pearl",
            City: "Lamar",
            State: "CO",
            Zip: "81052",
            Latitude: 38.0872,
            Longitude: -102.6208,
            FormattedAddress: "210 West Pearl, Lamar, CO 81052",
            PlaceId: "test"));

        gate.SetResult(true);
        await Task.Delay(150);

        vm.DepotLatitudeText.Should().Contain("38.0872");
        vm.DepotLongitudeText.Should().Contain("-102.6208");
        vm.DepotCity.Should().Be("Lamar");
    }

    [Test]
    public async Task SaveSettingsAsync_CaptureViewFieldsRunsBeforePersist()
    {
        var settings = CreateSettingsMock();
        settings.Setup(s => s.SaveSettingsAsync()).ReturnsAsync(true);
        var vm = new SettingsViewModel(settings.Object, new Mock<ISkinManagerService>().Object);
        await Task.Delay(50);
        vm.CaptureViewFields = () =>
        {
            vm.DepotLatitudeText = "38.1541";
            vm.DepotLongitudeText = "-102.7201";
        };

        await vm.SaveCommand.ExecuteAsync(null);

        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLatitude, "38.1541"), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.DistrictDepotLongitude, "-102.7201"), Times.Once);
    }

    [Test]
    public async Task ResetSettingsAsync_ReloadsDefaults()
    {
        var settings = new Mock<IUserSettingsService>();
        settings.SetupGet(s => s.EnableActivityLogging).Returns(true);
        settings.SetupGet(s => s.ShowDashboardOnStartup).Returns(true);
        settings.SetupGet(s => s.CachedTheme).Returns("FluentDark");
        settings.Setup(s => s.LoadSettingsAsync()).Returns(Task.CompletedTask);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.Theme, It.IsAny<string>()))
            .ReturnsAsync("FluentDark");
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.ResetSettingsAsync()).ReturnsAsync(true);

        var vm = new SettingsViewModel(settings.Object, new Mock<ISkinManagerService>().Object);
        await Task.Delay(100);

        await vm.ResetCommand.ExecuteAsync(null);

        settings.Verify(s => s.ResetSettingsAsync(), Times.Once);
        settings.Verify(s => s.LoadSettingsAsync(), Times.AtLeastOnce);
        vm.StatusMessage.Should().Be("Settings reset to defaults");
    }

    [Test]
    public async Task ApplyBoundaryJson_FillsExtentFromAnyPolygon()
    {
        var vm = new SettingsViewModel(CreateSettingsMock().Object, new Mock<ISkinManagerService>().Object);
        await Task.Delay(50);

        const string json = """
            {"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Polygon","coordinates":[[[-105.1,39.1],[-104.9,39.1],[-104.9,39.3],[-105.1,39.3],[-105.1,39.1]]]},"properties":{"NAME":"Example District"}}]}
            """;

        Assert.That(vm.ApplyBoundaryJson(json), Is.True);
        Assert.That(vm.BoundingBoxMinLatText, Is.EqualTo("39.1000"));
        Assert.That(vm.BoundingBoxMaxLatText, Is.EqualTo("39.3000"));
        Assert.That(vm.BoundingBoxMinLonText, Is.EqualTo("-105.1000"));
        Assert.That(vm.BoundingBoxMaxLonText, Is.EqualTo("-104.9000"));
        Assert.That(vm.StatusMessage, Does.Not.Contain("Lamar"));
        Assert.That(vm.StatusMessage, Does.Not.Contain("Example"));
    }

    [Test]
    public async Task ApplyBoundaryJson_RejectsAFileWithNoPolygon()
    {
        var vm = new SettingsViewModel(CreateSettingsMock().Object, new Mock<ISkinManagerService>().Object);
        await Task.Delay(50);

        Assert.That(vm.ApplyBoundaryJson("""{"type":"Point","coordinates":[-104.9,39.1]}"""), Is.False);
        Assert.That(vm.BoundingBoxMinLatText, Is.Empty);
        Assert.That(vm.StatusMessage, Is.EqualTo("The boundary file has no polygon."));
    }

    private static Mock<IUserSettingsService> CreateSettingsMock()
    {
        var settings = new Mock<IUserSettingsService>();
        settings.SetupGet(s => s.EnableActivityLogging).Returns(true);
        settings.SetupGet(s => s.ShowDashboardOnStartup).Returns(true);
        settings.SetupGet(s => s.CachedTheme).Returns("FluentDark");
        settings.Setup(s => s.LoadSettingsAsync()).Returns(Task.CompletedTask);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.Theme, It.IsAny<string>()))
            .ReturnsAsync("FluentDark");
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, It.IsAny<bool>()))
            .ReturnsAsync(true);
        return settings;
    }
}
