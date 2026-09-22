using System;
using System.IO;
using System.Threading.Tasks;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MapRouteExporterTests
{
    [Test]
    public async Task ExportAsync_WritesGeoJsonFromGeoServiceRoute()
    {
        var selected = new Route { RouteId = 4, RouteName = "Stale" };
        var loaded = new Route
        {
            RouteId = 4,
            RouteName = "AM-North",
            WaypointsJson = RouteWaypointSerializer.FromPairs(new[]
            {
                (38.15, -102.72),
                (38.16, -102.71)
            })
        };

        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRouteGeoDataAsync(4)).ReturnsAsync(loaded);

        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-route-export-{Guid.NewGuid():N}.geojson");
        try
        {
            var result = await MapRouteExporter.ExportAsync(geo.Object, selected, path);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Path, Is.EqualTo(path));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(await File.ReadAllTextAsync(path), Does.Contain("AM-North"));
            Assert.That(await File.ReadAllTextAsync(path), Does.Not.Contain("Stale"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task ExportAsync_NoWaypoints_DoesNotWriteFile()
    {
        var selected = new Route { RouteId = 5, RouteName = "Empty" };
        var geo = new Mock<IGeoDataService>();
        geo.Setup(g => g.GetRouteGeoDataAsync(5)).ReturnsAsync(selected);

        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-route-export-{Guid.NewGuid():N}.geojson");
        try
        {
            var result = await MapRouteExporter.ExportAsync(geo.Object, selected, path);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("no map waypoints"));
            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void SafeFileStem_ReplacesInvalidCharacters()
    {
        var stem = MapRouteExporter.SafeFileStem("AM/North: 1");
        Assert.That(stem, Does.Not.Contain("/"));
        Assert.That(stem, Does.Not.Contain(" "));
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            Assert.That(stem.IndexOf(c), Is.LessThan(0));
        }

        Assert.That(MapRouteExporter.SafeFileStem(null), Is.EqualTo("Route"));
    }

    [Test]
    public async Task ExportSelectedAsync_WithoutRoute_TellsClerkToSelectOne()
    {
        // No Settings gate any more — the button always answers. Missing selection is the only pre-check.
        var geo = new Mock<IGeoDataService>();

        var result = await MapRouteExporter.ExportSelectedAsync(geo.Object, selected: null);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Cancelled, Is.False);
        Assert.That(result.Message, Does.Contain("Select a route"));
        geo.Verify(g => g.GetRouteGeoDataAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void MapRouteExporter_HasNoSettingsGate()
    {
        var source = XamlViewFile.Read("Utilities/MapRouteExporter.cs");
        Assert.That(source, Does.Not.Contain("IUserSettingsService"));
        Assert.That(source, Does.Not.Contain("EnableRouteGeoExport"));
        Assert.That(source, Does.Contain("Cancelled: true"));
    }
}
