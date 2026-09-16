using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.Views.Fuel;
using CommunityToolkit.Mvvm.Input;
using Serilog.Context;
using FuelModel = BusBuddy.Core.Models.Fuel;

namespace BusBuddy.WPF.ViewModels.Fuel
{
    public class FuelManagementViewModel : BaseViewModel
    {
        private readonly IFuelService _fuelService;
        private readonly IBusService _busService;
        private readonly IFuelLocationCatalog? _fuelLocationCatalog;
        private readonly IUserSettingsService? _userSettings;
        private readonly SemaphoreSlim _loadGate = new(1, 1);
        private int _loadVersion;

        private ObservableCollection<FuelModel> _fuelRecords = new();
        public ObservableCollection<FuelModel> FuelRecords
        {
            get => _fuelRecords;
            set => SetProperty(ref _fuelRecords, value);
        }

        private ObservableCollection<FuelTrendPoint> _fuelTrends = new();
        public ObservableCollection<FuelTrendPoint> FuelTrends
        {
            get => _fuelTrends;
            set => SetProperty(ref _fuelTrends, value);
        }

        public bool HasFuelTrendData => FuelTrends.Count > 0;

        public string FuelTrendStatus =>
            HasFuelTrendData
                ? FuelTrends.Any(t => t.TripMpgSampleCount > 0)
                    ? "Gallons and trip MPG (from consecutive odometer readings)."
                    : "Gallons by month. Trip MPG appears after consecutive fill-ups with rising odometer."
                : "No fuel records yet — add fill-ups to populate this chart.";

