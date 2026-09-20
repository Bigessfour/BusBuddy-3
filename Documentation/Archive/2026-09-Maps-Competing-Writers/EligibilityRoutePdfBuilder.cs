using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Views.Reports;
using Serilog;
using RouteModel = BusBuddy.Core.Models.Route;

namespace BusBuddy.WPF.Services;

public readonly record struct EligibilityPdfBuild(
    byte[] Pdf,
    int MappedCount,
    int Total,
    string? Blocker,
    string StatusMessage);

/// <summary>
/// Builds the student-map eligibility PDF. Does not mutate District Map markers.
/// </summary>
public sealed class EligibilityRoutePdfBuilder
{
    private static readonly ILogger Logger = Log.ForContext<EligibilityRoutePdfBuilder>();
    private readonly PdfReportService _pdf;

    public EligibilityRoutePdfBuilder(PdfReportService? pdf = null)
    {
        _pdf = pdf ?? new PdfReportService();
    }

    public async Task<EligibilityPdfBuild> BuildAsync(
        IStudentService? studentService,
        Func<Task<IReadOnlyDictionary<int, PickupStop>>> loadPickups,
        Func<Task<(double Lat, double Lon)>> resolveStart,
        Func<Task<(double Lat, double Lon)>> resolveSchool,
        double averageRouteSpeedMph,
        int dwellMinutesPerStop,
        RouteTimeSlot slot,
        byte[]? snapshotPng)
    {
        List<Student> allStudents;
        try
        {
            if (studentService is null)
            {
                return Fail(Array.Empty<byte>(), 0, 0,
                    "Student records are not available.",
                    "Student service unavailable");
            }

            allStudents = await studentService.GetAllStudentsAsync() ?? new();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed loading students for eligibility route PDF");
            return Fail(
                Array.Empty<byte>(),
                0,
                0,
                "Could not load students. Check the database connection.",
                "Student map PDF: could not load students");
        }

        if (allStudents.Count == 0)
        {
            return Fail(Array.Empty<byte>(), 0, 0, "There are no students to map.", "Student map PDF: no students");
        }

        IReadOnlyDictionary<int, PickupStop> pickups;
        try
        {
            pickups = await loadPickups().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup catalog unavailable for student map PDF; using home pins only");
            pickups = StudentPlotLocation.Index(null);
        }

        var mappable = new List<(Student Student, StudentPlotPoint Pin)>();
        foreach (var student in allStudents)
        {
            var pin = StudentPlotLocation.TryFromStored(student, pickups);
            if (pin is null || !LocationCoordinate.IsValidated(pin.Value.Latitude, pin.Value.Longitude))
            {
                continue;
            }

            mappable.Add((student, pin.Value));
        }

        if (mappable.Count == 0)
        {
            Logger.Information("No students with map pins (Total={Total})", allStudents.Count);
            return Fail(
                Array.Empty<byte>(),
                0,
                allStudents.Count,
                $"None of the {allStudents.Count} students have a map pin (home or catalog stop). Validate addresses first.",
                "Student map PDF: no map pins");
        }

        var (startLat, startLon) = await resolveStart().ConfigureAwait(true);
        var school = await resolveSchool().ConfigureAwait(true);
        var remaining = mappable.ToList();
        var ordered = new List<(Student Student, StudentPlotPoint Pin)>();
        double currentLat = startLat, currentLon = startLon;
        while (remaining.Count > 0)
        {
            var nearestIndex = 0;
            var nearestDist = double.MaxValue;
            for (var i = 0; i < remaining.Count; i++)
            {
                var pin = remaining[i].Pin;
                var dist = HaversineMiles(currentLat, currentLon, pin.Latitude, pin.Longitude);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIndex = i;
                }
            }

            var nearest = remaining[nearestIndex];
            ordered.Add(nearest);
            currentLat = nearest.Pin.Latitude;
            currentLon = nearest.Pin.Longitude;
            remaining.RemoveAt(nearestIndex);
        }

