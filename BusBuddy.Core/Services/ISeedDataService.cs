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
        /// Import students from a user-selected CSV. Two header shapes are accepted:
        /// <list type="bullet">
        /// <item>Roster format — one header row of named columns including <c>StudentName</c>; carries the
        /// assigned campus, AM/PM route, and special-needs / aide flags. See
        /// <c>Documentation/STUDENT-ROSTER-INTAKE.md</c>.</item>
        /// <item>Legacy family-export format — group row, then a header row with
        /// <c>Fname, Lname, Grade, Address</c>.</item>
        /// </list>
        /// Returns the number of students added (existing names are skipped).
        /// Latitude/longitude are never read from CSV; coordinates come from Address Validation.
        /// Throws <see cref="InvalidOperationException"/> when the header matches neither format.
        /// </summary>
        Task<int> ImportStudentsFromCsvAsync(string csvPath);

        /// <summary>
        /// Repairs student-to-route linkage so every assigned <c>AMRoute</c>/<c>PMRoute</c> resolves:
        /// route names that differ from an existing route only by word order are rewritten to the
        /// existing spelling, and any name with no route row at all gets one created.
        /// <para>
        /// Needed because <c>StudentService.ValidateStudentAsync</c> matches <c>Routes.RouteName</c>
        /// exactly, so a student assigned to a route that does not exist cannot be saved — or even
        /// edited — from the form.
        /// </para>
        /// </summary>
        /// <returns>The number of route rows created.</returns>
        Task<int> EnsureRoutesForStudentAssignmentsAsync();

        /// <summary>
        /// Seed sample drivers for development/testing
        /// </summary>
        Task SeedDriversAsync(int count = 10);

        /// <summary>
        /// Seed sample buses for development/testing
        /// </summary>
        Task SeedBusesAsync(int count = 12);

        /// <summary>
        /// Seed all development data
        /// </summary>
        Task SeedAllAsync();

        /// <summary>
        /// Idempotent prep for special-needs routing tests: school, SN bus/driver/route, sample students.
        /// </summary>
        Task<SpecialNeedsPrepSummary> SeedSpecialNeedsTransportPrepAsync();

        /// <summary>
        /// Seed school/student coordinates and one route polyline for the district map.
        /// Does not assign live bus GPS — fleet tracking is deferred.
        /// </summary>
        Task EnsureMapDemoGeoAsync();

        /// <summary>
        /// Clear all seeded data (use with caution!)
        /// </summary>
        Task ClearSeedDataAsync();
    }
}
