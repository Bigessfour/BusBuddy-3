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
public class GoogleRoutingServiceTests
{
    [Test]
    public async Task ComputeDrivePath_WithPolyline_ReturnsPoints()
    {
        // Encoded polyline for a trivial two-point path (decoded by codec under test via response).
        var json = """
            {
              "routes": [{
                "distanceMeters": 1200,
                "duration": "180s",
                "polyline": { "encodedPolyline": "_p~iF~ps|U_ulLnnqC_mqNvxq`@" }
              }]
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var svc = new GoogleRoutingService(http, Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }));

        var result = await svc.ComputeDrivePathAsync(
            (38.15, -102.72),
            (38.16, -102.71),
            Array.Empty<(double, double)>());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.DistanceMeters, Is.EqualTo(1200));
        Assert.That(result.EncodedPolyline, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Points.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task ComputeDrivePath_MissingKey_ReturnsError()
    {
        var previous = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", null);
            using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"));
            var svc = new GoogleRoutingService(
                http,
                Options.Create(new GoogleMapsOptions { ApiKey = "${GOOGLE_MAPS_API_KEY}" }));

            var result = await svc.ComputeDrivePathAsync((1, 2), (3, 4), Array.Empty<(double, double)>());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("not configured"));
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
            Assert.That(request.Headers.Contains("X-Goog-FieldMask"), Is.True);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }
}
