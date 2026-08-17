namespace BusBuddy.Core.Services.RouteDetermination;

public enum RouteTimeSlotKind
{
    AM,
    PM,
    Both
}

public enum FleetKind
{
    HomeToSchool,
    Transfer
}

public enum AssignFitnessSeverity
{
    None,
    Warn,
    Block
}

public sealed class RouteGenerationOptions
{
    /// <summary>When true, return proposals without creating routes or assigning students.</summary>
    public bool DryRun { get; set; }

    /// <summary>Preferred seating capacity when no bus is assigned yet (default 72).</summary>
    public int DefaultSeatingCapacity { get; set; } = 72;
}

public sealed class RouteProposalDto
{
    public string ProposalKey { get; set; } = string.Empty;
    public string SuggestedRouteName { get; set; } = string.Empty;
    public int? PersistedRouteId { get; set; }
    public RouteTimeSlotKind Slot { get; set; }
    public FleetKind FleetKind { get; set; }
    public string CellId { get; set; } = string.Empty;
    public IReadOnlyList<int> OrderedStudentIds { get; set; } = Array.Empty<int>();
    public int SuggestedBusSeatingCapacity { get; set; }
    public double EstimatedMiles { get; set; }
    public double EstimatedMinutes { get; set; }
    public string Status { get; set; } = "Draft";
}

public sealed class RouteGenerationResult
{
    public Guid OperationId { get; set; }
    public int SchoolDestinationId { get; set; }
    public FleetKind FleetKind { get; set; }
    public IReadOnlyList<RouteProposalDto> Proposals { get; set; } = Array.Empty<RouteProposalDto>();
    public IReadOnlyList<int> UnclusteredStudentIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public int AssignedStudentCount { get; set; }
    /// <summary>Routes whose stop times were rewritten (schedule regen); not student assigns.</summary>
    public int RoutesUpdated { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class AssignFitnessResult
{
    public bool Allowed { get; set; } = true;
    public AssignFitnessSeverity Severity { get; set; } = AssignFitnessSeverity.None;
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
    public IReadOnlyList<int> SuggestedRouteIds { get; set; } = Array.Empty<int>();
    public bool SuggestNewRoute { get; set; }
}

public sealed class ClerkOverrideResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool RetainedMirrorStop { get; set; }
}
