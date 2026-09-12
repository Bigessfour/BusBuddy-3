using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF;
using BusBuddy.WPF.Logging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Activity
{
    /// <summary>
    /// Clerk trip board (office Activity Schedule). Route != Trip — this is not the daily route editor.
    /// </summary>
    public class ActivityManagementViewModel : BaseViewModel
    {
        private static readonly new ILogger Logger = Log.ForContext<ActivityManagementViewModel>();
        private readonly ITripEventService? _trips;

        public ObservableCollection<TripEvent> Trips { get; } = new();

        private TripEvent? _selectedTrip;
        public TripEvent? SelectedTrip
        {
            get => _selectedTrip;
            set
            {
                if (SetProperty(ref _selectedTrip, value) && value is not null)
                {
                    TryPlotSelectedTrip(value);
                }
            }
        }

        public ICommand ImportCsvCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OptimizeDayCommand { get; }

        public ActivityManagementViewModel()
            : this(App.ServiceProvider?.GetService<ITripEventService>())
        {
        }

        public ActivityManagementViewModel(ITripEventService? trips)
        {
            _trips = trips;
            ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync, () => _trips is not null);
            RefreshCommand = new AsyncRelayCommand(LoadTripsAsync);
            OptimizeDayCommand = new AsyncRelayCommand(OptimizeSameDayAsync, () => _trips is not null);
            _ = LoadTripsAsync();
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

        private static void TryPlotSelectedTrip(TripEvent trip)
        {
            var map = App.ServiceProvider?.GetService<Map.MapViewModel>();
            map?.TryPlotTrip(trip);
        }
    }
}
