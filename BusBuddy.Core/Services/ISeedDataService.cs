namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Interface for seeding development data
    /// </summary>
    public interface ISeedDataService
    {
        /// <summary>
        /// Seed sample activity logs for development/testing
        /// <summary>
        /// Seed students from real-world CSV data (BusRiders_25-26.xlsz.csv)
        /// </summary>
        Task SeedStudentsFromCsvAsync();
        /// Seed sample drivers for development/testing
        /// </summary>
        Task SeedDriversAsync(int count = 10);

        /// <summary>
        /// Seed sample buses for development/testing
        /// </summary>
        Task SeedBusesAsync(int count = 12);

        /// <summary>
        /// Seed sample activities for development/testing
        /// </summary>
        Task SeedActivitiesAsync(int count = 25);

        /// <summary>
        /// Seed all development data
        /// </summary>
        Task SeedAllAsync();

        /// <summary>
        /// Clear all seeded data (use with caution!)
        /// </summary>
        Task ClearSeedDataAsync();
    }
}
