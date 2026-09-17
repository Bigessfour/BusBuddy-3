namespace BusBuddy.Core.Services;

/// <summary>
/// Clerk-facing projection for a printed route sheet. Numbers and clocks only;
/// <see cref="RouteSummaryPdfRenderer"/> draws it.
/// </summary>
public sealed class RouteSummarySheet
{
    public required string Title { get; init; }
    public required string DisplayName { get; init; }
    public required string FullRouteName { get; init; }
    public required bool IsGeneratedName { get; init; }
    public required string School { get; init; }
    public required string District { get; init; }
    public required string SessionLabel { get; init; }
    public required string ServiceDate { get; init; }
    public required string DriverLabel { get; init; }
    public required string BusLabel { get; init; }
    public required string DepartureText { get; init; }
    public required string ArrivalText { get; init; }
    public required string TotalMilesText { get; init; }
    public required int RosterCount { get; init; }
    public required string? GenerateStopsOnlyNote { get; init; }
    public required IReadOnlyList<StopRow> Stops { get; init; }
    public required IReadOnlyList<StudentRow> Students { get; init; }

    public sealed record StopRow(
        int Sequence,
        string Name,
        string Address,
        string Arrival,
        string Departure,
        string Miles,
        string Cumulative,
        string Riders);

    public sealed record StudentRow(string Name, string Grade, string Stop);
}
