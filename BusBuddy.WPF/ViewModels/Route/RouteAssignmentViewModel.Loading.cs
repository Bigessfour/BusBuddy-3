using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Commands;
using Serilog;
using Microsoft.Extensions.DependencyInjection; // For resolving MapViewModel / services
using BusBuddy.WPF.ViewModels.Map; // Map markers
using BusBuddy.WPF.Utilities; // MapMarkerLabels pin kinds
using BusBuddy.WPF.Views.Route;
using BusBuddy.WPF.Views.Driver;
using BusBuddy.WPF.ViewModels.Driver;
using BusBuddy.Core.Services.GoogleMaps;
using System.Globalization;
using System.IO; // For PDF export file writing
using System.Text.RegularExpressions; // Start time validation

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>Roster/list load and plot of assigned students onto District Map.</summary>
    public partial class RouteAssignmentViewModel
    {
        #region Data Loading

        private async Task LoadDataFromServiceAsync()
        {
            try
            {
                UnassignedStudents.Clear();
                AvailableRoutes.Clear();
                AvailableBuses.Clear();
                AvailableDrivers.Clear();

                IsLoading = true;

                var studentsTask = _routeService.GetUnassignedStudentsAsync(NormalizeTimeSlot(SelectedTimeSlot));
                var routesTask = _routeService.GetAllRoutesAsync();
                var busesTask = _routeService.GetAvailableBusesAsync();
                var driversTask = _routeService.GetAvailableDriversAsync();

                await Task.WhenAll(studentsTask, routesTask, busesTask, driversTask);

                var loadFailures = new List<LoadFailure>();

                _allUnassignedStudents.Clear();
                if (studentsTask.Result.IsSuccess && studentsTask.Result.Value != null)
                {
                    _allUnassignedStudents.AddRange(studentsTask.Result.Value);
                }
                else
                {
                    NoteFailure(loadFailures, "unassigned students", studentsTask.Result);
                }
                FilterStudents();

                if (routesTask.Result.IsSuccess && routesTask.Result.Value != null)
                {
                    foreach (var r in routesTask.Result.Value)
                    {
                        AvailableRoutes.Add(r);
                    }
                }
                else
                {
                    NoteFailure(loadFailures, "routes", routesTask.Result);
                }

                if (busesTask.Result.IsSuccess && busesTask.Result.Value != null)
                {
                    foreach (var b in busesTask.Result.Value)
                    {
                        AvailableBuses.Add(b);
                    }
                }
                else
                {
                    NoteFailure(loadFailures, "buses", busesTask.Result);
                }

                if (driversTask.Result.IsSuccess && driversTask.Result.Value != null)
                {
                    foreach (var d in driversTask.Result.Value)
                    {
                        AvailableDrivers.Add(d);
                    }
                }
                else
                {
                    NoteFailure(loadFailures, "drivers", driversTask.Result);
                }

                if (_preselectedRouteId.HasValue
                    && AvailableRoutes.All(r => r.RouteId != _preselectedRouteId.Value))
                {
                    var preselected = await _routeService.GetRouteByIdAsync(_preselectedRouteId.Value);
                    if (preselected.IsSuccess && preselected.Value != null)
                    {
                        AvailableRoutes.Insert(0, preselected.Value);
                    }
                }

                if (AvailableRoutes.Any())
                {
                    if (_preselectedRouteId.HasValue)
                    {
                        SelectedRoute = AvailableRoutes.FirstOrDefault(r => r.RouteId == _preselectedRouteId.Value)
                            ?? AvailableRoutes.First();
                    }
                    else
                    {
                        SelectedRoute = AvailableRoutes.First();
                    }
                }

                OnPropertyChanged(nameof(UnassignedStudentCount));
                UpdateStatusMessage();

                ReportLoadFailures("Route assignment data partially unavailable", loadFailures);
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Failed loading route assignment data from service");
                SelectedRoute = null;
                UnassignedStudents.Clear();
                _allUnassignedStudents.Clear();
                AvailableRoutes.Clear();
                AvailableBuses.Clear();
                AvailableDrivers.Clear();
                RouteStops.Clear();
                AssignedStudentsForSelectedRoute.Clear();
                OnPropertyChanged(nameof(UnassignedStudentCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                OnPropertyChanged(nameof(RouteStopCount));
                StatusMessage = DatabaseUserMessage.IsConnectivityFailure(ex)
                    ? DatabaseUserMessage.UnavailableShort
                    : $"Could not load route data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private readonly record struct LoadFailure(string Label, string Error, Exception? Exception);

        private static void NoteFailure<T>(List<LoadFailure> failures, string label, Result<T> result) =>
            failures.Add(new LoadFailure(label, result.Error ?? "unknown error", result.Exception));

        private void ReportLoadFailures(string context, IReadOnlyList<LoadFailure> failures)
        {
            if (failures.Count == 0)
            {
                return;
            }

            var detail = string.Join("; ", failures.Select(f => $"{f.Label} ({f.Error})"));
            if (failures.All(f => DatabaseUserMessage.IsConnectivityFailure(f.Exception)))
            {
                Logger.Warning("{Context} because Postgres is unreachable: {Failures}", context, detail);
                StatusMessage = DatabaseUserMessage.UnavailableShort;
                return;
            }

            Logger.Error("{Context}: {Failures}", context, detail);
            StatusMessage = "Could not load " + detail;
        }

        private async Task ReloadStudentListsForRouteAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            var slot = NormalizeTimeSlot(SelectedTimeSlot);

            var loadFailures = new List<LoadFailure>();

            var assignedResult = await _routeService.GetStudentsForRouteAsync(SelectedRoute.RouteId, slot);
            AssignedStudentsForSelectedRoute.Clear();
            if (assignedResult.IsSuccess && assignedResult.Value != null)
            {
                foreach (var s in assignedResult.Value)
                {
                    AssignedStudentsForSelectedRoute.Add(s);
                }
            }
            else
            {
                NoteFailure(loadFailures, "assigned students", assignedResult);
            }

            var unassignedResult = await _routeService.GetUnassignedStudentsAsync(slot);
            _allUnassignedStudents.Clear();
            if (unassignedResult.IsSuccess && unassignedResult.Value != null)
            {
                _allUnassignedStudents.AddRange(unassignedResult.Value);
            }
            else
            {
                NoteFailure(loadFailures, "unassigned students", unassignedResult);
            }
            FilterStudents();

            SelectedRoute.StudentCount = AssignedStudentsForSelectedRoute.Count;

            OnPropertyChanged(nameof(AssignedStudentCount));
            OnPropertyChanged(nameof(UnassignedStudentCount));

            if (loadFailures.Count > 0)
            {
                ReportLoadFailures($"Route {SelectedRoute.RouteId} ({slot}) roster load failed", loadFailures);
            }
            else
            {
                UpdateStatusMessage();
            }
        }

        private void FilterStudents()
        {
            UnassignedStudents.Clear();
            var term = StudentSearchText?.Trim() ?? string.Empty;
            IEnumerable<BusBuddy.Core.Models.Student> source = _allUnassignedStudents;

            if (!string.IsNullOrEmpty(term))
            {
                source = source.Where(s =>
                    (s.StudentName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Grade?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.HomeAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.City?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            foreach (var s in source)
            {
                UnassignedStudents.Add(s);
            }

            OnPropertyChanged(nameof(UnassignedStudentCount));
        }

        private void UpdateStatusMessage()
        {
            if (SelectedRoute != null)
            {
                StatusMessage = $"Route: {SelectedRoute.RouteName} | " +
                               $"Students: {AssignedStudentCount} | " +
                               $"Unassigned: {UnassignedStudentCount}";
            }
            else
            {
                StatusMessage = $"No route selected | Unassigned students: {UnassignedStudentCount}";
            }
        }

        /// <summary>
        /// Basic plotting of the selected route's currently assigned students onto the shared map (MapViewModel).
        /// Reuses existing MapViewModel marker infrastructure; only plots students with coordinates or successfully geocoded addresses.
        /// </summary>
        private async Task PlotRouteOnMapAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }
            try
            {
                StatusMessage = $"Plotting {AssignedStudentCount} students for {SelectedRoute.RouteName}...";
                Logger.Information("PlotRouteOnMap invoked for RouteId={RouteId} Name={RouteName}", SelectedRoute.RouteId, SelectedRoute.RouteName);

                var mapVm = _map;
                if (mapVm == null)
                {
                    StatusMessage = "Map VM not registered";
                    return;
                }

                var mapsGeo = App.ServiceProvider?.GetService<IMapsGeoService>();

                // Schools, catalog stops and the depot are always-on district layers (specs/maps.md
                // "Default: district overlay of schools + catalog stops; selecting a route adds homes
                // on that run and the path"), so only the per-household pins from the previous route
                // are cleared here.
                for (int i = mapVm.MapMarkers.Count - 1; i >= 0; i--)
                {
                    if (MapMarkerLabels.IsPerHousehold(mapVm.MapMarkers[i].Kind))
                    {
                        mapVm.MapMarkers.RemoveAt(i);
                    }
                }

                // The path and the numbered stop pins are the map's own pipeline; pushing the selection
                // draws them instead of leaving this view with homes and no route line.
                var mapRoute = mapVm.Routes.FirstOrDefault(r => r.RouteId == SelectedRoute.RouteId);
                if (mapRoute != null)
                {
                    mapVm.SelectedRoute = mapRoute;
                }

                var students = AssignedStudentsForSelectedRoute.ToList();
                if (students.Count == 0)
                {
                    StatusMessage = "No students assigned to plot";
                    return;
                }

                // Fire-and-forget background geocode/plot to keep UI responsive
                _ = Task.Run(async () =>
                {
                    foreach (var s in students)
                    {
                        try
                        {
                            double? lat = null, lon = null;

                            // Prefer existing stored coordinates
                            if (s.HasValidatedHomeCoordinates)
                            {
                                lat = (double)s.Latitude!.Value;
                                lon = (double)s.Longitude!.Value;
                            }
                            else if (mapsGeo is not null && mapsGeo.IsConfigured && !string.IsNullOrWhiteSpace(s.HomeAddress))
                            {
                                var r = await mapsGeo.ValidateAndGeocodeAsync(s.HomeAddress, s.City, s.State, s.Zip);
                                if (r.Ok && LocationCoordinate.IsValidated(r.Latitude, r.Longitude))
                                {
                                    lat = r.Latitude!.Value;
                                    lon = r.Longitude!.Value;
                                }
                            }

                            if (lat == null || lon == null)
                            {
                                continue; // skip if no coordinates
                            }

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                mapVm.PlotStop(
                                    lat.Value,
                                    lon.Value,
                                    new[] { s.StudentName ?? "Student" },
                                    s.StudentName,
                                    studentIds: new[] { s.StudentId });
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Failed to plot student {StudentId} {StudentName}", s.StudentId, s.StudentName);
                        }
                    }
                    StatusMessage = $"Plotted {SelectedRoute.RouteName} students";
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error plotting route students on map");
                StatusMessage = "Error plotting students";
            }
        }

        #endregion
    }
}
