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
using BusBuddy.Core.Services.Interfaces; // IGeocodingService
using System.Globalization;
using System.IO; // For PDF export file writing
using System.Threading; // For debounce timer
using System.Text.RegularExpressions; // Start time validation

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>Route assignment command implementations (riders, fleet, stops, save).</summary>
    public partial class RouteAssignmentViewModel
    {
        #region Command Implementations

        // Enhanced Student Assignment Commands
        private async Task AssignStudentAsync()
        {
            if (SelectedStudent == null)
            {
                return;
            }

            await AssignStudentCoreAsync(SelectedStudent);
        }

        private async Task<bool> AssignStudentCoreAsync(BusBuddy.Core.Models.Student student)
        {
            if (student == null || SelectedRoute == null || IsLoading)
            {
                return false;
            }

            try
            {
                IsLoading = true;
                var route = SelectedRoute;
                var slot = NormalizeTimeSlot(SelectedTimeSlot);
                var studentName = GetStudentDisplayName(student);
                var routeName = GetRouteDisplayName(route);
                RefreshCommandStates();

                if (AssignedStudentsForSelectedRoute.Any(s => s.StudentId == student.StudentId))
                {
                    StatusMessage = $"{student.StudentName} is already on {routeName} ({slot})";
                    return true;
                }

                var overrideSeating = false;
                if (_routeDetermination is not null)
                {
                    var slotKind = slot == RouteTimeSlot.AM
                        ? RouteTimeSlotKind.AM
                        : RouteTimeSlotKind.PM;
                    var fitness = await _routeDetermination
                        .RecalculateOnAssignAsync(student.StudentId, route.RouteId, slotKind)
                        .ConfigureAwait(true);

                    if (fitness.Severity == AssignFitnessSeverity.Warn && fitness.Reasons.Count > 0)
                    {
                        StatusMessage = string.Join("; ", fitness.Reasons);
                        MessageBox.Show(
                            string.Join("\n", fitness.Reasons),
                            "Assignment warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    if (!fitness.Allowed)
                    {
                        // Special-needs pairing is not a seating question, so the seating override below
                        // must never be offered for it: a special-needs child rides a special-needs route
                        // and a special-needs route carries only those children.
                        if (StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student)
                            != StudentSpecialNeedsHelper.IsSpecialNeedsRoute(route))
                        {
                            var mismatch = StudentSpecialNeedsHelper.RequiresSpecialNeedsTransport(student)
                                ? $"{studentName} requires a special-needs route. Pick a special-needs route (home pickup, equipped bus, aide)."
                                : $"{routeName} is a special-needs route. Assign {studentName} to a general route instead.";
                            StatusMessage = mismatch;
                            Logger.Information(
                                "Assignment refused — special-needs mismatch Student={StudentId} Route={RouteId}",
                                student.StudentId,
                                route.RouteId);
                            MessageBox.Show(mismatch, "Assignment blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                        var body = string.Join("\n", fitness.Reasons);
                        if (fitness.SuggestedRouteIds.Count > 0)
                        {
                            body += "\n\nSuggested route IDs: " + string.Join(", ", fitness.SuggestedRouteIds);
                        }

                        if (fitness.SuggestNewRoute && student.DestinationId is int schoolId)
                        {
                            var gen = MessageBox.Show(
                                body + "\n\nCreate new draft routes for this student's school?",
                                "Assignment blocked",
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Warning);
                            if (gen == MessageBoxResult.Yes)
                            {
                                var genResult = await _routeDetermination.GenerateAndAssignAsync(
                                        schoolId,
                                        RouteTimeSlotKind.Both,
                                        FleetKind.HomeToSchool)
                                    .ConfigureAwait(true);
                                StatusMessage = genResult.Success
                                    ? $"Generated {genResult.Proposals.Count} draft route(s)"
                                    : (genResult.Error ?? "Route generation failed");
                                await LoadDataFromServiceAsync().ConfigureAwait(true);
                                return false;
                            }

                            if (gen == MessageBoxResult.Cancel)
                            {
                                StatusMessage = "Assignment cancelled";
                                return false;
                            }
                        }

                        var askOverride = MessageBox.Show(
                            body + "\n\nOverride seating capacity and assign anyway?",
                            "Assignment blocked",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        if (askOverride != MessageBoxResult.Yes)
                        {
                            StatusMessage = "Assignment blocked: " + string.Join("; ", fitness.Reasons);
                            return false;
                        }

                        overrideSeating = true;
                    }
                }

                var result = await _routeService.AssignStudentToRouteAsync(
                    student.StudentId, route.RouteId, slot, overrideSeating);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to assign student: {result.Error}";
                    MessageBox.Show(result.Error!, "Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                await ReloadStudentListsForRouteAsync();
                StatusMessage = $"Successfully assigned {studentName} to {routeName} ({slot})";
                Logger.Information("Student {StudentName} assigned to route {RouteName} ({Slot})", studentName, routeName, slot);

                if (ReferenceEquals(SelectedStudent, student))
                {
                    SelectedStudent = null;
                }

                (AssignStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveStudentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MarkNotRidingTodayCommand as RelayCommand)?.RaiseCanExecuteChanged();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign student to route");
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to assign student: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveStudentAsync()
        {
            if (SelectedAssignedStudent == null)
            {
                return;
            }

            await RemoveStudentCoreAsync(SelectedAssignedStudent);
        }

        /// <summary>
        /// Same-day not riding. Does not delete the published stop or the year assignment.
        /// </summary>
        private async Task MarkNotRidingTodayAsync()
        {
            if (SelectedAssignedStudent == null || SelectedRoute == null)
            {
                return;
            }

            var student = SelectedAssignedStudent;
            var route = SelectedRoute;
            var stopCountBefore = RouteStops.Count;
            var am = student.AMRoute;
            var pm = student.PMRoute;

            try
            {
                IsLoading = true;
                var result = await _routeService.RecordRiderExceptionAsync(
                    route.RouteId,
                    student.StudentId,
                    // Same UTC-labelled calendar day the route and its stop ETAs use, so "today"
                    // means one day for both and the per-day exception key cannot split in two.
                    DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                    "Not riding today");
                if (!result.IsSuccess)
                {
                    StatusMessage = result.Error ?? "Could not record not-riding exception";
                    return;
                }

                StatusMessage =
                    $"{student.StudentName} not riding today — published stops and year assignment unchanged";
                Logger.Information(
                    "Rider exception recorded Student={StudentId} Route={RouteId} Stops={Stops} AMRoute={AM} PMRoute={PM}",
                    student.StudentId,
                    route.RouteId,
                    stopCountBefore,
                    am,
                    pm);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to record rider exception");
                StatusMessage = $"Failed to mark not riding: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                RefreshCommandStates();
            }
        }

        private async Task<bool> RemoveStudentCoreAsync(BusBuddy.Core.Models.Student student)
        {
            if (student == null || SelectedRoute == null || IsLoading)
            {
                return false;
            }

            try
            {
                IsLoading = true;
                var route = SelectedRoute;
                var slot = NormalizeTimeSlot(SelectedTimeSlot);
                var studentName = GetStudentDisplayName(student);
                var routeName = GetRouteDisplayName(route);
                StatusMessage = $"Removing {studentName} from {routeName} ({slot})...";

                var result = await _routeService.RemoveStudentFromRouteAsync(student.StudentId, route.RouteId, slot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to remove student: {result.Error}";
                    MessageBox.Show(result.Error!, "Removal Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                await ReloadStudentListsForRouteAsync();
                StatusMessage = $"Successfully removed {studentName} from {routeName} ({slot})";
                Logger.Information("Student {StudentName} removed from route {RouteName} ({Slot})", studentName, routeName, slot);

                if (ReferenceEquals(SelectedAssignedStudent, student))
                {
                    SelectedAssignedStudent = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to remove student from route");
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to remove student: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AutoAssignStudentsAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                MessageBox.Show("Please select a route first.", "Route Required",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                IsLoading = true;
                var slot = NormalizeTimeSlot(SelectedTimeSlot);
                StatusMessage = $"Auto-assigning students to {SelectedRoute.RouteName} ({slot})...";

                var result = await _routeService.AutoAssignStudentsAsync(SelectedRoute.RouteId, slot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Auto-assignment failed: {result.Error}";
                    MessageBox.Show(result.Error!, "Auto-Assign Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await ReloadStudentListsForRouteAsync();
                var count = result.Value?.Count ?? 0;
                StatusMessage = $"Auto-assigned {count} student(s) to {SelectedRoute.RouteName} ({slot})";
                Logger.Information("Auto-assignment completed for route {RouteName} ({Slot}): {Count} students",
                    SelectedRoute.RouteName, slot, count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to auto-assign students");
                StatusMessage = $"Auto-assignment failed: {ex.Message}";
                MessageBox.Show($"Auto-assignment failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Vehicle and Driver Assignment Commands
        private async Task AssignVehicleAsync()
        {
            if (SelectedRoute == null || SelectedBus == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Assigning {SelectedBus.BusNumber} to {SelectedRoute.RouteName}...";

                var result = await _routeService.AssignVehicleToRouteAsync(SelectedRoute.RouteId, SelectedBus.BusId, SelectedTimeSlot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to assign vehicle: {result.Error}";
                    MessageBox.Show(result.Error!, "Vehicle Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update route properties based on time slot
                switch (SelectedTimeSlot)
                {
                    case BusBuddy.Core.Models.RouteTimeSlot.AM:
                        SelectedRoute.AMVehicleId = SelectedBus.BusId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.PM:
                        SelectedRoute.PMVehicleId = SelectedBus.BusId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.Both:
                        SelectedRoute.AMVehicleId = SelectedBus.BusId;
                        SelectedRoute.PMVehicleId = SelectedBus.BusId;
                        break;
                }

                StatusMessage = $"Successfully assigned {SelectedBus.BusNumber} to {SelectedRoute.RouteName} for {SelectedTimeSlot}";
                Logger.Information("Vehicle {BusNumber} assigned to route {RouteName} for {TimeSlot}",
                    SelectedBus.BusNumber, SelectedRoute.RouteName, SelectedTimeSlot);

                SelectedBus = null;
                OnPropertyChanged(nameof(SelectedRouteBusDisplay));
                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign vehicle to route");
                StatusMessage = $"Failed to assign vehicle: {ex.Message}";
                MessageBox.Show($"Failed to assign vehicle: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AssignDriverAsync()
        {
            if (SelectedRoute == null || SelectedDriver == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Assigning {SelectedDriver.DriverName} to {SelectedRoute.RouteName}...";

                var result = await _routeService.AssignDriverToRouteAsync(SelectedRoute.RouteId, SelectedDriver.DriverId, SelectedTimeSlot);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to assign driver: {result.Error}";
                    MessageBox.Show(result.Error!, "Driver Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update route properties based on time slot
                switch (SelectedTimeSlot)
                {
                    case BusBuddy.Core.Models.RouteTimeSlot.AM:
                        SelectedRoute.AMDriverId = SelectedDriver.DriverId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.PM:
                        SelectedRoute.PMDriverId = SelectedDriver.DriverId;
                        break;
                    case BusBuddy.Core.Models.RouteTimeSlot.Both:
                        SelectedRoute.AMDriverId = SelectedDriver.DriverId;
                        SelectedRoute.PMDriverId = SelectedDriver.DriverId;
                        break;
                }

                StatusMessage = $"Successfully assigned {SelectedDriver.DriverName} to {SelectedRoute.RouteName} for {SelectedTimeSlot}";
                Logger.Information("Driver {DriverName} assigned to route {RouteName} for {TimeSlot}",
                    SelectedDriver.DriverName, SelectedRoute.RouteName, SelectedTimeSlot);

                SelectedDriver = null;
                OnPropertyChanged(nameof(SelectedRouteDriverDisplay));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to assign driver to route");
                StatusMessage = $"Failed to assign driver: {ex.Message}";
                MessageBox.Show($"Failed to assign driver: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Route Stop Management Commands
        private async Task AddStopAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                var dialog = new RouteStopEditDialog($"Stop {RouteStops.Count + 1}", string.Empty)
                {
                    Owner = Application.Current?.MainWindow
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var stopName = dialog.StopName;
                var newStop = new RouteStop
                {
                    RouteId = SelectedRoute.RouteId,
                    StopName = stopName,
                    StopOrder = RouteStops.Count + 1,
                    StopAddress = string.IsNullOrWhiteSpace(dialog.StopAddress) ? stopName : dialog.StopAddress,
                    Latitude = dialog.Latitude,
                    Longitude = dialog.Longitude
                };

                IsLoading = true;
                StatusMessage = $"Adding stop '{stopName}' to {SelectedRoute.RouteName}...";

                var result = await _routeService.AddStopToRouteAsync(SelectedRoute.RouteId, newStop);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to add stop: {result.Error}";
                    MessageBox.Show(result.Error!, "Add Stop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Add(newStop);
                OnPropertyChanged(nameof(RouteStopCount));
                StatusMessage = $"Successfully added stop '{stopName}' to {SelectedRoute.RouteName}";
                Logger.Information("Added stop {StopName} to route {RouteName}", stopName, SelectedRoute.RouteName);
                // Schedule auto-retime
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to add stop to route");
                StatusMessage = $"Failed to add stop: {ex.Message}";
                MessageBox.Show($"Failed to add stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveStopAsync()
        {
            if (SelectedRouteStop == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Removing stop '{SelectedRouteStop.StopName}'...";

                var result = await _routeService.RemoveStopFromRouteAsync(SelectedRoute!.RouteId, SelectedRouteStop.RouteStopId);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to remove stop: {result.Error}";
                    MessageBox.Show(result.Error!, "Remove Stop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Remove(SelectedRouteStop);
                OnPropertyChanged(nameof(RouteStopCount));
                StatusMessage = $"Successfully removed stop '{SelectedRouteStop.StopName}'";
                Logger.Information("Removed stop {StopName} from route {RouteName}", SelectedRouteStop.StopName, SelectedRoute!.RouteName);

                SelectedRouteStop = null;
                // Schedule auto-retime
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to remove stop from route");
                StatusMessage = $"Failed to remove stop: {ex.Message}";
                MessageBox.Show($"Failed to remove stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                RefreshCommandStates();
            }
        }

        private async Task MoveStopUpAsync()
        {
            if (SelectedRouteStop == null || IsLoading)
            {
                return;
            }

            var currentIndex = RouteStops.IndexOf(SelectedRouteStop);
            if (currentIndex <= 0)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Reordering route stops...";

                var stops = RouteStops.ToList();
                stops.RemoveAt(currentIndex);
                stops.Insert(currentIndex - 1, SelectedRouteStop);

                var orderedStopIds = stops.Select(s => s.RouteStopId).ToList();
                var result = await _routeService.ReorderRouteStopsAsync(SelectedRoute!.RouteId, orderedStopIds);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to reorder stops: {result.Error}";
                    MessageBox.Show(result.Error!, "Reorder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Move(currentIndex, currentIndex - 1);
                StatusMessage = $"Successfully moved stop '{SelectedRouteStop.StopName}' up";
                Logger.Information("Moved stop {StopName} up in route {RouteName}", SelectedRouteStop.StopName, SelectedRoute!.RouteName);
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to move stop up");
                StatusMessage = $"Failed to move stop: {ex.Message}";
                MessageBox.Show($"Failed to move stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                RefreshCommandStates();
            }
        }

        private async Task MoveStopDownAsync()
        {
            if (SelectedRouteStop == null || IsLoading)
            {
                return;
            }

            var currentIndex = RouteStops.IndexOf(SelectedRouteStop);
            if (currentIndex >= RouteStops.Count - 1)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Reordering route stops...";

                var stops = RouteStops.ToList();
                stops.RemoveAt(currentIndex);
                stops.Insert(currentIndex + 1, SelectedRouteStop);

                var orderedStopIds = stops.Select(s => s.RouteStopId).ToList();
                var result = await _routeService.ReorderRouteStopsAsync(SelectedRoute!.RouteId, orderedStopIds);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to reorder stops: {result.Error}";
                    MessageBox.Show(result.Error!, "Reorder Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RouteStops.Move(currentIndex, currentIndex + 1);
                StatusMessage = $"Successfully moved stop '{SelectedRouteStop.StopName}' down";
                Logger.Information("Moved stop {StopName} down in route {RouteName}", SelectedRouteStop.StopName, SelectedRoute!.RouteName);
                _retimeDebounceTimer?.Change(RetimeDebounceMs, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to move stop down");
                StatusMessage = $"Failed to move stop: {ex.Message}";
                MessageBox.Show($"Failed to move stop: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Basic sequential timing of route stops based on a user-provided StartTimeString.
        /// Each stop gets arrival = current time cursor, departure = arrival + StopDuration minutes (default 2 if 0).
        /// Persisted via IRouteService.UpdateRouteStopsTimingAsync when available.
        /// </summary>
        private async Task TimeRouteStopsAsync()
        {
            if (SelectedRoute == null || !RouteStops.Any())
            {
                return;
            }

            if (!IsStartTimeValid)
            {
                StatusMessage = "Cannot time stops — invalid Start Time (HH:mm)";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Calculating stop times...";

                // Parse start time; fallback to 07:30 if invalid
                if (!TimeSpan.TryParseExact(
                        _startTimeString.Trim(),
                        new[] { @"hh\:mm", @"h\:mm" },
                        CultureInfo.InvariantCulture,
                        out var startOfRun))
                {
                    startOfRun = new TimeSpan(7, 30, 0); // 07:30 fallback
                    _startTimeString = "07:30"; // normalize
                    OnPropertyChanged(nameof(StartTimeString));
                }

                // A stop time is a face time — 07:00 means seven in the morning at the stop — so the
                // clock is written as-is on a UTC-labelled calendar day, matching Route.Date and the
                // timestamptz Kind converters. No time-zone shift here.
                var runDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var stampedUtc = DateTime.UtcNow;
                var cursor = startOfRun;

                // Order stops by StopOrder to ensure consistency
                foreach (var stop in RouteStops.OrderBy(s => s.StopOrder))
                {
                    var dwell = TimeSpan.FromMinutes(stop.StopDuration > 0 ? stop.StopDuration : 2); // default dwell
                    // ScheduledArrival is the published time PdfReportService prefers, so it has to move
                    // with the grid or the printed route keeps a stale generated time.
                    stop.ScheduledArrival = cursor;
                    stop.ScheduledDeparture = cursor + dwell;
                    stop.EstimatedArrivalTime = runDate + stop.ScheduledArrival;
                    stop.EstimatedDepartureTime = runDate + stop.ScheduledDeparture;
                    stop.UpdatedDate = stampedUtc;
                    cursor = stop.ScheduledDeparture; // advance cursor
                }

                var persistResult = await _routeService.UpdateRouteStopsTimingAsync(SelectedRoute.RouteId, RouteStops);
                if (!persistResult.IsSuccess)
                {
                    StatusMessage = $"Timing calculated but failed to persist: {persistResult.Error}";
                    MessageBox.Show(persistResult.Error ?? "Failed to persist timing", "Timing Persistence", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = $"Timing updated for {RouteStops.Count} stops (Start {StartTimeString})";
                }

                // Notify grid
                foreach (var prop in new[] { nameof(RouteStops) })
                {
                    OnPropertyChanged(prop);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to time route stops");
                StatusMessage = $"Error timing stops: {ex.Message}";
                MessageBox.Show($"Failed to time stops: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                (TimeRouteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // Route Activation Commands
        private async Task ActivateRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            // Activation guard – ensure minimal required components
            var missing = new List<string>();
            if (!AssignedStudentsForSelectedRoute.Any()) missing.Add("at least one student");
            if (!RouteStops.Any()) missing.Add("at least one stop");
            var hasVehicle = SelectedRoute.AMVehicleId.HasValue || SelectedRoute.PMVehicleId.HasValue;
            if (!hasVehicle) missing.Add("vehicle");
            var hasDriver = SelectedRoute.AMDriverId.HasValue || SelectedRoute.PMDriverId.HasValue;
            if (!hasDriver) missing.Add("driver");
            if (missing.Any())
            {
                var msg = "Cannot activate route – missing: " + string.Join(", ", missing);
                StatusMessage = msg;
                MessageBox.Show(msg, "Activation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Activating route '{SelectedRoute.RouteName}'...";

                // Perform full service validation first; surface issues and abort if invalid
                var validation = await _routeService.ValidateRouteForActivationAsync(SelectedRoute.RouteId);
                if (!validation.IsSuccess)
                {
                    StatusMessage = $"Validation error: {validation.Error}";
                    MessageBox.Show(validation.Error ?? "Unknown validation error", "Activation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (validation.Value != null && !validation.Value.IsValid)
                {
                    var issues = string.Join("\n", validation.Value.Issues);
                    var msg = $"Route failed validation:\n{issues}";
                    StatusMessage = "Activation blocked by validation";
                    MessageBox.Show(msg, "Activation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = await _routeService.ActivateRouteAsync(SelectedRoute.RouteId);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to activate route: {result.Error}";
                    MessageBox.Show(result.Error!, "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedRoute.IsActive = true;
                OnPropertyChanged(nameof(CanActivateRoute));
                OnPropertyChanged(nameof(CanDeactivateRoute));
                StatusMessage = $"Successfully activated route '{SelectedRoute.RouteName}'";
                Logger.Information("Activated route {RouteName}", SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to activate route");
                StatusMessage = $"Failed to activate route: {ex.Message}";
                MessageBox.Show($"Failed to activate route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeactivateRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Deactivating route '{SelectedRoute.RouteName}'...";

                var result = await _routeService.DeactivateRouteAsync(SelectedRoute.RouteId);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to deactivate route: {result.Error}";
                    MessageBox.Show(result.Error!, "Deactivation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedRoute.IsActive = false;
                OnPropertyChanged(nameof(CanActivateRoute));
                OnPropertyChanged(nameof(CanDeactivateRoute));
                StatusMessage = $"Successfully deactivated route '{SelectedRoute.RouteName}'";
                Logger.Information("Deactivated route {RouteName}", SelectedRoute.RouteName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to deactivate route");
                StatusMessage = $"Failed to deactivate route: {ex.Message}";
                MessageBox.Show($"Failed to deactivate route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Enhanced existing commands
        private async Task SaveRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Saving route '{SelectedRoute.RouteName}'...";

                var result = await _routeService.UpdateRouteAsync(SelectedRoute);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Failed to save route: {result.Error}";
                    MessageBox.Show(result.Error!, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (RouteStops.Any())
                {
                    var timingResult = await _routeService.UpdateRouteStopsTimingAsync(SelectedRoute.RouteId, RouteStops);
                    if (!timingResult.IsSuccess)
                    {
                        Logger.Warning("Route stop timing persistence failed: {Error}", timingResult.Error);
                    }
                }

                StatusMessage = $"Successfully saved route '{SelectedRoute.RouteName}'";
                Logger.Information("Saved route {RouteName}", SelectedRoute.RouteName);
                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save route");
                StatusMessage = $"Failed to save route: {ex.Message}";
                MessageBox.Show($"Failed to save route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteRouteAsync()
        {
            if (SelectedRoute == null || IsLoading)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete or retire route '{SelectedRoute.RouteName}'?\n\nEmpty routes are removed. Routes still referenced by schedules or student keys are retired.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Deleting route '{SelectedRoute.RouteName}'...";

                var deleteResult = await _routeService.DeleteRouteAsync(SelectedRoute.RouteId);
                if (!deleteResult.IsSuccess)
                {
                    StatusMessage = $"Failed to delete route: {deleteResult.Error}";
                    MessageBox.Show(deleteResult.Error!, "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var routeName = SelectedRoute.RouteName;
                if (!string.IsNullOrWhiteSpace(deleteResult.Error))
                {
                    SelectedRoute.IsActive = false;
                    StatusMessage = deleteResult.Error;
                    Logger.Information("Retired route {RouteName}", routeName);
                    return;
                }

                AvailableRoutes.Remove(SelectedRoute);
                SelectedRoute = AvailableRoutes.FirstOrDefault();

                StatusMessage = $"Successfully deleted route '{routeName}'";
                Logger.Information("Deleted route {RouteName}", routeName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete route");
                StatusMessage = $"Failed to delete route: {ex.Message}";
                MessageBox.Show($"Failed to delete route: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing data...";

                await LoadDataFromServiceAsync();
                Logger.Information("Data refreshed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh data");
                StatusMessage = $"Failed to refresh data: {ex.Message}";
                MessageBox.Show($"Failed to refresh data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Helper method to load route stops
        private async Task LoadRouteStopsAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            try
            {
                RouteStops.Clear();
                AssignedStudentsForSelectedRoute.Clear();

                var result = await _routeService.GetRouteStopsAsync(SelectedRoute.RouteId);
                if (result.IsSuccess)
                {
                    foreach (var stop in result.Value!)
                    {
                        RouteStops.Add(stop);
                    }
                }
                else
                {
                    StatusMessage = $"Could not load stops for {GetRouteDisplayName(SelectedRoute)}: {result.Error}";
                    Logger.Error("Failed to load route stops for route {RouteName}: {Error}", SelectedRoute.RouteName, result.Error);
                }

                await ReloadStudentListsForRouteAsync();

                OnPropertyChanged(nameof(RouteStopCount));
                OnPropertyChanged(nameof(AssignedStudentCount));
                Logger.Information("Loaded {StopCount} stops for route {RouteName}", RouteStops.Count, SelectedRoute.RouteName);
                if (result.IsSuccess && RouteStops.Count == 0)
                {
                    StatusMessage = $"No stops found for {GetRouteDisplayName(SelectedRoute)} — add stops to begin routing.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load route stops for route {RouteName}", SelectedRoute.RouteName);
                StatusMessage = $"Could not load stops for {GetRouteDisplayName(SelectedRoute)}: {ex.Message}";
            }
        }

        private async Task ViewScheduleAsync()
        {
            if (SelectedRoute == null)
            {
                return;
            }

            try
            {
                var stopsResult = await _routeService.GetRouteStopsAsync(SelectedRoute.RouteId);
                if (!stopsResult.IsSuccess)
                {
                    StatusMessage = stopsResult.Error ?? "Could not load published stops.";
                    return;
                }

                var appointments = DriverScheduleViewModel.FromPublishedStops(
                    SelectedRoute,
                    stopsResult.Value ?? []);
                var status = appointments.Count == 0
                    ? $"No published stops on {SelectedRoute.RouteName} — add stops before viewing the schedule."
                    : $"Published times for {SelectedRoute.RouteName} ({appointments.Count} stops)";

                new Window
                {
                    Title = $"Schedule — {SelectedRoute.RouteName}",
                    Content = new DriverScheduleView(new DriverScheduleViewModel(appointments, status)),
                    Width = 1200,
                    Height = 800,
                    Owner = Application.Current?.MainWindow
                }.Show();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open published schedule for route {RouteName}", SelectedRoute.RouteName);
                StatusMessage = $"Could not open schedule: {ex.Message}";
            }
        }

        private void GenerateReport()
        {
            ExportRouteAssignmentPdfAsync(includeMap: false);
        }

        #endregion
    }
}
