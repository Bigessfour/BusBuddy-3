using BusBuddy.Core.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
public class PostgresConnectionResolverTests
{
    [Test]
    public void RefreshHostIfNeeded_replaces_stale_host()
    {
        var current =
            "Host=192.168.1.153;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev";

        var refreshed = PostgresConnectionResolver.RefreshHostIfNeeded(current, "192.168.1.78");

        refreshed.Should().Contain("Host=192.168.1.78");
        refreshed.Should().NotContain("192.168.1.153");
    }

    [Test]
    public void RefreshHostIfNeeded_leaves_matching_host_unchanged()
    {
        var current =
            "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev";

        PostgresConnectionResolver.RefreshHostIfNeeded(current, "192.168.1.78").Should().Be(current);
    }

    [Test]
    public void DescribeEndpoint_returns_host_and_port()
    {
        PostgresConnectionResolver.DescribeEndpoint(
                "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=secret")
            .Should()
            .Be("192.168.1.78:5432");
    }

    [Test]
    public void EnsureConnectTimeout_appends_timeout_when_missing()
    {
        var withTimeout = PostgresConnectionResolver.EnsureConnectTimeout(
            "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev");

        withTimeout.Should().Contain("Timeout=5");
    }

    [Test]
    public void EnsureConnectTimeout_leaves_existing_timeout()
    {
        var current =
            "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev;Timeout=15";

        PostgresConnectionResolver.EnsureConnectTimeout(current).Should().Be(current);
    }

    [Test]
    public void BuildConnectionString_includes_short_connect_timeout()
    {
        PostgresConnectionResolver.BuildConnectionString("192.168.1.78")
            .Should()
            .Contain("Timeout=5");
    }
}
