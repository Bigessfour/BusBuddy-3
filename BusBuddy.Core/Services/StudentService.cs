using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service implementation for managing student transportation records
/// Provides CRUD operations and business logic for student management
/// </summary>
public partial class StudentService : IStudentService
{
    private static readonly ILogger Logger = Log.ForContext<StudentService>();
    private readonly IBusBuddyDbContextFactory _contextFactory;
    private readonly IGeocodingService? _geocodingService;

    public StudentService(
        IBusBuddyDbContextFactory contextFactory,
        IGeocodingService? geocodingService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _geocodingService = geocodingService;
    }

    // Context helpers: only dispose when using the concrete runtime factory
    // This prevents disposing shared in-memory contexts used by tests.
    private (BusBuddyDbContext Ctx, bool Dispose) GetReadContext()
    {
        var ctx = _contextFactory.CreateDbContext();
        var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
        return (ctx, shouldDispose);
    }

    private (BusBuddyDbContext Ctx, bool Dispose) GetWriteContext()
    {
        var ctx = _contextFactory.CreateWriteDbContext();
        var shouldDispose = _contextFactory is BusBuddy.Core.Data.BusBuddyDbContextFactory;
        return (ctx, shouldDispose);
    }

    #region Export

    public async Task<string> ExportStudentsToCsvAsync()
    {
        try
        {
            var students = await GetAllStudentsAsync();
            return StudentCsvExporter.ToCsv(students);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error exporting students to CSV");
            throw;
        }
    }

    #endregion

    #region DEBUG Instrumentation

#if DEBUG
    public Task<Dictionary<string, object>> GetStudentDiagnosticsAsync(int studentId) =>
        StudentDiagnostics.GetStudentDiagnosticsAsync(_contextFactory, studentId);

    public Task<Dictionary<string, object>> GetStudentOperationMetricsAsync() =>
        StudentDiagnostics.GetStudentOperationMetricsAsync(_contextFactory);
#endif

    #endregion
}
