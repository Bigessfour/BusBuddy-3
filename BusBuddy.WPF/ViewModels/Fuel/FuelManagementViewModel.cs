using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Views.Fuel;
using Serilog;
using Serilog.Context;
using BusBuddy.WPF.Commands;
using FuelModel = BusBuddy.Core.Models.Fuel;

// Disable async method without await operator warnings
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace BusBuddy.WPF.ViewModels.Fuel
{
    public class FuelManagementViewModel : BaseInDevelopmentViewModel
    {
        private readonly IFuelService _fuelService;
        private readonly IBusService _busService;

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

        private FuelModel? _selectedFuelRecord;
        public FuelModel? SelectedFuelRecord
        {
            get => _selectedFuelRecord;
            set
            {
                SetProperty(ref _selectedFuelRecord, value);
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanDelete));
            }
        }

        public bool CanEdit => SelectedFuelRecord != null;
        public bool CanDelete => SelectedFuelRecord != null;

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand ReportCommand { get; }
        public RelayCommand ReconciliationCommand { get; }

        public FuelManagementViewModel(IFuelService fuelService, IBusService busService)
            : base()
        {
            _fuelService = fuelService;
            _busService = busService;

            using (LogContext.PushProperty("ViewModelType", nameof(FuelManagementViewModel)))
            using (LogContext.PushProperty("OperationType", "Construction"))
            {
                Logger.Information("FuelManagementViewModel constructor started");

                AddCommand = new RelayCommand(_ => { _ = AddFuelRecordAsync(); });
                EditCommand = new RelayCommand(_ => { _ = EditFuelRecordAsync(); }, _ => CanEdit);
                DeleteCommand = new RelayCommand(_ => { _ = DeleteFuelRecordAsync(); }, _ => CanDelete);
                ExportCommand = new RelayCommand(_ => { _ = ExportFuelDataAsync(); });
                ReportCommand = new RelayCommand(_ => { _ = ShowFuelReportAsync(); });
                ReconciliationCommand = new RelayCommand(_ => { _ = ShowFuelReconciliationAsync(); });

                // Set as ready for development
                IsInDevelopment = false;

                Logger.Information("FuelManagementViewModel constructor completed, initiating LoadFuelRecordsAsync");
                _ = LoadFuelRecordsAsync();
            }
        }

        private async Task LoadFuelRecordsAsync()
        {
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
                    foreach (var record in records)
                    {
                        FuelRecords.Add(record);
                    }


                    CalculateTrends();

                    Logger.Information("Loaded {RecordCount} fuel records", FuelRecords.Count);
                }
            });
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

                    // Get first bus for default
                    var buses = await _busService.GetAllBusesAsync();
                    var firstBus = buses.FirstOrDefault();
                    if (firstBus == null)
                    {
                        Logger.Warning("No buses available for fuel record creation");
                        MessageBox.Show("No buses available. Please add buses first.", "No Buses", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newFuel = new FuelModel
                    {
                        FuelDate = DateTime.Now,
                        FuelLocation = "Key Pumps",
                        VehicleFueledId = firstBus.BusId,
                        VehicleOdometerReading = 0,
                        FuelType = "Diesel",
                        Gallons = 0,
                        PricePerGallon = 0,
                        TotalCost = 0
                    };

                    // Show dialog to edit the new record
                    var dialog = new FuelDialog(newFuel, _busService);

                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value)
                    {
                        var created = await _fuelService.CreateFuelRecordAsync(newFuel);
                        FuelRecords.Add(created);
                        CalculateTrends();

                        Logger.Information("Added new fuel record with ID {FuelId}", created.FuelId);
                    }
                    else
                    {
                        Logger.Information("Fuel record creation cancelled by user");
                    }
                }
            });
        }

        private async Task EditFuelRecordAsync()
        {
            if (SelectedFuelRecord == null)
            {
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

                    // Create a copy to edit
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

                    // Show dialog to edit the record
                    var dialog = new FuelDialog(recordToEdit, _busService);

                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value)
                    {
                        var updated = await _fuelService.UpdateFuelRecordAsync(recordToEdit);

                        // Update the selected record with new values
                        var index = FuelRecords.IndexOf(SelectedFuelRecord);
                        if (index >= 0)
                        {
                            FuelRecords[index] = updated;
                            SelectedFuelRecord = updated;
                        }

                        CalculateTrends();
                        Logger.Information("Updated fuel record with ID {FuelId}", updated.FuelId);
                    }
                    else
                    {
                        Logger.Information("Fuel record edit cancelled by user");
                    }
                }
            });
        }

        private async Task DeleteFuelRecordAsync()
        {
            if (SelectedFuelRecord == null)
            {
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

                    // Confirm deletion
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete the fuel record from {SelectedFuelRecord.FuelDate:d} for {SelectedFuelRecord.Gallons} gallons?",
                        "Confirm Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var deleted = await _fuelService.DeleteFuelRecordAsync(SelectedFuelRecord.FuelId);
                        if (deleted)
                        {
                            var fuelId = SelectedFuelRecord.FuelId;
                            FuelRecords.Remove(SelectedFuelRecord);
                            SelectedFuelRecord = null;
                            CalculateTrends();

                            Logger.Information("Deleted fuel record with ID {FuelId}", fuelId);
                        }
                        else
                        {
                            Logger.Warning("Failed to delete fuel record with ID {FuelId}", SelectedFuelRecord.FuelId);
                        }
                    }
                    else
                    {
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

                    // Create a Save File Dialog
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = $"Fuel_Records_{DateTime.Now:yyyyMMdd}",
                        DefaultExt = ".csv",
                        Filter = "CSV Files (*.csv)|*.csv"
                    };

                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value)
                    {
                        // Export data to CSV
                        using var writer = new System.IO.StreamWriter(dialog.FileName);

                        // Write header
                        writer.WriteLine("Fuel ID,Date,Location,Bus,Odometer,Fuel Type,Gallons,Price/Gallon,Total Cost,Notes");

                        // Write data
                        foreach (var record in FuelRecords)
                        {
                            var busNumber = "Unknown";
                            try
                            {
                                var bus = await _busService.GetBusByIdAsync(record.VehicleFueledId);
                                if (bus != null)
                                {
                                    busNumber = bus.BusNumber;
                                }
                            }
                            catch
                            {
                                /* Ignore errors and use default */
                            }

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

                        MessageBox.Show($"Successfully exported {FuelRecords.Count} fuel records to {dialog.FileName}",
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        Logger.Information("Exported {RecordCount} fuel records to CSV", FuelRecords.Count);
                    }
                    else
                    {
                        Logger.Information("Fuel data export cancelled by user");
                    }
                }
            });
        }

        private Task ShowFuelReportAsync()
        {
            MessageBox.Show("Fuel reports will be implemented in the next sprint.",
                "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
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

                    var dialog = new FuelReconciliationDialog(_fuelService, _busService);
                    _ = dialog.ShowDialog();

                    // No need to refresh data as reconciliation doesn't modify records
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

                    FuelTrends.Clear();
                    // Simple trend: average MPG per month
                    var grouped = FuelRecords
                        .GroupBy(f => new { f.FuelDate.Year, f.FuelDate.Month })
                        .Select(g => new FuelTrendPoint
                        {
                            Period = new DateTime(g.Key.Year, g.Key.Month, 1),
                            AvgMPG = g.Average(f => f.Gallons.HasValue && f.Gallons > 0 ? (f.VehicleOdometerReading / (double)f.Gallons.Value) : 0)
                        });
                    foreach (var pt in grouped)
                    {
                        FuelTrends.Add(pt);
                    }

                    Logger.Information("Calculated {TrendPointCount} fuel trend points", FuelTrends.Count);
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
            catch (Exception ex)
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Command execution failed: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // BaseViewModel already provides PropertyChanged functionality
    }
}
