using System;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Tests.Helpers
{
    /// <summary>
    /// Test implementation of IBusBuddyDbContextFactory for unit tests.
    /// Provides isolated in-memory database contexts for each test.
    /// </summary>
    public class TestDbContextFactory : IBusBuddyDbContextFactory, IDisposable
    {
        private readonly DbContextOptions<BusBuddyDbContext> _options;
        private bool _disposed;

        public TestDbContextFactory(DbContextOptions<BusBuddyDbContext> options)
        {
            _options = options;
        }

        public BusBuddyDbContext CreateDbContext()
        {
            var context = new BusBuddyDbContext(_options);
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return context;
        }

        public BusBuddyDbContext CreateWriteDbContext()
        {
            return new BusBuddyDbContext(_options);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
