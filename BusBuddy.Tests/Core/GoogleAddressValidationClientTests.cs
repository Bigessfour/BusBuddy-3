using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class GoogleAddressValidationClientTests
{
    private static GoogleMapsOptions TestOptions => new()
    {
        ApiKey = "test-key",
        QuotaProject = "new-coursera-490518",
        EnableUspsCass = true,
        RegionCode = "US"
    };

    [Test]
    public async Task ValidateAndGeocode_Deliverable_ReturnsCoords()
    {
        var json = """
            {
              "result": {
                "verdict": { "addressComplete": true, "validationGranularity": "PREMISE" },
                "address": { "formattedAddress": "510 Ward St, Wiley, CO 81092, USA" },
                "geocode": { "location": { "latitude": 38.1527, "longitude": -102.7204 } }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("510 Ward St", "Wiley", "CO", "81092");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Latitude, Is.EqualTo(38.1527).Within(0.0001));
        Assert.That(result.Longitude, Is.EqualTo(-102.7204).Within(0.0001));
        Assert.That(result.FormattedAddress, Does.Contain("Wiley"));
    }

    [Test]
    public async Task ValidateAndGeocode_Undeliverable_ReturnsNotOk()
    {
        var json = """
            {
              "result": {
                "verdict": { "addressComplete": false, "validationGranularity": "OTHER" },
                "address": { "formattedAddress": "Nowhere" }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("999 Fake Rd", "Nowhere", "CO", "00000");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Latitude, Is.Null);
        Assert.That(await client.GeocodeAsync("999 Fake Rd", "Nowhere", "CO", "00000"), Is.Null);
    }

    [Test]
    public async Task ValidateAndGeocode_MissingKey_ReturnsMappingUnconfigured()
    {
        var previous = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", null);
            using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"));
            var opts = new GoogleMapsOptions { ApiKey = "${GOOGLE_MAPS_API_KEY}" };
            var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(opts));

            var result = await client.ValidateAndGeocodeAsync("510 Ward St", "Wiley", "CO", "81092");

            Assert.That(result.MappingUnconfigured, Is.True);
            Assert.That(result.Ok, Is.False);
            Assert.That(await client.GeocodeAsync("510 Ward St", "Wiley", "CO", "81092"), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", previous);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            });
        }
    }
}
