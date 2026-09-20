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
            "Host=192.168.1.153;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test";

        var refreshed = PostgresConnectionResolver.RefreshHostIfNeeded(current, "192.168.1.78");

        refreshed.Should().Contain("Host=192.168.1.78");
        refreshed.Should().NotContain("192.168.1.153");
    }

    [Test]
    public void RefreshHostIfNeeded_leaves_matching_host_unchanged()
    {
        var current =
            "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test";

        PostgresConnectionResolver.RefreshHostIfNeeded(current, "192.168.1.78").Should().Be(current);
    }

    [Test]
    public void RefreshHostIfNeeded_leaves_loopback_host_unchanged()
    {
        var current =
            "Host=localhost;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test";

        PostgresConnectionResolver.RefreshHostIfNeeded(current, "192.168.64.1").Should().Be(current);
    }

    [Test]
    public void DescribeEndpoint_returns_host_and_port()
    {
        PostgresConnectionResolver.DescribeEndpoint(
                "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test")
            .Should()
            .Be("192.168.1.78:5432");
    }

    [Test]
    public void EnsureConnectTimeout_appends_timeout_when_missing()
    {
        var withTimeout = PostgresConnectionResolver.EnsureConnectTimeout(
            "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test");

        withTimeout.Should().Contain("Timeout=5");
    }

    [Test]
    public void EnsureConnectTimeout_leaves_existing_timeout()
    {
        var current =
            "Host=192.168.1.78;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test;Timeout=15";

        PostgresConnectionResolver.EnsureConnectTimeout(current).Should().Be(current);
    }

    [Test]
    public void ResolveProvider_defaults_to_postgres()
    {
        PostgresConnectionResolver.ResolveProvider(null, null).Should().Be("Postgres");
        PostgresConnectionResolver.ResolveProvider("", null).Should().Be("Postgres");
    }

    [Test]
    public void ResolveProvider_live_host_connection_is_postgres()
    {
        var conn =
            "Host=192.168.64.1;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test";

        PostgresConnectionResolver.ResolveProvider("SqlServer", conn).Should().Be("Postgres");
        PostgresConnectionResolver.ResolveProvider("LocalDB", conn).Should().Be("Postgres");
    }

    [Test]
    public void ResolveProvider_keeps_explicit_localdb_without_postgres_connection()
    {
        PostgresConnectionResolver.ResolveProvider("LocalDB", null).Should().Be("LocalDB");
        PostgresConnectionResolver.ResolveProvider("SqlServer", null).Should().Be("SqlServer");
        PostgresConnectionResolver.ResolveProvider("Local", null).Should().Be("Local");
    }

    [Test]
    public void ApplyDatabaseProvider_sets_postgres_from_live_connection()
    {
        var prevProvider = Environment.GetEnvironmentVariable("DatabaseProvider");
        var prevConn = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("DatabaseProvider", "SqlServer");
            var conn =
                "Host=192.168.64.1;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test";
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", conn);

            PostgresConnectionResolver.ApplyDatabaseProvider(conn)
                .Should()
                .Be("Postgres");
            Environment.GetEnvironmentVariable("DatabaseProvider").Should().Be("Postgres");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DatabaseProvider", prevProvider);
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", prevConn);
        }
    }

    [Test]
    public void BuildConnectionString_includes_short_connect_timeout()
    {
        PostgresConnectionResolver.BuildConnectionString("192.168.1.78")
            .Should()
            .Contain("Timeout=5");
    }
}
