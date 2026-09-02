namespace BusBuddy.Core.Services;

/// <summary>Result of <see cref="ISeedDataService.SeedSpecialNeedsTransportPrepAsync"/>.</summary>
public sealed class SpecialNeedsPrepSummary
{
    public int SchoolDestinationId { get; set; }
    public int SpecialNeedsRouteId { get; set; }
    public string SpecialNeedsRouteName { get; set; } = string.Empty;
    public int SpecialNeedsDriverId { get; set; }
    public int SpecialNeedsBusId { get; set; }
    public int SpecialNeedsStudentsPrepared { get; set; }
    public int RegularStudentsPrepared { get; set; }
    public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();
}
