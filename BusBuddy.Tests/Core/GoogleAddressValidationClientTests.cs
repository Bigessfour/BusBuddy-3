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
        QuotaProject = "busbuddy-507301",
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
                "address": { "formattedAddress": "100 Main St, Oakridge, CO 80000, USA" },
                "geocode": {
                  "placeId": "ChIJtestplace",
                  "location": { "latitude": 38.1527, "longitude": -102.7204 }
                }
              }
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Oakridge", "CO", "80000");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Latitude, Is.EqualTo(38.1527).Within(0.0001));
        Assert.That(result.Longitude, Is.EqualTo(-102.7204).Within(0.0001));
        Assert.That(result.PlaceId, Is.EqualTo("ChIJtestplace"));
        Assert.That(result.FormattedAddress, Does.Contain("Oakridge"));
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

            var result = await client.ValidateAndGeocodeAsync("100 Main St", "Oakridge", "CO", "80000");

            Assert.That(result.MappingUnconfigured, Is.True);
            Assert.That(result.Ok, Is.False);
            Assert.That(await client.GeocodeAsync("100 Main St", "Oakridge", "CO", "80000"), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", previous);
        }
    }

    [Test]
    public async Task ValidateAndGeocode_Forbidden_FallsBackToGeocoding()
    {
        var geocodeJson = """
            {
              "status": "OK",
              "results": [{
                "place_id": "ChIJgeocode",
                "formatted_address": "1600 Amphitheatre Parkway, Mountain View, CA 94043, USA",
                "geometry": { "location": { "lat": 37.422, "lng": -122.084 } }
              }]
            }
            """;
        HttpRequestMessage? geocodeRequest = null;
        using var http = new HttpClient(new SequenceStubHandler(
            (HttpStatusCode.Forbidden, "{\"error\":{\"status\":\"PERMISSION_DENIED\"}}"),
            (HttpStatusCode.OK, geocodeJson),
            onRequest: req =>
            {
                if (req.RequestUri?.AbsolutePath.Contains("geocode", StringComparison.OrdinalIgnoreCase) == true)
                {
                    geocodeRequest = req;
                }
            }));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync(
            "1600 Amphitheatre Parkway", "Mountain View", "CA", "94043");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.MappingUnconfigured, Is.False);
        Assert.That(result.Precision, Is.EqualTo("geocode"));
        Assert.That(result.PlaceId, Is.EqualTo("ChIJgeocode"));
        Assert.That(result.Latitude, Is.EqualTo(37.422).Within(0.001));
        Assert.That(result.FormattedAddress, Does.Contain("Mountain View"));
        Assert.That(geocodeRequest, Is.Not.Null);
        Assert.That(geocodeRequest!.RequestUri!.Query, Does.Not.Contain("key="));
        Assert.That(geocodeRequest.Headers.Contains("X-Goog-Api-Key"), Is.True);
    }

    [Test]
    public async Task ValidateAndGeocode_ForbiddenApiNotEnabled_DoesNotMarkMappingUnconfigured()
    {
        var avJson = """
            {
              "error": {
                "code": 403,
                "message": "Address Validation API has not been used in project busbuddy-507301 before or it is disabled.",
                "status": "PERMISSION_DENIED",
                "details": [{ "reason": "SERVICE_DISABLED" }]
              }
            }
            """;
        using var http = new HttpClient(new SequenceStubHandler(
            (HttpStatusCode.Forbidden, avJson),
            (HttpStatusCode.Forbidden, "denied")));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Wiley", "CO", "81092");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.MappingUnconfigured, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("not enabled"));
    }

    [Test]
    public async Task ValidateAndGeocode_ForbiddenQuotaProject_DoesNotMarkMappingUnconfigured()
    {
        var avJson = """
            {
              "error": {
                "code": 403,
                "message": "Permission denied on the caller project's quota.",
                "status": "PERMISSION_DENIED",
                "details": [{ "reason": "USER_PROJECT_DENIED" }]
              }
            }
            """;
        using var http = new HttpClient(new SequenceStubHandler(
            (HttpStatusCode.Forbidden, avJson),
            (HttpStatusCode.Forbidden, "denied")));
        var client = new GoogleAddressValidationClient(http, Microsoft.Extensions.Options.Options.Create(TestOptions));

        var result = await client.ValidateAndGeocodeAsync("100 Main St", "Wiley", "CO", "81092");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.MappingUnconfigured, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("quota project"));
        Assert.That(result.ErrorMessage, Does.Contain("GCP_BILLING_PROJECT"));
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

    private sealed class SequenceStubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;
        private readonly Action<HttpRequestMessage>? _onRequest;

        public SequenceStubHandler(
            params (HttpStatusCode Status, string Body)[] responses)
            : this(null, responses)
        {
        }

        public SequenceStubHandler(
            Action<HttpRequestMessage>? onRequest,
            params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
            _onRequest = onRequest;
        }

        // Convenience overload used by the geocode-header assertion test.
        public SequenceStubHandler(
            (HttpStatusCode Status, string Body) first,
            (HttpStatusCode Status, string Body) second,
            Action<HttpRequestMessage>? onRequest)
            : this(onRequest, first, second)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke(request);
            var (status, body) = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
        }
    }
}
