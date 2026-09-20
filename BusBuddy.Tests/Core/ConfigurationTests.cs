using System;
using System.IO;
using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using BusBuddy.Core.Utilities;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void GetConnectionString_ExpandsEnvPlaceholders_ForPostgres()
        {
            var prevPwd = Environment.GetEnvironmentVariable("BUSBUDDY_PG_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("BUSBUDDY_PG_PASSWORD", "pg_ci_placeholder");

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new KeyValuePair<string, string?>[]
                    {
                        new("DatabaseProvider", "Postgres"),
                        new("ConnectionStrings:PostgresConnection", "Host=localhost;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=${BUSBUDDY_PG_PASSWORD}")
                    })
                    .Build();

                var conn = EnvironmentHelper.GetConnectionString(config);

                conn.Should().Contain("Password=pg_ci_placeholder");
                conn.Should().NotContain("${BUSBUDDY_PG_PASSWORD}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("BUSBUDDY_PG_PASSWORD", prevPwd);
            }
        }

        [Test]
        public void GetConnectionString_FallsBackToLocalDb_WhenPlaceholdersUnresolved()
        {
            var prevConn = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            try
            {
                Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", null);

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new KeyValuePair<string, string?>[]
                    {
                        new("DatabaseProvider", "LocalDB"),
                        new("ConnectionStrings:DefaultConnection", "Server=tcp:server;User ID=${MISSING_USER};Password=${MISSING_PASSWORD};")
                    })
                    .Build();

                var conn = EnvironmentHelper.GetConnectionString(config);

                conn.Should().Contain("(localdb)");
                conn.Should().NotContain("${MISSING_USER}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", prevConn);
            }
        }

        [Test]
        public void GetDatabaseProvider_UsesPostgres_WhenConnectionIsHost()
        {
            var prevPwd = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            try
            {
                Environment.SetEnvironmentVariable(
                    "BUSBUDDY_CONNECTION",
                    "Host=192.168.64.1;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=local-test");

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new KeyValuePair<string, string?>[]
                    {
                        new("DatabaseProvider", "SqlServer"),
                        new("ConnectionStrings:PostgresConnection", "Host=localhost;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=pg_ci_placeholder"),
                        new("ConnectionStrings:BusBuddyDatabase", "Data Source=BusBuddy.db")
                    })
                    .Build();

                EnvironmentHelper.GetDatabaseProvider(config).Should().Be("Postgres");
                EnvironmentHelper.IsUsingPostgres(config).Should().BeTrue();
                EnvironmentHelper.GetConnectionString(config).Should().Contain("Host=");
                EnvironmentHelper.GetConnectionString(config).Should().NotContain("BusBuddy.db");
            }
            finally
            {
                Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", prevPwd);
            }
        }

        [Test]
        public void GuestLaunchers_PinPostgresProviderAfterDotEnv()
        {
            var dir = TestContext.CurrentContext.TestDirectory;
            string? root = null;
            while (dir is not null)
            {
                var candidate = Path.Combine(dir, "utm_run_in_vm.ps1");
                if (File.Exists(candidate))
                {
                    root = dir;
                    break;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            Assert.That(root, Is.Not.Null, "utm_run_in_vm.ps1");
            var launcher = File.ReadAllText(Path.Combine(root!, "utm_run_in_vm.ps1"));
            Assert.That(launcher, Does.Contain("Set-BusBuddyPostgresProvider"));
            Assert.That(launcher, Does.Contain("if ($name -eq 'DatabaseProvider' -and $value -eq 'Azure')"));
            Assert.That(
                launcher.LastIndexOf("Set-BusBuddyPostgresProvider", StringComparison.Ordinal),
                Is.GreaterThan(launcher.IndexOf("Loaded keys/.env from share.", StringComparison.Ordinal)));
            var schtasks = File.ReadAllText(Path.Combine(root!, "Scripts", "UtmLaunchWpf.ps1"));
            Assert.That(schtasks, Does.Contain("DatabaseProvider"));
            Assert.That(schtasks, Does.Contain("Postgres"));
        }
    }
}
