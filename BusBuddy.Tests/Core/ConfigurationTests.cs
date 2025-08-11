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
        public void GetConnectionString_ExpandsEnvPlaceholders_ForAzure()
        {
            // Arrange: set temporary environment variables
            var prevUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            var prevPwd = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("AZURE_SQL_USER", "test_user");
                Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", "test_password");

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new KeyValuePair<string, string?>[]
                    {
                        new("DatabaseProvider", "Azure"),
                        new("ConnectionStrings:AzureConnection", "Server=tcp:server;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};")
                    })
                    .Build();

                // Act
                var conn = EnvironmentHelper.GetConnectionString(config);

                // Assert
                conn.Should().Contain("User ID=test_user");
                conn.Should().Contain("Password=test_password");
                conn.Should().NotContain("${AZURE_SQL_USER}");
                conn.Should().NotContain("${AZURE_SQL_PASSWORD}");
            }
            finally
            {
                // Restore env
                Environment.SetEnvironmentVariable("AZURE_SQL_USER", prevUser);
                Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", prevPwd);
            }
        }

        [Test]
        public void GetConnectionString_KeepsPlaceholders_WhenEnvUnset()
        {
            // Arrange: ensure variables are not set for this test
            var prevUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            var prevPwd = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("AZURE_SQL_USER", null);
                Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", null);

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new KeyValuePair<string, string?>[]
                    {
                        new("DatabaseProvider", "Azure"),
                        new("ConnectionStrings:AzureConnection", "Server=tcp:server;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};")
                    })
                    .Build();

                // Act
                var conn = EnvironmentHelper.GetConnectionString(config);

                // Assert
                conn.Should().Contain("${AZURE_SQL_USER}");
                conn.Should().Contain("${AZURE_SQL_PASSWORD}");
            }
            finally
            {
                // Restore env
                Environment.SetEnvironmentVariable("AZURE_SQL_USER", prevUser);
                Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", prevPwd);
            }
        }
    }
}
