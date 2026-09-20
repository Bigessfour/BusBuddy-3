namespace BusBuddy.Core.Services;

/// <summary>
/// Clerk-facing pickup hints after a home geocode. Suggests a published catalog stop;
/// never auto-assigns or auto-creates one. specs/students.md, specs/locations.md.
/// </summary>
public static class StudentPickupHint
{
    public static string NearbyCatalog(string stopName, double maxMeters) =>
        $"Nearby catalog stop: {stopName} (within {maxMeters:F0} m). Select it if that corner is a safe pickup, or keep home.";

    public static string HomeOnly(double maxMeters) =>
        $"No catalog stop within {maxMeters:F0} m — using home pickup.";

    public static string NoPublishedCatalog(double maxMeters) =>
        $"No published catalog stops yet — using home pickup. Add a catalog stop if this corner is a shared pickup (walk radius {maxMeters:F0} m).";

    public static string HomeWithCluster(int otherHomeCount, double maxMeters) =>
        $"No catalog stop within {maxMeters:F0} m — using home pickup. {otherHomeCount} other home pickup(s) nearby. Add a catalog stop if this corner is safe, then assign those students so the bus makes one stop.";

    public static string AssignedCatalogStillInRange(string stopName, double maxMeters) =>
        $"Boarding at {stopName} (still within {maxMeters:F0} m of home).";

    public static string NewCatalogNearbyHomes(int homeCount, double maxMeters) =>
        homeCount <= 0
            ? string.Empty
            : $"{homeCount} home pickup(s) within {maxMeters:F0} m. Assign those students to this stop from each student form if this corner is a safe pickup.";

    public static bool ShouldHintCatalogCluster(int otherHomeCount, int clusterMinHomes) =>
        otherHomeCount > 0 && otherHomeCount + 1 >= Math.Max(2, clusterMinHomes);
}
