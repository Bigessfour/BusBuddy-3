using BusBuddy.Core.Configuration;
using BusBuddy.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class GoogleMapsOptionsQuotaProjectTests
{
    private string? _previousBilling;
    private string? _previousCloud;
    private string? _previousMapsKey;

    [SetUp]
    public void SetUp()
    {
        _previousBilling = Environment.GetEnvironmentVariable("GCP_BILLING_PROJECT");
        _previousCloud = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        _previousMapsKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", null);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
        Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", _previousBilling);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", _previousCloud);
        Environment.SetEnvironmentVariable("GOOGLE_MAPS_API_KEY", _previousMapsKey);
    }

    [Test]
    public void DescribeQuotaAndApiKeySource_ReportWhichInputWon()
    {
        Assert.That(GoogleMapsOptions.DescribeQuotaSource(null), Is.EqualTo("none"));
        Assert.That(GoogleMapsOptions.DescribeQuotaSource("from-json"), Is.EqualTo("config:GoogleMaps:QuotaProject"));

        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", "busbuddy-507301");
        Assert.That(GoogleMapsOptions.DescribeQuotaSource("from-json"), Is.EqualTo("env:GCP_BILLING_PROJECT"));

        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", null);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "busbuddy-507301");
        Assert.That(GoogleMapsOptions.DescribeQuotaSource("from-json"), Is.EqualTo("env:GOOGLE_CLOUD_PROJECT"));

        Assert.That(GoogleMapsOptions.DescribeApiKeySource(null), Is.EqualTo("none"));
        Assert.That(GoogleMapsOptions.DescribeApiKeySource("key"), Is.EqualTo("config:GoogleMaps:ApiKey"));
        Assert.That(GoogleMapsOptions.DescribeApiKeySource("${SECRET}"), Is.EqualTo("none"));
    }

    [Test]
    public void ResolveQuotaProject_PrefersBillingEnvOverCloudAndBound()
    {
        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", "busbuddy-507301");
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "new-coursera-490518");

        var resolved = GoogleMapsOptions.ResolveQuotaProject("stale-json-project");

        Assert.That(resolved, Is.EqualTo("busbuddy-507301"));
    }

    [Test]
    public void ResolveQuotaProject_UsesCloudEnvWhenBillingUnset()
    {
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "  busbuddy-507301  ");

        var resolved = GoogleMapsOptions.ResolveQuotaProject("stale-json-project");

        Assert.That(resolved, Is.EqualTo("busbuddy-507301"));
    }

    [Test]
    public void ResolveQuotaProject_UsesBoundConfigOrEmptyWhenUnset()
    {
        Assert.That(GoogleMapsOptions.ResolveQuotaProject("from-json"), Is.EqualTo("from-json"));
        Assert.That(GoogleMapsOptions.ResolveQuotaProject("  "), Is.EqualTo(string.Empty));
        Assert.That(GoogleMapsOptions.ResolveQuotaProject(null), Is.EqualTo(string.Empty));
        Assert.That(GoogleMapsOptions.CanonicalProjectId, Is.EqualTo("busbuddy-507301"));
    }

    [Test]
    public void AddGoogleMapsOptions_PostConfigureOverlaysBillingEnv()
    {
        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", "busbuddy-507301");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{GoogleMapsOptions.SectionName}:QuotaProject"] = "new-coursera-490518",
                [$"{GoogleMapsOptions.SectionName}:ApiKey"] = "test-key",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGoogleMapsOptions(configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;

        Assert.That(opts.QuotaProject, Is.EqualTo("busbuddy-507301"));
        Assert.That(opts.ApiKey, Is.EqualTo("test-key"));
    }

    [Test]
    public void AddGoogleMapsOptions_OmitsQuotaHeaderWhenEnvAndBoundEmpty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{GoogleMapsOptions.SectionName}:QuotaProject"] = "",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGoogleMapsOptions(configuration);
        using var sp = services.BuildServiceProvider();

        Assert.That(
            sp.GetRequiredService<IOptions<GoogleMapsOptions>>().Value.QuotaProject,
            Is.EqualTo(string.Empty));
    }

    [Test]
    public void Normalize_ClampsBiasRadiusAndDropsPartialCoordinates()
    {
        var partial = new GoogleMapsOptions
        {
            AutocompleteBiasRadiusMeters = 80_000,
            AutocompleteBiasLatitude = 38.0872
        };
        partial.Normalize();

        Assert.That(partial.AutocompleteBiasRadiusMeters, Is.EqualTo(GoogleMapsOptions.MaxAutocompleteBiasRadiusMeters));
        Assert.That(partial.AutocompleteBiasLatitude, Is.Null);
        Assert.That(partial.AutocompleteBiasLongitude, Is.Null);

        var invalidRadius = new GoogleMapsOptions { AutocompleteBiasRadiusMeters = 0 };
        invalidRadius.Normalize();
        Assert.That(invalidRadius.AutocompleteBiasRadiusMeters, Is.EqualTo(GoogleMapsOptions.MaxAutocompleteBiasRadiusMeters));
    }

    [Test]
    public void AddGoogleMapsOptions_NormalizesCompleteBias()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{GoogleMapsOptions.SectionName}:AutocompleteBiasRadiusMeters"] = "80000",
                [$"{GoogleMapsOptions.SectionName}:AutocompleteBiasLatitude"] = "38.0872",
                [$"{GoogleMapsOptions.SectionName}:AutocompleteBiasLongitude"] = "-102.6208",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGoogleMapsOptions(configuration);
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;

        Assert.That(opts.AutocompleteBiasRadiusMeters, Is.EqualTo(GoogleMapsOptions.MaxAutocompleteBiasRadiusMeters));
        Assert.That(opts.AutocompleteBiasLatitude, Is.EqualTo(38.0872).Within(0.0001));
        Assert.That(opts.AutocompleteBiasLongitude, Is.EqualTo(-102.6208).Within(0.0001));
    }
}
