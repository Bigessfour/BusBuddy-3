using System;
using System.IO;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Services;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class DistrictSettingsAccessorTests
{
    [Test]
    public async Task OverlayFromUserSettings_AppliesDepotCoordinates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-district-{Guid.NewGuid():N}.json");
        try
        {
            var user = new UserSettingsService(path);
            await user.SetSettingAsync(UserSettingsKeys.DistrictDepotLatitude, "38.0866");
            await user.SetSettingAsync(UserSettingsKeys.DistrictDepotLongitude, "-102.6201");
            await user.SaveSettingsAsync();

            var loaded = new UserSettingsService(path);
            await loaded.LoadSettingsAsync();

            var accessor = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings()));
            accessor.OverlayFromUserSettings(loaded);

            Assert.That(DistrictDepot.TryGetCoordinates(accessor.Current, out var lat, out var lon), Is.True);
            Assert.That(lat, Is.EqualTo(38.0866).Within(0.0001));
            Assert.That(lon, Is.EqualTo(-102.6201).Within(0.0001));
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
    public void Current_StartsFromAppsettingsWithoutUserOverlay()
    {
        var accessor = new DistrictSettingsAccessor(Options.Create(new RoutingDistrictSettings
        {
            BoundingBoxMinLat = 38.05,
            BoundingBoxMaxLat = 38.25,
            BoundingBoxMinLon = -102.80,
            BoundingBoxMaxLon = -102.60
        }));

        Assert.That(DistrictMapAnchor.TryGetBboxCentroid(accessor.Current, out var lat, out var lon), Is.True);
        Assert.That(lat, Is.EqualTo(38.15).Within(0.0001));
        Assert.That(lon, Is.EqualTo(-102.70).Within(0.0001));
        Assert.That(DistrictDepot.TryGetCoordinates(accessor.Current, out _, out _), Is.False);
    }
}
