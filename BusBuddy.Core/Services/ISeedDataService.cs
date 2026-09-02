namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Interface for seeding development data
    /// </summary>
    public interface ISeedDataService
    {
        /// <summary>
        /// Seed sample activity logs for development/testing
        /// </summary>
        Task SeedActivityLogsAsync(int count = 50);

        /// <summary>
        /// Seed students from real-world CSV data (BusRiders_25-26.xlsz.csv)
        /// </summary>
        Task SeedStudentsFromCsvAsync();

        /// <summary>
        /// Import students from a user-selected CSV (Fname, Lname, Grade, Address header).
        /// Returns the number of students added (existing names are skipped).
        /// Throws <see cref="InvalidOperationException"/> when the header is not the expected format.
        /// </summary>
        Task<int> ImportStudentsFromCsvAsync(string csvPath);

        /// <summary>
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
        /// Idempotent prep for special-needs routing tests: school, SN bus/driver/route, sample students.
        /// </summary>
        Task<SpecialNeedsPrepSummary> SeedSpecialNeedsTransportPrepAsync();

        /// <summary>
        /// Clear all seeded data (use with caution!)
        /// </summary>
        Task ClearSeedDataAsync();
    }
}
