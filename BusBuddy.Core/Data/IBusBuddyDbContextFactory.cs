using System;

namespace BusBuddy.Core.Data
{
    /// <summary>
    /// Factory interface for creating BusBuddyDbContext instances.
    /// Enables dependency injection and test isolation.
    /// </summary>
    public interface IBusBuddyDbContextFactory
    {
        /// <summary>
        /// Creates a new BusBuddyDbContext instance for read operations.
        /// </summary>
        /// <returns>A new BusBuddyDbContext instance.</returns>
        BusBuddyDbContext CreateDbContext();

        /// <summary>
        /// Creates a new BusBuddyDbContext instance for write operations.
        /// </summary>
        /// <returns>A new BusBuddyDbContext instance.</returns>
        BusBuddyDbContext CreateWriteDbContext();
    }
}
