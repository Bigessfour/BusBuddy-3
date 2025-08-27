using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using BusBuddy.Core.Data;
using BusBuddy.Core.Domain;
using BusBuddy.Core.Services;
using System.Linq;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Base class for integration tests requiring database access
    /// Provides isolated, in-memory SQLite database for each test with transaction-based isolation
    /// Follows Microsoft EF Core testing best practices for proper test isolation
    /// </summary>
    public abstract class DatabaseTestBase : IDisposable
    {
        protected BusBuddyDbContext Context { get; private set; } = null!;
        protected IBusBuddyDbContextFactory ContextFactory { get; private set; } = null!;
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        private string? _oldBusBuddyConnection;
        private string? _oldAzureSqlUser;
        private string? _oldAzureSqlPassword;
        private IDbContextTransaction? _transaction;
        private bool _isDisposed;

        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            // Set environment variable to force in-memory database for tests
            Environment.SetEnvironmentVariable("BUSBUDDY_USE_INMEMORY", "1");

            // Clear environment variables to prevent production DB access
            _oldBusBuddyConnection = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            _oldAzureSqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            _oldAzureSqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_USER", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", null);

            // Configure test services - let the factory handle database creation
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "InMemory"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddMemoryCache(); // Add memory cache for IEnhancedCachingService
            services.AddSingleton<IEnhancedCachingService, EnhancedCachingService>();
            
            // Register the factory - it will handle DbContext creation with shared in-memory database
            services.AddScoped<IBusBuddyDbContextFactory, BusBuddyDbContextFactory>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IBusService, BusService>();
            services.AddScoped<IRouteService, RouteService>();
            services.AddScoped<IDriverService, DriverService>();
            services.AddScoped<IActivityService, ActivityService>();

            ServiceProvider = services.BuildServiceProvider();
            ContextFactory = ServiceProvider.GetRequiredService<IBusBuddyDbContextFactory>();
            Context = ContextFactory.CreateDbContext();

            // Create database schema
            Context.Database.EnsureCreated();

            // Seed test data once per test class
            TestDataSeeder.SeedDatabase(Context);
        }

        [SetUp]
        public virtual void SetUp()
        {
            // No transaction needed for in-memory database - each test gets a fresh context
            // Test isolation is achieved through OneTimeSetUp/OneTimeTearDown pattern
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (!_isDisposed)
            {
                // Rollback transaction to ensure test isolation (only if transaction exists)
                if (_transaction != null)
                {
                    try
                    {
                        _transaction.Rollback();
                    }
                    catch (Exception ex)
                    {
                        // Log transaction rollback failure but don't fail the test
                        TestContext.WriteLine($"Transaction rollback failed: {ex.Message}");
                    }
                    finally
                    {
                        _transaction.Dispose();
                        _transaction = null;
                    }
                }
            }
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            if (!_isDisposed)
            {
                // Dispose context and connection
                Context?.Dispose();
                ServiceProvider?.GetService<IServiceScope>()?.Dispose();

                // Restore environment variables
                Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", _oldBusBuddyConnection);
                Environment.SetEnvironmentVariable("AZURE_SQL_USER", _oldAzureSqlUser);
                Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", _oldAzureSqlPassword);
                Environment.SetEnvironmentVariable("BUSBUDDY_USE_INMEMORY", null);
            }
        }

        public virtual void Dispose()
        {
            if (!_isDisposed)
            {
                OneTimeTearDown();
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
