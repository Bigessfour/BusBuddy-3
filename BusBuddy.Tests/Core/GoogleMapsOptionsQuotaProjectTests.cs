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

    [SetUp]
    public void SetUp()
    {
        _previousBilling = Environment.GetEnvironmentVariable("GCP_BILLING_PROJECT");
        _previousCloud = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", null);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("GCP_BILLING_PROJECT", _previousBilling);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", _previousCloud);
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
    public void ResolveQuotaProject_UsesBoundConfigThenDefault()
    {
        Assert.That(GoogleMapsOptions.ResolveQuotaProject("from-json"), Is.EqualTo("from-json"));
        Assert.That(GoogleMapsOptions.ResolveQuotaProject("  "), Is.EqualTo(GoogleMapsOptions.DefaultQuotaProject));
        Assert.That(GoogleMapsOptions.ResolveQuotaProject(null), Is.EqualTo("busbuddy-507301"));
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
    public void AddGoogleMapsOptions_KeepsBoundQuotaWhenEnvUnset()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{GoogleMapsOptions.SectionName}:QuotaProject"] = "busbuddy-507301",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGoogleMapsOptions(configuration);
        using var sp = services.BuildServiceProvider();

        Assert.That(
            sp.GetRequiredService<IOptions<GoogleMapsOptions>>().Value.QuotaProject,
            Is.EqualTo("busbuddy-507301"));
    }
}
