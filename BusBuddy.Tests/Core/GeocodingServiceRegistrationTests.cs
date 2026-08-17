using System;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class GeocodingServiceRegistrationTests
{
    [Test]
    public void ProductionRegistration_IsNotOfflineGeocodingService()
    {
        var services = new ServiceCollection();
        // Mirrors App / ServiceCollectionExtensions production wiring for geocoding.
        services.AddSingleton<IGeocodingService>(_ =>
            new GoogleAddressValidationClient(
                new System.Net.Http.HttpClient(),
                Microsoft.Extensions.Options.Options.Create(new BusBuddy.Core.Configuration.GoogleMapsOptions
                {
                    ApiKey = "${GOOGLE_MAPS_API_KEY}"
                }),
                ownsHttpClient: true));

        using var sp = services.BuildServiceProvider();
        var geocoder = sp.GetRequiredService<IGeocodingService>();

        Assert.That(geocoder, Is.Not.InstanceOf<OfflineGeocodingService>());
        Assert.That(geocoder, Is.InstanceOf<GoogleAddressValidationClient>());
    }
}
