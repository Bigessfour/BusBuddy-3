using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services;

/// <summary>Shared persist rules for duty / inspection events (#112).</summary>
public static class DutyEventValidator
{
    public static IReadOnlyList<DutySession> AllowedSessions { get; } =
        Enum.GetValues<DutySession>();

    public static IReadOnlyList<DutyEventType> AllowedEventTypes { get; } =
        Enum.GetValues<DutyEventType>();

    /// <summary>Throws <see cref="ArgumentException"/> when the event cannot be saved.</summary>
    public static void ValidateForPersist(DutyEvent dutyEvent)
    {
        ArgumentNullException.ThrowIfNull(dutyEvent);

        if (dutyEvent.DriverId <= 0)
        {
            throw new ArgumentException("A driver must be selected.", nameof(dutyEvent));
        }

        if (dutyEvent.BusId <= 0)
        {
            throw new ArgumentException("A bus must be selected.", nameof(dutyEvent));
        }

        if (!Enum.IsDefined(dutyEvent.Session))
        {
            throw new ArgumentException("Session must be AM, PM, or Trip.", nameof(dutyEvent));
        }

        if (!Enum.IsDefined(dutyEvent.EventType))
        {
            throw new ArgumentException(
                "Event type must be PreTrip, PostTrip, Defect, or Fuel.",
                nameof(dutyEvent));
        }

        if (dutyEvent.EventedAt == default)
        {
            throw new ArgumentException("EventedAt is required.", nameof(dutyEvent));
        }

        dutyEvent.EventedAt = DateTime.SpecifyKind(
            dutyEvent.EventedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dutyEvent.EventedAt, DateTimeKind.Utc)
                : dutyEvent.EventedAt.ToUniversalTime(),
            DateTimeKind.Utc);

        if (dutyEvent.Notes is { Length: > DutyEvent.MaxNotesLength })
        {
            throw new ArgumentException(
                $"Notes cannot exceed {DutyEvent.MaxNotesLength} characters.",
                nameof(dutyEvent));
        }

        if (dutyEvent.EventType == DutyEventType.Defect
            && string.IsNullOrWhiteSpace(dutyEvent.Notes))
        {
            throw new ArgumentException("Defect reports require notes.", nameof(dutyEvent));
        }

        if (dutyEvent.Notes is not null)
        {
            dutyEvent.Notes = dutyEvent.Notes.Trim();
        }

        if (dutyEvent.OdometerReading is < 0)
        {
            throw new ArgumentException("Odometer reading cannot be negative.", nameof(dutyEvent));
        }

        if (dutyEvent.FuelId is <= 0)
        {
            throw new ArgumentException("FuelId must be a positive id when set.", nameof(dutyEvent));
        }

        // Fuel type never stores gallons here — optional FuelId is attribution only.
        if (dutyEvent.EventType != DutyEventType.Fuel)
        {
            dutyEvent.FuelId = null;
        }
    }
}
