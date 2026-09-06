using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Central Npgsql configuration per Microsoft EF Core connection resiliency guidance:
/// https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency
/// </summary>
public static class EntityFrameworkPostgresExtensions
{
    /// <summary>Call once at process startup before any Npgsql connection.</summary>
    public static void ConfigureNpgsqlAppContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public static DbContextOptionsBuilder UseBusBuddyPostgres(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
    }
}
