using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Core.Data;

/// <summary>
/// Factory for creating DbContext instances, useful for async and multi-threaded scenarios
/// where a context might be needed outside the standard DI lifecycle
/// </summary>
public class BusBuddyDbContextFactory : IBusBuddyDbContextFactory, IDesignTimeDbContextFactory<BusBuddyDbContext>
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly IConfiguration? _configuration;
    private static readonly ILogger Logger = Log.ForContext<BusBuddyDbContextFactory>();

    // Default connection string for design-time and fallback scenarios
    private const string DefaultConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True";

    // Parameterless constructor for design-time services
    public BusBuddyDbContextFactory()
    {
        _serviceProvider = null;
        _configuration = null;
    }

    public BusBuddyDbContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _configuration = serviceProvider?.GetService<IConfiguration>();
    }

    /// <summary>
    /// Creates a new instance of BusBuddyDbContext with proper configuration
    /// Use this method when you need a fresh context outside the DI lifecycle
    /// </summary>
    /// <returns>A new instance of BusBuddyDbContext</returns>
    public BusBuddyDbContext CreateDbContext()
    {
        if (_serviceProvider == null || _configuration == null)
        {
            // Fallback for design-time scenarios
            return CreateDbContext(Array.Empty<string>());
        }

        // Get configuration-based connection string and provider
        var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(_configuration);
        var databaseProvider = _configuration["DatabaseProvider"] ?? "LocalDB";

        var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();

        // Configure based on database provider
        if (databaseProvider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else if (databaseProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else if (databaseProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else
        {
            // Default to in-memory for unknown providers
            optionsBuilder.UseInMemoryDatabase("BusBuddyDb");
        }

        var context = new BusBuddyDbContext(optionsBuilder.Options);

        // Configure query tracking to improve performance for read-only operations
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        return context;
    }

    /// <summary>
    /// Creates a new instance of BusBuddyDbContext for write operations
    /// </summary>
    /// <returns>A new instance of BusBuddyDbContext configured for tracking changes</returns>
    public BusBuddyDbContext CreateWriteDbContext()
    {
        if (_serviceProvider == null || _configuration == null)
        {
            // Fallback for design-time scenarios
            var context = CreateDbContext(Array.Empty<string>());
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            return context;
        }

        // Get configuration-based connection string and provider
        var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(_configuration);
        var databaseProvider = _configuration["DatabaseProvider"] ?? "LocalDB";

        var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();

        // Configure based on database provider
        if (databaseProvider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else if (databaseProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else if (databaseProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else
        {
            // Default to in-memory for unknown providers
            optionsBuilder.UseInMemoryDatabase("BusBuddyDb");
        }

        var dbContext = new BusBuddyDbContext(optionsBuilder.Options);

        // Configure for tracking entities when we need to make changes
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        return dbContext;
    }

    /// <summary>
    /// Creates a new instance of BusBuddyDbContext for design-time use
    /// This is typically used by EF Core tools for migrations and scaffolding
    /// </summary>
    /// <param name="args">Command-line arguments (not used)</param>
    /// <returns>A new instance of BusBuddyDbContext configured for design-time services</returns>
    public BusBuddyDbContext CreateDbContext(string[] args)
    {
        Logger.Information("Creating DbContext for design-time services");

        var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();

        // Use Azure connection string with environment variables for design-time
        var azureUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
        var azurePassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");

        if (!string.IsNullOrEmpty(azureUser) && !string.IsNullOrEmpty(azurePassword))
        {
            var azureConnectionString = $"Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID={azureUser};Password={azurePassword};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;";
            optionsBuilder.UseSqlServer(azureConnectionString, sql => sql.EnableRetryOnFailure());
            Logger.Information("Using Azure connection string for design-time DbContext");
        }
        else
        {
            optionsBuilder.UseSqlServer(DefaultConnectionString);
            Logger.Information("Using default LocalDB connection string for design-time DbContext");
        }

        return new BusBuddyDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Interface for the DbContext factory to enable proper dependency injection
/// </summary>
public interface IBusBuddyDbContextFactory
{
    BusBuddyDbContext CreateDbContext();
    BusBuddyDbContext CreateWriteDbContext();
}
