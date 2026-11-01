using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Contract + no-op guards for <see cref="MapViewLauncher"/> without opening a live MainWindow/MapView.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapViewLauncherTests
{
    [Test]
    public void IsOpen_WithoutMainWindowOrFloatingMap_IsFalse()
    {
        Assert.That(MapViewLauncher.IsOpen, Is.False);
    }

    [Test]
    public void TryActivateDockedMap_WithoutMainWindow_ReturnsFalse()
    {
        Assert.That(MapViewLauncher.TryActivateDockedMap(), Is.False);
        Assert.That(MapViewLauncher.TryActivateDockedMap(_ => Assert.Fail("configure must not run")), Is.False);
    }

    [Test]
    public void ResolveOwner_WithoutApplicationWindows_ReturnsRequested()
    {
        Assert.That(MapViewLauncher.ResolveOwner(null), Is.Null);
    }

    [Test]
    public void MapViewLauncher_Source_PrefersDockedMapViewAndGuardsConfigure()
    {
        // Keep needle "MapViewLauncher.cs" early for inventory proof linkage.
        var launcher = XamlViewFile.Read("Utilities/MapViewLauncher.cs");
        Assert.That(launcher, Does.Contain("TryActivateDockedMap"));
        Assert.That(launcher, Does.Contain("TryActivateMapDocument"));
        Assert.That(launcher, Does.Contain("new MapView()"));
        Assert.That(launcher, Does.Contain("Title = \"District Map\""));
        Assert.That(launcher, Does.Contain("Content = mapView"));
        Assert.That(launcher, Does.Contain("CloseFloatingMapWindowIfAny"));
        Assert.That(launcher, Does.Contain("MapViewLauncher configure skipped"));
        Assert.That(launcher, Does.Contain("GetService<MapViewModel>()"));
        Assert.That(launcher, Does.Contain("OnDistrictMapSurfaceActivatedAsync"));
        Assert.That(launcher, Does.Not.Contain("LayerType=\"OSM\""));
    }
}
