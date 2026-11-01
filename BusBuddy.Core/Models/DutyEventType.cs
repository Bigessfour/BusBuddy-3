namespace BusBuddy.Core.Models;

/// <summary>
/// Daily duty / inspection event kinds. Specs: drivers.md.
/// Fuel is attribution only — gallons stay on <see cref="Fuel"/>.
/// </summary>
public enum DutyEventType
{
    PreTrip,
    PostTrip,
    Defect,
    Fuel
}
