using System.IO;
using System.Text.RegularExpressions;
using BusBuddy.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;

namespace BusBuddy.Core.Data;

/// <summary>
/// Connection fallback for <c>new BusBuddyDbContext()</c> when the factory did not pass options.
/// Runtime scopes use <see cref="BusBuddyDbContextFactory"/>; this is the parameterless-ctor path only.
/// </summary>
internal static class UnconfiguredDbContextOptions
{
    private static readonly Regex Placeholder = new(
        @"\$\{(?<name>[A-Za-z0-9_]+)\}",
        RegexOptions.Compiled);

    internal static void Apply(DbContextOptionsBuilder optionsBuilder)
    {
        var logger = Log.ForContext<BusBuddyDbContext>();
        logger.Information("Starting DbContext configuration (dynamic)");

        var envOverride = PostgresConnectionResolver.ResolveAndApply()
                          ?? Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            logger.Information("Using BUSBUDDY_CONNECTION environment override");
            if (PostgresConnectionResolver.IsPostgresConnection(envOverride))
            {
                logger.Information("Detected Postgres connection in BUSBUDDY_CONNECTION - using Npgsql provider");
                optionsBuilder.UseBusBuddyPostgres(envOverride);
            }
            else
            {
                optionsBuilder.UseSqlServer(envOverride, sql =>
                {
                    sql.CommandTimeout(60);
                    sql.EnableRetryOnFailure();
                });
            }

            ConfigureEfLogging(optionsBuilder);
            return;
        }

        if (TryUseAppSettings(optionsBuilder, logger))
        {
            return;
        }

        const string fallback =
            "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True";
        logger.Information("Using LocalDB fallback connection");
        optionsBuilder.UseSqlServer(fallback, sql =>
        {
            sql.CommandTimeout(60);
            sql.EnableRetryOnFailure(
                5,
                TimeSpan.FromSeconds(10),
                new[] { 40613, 40501, 40197, 10928, 10929, 10060, 10054, 10053 });
        });
        ConfigureEfLogging(optionsBuilder);
    }

    private static bool TryUseAppSettings(DbContextOptionsBuilder optionsBuilder, ILogger logger)
    {
        try
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connString = config.GetConnectionString("BusBuddyDb") ?? config.GetConnectionString("DefaultConnection");
            var expandedConn = ExpandPlaceholders(connString);
            if (string.IsNullOrWhiteSpace(connString))
            {
                return false;
            }

            logger.Information(
                "Using connection string from appsettings: {ConnectionName}",
                config.GetConnectionString("BusBuddyDb") != null ? "BusBuddyDb" : "DefaultConnection");
            if (!string.Equals(connString, expandedConn, StringComparison.Ordinal))
            {
                logger.Information("Expanded environment variables in connection string");
            }

            var dbProvider = config["DatabaseProvider"] ?? "";
            if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                expandedConn.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                expandedConn.Contains("postgres", StringComparison.OrdinalIgnoreCase))
            {
                logger.Information("Using Postgres provider from appsettings/config");
                optionsBuilder.UseNpgsql(expandedConn);
            }
            else
            {
                optionsBuilder.UseSqlServer(expandedConn, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        5,
                        TimeSpan.FromSeconds(10),
                        new[] { 40613, 40501, 40197, 10928, 10929, 10060, 10054, 10053 });
                    sqlOptions.CommandTimeout(60);
                });
            }

            ConfigureEfLogging(optionsBuilder);
            return true;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to load connection string from appsettings");
            return false;
        }
    }

    private static string ExpandPlaceholders(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(cs);
        return Placeholder.Replace(expanded, match =>
        {
            var value = Environment.GetEnvironmentVariable(match.Groups["name"].Value);
            return value ?? match.Value;
        });
    }

    private static void ConfigureEfLogging(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.LogTo(message =>
        {
            using (LogContext.PushProperty("DatabaseContext", "BusBuddyDbContext"))
            using (LogContext.PushProperty("SourceContext", "EntityFramework"))
            {
                var innerLogger = Log.ForContext("SourceContext", "BusBuddyDbContext");
                if (message.Contains("warn", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("warning", StringComparison.OrdinalIgnoreCase))
                {
                    innerLogger.Warning("EF Core: {Message}", message);
                }
                else if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                         message.Contains("exception", StringComparison.OrdinalIgnoreCase))
                {
                    innerLogger.Error("EF Core: {Message}", message);
                }
                else
                {
                    innerLogger.Information("EF Core: {Message}", message);
                }
            }
        });

        if (System.Diagnostics.Debugger.IsAttached)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }
}
