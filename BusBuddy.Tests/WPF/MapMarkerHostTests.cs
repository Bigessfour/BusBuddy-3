using System;
using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Fail-closed and contract proof for <see cref="MapMarkerHost"/> without a live SfMap tile session.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapMarkerHostTests
{
    [Test]
    public void CanHost_NullMapOrLayer_ReturnsFalse()
    {
        Assert.That(MapMarkerHost.CanHost(null, null), Is.False);
        Assert.That(MapMarkerHost.CanHost(null, layer: null), Is.False);
    }

    [Test]
    public void TryAssign_WhenCannotHost_DoesNotThrowAndReturnsFalse()
    {
        // Null map/layer never reaches ImageryLayer.Markers — no live SfMap session required.
        Assert.That(MapMarkerHost.TryAssign(null, null, markers: null), Is.False);
        Assert.That(MapMarkerHost.TryAssign(null, null, Array.Empty<object>()), Is.False);
        Assert.That(MapMarkerHost.TryAssignAndLayout(null, null, Array.Empty<object>()), Is.False);
    }

    [Test]
    public void RetryScheduler_RequiresDispatcherAndTryAssign()
    {
        Assert.Throws<ArgumentNullException>(
            (Action)(() => _ = new MapMarkerHost.RetryScheduler(null!, () => true)));
        Assert.Throws<ArgumentNullException>(
            (Action)(() =>
                _ = new MapMarkerHost.RetryScheduler(System.Windows.Threading.Dispatcher.CurrentDispatcher, null!)));
    }

    [Test]
    public void MapMarkerHost_Source_GatesMarkersAndLayoutWithoutXamlBinding()
    {
        // Keep needle "MapMarkerHost.cs" early for inventory proof linkage.
        var host = XamlViewFile.Read("Utilities/MapMarkerHost.cs");
        Assert.That(host, Does.Contain("CanHost"));
        Assert.That(host, Does.Contain("TryAssign"));
        Assert.That(host, Does.Contain("TryAssignAndLayout"));
        Assert.That(host, Does.Contain("layer.Markers = markers"));
        Assert.That(host, Does.Contain("BeginMarkerHostCheck"));
        Assert.That(host, Does.Contain("RetryLimit = 20"));
        Assert.That(host, Does.Contain("MarkerTemplateSelector"));
        Assert.That(MapMarkerHost.RetryLimit, Is.EqualTo(20));
        Assert.That(MapMarkerHost.RetryInterval, Is.EqualTo(TimeSpan.FromMilliseconds(150)));
    }
}
