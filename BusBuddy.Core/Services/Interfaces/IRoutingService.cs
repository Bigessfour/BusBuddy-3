namespace BusBuddy.Core.Services.Interfaces;

/// <summary>Road routing via Google Routes API (drive path).</summary>
public interface IRoutingService
{
    Task<DrivePathResult> ComputeDrivePathAsync(
        (double Latitude, double Longitude) origin,
        (double Latitude, double Longitude) destination,
        IReadOnlyList<(double Latitude, double Longitude)> waypoints,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a drive-path computation.</summary>
public sealed class DrivePathResult
{
    public string? EncodedPolyline { get; init; }
    public IReadOnlyList<(double Latitude, double Longitude)> Points { get; init; } =
        Array.Empty<(double, double)>();
    public int? DistanceMeters { get; init; }
    public string? Duration { get; init; }
    public string? Error { get; init; }
    public bool Succeeded => string.IsNullOrEmpty(Error) && !string.IsNullOrWhiteSpace(EncodedPolyline);
}
