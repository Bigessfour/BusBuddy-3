using System;
using System.IO;
using System.Threading.Tasks;
using BusBuddy.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class TripReasonCatalogTests
{
    [Test]
    public async Task RememberAndForget_PersistAcrossReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-trip-reasons-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new UserSettingsService(path);
            var catalog = new TripReasonCatalog(settings);

            var seeded = await catalog.GetSportsAsync();
            seeded.Should().Contain("Football");

            await catalog.RememberSportAsync("Track");
            await catalog.RememberPurposeAsync("Band");
            await catalog.ForgetSportAsync("Football");

            var reloadedSettings = new UserSettingsService(path);
            await reloadedSettings.LoadSettingsAsync();
            var reloaded = new TripReasonCatalog(reloadedSettings);

            var sports = await reloaded.GetSportsAsync();
            var purposes = await reloaded.GetPurposesAsync();

            sports.Should().Contain("Track");
            sports.Should().NotContain("Football");
            purposes.Should().Contain("Band");
            purposes.Should().Contain("Sports");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
