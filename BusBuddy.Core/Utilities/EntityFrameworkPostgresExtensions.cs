using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Central Npgsql configuration per Microsoft EF Core connection resiliency guidance:
/// https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency
/// </summary>
public static class EntityFrameworkPostgresExtensions
{
    /// <summary>
    /// Call once at process startup before any Npgsql connection.
    /// <para>
    /// Deliberately a no-op. Every timestamp column in this database is <c>timestamp with time zone</c>, which is
    /// Npgsql's default mapping for <see cref="DateTime"/>. Turning on <c>Npgsql.EnableLegacyTimestampBehavior</c>
    /// would flip that default to <c>timestamp without time zone</c> and put the runtime model permanently at odds
    /// with both the schema and the model snapshot, which blocks every migration. Instants must therefore be written
    /// with <see cref="DateTimeKind.Utc"/>; see <c>DateTimeKindUtc</c> in <c>BusBuddyDbContext</c> for the wall-clock
    /// columns that are stored as face times instead.
    /// </para>
    /// </summary>
    public static void ConfigureNpgsqlAppContext()
    {
    }

    public static DbContextOptionsBuilder UseBusBuddyPostgres(
        this DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConfigureNpgsqlAppContext();

        return optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null));
    }
}
