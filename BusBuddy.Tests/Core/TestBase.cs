using System;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Base class for database tests that provides proper isolation and cleanup
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected BusBuddyDbContext DbContext { get; private set; } = null!;
        private DbContextOptions<BusBuddyDbContext> _dbOptions = null!;
        private bool _disposed;

        protected void InitializeDatabase()
        {
            _dbOptions = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase(databaseName: $"{GetType().Name}_{Guid.NewGuid()}_{DateTime.Now.Ticks}")
                .Options;

            DbContext = new BusBuddyDbContext(_dbOptions);
            DbContext.Database.EnsureCreated();
        }

        protected void CleanupDatabase()
        {
            if (!_disposed && DbContext != null)
            {
                try
                {
                    DbContext.Database.EnsureDeleted();
                }
                catch (ObjectDisposedException)
                {
                    // Context already disposed, ignore
                }
                finally
                {
                    try
                    {
                        DbContext?.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Context already disposed, ignore
                    }
                    finally
                    {
                        DbContext = null!;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                CleanupDatabase();
                GC.SuppressFinalize(this);
            }
        }
    }
}