        var averageMph = Math.Max(5.0, averageRouteSpeedMph);
        var dwellPerStop = TimeSpan.FromMinutes(Math.Max(0, dwellMinutesPerStop));
        var dwellMinutes = Math.Max(0, dwellMinutesPerStop);
        var departTimeOfDay = new TimeSpan(6, 50, 0);
        var routeDay = DateTime.Today;
        var cumulative = TimeSpan.Zero;
        double totalMiles = 0.0;
        var stops = new List<RouteStop>();
        var order = 1;
        currentLat = startLat;
        currentLon = startLon;
        foreach (var (stu, pin) in ordered)
        {
            var legMiles = HaversineMiles(currentLat, currentLon, pin.Latitude, pin.Longitude);
            totalMiles += legMiles;
            cumulative += TimeSpan.FromMinutes(legMiles / averageMph * 60.0);
            var arrival = departTimeOfDay + cumulative;
            var departure = arrival + dwellPerStop;
            cumulative += dwellPerStop;
            var stopName = pin.AtPickup && !string.IsNullOrWhiteSpace(pin.PickupName)
                ? $"{stu.StudentName ?? "Student"} @ {pin.PickupName}"
                : stu.StudentName ?? "(Student)";
            stops.Add(new RouteStop
            {
                RouteId = -1,
                StopOrder = order++,
                StopName = stopName,
                Latitude = (decimal)pin.Latitude,
                Longitude = (decimal)pin.Longitude,
                ScheduledArrival = arrival,
                ScheduledDeparture = departure,
                EstimatedArrivalTime = routeDay.Add(arrival),
                EstimatedDepartureTime = routeDay.Add(departure),
                StopDuration = dwellMinutes,
                CreatedDate = DateTime.UtcNow
            });
            currentLat = pin.Latitude;
            currentLon = pin.Longitude;
        }

        var backLegMiles = HaversineMiles(currentLat, currentLon, school.Lat, school.Lon);
        totalMiles += backLegMiles;
        cumulative += TimeSpan.FromMinutes(backLegMiles / averageMph * 60.0);
        var arrivalBack = departTimeOfDay + cumulative;

        var route = new RouteModel
        {
            RouteId = -1,
            RouteName = $"Student Map {routeDay:MMM d}",
            Date = routeDay,
            IsActive = true,
            WaypointsJson = RouteWaypointSerializer.FromPairs(
                ordered.Select(x => (x.Pin.Latitude, x.Pin.Longitude)))
        };

        var bus = new Bus
        {
            BusNumber = "17",
            SeatingCapacity = 84,
            Status = "Active"
        };

        var roster = ordered.Select(x => x.Student).ToList();
        byte[] pdf;
        try
        {
            pdf = _pdf.GenerateRouteSummaryReport(
                route,
                stops,
                roster,
                bus,
                null,
                slot,
                snapshotPng);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PDF generation failed for eligibility route");
            pdf = Array.Empty<byte>();
        }

        if (pdf.Length == 0 || !PdfPreviewWindow.IsPdfPayload(pdf))
        {
            return Fail(
                Array.Empty<byte>(),
                roster.Count,
                allStudents.Count,
                "The student map PDF could not be created.",
                "Student map PDF: generation failed");
        }

        Logger.Information(
            "Student map PDF generated Mapped={Mapped} Total={Total} Stops={Stops} Miles~{Miles:F1} ETA-Back={EtaBack} HasSnapshot={HasSnapshot}",
            roster.Count,
            allStudents.Count,
            stops.Count,
            totalMiles,
            arrivalBack,
            snapshotPng is { Length: > 0 });

        return new EligibilityPdfBuild(
            pdf,
            roster.Count,
            allStudents.Count,
            null,
            $"Student map PDF: {stops.Count} stops ~{totalMiles:F1} mi");
    }

    private static EligibilityPdfBuild Fail(
        byte[] pdf,
        int mapped,
        int total,
        string blocker,
        string status) =>
        new(pdf, mapped, total, blocker, status);

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3958.8;
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);
        double a = Math.Pow(Math.Sin(dLat / 2), 2)
                   + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                   * Math.Pow(Math.Sin(dLon / 2), 2);
        double c = 2 * Math.Asin(Math.Sqrt(a));
        return R * c;
    }

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
}
