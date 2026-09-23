using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Grid-row address validate/geocode for <see cref="StudentsViewModel"/>.</summary>
public sealed class StudentsGridAddressCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsGridAddressCoordinator>();

    private readonly IStudentService? _studentService;

    public StudentsGridAddressCoordinator(IStudentService? studentService = null)
    {
        _studentService = studentService;
    }

    public async Task<string> ValidateAndPersistAsync(StudentModel student)
    {
        if (string.IsNullOrWhiteSpace(student.HomeAddress))
        {
            return "No address to validate";
        }

        Logger.Information(
            "Validating address for student StudentId={StudentId} Address={Address}",
            student.StudentId,
            student.HomeAddress);

        var mapsGeo = App.ServiceProvider?.GetService<IMapsGeoService>();
        if (mapsGeo is not null)
        {
            var maps = await mapsGeo.ValidateAndGeocodeAsync(
                student.HomeAddress,
                student.City,
                student.State,
                student.Zip).ConfigureAwait(true);

            if (maps.Ok
                && LocationCoordinate.IsPlotPrecision(maps.Precision)
                && maps.Latitude.HasValue
                && maps.Longitude.HasValue)
            {
                if (student.HomePickupClerkAdjusted && student.HasValidatedHomeCoordinates)
                {
                    await PersistCoordinatesAsync(student).ConfigureAwait(true);
                    var kept = string.IsNullOrWhiteSpace(maps.FormattedAddress)
                        ? "Address validated. Clerk pickup pin was kept."
                        : $"Address validated: {maps.FormattedAddress}. Clerk pickup pin was kept.";
                    Logger.Information(
                        "Clerk home pickup pin kept for student {StudentId}",
                        student.StudentId);
                    return kept;
                }

                student.Latitude = (decimal)maps.Latitude.Value;
                student.Longitude = (decimal)maps.Longitude.Value;
                if (!string.IsNullOrWhiteSpace(maps.PlaceId))
                {
                    student.PlaceId = maps.PlaceId;
                }

                await PersistCoordinatesAsync(student).ConfigureAwait(true);

                var precisionSuffix = string.IsNullOrWhiteSpace(maps.Precision)
                    ? string.Empty
                    : $" ({maps.Precision})";
                var message = string.IsNullOrWhiteSpace(maps.FormattedAddress)
                    ? $"Address validated{precisionSuffix} ({student.Latitude:F5}, {student.Longitude:F5})"
                    : $"Address validated{precisionSuffix}: {maps.FormattedAddress}";
                Logger.Information(
                    "Address validated for student {StudentId} Precision={Precision}",
                    student.StudentId,
                    maps.Precision);
                return message;
            }

            if (maps.MappingUnconfigured)
            {
                Logger.Warning("Validate address — mapping unconfigured for student {StudentId}", student.StudentId);
                return WithStoredPinNote(
                    student,
                    maps.ErrorMessage ?? "Mapping is not configured (set GOOGLE_MAPS_API_KEY).");
            }

            Logger.Warning(
                "Address validation failed for student {StudentId}: {Error}",
                student.StudentId,
                maps.ErrorMessage);
            if (!(student.HomePickupClerkAdjusted && student.HasValidatedHomeCoordinates))
            {
                student.HomePickupClerkAdjusted = false;
                student.Latitude = null;
                student.Longitude = null;
                student.PlaceId = null;
                await PersistCoordinatesAsync(student).ConfigureAwait(true);
            }
            var failed = maps.ErrorMessage ?? "Address could not be validated.";
            var failedMessage = AddressValidationPinPolicy.IsClerkRejectCopy(failed)
                || failed.Contains(LocationCoordinate.NeedsValidation, StringComparison.OrdinalIgnoreCase)
                ? failed
                : $"Address validation failed: {failed}";
            return student.HomePickupClerkAdjusted && student.HasValidatedHomeCoordinates
                ? $"{failedMessage} Clerk pickup pin was kept."
                : failedMessage;
        }

        var geocoder = App.ServiceProvider?.GetService<IGeocodingService>();
        if (geocoder is not null)
        {
            var geo = await geocoder.GeocodeAsync(
                student.HomeAddress,
                student.City,
                student.State,
                student.Zip).ConfigureAwait(true);
            if (geo.HasValue)
            {
                if (student.HomePickupClerkAdjusted && student.HasValidatedHomeCoordinates)
                {
                    Logger.Information(
                        "Clerk home pickup pin kept for student {StudentId}",
                        student.StudentId);
                    return $"Address geocoded. Clerk pickup pin was kept ({student.Latitude:F5}, {student.Longitude:F5}).";
                }

                student.Latitude = (decimal)geo.Value.latitude;
                student.Longitude = (decimal)geo.Value.longitude;
                await PersistCoordinatesAsync(student).ConfigureAwait(true);
                Logger.Information(
                    "Address geocoded for student {StudentId}: {Lat},{Lon}",
                    student.StudentId,
                    student.Latitude,
                    student.Longitude);
                return $"Address geocoded ({student.Latitude:F5}, {student.Longitude:F5})";
            }
        }

        Logger.Warning(
            "Address not validated for student {StudentId} — Google Address Validation unavailable",
            student.StudentId);
        return WithStoredPinNote(
            student,
            "Address could not be validated. Set GOOGLE_MAPS_API_KEY or use View on Map after a successful validate.");
    }

    private static string WithStoredPinNote(StudentModel student, string message)
    {
        if (!student.HasValidatedHomeCoordinates)
        {
            return message;
        }

        return $"{message} Stored lat/lng on this record were kept — they are not missing.";
    }

    private async Task PersistCoordinatesAsync(StudentModel student)
    {
        if (student.StudentId <= 0)
        {
            return;
        }

        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is null)
        {
            Logger.Warning("Cannot persist geocode — IStudentService unavailable");
            return;
        }

        try
        {
            if (student.HomePickupClerkAdjusted)
            {
                await studentService.UpdateHomeGeocodeAsync(
                    student.StudentId,
                    student.Latitude,
                    student.Longitude,
                    student.PlaceId,
                    homePickupClerkAdjusted: true).ConfigureAwait(true);
            }
            else
            {
                await studentService.UpdateHomeGeocodeAsync(
                    student.StudentId,
                    student.Latitude,
                    student.Longitude,
                    student.PlaceId).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to persist geocode for StudentId={StudentId}", student.StudentId);
            throw;
        }
    }
}
