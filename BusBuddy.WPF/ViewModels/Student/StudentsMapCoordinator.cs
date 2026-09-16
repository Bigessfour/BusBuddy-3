using System.Windows;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// District-map launching from the students grid. Plotting uses stored validated coordinates
/// (or a catalog stop pin); 0,0 pins are never invented here.
/// </summary>
public sealed class StudentsMapCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsMapCoordinator>();

    /// <summary>
    /// Plots one student on the district map. Returns a clerk-facing status line.
    /// </summary>
    public async Task<string> ViewOnMapAsync(StudentModel? student)
    {
        if (student is null)
        {
            return string.Empty;
        }

        try
        {
            if (!student.HasValidatedHomeCoordinates && student.PickupStopId is null)
            {
                Logger.Warning(
                    "Plot blocked for StudentId={StudentId} — {IntakeStatus}",
                    student.StudentId,
                    student.IntakeStatus);
                return $"Cannot plot {student.StudentName ?? "student"} — {student.IntakeStatus}";
            }

            var sp = App.ServiceProvider;
            if (sp is null)
            {
                return "Mapping not available";
            }

            IReadOnlyDictionary<int, PickupStop>? pickups = null;
            if (student.PickupStopId is int stopId)
            {
                var stopService = sp.GetService<IPickupStopService>();
                var stop = stopService is not null
                    ? await stopService.GetByIdAsync(stopId).ConfigureAwait(true)
                    : null;
                if (stop is not null)
                {
                    pickups = StudentPlotLocation.Index([stop]);
                }
            }

            var pins = StudentPlotLocation.PinsFromStored(student, pickups);
            if (pins.Count == 0)
            {
                var geocoder = sp.GetService<IGeocodingService>();
                if (geocoder is null)
                {
                    return "Geocoding not available";
                }

                var result = await geocoder.GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip);
                if (result is null
                    || !LocationCoordinate.IsValidated(result.Value.latitude, result.Value.longitude))
                {
                    return "Could not locate address";
                }

                pins =
                [
                    new StudentPlotPoint(
                        result.Value.latitude,
                        result.Value.longitude,
                        AtPickup: false,
                        PickupName: null)
                ];
            }

            var studentName = student.StudentName ?? "Student";
            MapViewLauncher.Show(Application.Current?.MainWindow as Window, vm =>
            {
                MapStudentPlot.Draw(
                    (lat, lon, names, label, ids) => vm.PlotStop(lat, lon, names, label, studentIds: ids),
                    student,
                    pins);
                vm.CenterOnMarkers();
            });

            var boarding = pins[0];
            return boarding.AtPickup
                ? $"District Map opened — plotted {studentName} at {boarding.PickupName}"
                : $"District Map opened — plotted {studentName}";
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error plotting student on map");
            return "Error plotting on map";
        }
    }

    /// <summary>Opens the district map and bulk-plots eligible student homes.</summary>
    public string ViewMap()
    {
        try
        {
            Logger.Information("View map command executed (bulk plot)");
            MapViewLauncher.Show(Application.Current?.MainWindow as Window, vm =>
            {
                if (vm.BulkPlotEligibleStudentsCommand is IAsyncRelayCommand plotCmd)
                {
                    _ = plotCmd.ExecuteAsync(null);
                }
                else if (vm.BulkPlotEligibleStudentsCommand.CanExecute(null))
                {
                    vm.BulkPlotEligibleStudentsCommand.Execute(null);
                }
            });

            return "District Map opened — student homes plot as pins";
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing view map command");
            return "Error opening map view";
        }
    }

    /// <summary>
    /// Kicks the same optimizer the bulk-assign toolbar uses. The grid has no dedicated AI
    /// suggestion surface; this is a thin alias so the row command stays bound.
    /// </summary>
    public string SuggestRoute(StudentModel? student, Func<Task> optimizeRoutes)
    {
        if (student is null)
        {
            return string.Empty;
        }

        try
        {
            Logger.Information("AI route suggestion for student {StudentId}", student.StudentId);
            _ = optimizeRoutes();
            return $"Getting AI route suggestions for {student.StudentName}";
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error getting route suggestions");
            return "Error getting route suggestions";
        }
    }
}
