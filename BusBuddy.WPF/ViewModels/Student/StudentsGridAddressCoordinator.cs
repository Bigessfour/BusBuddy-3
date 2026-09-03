using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>Grid-row address validate/geocode for <see cref="StudentsViewModel"/>.</summary>
public sealed class StudentsGridAddressCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsGridAddressCoordinator>();

    private readonly AddressService _addressService;
    private readonly IStudentService? _studentService;

    public StudentsGridAddressCoordinator(AddressService addressService, IStudentService? studentService = null)
    {
        _addressService = addressService ?? throw new ArgumentNullException(nameof(addressService));
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

            if (maps.Ok && maps.Latitude.HasValue && maps.Longitude.HasValue)
            {
                student.Latitude = (decimal)maps.Latitude.Value;
                student.Longitude = (decimal)maps.Longitude.Value;
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
                return maps.ErrorMessage ?? "Mapping is not configured (set GOOGLE_MAPS_API_KEY).";
            }

            Logger.Warning(
                "Address validation failed for student {StudentId}: {Error}",
                student.StudentId,
                maps.ErrorMessage);
            return maps.ErrorMessage ?? "Address could not be validated.";
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

        var validation = _addressService.ValidateAddress(student.HomeAddress);
        Logger.Information(
            "Address validation performed for student {StudentId}: {IsValid}",
            student.StudentId,
            validation.IsValid);
        return validation.IsValid
            ? "Address format is valid (GPS unavailable)"
            : $"Address validation failed: {validation.Error}";
    }

    private async Task PersistCoordinatesAsync(StudentModel student)
    {
        var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();
        if (studentService is not null && student.StudentId > 0)
        {
            await studentService.UpdateStudentAsync(student).ConfigureAwait(true);
        }
    }
}
