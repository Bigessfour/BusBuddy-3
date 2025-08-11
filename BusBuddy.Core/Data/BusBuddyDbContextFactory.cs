using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.Core.Data
{
    /// <summary>
    /// EF Core DbContext factory used both at runtime (manual scope creation) and design-time for migrations —
    /// Documentation: https://learn.microsoft.com/ef/core/cli/dbcontext-creation and https://learn.microsoft.com/ef/core/dbcontext-configuration/
    /// </summary>
    public class BusBuddyDbContextFactory : IBusBuddyDbContextFactory, IDesignTimeDbContextFactory<BusBuddyDbContext>
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly IConfiguration? _configuration;
        private static readonly ILogger Logger = Log.ForContext<BusBuddyDbContextFactory>();

        private const string DefaultConnectionString =
            "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=BusBuddy;Integrated Security=True;MultipleActiveResultSets=True";

        // Parameterless constructor for design-time tooling
        public BusBuddyDbContextFactory()
        {
            _serviceProvider = null;
            _configuration = null;
        }

        public BusBuddyDbContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _configuration = serviceProvider.GetService<IConfiguration>();
        }

        /// <summary>
        /// Creates a no‑tracking DbContext for read operations.
        /// </summary>
        public BusBuddyDbContext CreateDbContext()
        {
            // Highest precedence: environment override
            var envOverride = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();
                optionsBuilder.UseSqlServer(envOverride);
                var ctx = new BusBuddyDbContext(optionsBuilder.Options);
                ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return ctx;
            }

            if (_serviceProvider == null || _configuration == null)
            {
                // Design-time fallback
                return CreateDbContext(Array.Empty<string>());
            }

            var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(_configuration);
            var provider = _configuration["DatabaseProvider"] ?? "LocalDB";

            var configuredOptionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();
            ConfigureProvider(configuredOptionsBuilder, provider, connectionString);

            var configuredCtx = new BusBuddyDbContext(configuredOptionsBuilder.Options);
            configuredCtx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return configuredCtx;
        }

        /// <summary>
        /// Creates a tracking DbContext for write operations.
        /// </summary>
        public BusBuddyDbContext CreateWriteDbContext()
        {
            // Highest precedence: environment override
            var envOverride = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();
                optionsBuilder.UseSqlServer(envOverride);
                var writeCtxOverride = new BusBuddyDbContext(optionsBuilder.Options)
                {
                    ChangeTracker = { QueryTrackingBehavior = QueryTrackingBehavior.TrackAll }
                };
                return writeCtxOverride;
            }

            if (_serviceProvider == null || _configuration == null)
            {
                var ctx = CreateDbContext(Array.Empty<string>());
                ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
                return ctx;
            }

            var connectionString = BusBuddy.Core.Utilities.EnvironmentHelper.GetConnectionString(_configuration);
            var provider = _configuration["DatabaseProvider"] ?? "LocalDB";

            var optionsBuilderConfigured = new DbContextOptionsBuilder<BusBuddyDbContext>();
            ConfigureProvider(optionsBuilderConfigured, provider, connectionString);

            var writeCtx = new BusBuddyDbContext(optionsBuilderConfigured.Options);
            writeCtx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            return writeCtx;
        }

        /// <summary>
        /// Design-time creation (migrations / scaffolding). Fallback order:
        /// 1) BUSBUDDY_CONNECTION env override
        /// 2) AZURE_SQL_USER / AZURE_SQL_PASSWORD
        /// 3) LocalDB default
        /// </summary>
        public BusBuddyDbContext CreateDbContext(string[] args)
        {
            Logger.Information("Creating design-time BusBuddyDbContext");

            var optionsBuilder = new DbContextOptionsBuilder<BusBuddyDbContext>();

            // 1. Explicit override
            var envOverride = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            if (!string.IsNullOrWhiteSpace(envOverride))
            {
                Logger.Information("Using BUSBUDDY_CONNECTION environment override");
                optionsBuilder.UseSqlServer(envOverride, sql => sql.EnableRetryOnFailure());
                return new BusBuddyDbContext(optionsBuilder.Options);
            }

            // 2. Azure user/password fallback
            var azureUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            var azurePassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            if (!string.IsNullOrEmpty(azureUser) && !string.IsNullOrEmpty(azurePassword))
            {
                var azureConnectionString =
                    $"Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID={azureUser};Password={azurePassword};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;";
                Logger.Information("Using Azure SQL credential fallback for design-time context");
                optionsBuilder.UseSqlServer(azureConnectionString, sql => sql.EnableRetryOnFailure());
                return new BusBuddyDbContext(optionsBuilder.Options);
            }

            // 3. LocalDB final fallback
            Logger.Information("Using LocalDB default fallback for design-time context");
            optionsBuilder.UseSqlServer(DefaultConnectionString, sql => sql.EnableRetryOnFailure());
            return new BusBuddyDbContext(optionsBuilder.Options);
        }

        private static void ConfigureProvider(DbContextOptionsBuilder optionsBuilder, string provider, string connection)
        {
            // Provider selection (doc pattern: https://learn.microsoft.com/ef/core/dbcontext-configuration/)
            if (provider.Equals("LocalDB", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseSqlServer(connection);
            }
            else if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseSqlite(connection);
            }
            else
            {
                optionsBuilder.UseInMemoryDatabase("BusBuddyDb");
            }
        }
    }

    /// <summary>
    /// Interface abstraction for runtime factory usage.
    /// </summary>
    public interface IBusBuddyDbContextFactory
    {
        BusBuddyDbContext CreateDbContext();
        BusBuddyDbContext CreateWriteDbContext();
    }
}
