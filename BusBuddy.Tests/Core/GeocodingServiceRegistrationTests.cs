using System.Net.Http;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class GeocodingServiceRegistrationTests
{
    [Test]
    public void ProductionRegistration_ResolvesMapsGeoServiceAsGeocoder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{GoogleMapsOptions.SectionName}:ApiKey"] = "test-key",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<GoogleMapsOptions>(configuration.GetSection(GoogleMapsOptions.SectionName));
        services.AddSingleton(_ =>
            new GoogleAddressValidationClient(
                new HttpClient(),
                Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }),
                ownsHttpClient: true));
        services.AddSingleton<IMapsAddressCache>(_ => new MapsAddressCache());
        services.AddSingleton<IMapsGeoService>(sp =>
            new MapsGeoService(
                sp.GetRequiredService<GoogleAddressValidationClient>(),
                sp.GetRequiredService<IMapsAddressCache>()));
        services.AddSingleton<IGeocodingService>(sp => sp.GetRequiredService<IMapsGeoService>());

        using var sp = services.BuildServiceProvider();
        var geocoder = sp.GetRequiredService<IGeocodingService>();

        Assert.That(geocoder, Is.InstanceOf<MapsGeoService>());
        Assert.That(sp.GetRequiredService<IMapsGeoService>(), Is.SameAs(geocoder));
    }
}
