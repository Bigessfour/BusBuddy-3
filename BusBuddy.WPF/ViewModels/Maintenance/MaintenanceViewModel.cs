using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Commands;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Maintenance;

public class MaintenanceViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<MaintenanceViewModel>();
    private readonly IMaintenanceService _maintenanceService;
    private readonly IBusService _busService;
    private BusBuddy.Core.Models.Maintenance? _selectedRecord;

    public MaintenanceViewModel(IMaintenanceService maintenanceService, IBusService busService)
    {
        _maintenanceService = maintenanceService;
        _busService = busService;
        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        AddCommand = new RelayCommand(async _ => await AddAsync());
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => SelectedRecord != null);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedRecord != null);
        Logger.Information("MaintenanceViewModel constructed — loading records");
        _ = LoadAsync();
    }

    public ObservableCollection<BusBuddy.Core.Models.Maintenance> Records { get; } = new();
    public ObservableCollection<BusBuddy.Core.Models.Bus> Vehicles { get; } = new();

    public BusBuddy.Core.Models.Maintenance? SelectedRecord
    {
        get => _selectedRecord;
        set => SetProperty(ref _selectedRecord, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }

    private async Task LoadAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StatusMessage = "Loading maintenance records...";
            Logger.Information("Loading maintenance records and vehicles");
            var records = await _maintenanceService.GetAllMaintenanceRecordsAsync();
            var buses = await _busService.GetAllBusesAsync();
            Records.Clear();
            foreach (var record in records)
            {
                Records.Add(record);
            }

            Vehicles.Clear();
            foreach (var bus in buses)
            {
                Vehicles.Add(bus);
            }

            stopwatch.Stop();
            StatusMessage = $"{Records.Count} maintenance records";
            Logger.Information(
                "Maintenance UI loaded Records={RecordCount} Vehicles={VehicleCount} ElapsedMs={ElapsedMs}",
                Records.Count, Vehicles.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error(ex, "Failed to load maintenance records after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            StatusMessage = "Load failed";
        }
    }

    private async Task AddAsync()
    {
        var firstBus = Vehicles.FirstOrDefault();
        if (firstBus is null)
        {
            Logger.Warning("Add maintenance skipped — no vehicles loaded");
            StatusMessage = "Add a bus before creating maintenance records";
            return;
        }

        try
        {
            Logger.Information("Preparing draft maintenance row VehicleId={VehicleId}", firstBus.BusId);
            var draft = new BusBuddy.Core.Models.Maintenance
            {
                Date = DateTime.Today,
                VehicleId = firstBus.BusId,
                OdometerReading = firstBus.CurrentOdometer ?? 0,
                MaintenanceCompleted = string.Empty,
                Vendor = string.Empty,
                RepairCost = 0,
                Status = "Scheduled",
                Priority = "Normal"
            };
            Records.Insert(0, draft);
            SelectedRecord = draft;
            StatusMessage = "Fill in the new row, then click Save";
            Logger.Information("Draft maintenance row added for vehicle {VehicleId}", firstBus.BusId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add maintenance record");
            StatusMessage = "Add failed";
        }
    }

    private async Task SaveAsync()
    {
        if (SelectedRecord is null)
        {
            Logger.Debug("Save skipped — no maintenance record selected");
            return;
        }

        try
        {
            if (SelectedRecord.MaintenanceId == 0)
            {
                Logger.Information("Creating maintenance record VehicleId={VehicleId}", SelectedRecord.VehicleId);
                var created = await _maintenanceService.CreateMaintenanceRecordAsync(SelectedRecord);
                StatusMessage = "Saved";
                Logger.Information("Created maintenance record {MaintenanceId}", created.MaintenanceId);
            }
            else
            {
                Logger.Information("Saving maintenance record {MaintenanceId}", SelectedRecord.MaintenanceId);
                await _maintenanceService.UpdateMaintenanceRecordAsync(SelectedRecord);
                StatusMessage = "Saved";
                Logger.Information("Saved maintenance record {MaintenanceId}", SelectedRecord.MaintenanceId);
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save maintenance record {MaintenanceId}", SelectedRecord.MaintenanceId);
            StatusMessage = "Save failed";
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedRecord is null)
        {
            Logger.Debug("Delete skipped — no maintenance record selected");
            return;
        }

        var id = SelectedRecord.MaintenanceId;
        var confirm = MessageBox.Show(
            "Delete this maintenance record? This cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            Logger.Information("Delete cancelled for maintenance record {MaintenanceId}", id);
            StatusMessage = "Delete cancelled";
            return;
        }

        if (id == 0)
        {
            Records.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = "Draft discarded";
            return;
        }

        try
        {
            Logger.Information("Deleting maintenance record {MaintenanceId}", id);
            await _maintenanceService.DeleteMaintenanceRecordAsync(id);
            Records.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = "Deleted";
            Logger.Information("Deleted maintenance record {MaintenanceId} from UI", id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to delete maintenance record {MaintenanceId}", id);
            StatusMessage = "Delete failed";
        }
    }
}
