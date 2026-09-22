using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class GoogleRouteOptimizationServiceTests
{
    private static GoogleMapsOptions TestOptions => new()
    {
        ApiKey = "test-key",
        QuotaProject = "busbuddy-507301",
    };

    [Test]
    public void BuildRequestJson_IncludesVendorRequiredCostsAndUsProject()
    {
        var problem = RouteOptimizationVisitOrder.ForPinnedEnds(
            [
                new RouteOptimizationStop { Label = "start", Latitude = 38.09, Longitude = -102.62 },
                new RouteOptimizationStop { Label = "a", Latitude = 38.10, Longitude = -102.61 },
                new RouteOptimizationStop { Label = "b", Latitude = 38.11, Longitude = -102.60 },
                new RouteOptimizationStop { Label = "end", Latitude = 38.08, Longitude = -102.62 },
            ],
            seatingCapacity: 48,
            utcDay: new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc));

        var json = GoogleRouteOptimizationService.BuildRequestJson(problem);
        Assert.That(json, Does.Contain("costPerHour"));
        Assert.That(json, Does.Contain("costPerKilometer"));
        Assert.That(json, Does.Contain("loadLimits"));
        Assert.That(json, Does.Contain("\"label\":\"a\""));
        Assert.That(json, Does.Contain("RETURN_FAST"));
        Assert.That(json, Does.Contain("globalStartTime"));
        Assert.That(problem.Vehicles[0].Capacity, Is.EqualTo(48));
    }

    [Test]
    public void ParseResponse_ReadsVisitOrderAndSkippedLabels()
    {
        var problem = RouteOptimizationVisitOrder.ForPinnedEnds(
            [
                new RouteOptimizationStop { Label = "start", Latitude = 38.09, Longitude = -102.62 },
                new RouteOptimizationStop { Label = "a", Latitude = 38.10, Longitude = -102.61 },
                new RouteOptimizationStop { Label = "b", Latitude = 38.11, Longitude = -102.60 },
                new RouteOptimizationStop { Label = "end", Latitude = 38.08, Longitude = -102.62 },
            ],
            48,
            DateTime.UtcNow);

        var json = """
            {
              "routes": [{
                "vehicleLabel": "route",
                "visits": [
                  { "shipmentLabel": "b", "isPickup": true },
                  { "shipmentLabel": "a", "isPickup": true }
                ]
              }],
              "skippedShipments": []
            }
            """;

        var parsed = GoogleRouteOptimizationService.ParseResponse(json, problem);
        Assert.That(parsed.Succeeded, Is.True);
        Assert.That(parsed.Visits.Select(v => v.ShipmentLabel), Is.EqualTo(new[] { "b", "a" }));
    }

    [Test]
    public void MergePinnedOrder_KeepsStartAndEnd()
    {
        var merged = RouteOptimizationVisitOrder.MergePinnedOrder(
            ["start", "a", "b", "end"],
            [
                new OptimizedVisit { ShipmentLabel = "b", IsPickup = true },
                new OptimizedVisit { ShipmentLabel = "a", IsPickup = true },
            ]);

        Assert.That(merged, Is.EqualTo(new[] { "start", "b", "a", "end" }));
    }

    [Test]
    public async Task ComputePinnedOrder_ReturnsPinnedStartEndWithSolverMiddle()
    {
        var stops = new List<RouteStop>
        {
            new() { RouteStopId = 10, StopOrder = 1, Latitude = 38.07m, Longitude = -102.61m },
            new() { RouteStopId = 11, StopOrder = 2, Latitude = 38.08m, Longitude = -102.62m },
            new() { RouteStopId = 12, StopOrder = 3, Latitude = 38.09m, Longitude = -102.63m },
        };
        var optimization = new Mock<IRouteOptimizationService>();
        optimization.Setup(s => s.IsConfigured).Returns(true);
        optimization.Setup(s => s.OptimizeToursAsync(It.IsAny<OptimizeToursProblem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OptimizeToursResult.Ok(new[]
            {
                new OptimizedVisit { ShipmentLabel = "11", IsPickup = true }
            }));

        var result = await RouteStopOrderPlanner.ComputePinnedOrderAsync(
            stops,
            optimization.Object,
            seatingCapacity: 48,
            DateTime.UtcNow);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new[] { 10, 11, 12 }));
    }

    [Test]
    public void ForDropoffRun_PinsSchoolStartAndDepotEnd()
    {
        var school = new RouteOptimizationStop { Label = "school", Latitude = 38.08, Longitude = -102.62 };
        var depot = new RouteOptimizationStop { Label = "depot", Latitude = 38.09, Longitude = -102.63 };
        var problem = RouteOptimizationVisitOrder.ForDropoffRun(
            school,
            [
                new RouteOptimizationStop { Label = "0", Latitude = 38.10, Longitude = -102.61 },
                new RouteOptimizationStop { Label = "1", Latitude = 38.11, Longitude = -102.60 },
            ],
            depot,
            seatingCapacity: 48,
            utcDay: DateTime.UtcNow);

        Assert.That(problem.Vehicles[0].StartLatitude, Is.EqualTo(school.Latitude));
        Assert.That(problem.Vehicles[0].StartLongitude, Is.EqualTo(school.Longitude));
        Assert.That(problem.Vehicles[0].EndLatitude, Is.EqualTo(depot.Latitude));
        Assert.That(problem.Vehicles[0].EndLongitude, Is.EqualTo(depot.Longitude));
        Assert.That(problem.Shipments.Select(s => s.Label), Is.EqualTo(new[] { "0", "1" }));
    }

    [Test]
    public async Task OptimizeTours_PostsToProjectsOptimizeTours()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"routes":[]}""");
        using var http = new HttpClient(handler);
        var svc = new GoogleRouteOptimizationService(
            http,
            Options.Create(TestOptions),
            ownsHttpClient: false,
            accessTokens: new StaticAccessToken("unit-test-token"));
        var problem = RouteOptimizationVisitOrder.ForPinnedEnds(
            [
                new RouteOptimizationStop { Label = "start", Latitude = 38.09, Longitude = -102.62 },
                new RouteOptimizationStop { Label = "a", Latitude = 38.10, Longitude = -102.61 },
                new RouteOptimizationStop { Label = "end", Latitude = 38.08, Longitude = -102.62 },
            ],
            12,
            DateTime.UtcNow);

        await svc.OptimizeToursAsync(problem);

        Assert.That(handler.LastRequest, Is.Not.Null);
        Assert.That(handler.LastRequest!.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(handler.LastRequest.RequestUri!.AbsolutePath, Does.Contain("projects/busbuddy-507301:optimizeTours"));
        Assert.That(handler.LastRequest.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
        Assert.That(handler.LastRequest.Headers.Authorization?.Parameter, Is.EqualTo("unit-test-token"));
        Assert.That(handler.LastRequest.Headers.Contains("X-Goog-Api-Key"), Is.False);
        Assert.That(handler.LastRequestBody, Does.Contain("costPerHour"));
    }

    [Test]
    public async Task OptimizeTours_WithoutGoogleSignIn_DoesNotCallGoogle()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"routes":[]}""");
        using var http = new HttpClient(handler);
        var svc = new GoogleRouteOptimizationService(
            http,
            Options.Create(TestOptions),
            ownsHttpClient: false,
            accessTokens: new StaticAccessToken(null));
        var problem = RouteOptimizationVisitOrder.ForPinnedEnds(
            [
                new RouteOptimizationStop { Label = "start", Latitude = 38.09, Longitude = -102.62 },
                new RouteOptimizationStop { Label = "a", Latitude = 38.10, Longitude = -102.61 },
                new RouteOptimizationStop { Label = "end", Latitude = 38.08, Longitude = -102.62 },
            ],
            12,
            DateTime.UtcNow);

        var result = await svc.OptimizeToursAsync(problem);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("application-default login"));
        Assert.That(handler.LastRequest, Is.Null);
    }

    [Test]
    public async Task OptimizeTours_Unauthorized_FailOpenMessage()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden, """{"error":{"status":"PERMISSION_DENIED"}}""");
        using var http = new HttpClient(handler);
        var svc = new GoogleRouteOptimizationService(
            http,
            Options.Create(TestOptions),
            ownsHttpClient: false,
            accessTokens: new StaticAccessToken("unit-test-token"));
        var problem = RouteOptimizationVisitOrder.ForPinnedEnds(
            [
                new RouteOptimizationStop { Label = "start", Latitude = 38.09, Longitude = -102.62 },
                new RouteOptimizationStop { Label = "a", Latitude = 38.10, Longitude = -102.61 },
                new RouteOptimizationStop { Label = "end", Latitude = 38.08, Longitude = -102.62 },
            ],
            12,
            DateTime.UtcNow);

        var result = await svc.OptimizeToursAsync(problem);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Does.Contain("rejected the Google sign-in"));
    }

    private sealed class StaticAccessToken : IGoogleCloudAccessTokenSource
    {
        private readonly string? _token;

        public StaticAccessToken(string? token) => _token = token;

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_token);
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

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
