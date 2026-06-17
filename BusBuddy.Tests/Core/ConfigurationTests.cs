using System;
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
                Environment.SetEnvironmentVariable("BUSBUDDY_PG_PASSWORD", "test_password");

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new KeyValuePair<string, string?>[]
                    {
                        new("DatabaseProvider", "Postgres"),
                        new("ConnectionStrings:PostgresConnection", "Host=localhost;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=${BUSBUDDY_PG_PASSWORD}")
                    })
                    .Build();

                var conn = EnvironmentHelper.GetConnectionString(config);

                conn.Should().Contain("Password=test_password");
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
    }
}
