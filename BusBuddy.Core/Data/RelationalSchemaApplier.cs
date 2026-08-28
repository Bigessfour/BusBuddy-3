using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;

namespace BusBuddy.Core.Data;

/// <summary>
/// Applies the EF migration chain on relational providers.
/// Refuses catalogs created with <c>EnsureCreated</c> (tables but no history).
/// </summary>
public static class RelationalSchemaApplier
{
    public static void Apply(DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (!database.IsRelational())
        {
            return;
        }

        ThrowIfEnsureCreatedCatalog(database);
        database.Migrate();
    }

    public static async Task ApplyAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (!database.IsRelational())
        {
            return;
        }

        ThrowIfEnsureCreatedCatalog(database);
        await database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static void ThrowIfEnsureCreatedCatalog(DatabaseFacade database)
    {
        if (!database.CanConnect())
        {
            return;
        }

        if (database.GetAppliedMigrations().Any())
        {
            return;
        }

        var creator = database.GetService<IRelationalDatabaseCreator>();
        if (!creator.HasTables())
        {
            return;
        }

        const string message =
            "Database has tables but no EF migration history (likely created with EnsureCreated). " +
            "Drop and recreate the catalog, or point BUSBUDDY_CONNECTION at a new database. " +
            "See Documentation/DATABASE-CONFIGURATION.md.";
        Log.Warning(message);
        throw new InvalidOperationException(message);
    }
}
