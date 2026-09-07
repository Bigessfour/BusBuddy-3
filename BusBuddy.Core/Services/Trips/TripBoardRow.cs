namespace BusBuddy.Core.Services.Trips;

/// <summary>One office trip-board spreadsheet row (one Trip).</summary>
public sealed class TripBoardRow
{
    public string? ExternalTicketNo { get; init; }
    public DateTime? TripDate { get; init; }
    public string? RequestingSchool { get; init; }
    public string? GroupOrActivity { get; init; }
    public string? DestinationName { get; init; }
    public TimeSpan? PickupTime { get; init; }
    public TimeSpan? ReturnClockTime { get; init; }
    public bool ReturnIsNextDay { get; init; }
    public int? PlannedHeadcount { get; init; }
    public int? ActualHeadcount { get; init; }
    public decimal? PlannedMiles { get; init; }
    public string? DriverName { get; init; }
    public string? BusNumber { get; init; }
    public string? Notes { get; init; }
    public string? MonthYearLabel { get; init; }
    public string? LinkedTicketNo { get; init; }
    public bool IsDay2WithoutTicket { get; init; }
    public bool IsMultiAsset { get; init; }
    public bool IsOvernightPending { get; init; }
    public bool LinkBothTeams { get; init; }
    public string? OriginName { get; init; }
}

public sealed class TripBoardParseResult
{
    public IReadOnlyList<string> OutOfServiceBusNumbers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TripBoardRow> Rows { get; init; } = Array.Empty<TripBoardRow>();
}

public sealed class TripBoardImportResult
{
    public int Upserted { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int MissingInfo { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
