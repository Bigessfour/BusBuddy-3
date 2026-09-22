using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using BusBuddy.WPF;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Map;
using BusBuddy.WPF.Views.Activity;
using BusBuddy.WPF.Views.Reports;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Activity
{
    /// <summary>
    /// Clerk trip board. Route != Trip — this is not the daily route editor.
    /// Does not write leftover calendars or hop-5 Schedule rows.
    /// </summary>
    public class ActivityManagementViewModel : BaseViewModel
    {
        private static readonly new ILogger Logger = Log.ForContext<ActivityManagementViewModel>();
        private readonly ITripEventService? _trips;
        private readonly IRouteService? _routes;
        private readonly IDestinationService? _destinations;
        private readonly PdfReportService _pdf;
        private readonly MapViewModel? _map;
        private readonly ITripReasonCatalog? _reasons;

        public ObservableCollection<TripEvent> Trips { get; } = new();
        public ObservableCollection<TripBoardAppointment> Appointments { get; } = new();

        private TripEvent? _selectedTrip;
        public TripEvent? SelectedTrip
        {
            get => _selectedTrip;
            set
            {
                if (SetProperty(ref _selectedTrip, value) && value is not null)
                {
                    _ = PlotSelectedTripAsync(value);
                }

                NotifyTripCommands();
            }
        }

        public ICommand ImportCsvCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OptimizeDayCommand { get; }
        public ICommand NewTripCommand { get; }
        public ICommand EditTripCommand { get; }
        public ICommand ConfirmTripCommand { get; }
        public ICommand CalculateDistanceCommand { get; }
        public ICommand PrintTicketCommand { get; }

        public ActivityManagementViewModel()
            : this(
                App.ServiceProvider?.GetService<ITripEventService>(),
                App.ServiceProvider?.GetService<IRouteService>(),
                App.ServiceProvider?.GetService<IDestinationService>(),
                App.ServiceProvider?.GetService<PdfReportService>(),
                App.ServiceProvider?.GetService<MapViewModel>(),
                App.ServiceProvider?.GetService<ITripReasonCatalog>())
        {
        }

        public ActivityManagementViewModel(ITripEventService? trips)
            : this(trips, null, null, null, null, null)
        {
        }

        public ActivityManagementViewModel(
            ITripEventService? trips,
            IRouteService? routes,
            IDestinationService? destinations,
            PdfReportService? pdf,
            MapViewModel? map)
            : this(trips, routes, destinations, pdf, map, null)
        {
        }

        public ActivityManagementViewModel(
            ITripEventService? trips,
            IRouteService? routes,
            IDestinationService? destinations,
            PdfReportService? pdf,
            MapViewModel? map,
            ITripReasonCatalog? reasons)
        {
            _trips = trips;
            _routes = routes;
            _destinations = destinations;
            _pdf = pdf ?? new PdfReportService();
            _map = map;
            _reasons = reasons;
            ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync, () => _trips is not null);
            RefreshCommand = new AsyncRelayCommand(LoadTripsAsync);
            OptimizeDayCommand = new AsyncRelayCommand(OptimizeSameDayAsync, () => _trips is not null);
            NewTripCommand = new AsyncRelayCommand(NewTripAsync, () => _trips is not null);
            EditTripCommand = new AsyncRelayCommand(EditTripAsync, () => _trips is not null && SelectedTrip is not null);
            ConfirmTripCommand = new AsyncRelayCommand(ConfirmTripAsync, () => _trips is not null && SelectedTrip is not null);
            CalculateDistanceCommand = new AsyncRelayCommand(CalculateDistanceAsync, () => _trips is not null && SelectedTrip is not null);
            PrintTicketCommand = new RelayCommand(PrintTicket, () => SelectedTrip is not null);
            _ = LoadTripsAsync();
        }

        public void SelectTripById(int tripEventId)
        {
            var match = Trips.FirstOrDefault(t => t.TripEventId == tripEventId);
            if (match is not null)
            {
                SelectedTrip = match;
            }
        }

        private async Task LoadTripsAsync()
        {
            if (_trips is null)
            {
                StatusMessage = "Trip board is unavailable until services start.";
                Logger.Warning("Trip board load skipped — ITripEventService is not available");
                return;
            }

            try
            {
                IsLoading = true;
                var items = await _trips.GetAllTripsAsync();
                Trips.Clear();
                foreach (var trip in items)
                {
                    Trips.Add(trip);
                }

                RebuildAppointments();
                StatusMessage = $"Trip board: {Trips.Count} row(s).";
                Logger.Information("Trip board loaded Rows={Count}", Trips.Count);
                UiProofLog.Write(Logger, "Trip Board", "ActivityManagementView", "loaded", $"Rows={Trips.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load trip board");
                StatusMessage = "Could not load trip board.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RebuildAppointments()
        {
            Appointments.Clear();
            foreach (var trip in Trips)
            {
                Appointments.Add(new TripBoardAppointment
                {
                    TripEventId = trip.TripEventId,
                    StartTime = trip.StartTime,
                    EndTime = trip.EndTime,
                    Subject = trip.Subject,
                    Location = trip.DestinationName ?? trip.Destination ?? string.Empty,
                    Notes = trip.TripEventId.ToString()
                });
            }
        }

        private async Task ImportCsvAsync()
        {
            if (_trips is null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Import trip board CSV"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                IsLoading = true;
                var csv = await File.ReadAllTextAsync(dialog.FileName);
                var result = await _trips.ImportBoardCsvAsync(csv);
                await LoadTripsAsync();
                var warningText = result.Warnings.Count == 0
                    ? string.Empty
                    : $" {result.Warnings.Count} warning(s).";
                StatusMessage =
                    $"Imported {result.Upserted} trip(s) by ticket # ({result.MissingInfo} MissingInfo).{warningText}";
                if (result.Warnings.Count > 0)
                {
                    Logger.Warning("Trip board import warnings: {Warnings}", result.Warnings);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Trip board CSV import failed");
                StatusMessage = "Trip board import failed.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OptimizeSameDayAsync()
        {
            if (_trips is null)
            {
                return;
            }

            try
            {
                IsLoading = true;
                var day = SelectedTrip?.TripDate.Date ?? DateTime.Today;
                var result = await _trips.SuggestSameDayFleetAsync(day, applyToUnassigned: true);
                await LoadTripsAsync();
                StatusMessage = result.Status;
                Logger.Information("Trip board optimize-day Succeeded={Succeeded} Applied={Applied}", result.Succeeded, result.AppliedCount);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Trip board Route Optimization failed");
                StatusMessage = "Could not optimize today's trips.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task NewTripAsync()
        {
            await OpenEditorAsync(null).ConfigureAwait(true);
        }

        private async Task EditTripAsync()
        {
            if (SelectedTrip is null)
            {
                return;
            }

            await OpenEditorAsync(SelectedTrip).ConfigureAwait(true);
        }

        private async Task OpenEditorAsync(TripEvent? existing)
        {
            if (_trips is null)
            {
                return;
            }

            var editor = new TripEventEditDialogViewModel(existing, _trips, _routes, _destinations, _reasons);
            await editor.LoadAvailableDataAsync().ConfigureAwait(true);
            var dialog = new TripEventEditDialog(editor);
            DialogOwner.Assign(dialog);
            if (dialog.ShowDialog() != true || editor.Result is null)
            {
                return;
            }

            var trip = editor.Result;
            trip.RouteId = null;
            if (trip.TripEventId == 0)
            {
                await _trips.AddTripAsync(trip).ConfigureAwait(true);
            }
            else
            {
                await _trips.UpdateTripAsync(trip).ConfigureAwait(true);
            }

            await _trips.RefreshPathMilesAsync(trip.TripEventId).ConfigureAwait(true);

            await LoadTripsAsync().ConfigureAwait(true);
            SelectedTrip = Trips.FirstOrDefault(t => t.TripEventId == trip.TripEventId)
                              ?? Trips.FirstOrDefault(t => t.ExternalTicketNo == trip.ExternalTicketNo);
            StatusMessage = "Trip saved.";
        }

        private async Task ConfirmTripAsync()
        {
            if (_trips is null || SelectedTrip is null)
            {
                return;
            }

            var result = await _trips.ConfirmTripAsync(SelectedTrip.TripEventId).ConfigureAwait(true);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
                return;
            }

            await LoadTripsAsync().ConfigureAwait(true);
            StatusMessage = "Trip confirmed.";
        }

        private async Task CalculateDistanceAsync()
        {
            if (_trips is null || SelectedTrip is null)
            {
                return;
            }

            await _trips.RefreshPathMilesAsync(SelectedTrip.TripEventId).ConfigureAwait(true);
            var refreshed = await _trips.GetTripByIdAsync(SelectedTrip.TripEventId).ConfigureAwait(true);
            await LoadTripsAsync().ConfigureAwait(true);
            if (refreshed is not null)
            {
                SelectedTrip = Trips.FirstOrDefault(t => t.TripEventId == refreshed.TripEventId);
                await PlotSelectedTripAsync(refreshed).ConfigureAwait(true);
                StatusMessage = refreshed.PathMiles.HasValue
                    ? $"Distance {refreshed.PathMiles.Value:0.00} miles"
                    : "Distance needs a validated origin and destination.";
            }
        }

        private void PrintTicket()
        {
            if (SelectedTrip is null)
            {
                return;
            }

            try
            {
                var pdf = _pdf.GenerateTripTicket(SelectedTrip, _map?.LatestMapSnapshotPng);
                var preview = new PdfPreviewWindow(pdf, "Trip Ticket");
                DialogOwner.Assign(preview);
                preview.Show();
                UiProofLog.Write(Logger, "Trip Board", "ActivityManagementView", "ticket", SelectedTrip.ExternalTicketNo ?? SelectedTrip.TripEventId.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Trip ticket PDF failed");
                StatusMessage = "Could not generate trip ticket.";
            }
        }

        private int _plotGeneration;

        private async Task PlotSelectedTripAsync(TripEvent trip)
        {
            var generation = Interlocked.Increment(ref _plotGeneration);
            var map = _map ?? App.ServiceProvider?.GetService<MapViewModel>();
            if (map is null || generation != _plotGeneration)
            {
                return;
            }

            await map.TryPlotTripAsync(trip).ConfigureAwait(true);
            if (generation != _plotGeneration)
            {
                return;
            }
        }

        private void NotifyTripCommands()
        {
            if (EditTripCommand is AsyncRelayCommand edit)
            {
                edit.NotifyCanExecuteChanged();
            }

            if (ConfirmTripCommand is AsyncRelayCommand confirm)
            {
                confirm.NotifyCanExecuteChanged();
            }

            if (CalculateDistanceCommand is AsyncRelayCommand distance)
            {
                distance.NotifyCanExecuteChanged();
            }

            if (PrintTicketCommand is IRelayCommand print)
            {
                print.NotifyCanExecuteChanged();
            }
        }
    }

    public sealed class TripBoardAppointment
    {
        public int TripEventId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
