using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;

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
        Assert.That(result.Steps, Is.Empty);
    }

    [Test]
    public async Task ComputeDrivePath_ReadsTurnInstructions()
    {
        var json = """
            {
              "routes": [{
                "distanceMeters": 800,
                "duration": "90s",
                "polyline": { "encodedPolyline": "_p~iF~ps|U_ulLnnqC_mqNvxq`@" },
                "legs": [{
                  "steps": [
                    {
                      "distanceMeters": 200,
                      "navigationInstruction": { "instructions": "Head <b>north</b> on Main St" }
                    },
                    {
                      "distanceMeters": 600,
                      "navigationInstruction": { "instructions": "Turn left onto Oak St" }
                    }
                  ]
                }]
              }]
            }
            """;
        string? fieldMask = null;
        using var http = new HttpClient(new CapturingStubHandler(HttpStatusCode.OK, json, mask => fieldMask = mask));
        var svc = new GoogleRoutingService(http, Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }));

        var result = await svc.ComputeDrivePathAsync((38.15, -102.72), (38.16, -102.71), Array.Empty<(double, double)>());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(fieldMask, Does.Contain("navigationInstruction"));
        Assert.That(result.Steps, Has.Count.EqualTo(2));
        Assert.That(result.Steps[0], Does.Contain("Head north on Main St").And.Not.Contain("<b>"));
        Assert.That(result.Steps[1], Does.StartWith("Turn left onto Oak St"));
        Assert.That(fieldMask, Does.Contain("routes.legs.duration"));
        Assert.That(result.LegDurationSeconds, Is.Empty);
    }

    [Test]
    public async Task ComputeDrivePath_ReadsLegDurations()
    {
        var json = """
            {
              "routes": [{
                "distanceMeters": 800,
                "duration": "90s",
                "polyline": { "encodedPolyline": "_p~iF~ps|U_ulLnnqC_mqNvxq`@" },
                "legs": [
                  { "duration": "40s" },
                  { "duration": "50.4s" }
                ]
              }]
            }
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var svc = new GoogleRoutingService(http, Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }));

        var result = await svc.ComputeDrivePathAsync(
            (38.15, -102.72),
            (38.16, -102.71),
            new[] { (38.155, -102.715) });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.LegDurationSeconds, Is.EqualTo(new[] { 40, 50 }));
    }

    [Test]
    public async Task ComputeDrivePath_Http400_LogsStatusAndErrorBodyWithoutApiKey()
    {
        const string key = "test-key";
        var json = """
            {
              "error": {
                "code": 400,
                "message": "Request contains an invalid argument.",
                "status": "INVALID_ARGUMENT",
                "details": [{
                  "fieldViolations": [{
                    "field": "intermediates",
                    "description": "Repeated consecutive waypoints are not allowed. X-Goog-Api-Key: test-key AIzaSyEXAMPLEKEY1234567890"
                  }]
                }]
              }
            }
            """;
        var sink = new CollectingSink();
        var testLogger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var previous = Log.Logger;
        Log.Logger = testLogger;
        try
        {
            using var http = new HttpClient(new StubHandler(HttpStatusCode.BadRequest, json));
            var svc = new GoogleRoutingService(http, Options.Create(new GoogleMapsOptions { ApiKey = key }));
            var result = await svc.ComputeDrivePathAsync(
                (38.15, -102.72),
                (38.16, -102.71),
                Array.Empty<(double, double)>());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("HTTP 400"));
            Assert.That(result.Error, Does.Contain("invalid argument"));
            Assert.That(result.Error, Does.Contain("intermediates"));
            Assert.That(result.Error, Does.Not.Contain(key));
            Assert.That(result.Error, Does.Not.Contain("AIzaSyEXAMPLEKEY1234567890"));
        }
        finally
        {
            Log.Logger = previous;
            (testLogger as IDisposable)?.Dispose();
        }

        var logged = sink.Rendered()
            .Where(message => message.Contains("Routes API HTTP", StringComparison.Ordinal))
            .ToList();
        Assert.That(logged, Has.Count.EqualTo(1));
        Assert.That(logged[0], Does.Contain("400"));
        Assert.That(logged[0], Does.Contain("intermediates"));
        Assert.That(logged[0], Does.Not.Contain(key));
        Assert.That(logged[0], Does.Not.Contain("AIzaSyEXAMPLEKEY1234567890"));
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

    [Test]
    public async Task ComputeRouteMatrix_ReturnsElements()
    {
        var json = """
            [
              {
                "originIndex": 0,
                "destinationIndex": 0,
                "distanceMeters": 5000,
                "duration": "600s",
                "condition": "ROUTE_EXISTS"
              },
              {
                "originIndex": 0,
                "destinationIndex": 1,
                "distanceMeters": 12000,
                "duration": "900s",
                "condition": "ROUTE_EXISTS"
              }
            ]
            """;
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, json));
        var svc = new GoogleRoutingService(http, Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }));

        var result = await svc.ComputeRouteMatrixAsync(
            (38.15, -102.72),
            new[] { (38.16, -102.71), (37.08, -102.62) });

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].DestinationIndex, Is.EqualTo(0));
        Assert.That(result[0].DistanceMeters, Is.EqualTo(5000));
        Assert.That(result[0].Duration, Is.EqualTo("600s"));
        Assert.That(result[0].Succeeded, Is.True);
        Assert.That(result[1].DestinationIndex, Is.EqualTo(1));
        Assert.That(result[1].DistanceMeters, Is.EqualTo(12000));
    }

    [Test]
    public async Task ComputeRouteMatrix_FieldMaskIncludesCondition()
    {
        var json = """[{"destinationIndex":0,"distanceMeters":1,"duration":"1s","condition":"ROUTE_EXISTS"}]""";
        string? fieldMask = null;
        using var http = new HttpClient(new CapturingStubHandler(HttpStatusCode.OK, json, mask => fieldMask = mask));
        var svc = new GoogleRoutingService(http, Options.Create(new GoogleMapsOptions { ApiKey = "test-key" }));

        _ = await svc.ComputeRouteMatrixAsync((1, 2), new[] { (3.0, 4.0) });

        Assert.That(fieldMask, Does.Contain("condition"));
    }

    [Test]
    public async Task ComputeRouteMatrix_MissingKey_ReturnsEmpty()
    {
        var previous = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", null);
            using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"));
            var svc = new GoogleRoutingService(
                http,
                Options.Create(new GoogleMapsOptions { ApiKey = "${GOOGLE_MAPS_API_KEY}" }));

            var result = await svc.ComputeRouteMatrixAsync((1, 2), new[] { (3.0, 4.0) });

            Assert.That(result, Is.Empty);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", previous);
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<string> _messages = new();

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level != LogEventLevel.Warning)
            {
                return;
            }

            lock (_messages)
            {
                _messages.Add(logEvent.RenderMessage());
            }
        }

        public string[] Rendered()
        {
            lock (_messages)
            {
                return _messages.ToArray();
            }
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

    private sealed class CapturingStubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Action<string?> _onFieldMask;

        public CapturingStubHandler(HttpStatusCode status, string body, Action<string?> onFieldMask)
        {
            _status = status;
            _body = body;
            _onFieldMask = onFieldMask;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues("X-Goog-FieldMask", out var values))
            {
                _onFieldMask(string.Join(",", values));
            }
            else
            {
                _onFieldMask(null);
            }

            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }
}