        private FuelModel? _selectedFuelRecord;
        public FuelModel? SelectedFuelRecord
        {
            get => _selectedFuelRecord;
            set
            {
                if (SetProperty(ref _selectedFuelRecord, value))
                {
                    EditCommand.NotifyCanExecuteChanged();
                    DeleteCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand AddCommand { get; }
        public IAsyncRelayCommand EditCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IAsyncRelayCommand ReportCommand { get; }
        public IAsyncRelayCommand ReconciliationCommand { get; }

        public FuelManagementViewModel(
            IFuelService fuelService,
            IBusService busService,
            IFuelLocationCatalog? fuelLocationCatalog = null,
            IUserSettingsService? userSettings = null)
            : base()
        {
            _fuelService = fuelService;
            _busService = busService;
            _fuelLocationCatalog = fuelLocationCatalog;
            _userSettings = userSettings;

            using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
            using (LogContext.PushProperty("OperationType", "Construction"))
            {
                Logger.Information("FuelManagementViewModel constructor started");

                AddCommand = new AsyncRelayCommand(AddFuelRecordAsync);
                EditCommand = new AsyncRelayCommand(EditFuelRecordAsync, () => SelectedFuelRecord != null);
                DeleteCommand = new AsyncRelayCommand(DeleteFuelRecordAsync, () => SelectedFuelRecord != null);
                ExportCommand = new AsyncRelayCommand(ExportFuelDataAsync);
                ReportCommand = new AsyncRelayCommand(ShowReportsAsync);
                ReconciliationCommand = new AsyncRelayCommand(ShowFuelReconciliationAsync);

                Logger.Information("FuelManagementViewModel constructor completed, initiating LoadFuelRecordsAsync");
                _ = LoadFuelRecordsAsync();
            }
        }

        internal Task ReloadFuelRecordsAsync() => LoadFuelRecordsAsync();

        private async Task LoadFuelRecordsAsync()
        {
            var version = Interlocked.Increment(ref _loadVersion);
            await _loadGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (version != _loadVersion)
                {
                    return;
                }

                await LoadDataAsync(async () =>
                {
                    var correlationId = Guid.NewGuid().ToString("N")[..8];

                    using (LogContext.PushProperty("CorrelationId", correlationId))
                    using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
                    using (LogContext.PushProperty("OperationType", "LoadFuelRecords"))
                    {
                        Logger.Information("Loading fuel records");

                        FuelRecords.Clear();
                        var records = await _fuelService.GetAllFuelRecordsAsync();
                        if (version != _loadVersion)
                        {
                            return;
                        }

                        foreach (var record in records)
                        {
                            FuelRecords.Add(record);
                        }

                        CalculateTrends();

                        Logger.Information("Loaded {RecordCount} fuel records", FuelRecords.Count);
                        StatusMessage = $"Loaded {FuelRecords.Count} fuel records";
                    }
                });
            }
            finally
            {
                _loadGate.Release();
            }
        }

        private async Task AddFuelRecordAsync()
        {
            await ExecuteCommandAsync(async () =>
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];

                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
                using (LogContext.PushProperty("OperationType", "AddFuelRecord"))
                {
                    Logger.Information("Adding new fuel record");

                    var buses = await _busService.GetAllBusesAsync();
                    var firstBus = buses.FirstOrDefault();
                    if (firstBus == null)
                    {
                        Logger.Warning("No buses available for fuel record creation");
                        StatusMessage = "No buses available — add a bus first";
                        UserToast.Warning("No buses available. Please add buses before creating a fuel record.", "No buses");
                        return;
                    }

                    var newFuel = new FuelModel
                    {
                        FuelDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                        FuelLocation = string.Empty,
                        VehicleFueledId = firstBus.BusId,
                        VehicleOdometerReading = 0,
                        FuelType = "Diesel",
                        Gallons = null,
                        PricePerGallon = null,
                        TotalCost = null
                    };

                    var dialog = new FuelDialog(newFuel, _busService, _fuelLocationCatalog, _fuelService);
                    if (dialog.Owner == null && Application.Current?.MainWindow != null)
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }

                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value)
                    {
                        if (_fuelLocationCatalog != null && !string.IsNullOrWhiteSpace(newFuel.FuelLocation))
                        {
                            await _fuelLocationCatalog.RememberAsync(newFuel.FuelLocation);
                        }

                        var created = await _fuelService.CreateFuelRecordAsync(newFuel);
                        if (!created.IsSuccess)
                        {
                            StatusMessage = created.Error;
                            UserToast.Error(created.Error, "Fuel save failed");
                            return;
                        }

                        FuelRecords.Add(created.Value);
                        SelectedFuelRecord = created.Value;
                        CalculateTrends();
                        StatusMessage = $"Saved fuel record #{created.Value.FuelId}";
                        UserToast.Success(
                            $"Fuel record saved — {created.Value.Gallons:N3} gal on {created.Value.FuelDate:d}.",
                            "Save successful");
                        if (dialog.RememberedNewLocation)
                        {
                            UserToast.Info(
                                $"“{created.Value.FuelLocation}” saved to the fuel location list for future fill-ups.",
                                "New fuel location");
                        }

                        Logger.Information("Added new fuel record with ID {FuelId}", created.Value.FuelId);
                    }
                    else
                    {
                        StatusMessage = "Add cancelled";
                        Logger.Information("Fuel record creation cancelled by user");
                    }
                }
            });
        }

        private async Task EditFuelRecordAsync()
        {
            if (SelectedFuelRecord == null)
            {
                UserToast.Warning("Select a fuel record in the grid first.", "No selection");
                return;
            }

            await ExecuteCommandAsync(async () =>
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];

                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
                using (LogContext.PushProperty("OperationType", "EditFuelRecord"))
                using (LogContext.PushProperty("FuelId", SelectedFuelRecord.FuelId))
                {
                    Logger.Information("Editing fuel record with ID {FuelId}", SelectedFuelRecord.FuelId);

                    var recordToEdit = new FuelModel
                    {
                        FuelId = SelectedFuelRecord.FuelId,
                        FuelDate = SelectedFuelRecord.FuelDate,
                        FuelLocation = SelectedFuelRecord.FuelLocation,
                        VehicleFueledId = SelectedFuelRecord.VehicleFueledId,
                        VehicleOdometerReading = SelectedFuelRecord.VehicleOdometerReading,
                        FuelType = SelectedFuelRecord.FuelType,
                        Gallons = SelectedFuelRecord.Gallons,
                        PricePerGallon = SelectedFuelRecord.PricePerGallon,
                        TotalCost = SelectedFuelRecord.TotalCost,
                        Notes = SelectedFuelRecord.Notes
                    };

                    var dialog = new FuelDialog(recordToEdit, _busService, _fuelLocationCatalog, _fuelService);
                    if (dialog.Owner == null && Application.Current?.MainWindow != null)
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }

                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value)
                    {
                        if (_fuelLocationCatalog != null && !string.IsNullOrWhiteSpace(recordToEdit.FuelLocation))
                        {
                            await _fuelLocationCatalog.RememberAsync(recordToEdit.FuelLocation);
                        }

                        var updated = await _fuelService.UpdateFuelRecordAsync(recordToEdit);
                        if (!updated.IsSuccess)
                        {
                            StatusMessage = updated.Error;
                            UserToast.Error(updated.Error, "Fuel save failed");
                            return;
                        }

                        var index = FuelRecords.IndexOf(SelectedFuelRecord);
                        if (index >= 0)
                        {
                            FuelRecords[index] = updated.Value;
                            SelectedFuelRecord = updated.Value;
                        }

                        CalculateTrends();
                        StatusMessage = $"Updated fuel record #{updated.Value.FuelId}";
                        UserToast.Success(
                            $"Fuel record #{updated.Value.FuelId} saved successfully.",
                            "Save successful");
                        if (dialog.RememberedNewLocation)
                        {
                            UserToast.Info(
                                $"“{updated.Value.FuelLocation}” saved to the fuel location list for future fill-ups.",
                                "New fuel location");
                        }

                        Logger.Information("Updated fuel record with ID {FuelId}", updated.Value.FuelId);
                    }
                    else
                    {
                        StatusMessage = "Edit cancelled";
                        Logger.Information("Fuel record edit cancelled by user");
                    }
                }
            });
        }

        private async Task DeleteFuelRecordAsync()
        {
            if (SelectedFuelRecord == null)
            {
                UserToast.Warning("Select a fuel record in the grid first.", "No selection");
                return;
            }

            await ExecuteCommandAsync(async () =>
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];

                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
                using (LogContext.PushProperty("OperationType", "DeleteFuelRecord"))
                using (LogContext.PushProperty("FuelId", SelectedFuelRecord.FuelId))
                {
                    Logger.Information("Deleting fuel record with ID {FuelId}", SelectedFuelRecord.FuelId);

                    var result = MessageBox.Show(
                        $"Are you sure you want to delete the fuel record from {SelectedFuelRecord.FuelDate:d} for {SelectedFuelRecord.Gallons} gallons?",
                        "Confirm Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var fuelId = SelectedFuelRecord.FuelId;
                        var deleted = await _fuelService.DeleteFuelRecordAsync(fuelId);
                        if (deleted.IsSuccess)
                        {
                            FuelRecords.Remove(SelectedFuelRecord);
                            SelectedFuelRecord = null;
                            CalculateTrends();
                            StatusMessage = ClerkWriteMessages.Deleted($"Fuel record {fuelId}");
                            UserToast.Success(ClerkWriteMessages.Deleted($"Fuel record {fuelId}"), "Deleted");
                            Logger.Information("Deleted fuel record with ID {FuelId}", fuelId);
                        }
                        else
                        {
                            StatusMessage = deleted.Error;
                            UserToast.Error(deleted.Error, "Delete failed");
                            Logger.Warning("Failed to delete fuel record with ID {FuelId}: {Error}", fuelId, deleted.Error);
                        }
                    }
                    else
                    {
                        StatusMessage = "Delete cancelled";
                        Logger.Information("Fuel record deletion cancelled by user");
                    }
                }
            });
        }

        private async Task ExportFuelDataAsync()
        {
            await ExecuteCommandAsync(async () =>
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];

                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
                using (LogContext.PushProperty("OperationType", "ExportFuelData"))
                {
                    Logger.Information("Starting fuel data export");

                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"Fuel_Records_{DateTime.Now:yyyyMMdd}",
                        DefaultExt = ".csv",
                        Filter = "CSV Files (*.csv)|*.csv"
                    };

                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value)
                    {
                        var busNumbers = new Dictionary<int, string>();
                        try
                        {
                            foreach (var bus in await _busService.GetAllBusesAsync())
                            {
                                busNumbers[bus.BusId] = bus.BusNumber;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Could not preload bus numbers for fuel CSV export");
                        }

                        using var writer = new System.IO.StreamWriter(dialog.FileName);

                        writer.WriteLine("Fuel ID,Date,Location,Bus,Odometer,Fuel Type,Gallons,Price/Gallon,Total Cost,Notes");

                        foreach (var record in FuelRecords)
                        {
                            var busNumber = record.Vehicle?.BusNumber
                                ?? (busNumbers.TryGetValue(record.VehicleFueledId, out var n) ? n : "Unknown");

                            writer.WriteLine(
                                $"{record.FuelId}," +
                                $"{record.FuelDate:yyyy-MM-dd}," +
                                $"\"{record.FuelLocation}\"," +
                                $"\"{busNumber}\"," +
                                $"{record.VehicleOdometerReading}," +
                                $"\"{record.FuelType}\"," +
                                $"{record.Gallons}," +
                                $"{record.PricePerGallon}," +
                                $"{record.TotalCost}," +
                                $"\"{record.Notes?.Replace("\"", "\"\"")}\"");
                        }

                        StatusMessage = $"Exported {FuelRecords.Count} fuel records to {dialog.FileName}";
                        UserToast.Success(
                            $"Exported {FuelRecords.Count} fuel records to CSV.",
                            "Export successful");
                        Logger.Information("Exported {RecordCount} fuel records to CSV", FuelRecords.Count);
                    }
                    else
                    {
                        StatusMessage = "Export cancelled";
                        Logger.Information("Fuel data export cancelled by user");
                    }
                }
            });
        }

        private Task ShowReportsAsync()
        {
            try
            {
                var reportsView = new BusBuddy.WPF.Views.Reports.ReportsView();
                var host = new Window
                {
                    Title = "Reports",
                    Width = 1050,
                    Height = 750,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = reportsView
                };
                host.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open reports");
                UserToast.Error($"Could not open reports: {ex.Message}", "Reports");
            }

            return Task.CompletedTask;
        }

        private async Task ShowFuelReconciliationAsync()
        {
            await ExecuteCommandAsync(async () =>
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8];

                using (LogContext.PushProperty("CorrelationId", correlationId))
                using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
                using (LogContext.PushProperty("OperationType", "ShowFuelReconciliation"))
                {
                    Logger.Information("Opening fuel reconciliation dialog");

                    var dialog = new FuelReconciliationDialog(_fuelService, _busService, _fuelLocationCatalog, _userSettings);
                    _ = dialog.ShowDialog();

                    Logger.Information("Opened fuel reconciliation dialog");
                }
            });
        }

        private void CalculateTrends()
        {
            using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
            using (LogContext.PushProperty("OperationType", "CalculateTrends"))
            {
                try
                {
                    Logger.Information("Calculating fuel trends");

                    var monthly = FuelTrendAggregator.AggregateMonthly(FuelRecords);
                    FuelTrends = new ObservableCollection<FuelTrendPoint>(
                        monthly.Select(m => new FuelTrendPoint
                        {
                            Period = m.Period,
                            AvgMPG = m.AvgMpg,
                            TotalGallons = (double)m.TotalGallons,
                            TotalCost = (double)m.TotalCost,
                            FillCount = m.FillCount,
                            TripMpgSampleCount = m.TripMpgSampleCount
                        }));

                    OnPropertyChanged(nameof(HasFuelTrendData));
                    OnPropertyChanged(nameof(FuelTrendStatus));
                    Logger.Information(
                        "Calculated {TrendPointCount} fuel trend points (MPG samples={MpgMonths})",
                        FuelTrends.Count,
                        FuelTrends.Count(t => t.TripMpgSampleCount > 0));
                }
                catch (Exception ex)
                {
                    using (LogContext.PushProperty("ExceptionType", ex.GetType().Name))
                    {
                        Logger.Error(ex, "Error calculating fuel trends: {ErrorMessage}", ex.Message);
                    }
                }
            }
        }

        private async Task ExecuteCommandAsync(Func<Task> operation)
        {
            try
            {
                IsLoading = true;
                await operation();
            }
            catch (ArgumentException ex)
            {
                Logger.Warning(ex, "Fuel validation failed");
                StatusMessage = ex.Message;
                UserToast.Warning(ex.Message, "Cannot save fuel");
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error(ex, "Fuel persistence failed");
                StatusMessage = ex.Message;
                UserToast.Error(ex.Message, "Fuel save failed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Fuel command failed");
                StatusMessage = $"Error: {ex.Message}";
                UserToast.Error(ex.Message, "Fuel error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadDataAsync(Func<Task> action)
        {
            try
            {
                IsLoading = true;
                await action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading fuel data");
                StatusMessage = $"Error loading fuel records: {ex.Message}";
                UserToast.Error($"Could not load fuel records: {ex.Message}", "Load failed");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
