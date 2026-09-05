using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.ViewModels.Vehicle
{
    /// <summary>
    /// ViewModel for managing vehicles/buses in Phase 1
    /// Provides CRUD operations and fleet status monitoring
    /// </summary>
    public partial class VehicleManagementViewModel : BaseViewModel
    {
        private static new readonly ILogger Logger = Log.ForContext<VehicleManagementViewModel>();

        private readonly IBusService _busService;
        private BusBuddy.Core.Models.Bus? _lastSelectedVehicle;

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> _vehicles = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> _filteredVehicles = new();

        [ObservableProperty]
        private BusBuddy.Core.Models.Bus? _selectedVehicle;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedStatusFilter = "All Status";

        [ObservableProperty]
        private bool _isBusy;

        // Status filter options for the ComboBox
        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Status",
            "Active",
            "InService",
            "Maintenance",
            "OutOfService",
            "Retired"
        };

        // Operational status options for vehicle form
        public List<string> FleetTypeOptions { get; } = new()
        {
            "Regular",
            "Special Needs",
            "Activity"
        };

        // Total vehicle count for status bar
        public int TotalVehicleCount => FilteredVehicles?.Count ?? 0;

        public ICommand LoadVehiclesCommand { get; }
        public ICommand AddVehicleCommand { get; }
        public ICommand EditVehicleCommand { get; }
        public ICommand UpdateVehicleCommand { get; }
        public ICommand SaveVehicleCommand { get; }
        public ICommand DeleteVehicleCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SearchVehiclesCommand { get; }
        public ICommand RefreshCommand { get; }

        public VehicleManagementViewModel(IBusService busService)
        {
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));

            LoadVehiclesCommand = new AsyncRelayCommand(LoadVehiclesAsync);
            AddVehicleCommand = new AsyncRelayCommand(AddVehicleAsync);
            EditVehicleCommand = new AsyncRelayCommand(EditVehicleAsync, CanEditVehicle);
            UpdateVehicleCommand = new AsyncRelayCommand(UpdateVehicleAsync, CanUpdateVehicle);
            SaveVehicleCommand = new AsyncRelayCommand(SaveVehicleAsync, CanSaveVehicle);
            DeleteVehicleCommand = new AsyncRelayCommand(DeleteVehicleAsync, CanDeleteVehicle);
            CancelEditCommand = new RelayCommand(CancelEdit);
            SearchVehiclesCommand = new AsyncRelayCommand(SearchVehiclesAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);

            // Initialize with loading
            _ = LoadVehiclesAsync();
        }

        /// <summary>
        /// Runs a deferred startup action after the view loads (e.g. Add Bus shortcut).
        /// </summary>
        public async Task ApplyStartupAsync(VehicleManagementStartup startup)
        {
            if (startup == VehicleManagementStartup.None)
            {
                return;
            }

            using (LogContext.PushProperty("Startup", startup.ToString()))
            {
                Logger.Information("Applying fleet startup action {Startup}", startup);
                switch (startup)
                {
                    case VehicleManagementStartup.AddVehicle:
                        await AddVehicleAsync();
                        break;
                }
            }
        }

        private void RefreshCommandStates()
        {
            ((AsyncRelayCommand)EditVehicleCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)UpdateVehicleCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)SaveVehicleCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)DeleteVehicleCommand).NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Load all vehicles from the service
        /// </summary>
        private async Task LoadVehiclesAsync()
        {
            using (LogContext.PushProperty("Operation", "LoadVehicles"))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Loading vehicles...";
                    Logger.Information("Loading vehicles from IBusService");

                    var vehicles = await _busService.GetAllBusesAsync();

                    Vehicles.Clear();
                    foreach (var vehicle in vehicles)
                    {
                        Vehicles.Add(vehicle);
                    }

                    ApplyFilters();
                    StatusMessage = $"Loaded {Vehicles.Count} vehicles";
                    Logger.Information("Loaded {VehicleCount} vehicles", Vehicles.Count);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading vehicles: {ex.Message}";
                    Logger.Error(ex, "Failed to load buses; leaving vehicle grid empty");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        /// <summary>
        /// Apply search and status filters
        /// </summary>
        private void ApplyFilters()
        {
            if (Vehicles == null)
            {
                return;
            }

            var filtered = Vehicles.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText;
                filtered = filtered.Where(v =>
                    (v.Make?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.Model?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.LicenseNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.BusNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) == true));
            }

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "All Status")
            {
                filtered = filtered.Where(v => v.Status == SelectedStatusFilter);
            }

            FilteredVehicles.Clear();
            foreach (var vehicle in filtered)
            {
                FilteredVehicles.Add(vehicle);
            }

            OnPropertyChanged(nameof(TotalVehicleCount));
        }

        /// <summary>
        /// Property change handlers to trigger filtering
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnSelectedStatusFilterChanged(string value)
        {
            ApplyFilters();
        }

        /// <summary>
        /// Add a new vehicle
        /// </summary>
        private async Task AddVehicleAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Adding new vehicle...";

                var newVehicle = new BusBuddy.Core.Models.Bus
                {
                    BusNumber = $"BUS{(Vehicles.Count + 1):000}",
                    Make = "",
                    Model = "",
                    LicenseNumber = "",
                    SeatingCapacity = 40,
                    Year = DateTime.Now.Year,
                    Status = "Active",
                    FleetType = "Regular"
                };

                SelectedVehicle = newVehicle;
                StatusMessage = "Ready to add new vehicle - fill in details and click Save";
                Logger.Information(
                    "Prepared new vehicle draft BusNumber={BusNumber}",
                    newVehicle.BusNumber);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding vehicle: {ex.Message}";
                Logger.Error(ex, "Error preparing new vehicle draft");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Edit selected vehicle
        /// </summary>
        private async Task EditVehicleAsync()
        {
            if (SelectedVehicle == null)
            {
                return;
            }

            StatusMessage = $"Editing vehicle {SelectedVehicle.BusNumber}";
        }

        /// <summary>
        /// Save vehicle (add or update)
        /// </summary>
        private async Task SaveVehicleAsync()
        {
            if (SelectedVehicle is null)
            {
                return;
            }

            var vehicle = SelectedVehicle;
            var isNew = vehicle.BusId == 0;

            using (LogContext.PushProperty("Operation", "SaveVehicle"))
            using (LogContext.PushProperty("BusId", vehicle.BusId))
            using (LogContext.PushProperty("BusNumber", vehicle.BusNumber))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Saving vehicle...";
                    Logger.Information(
                        "Saving vehicle BusNumber={BusNumber} IsNew={IsNew}",
                        vehicle.BusNumber,
                        isNew);

                    // Ensure latest UI edits are propagated (in case some controls haven't lost focus yet)
                    vehicle.BusNumber = vehicle.BusNumber;

                    if (string.IsNullOrWhiteSpace(vehicle.BusNumber))
                    {
                        Logger.Warning("Vehicle save blocked — BusNumber blank");
                        StatusMessage = "Bus Number is required";
                        return;
                    }

                    if (isNew)
                    {
                        var added = await _busService.AddBusAsync(vehicle);
                        Vehicles.Add(added);
                        SelectedVehicle = added;
                        Logger.Information(
                            "Added vehicle BusId={BusId} BusNumber={BusNumber}",
                            added.BusId,
                            added.BusNumber);
                        StatusMessage = $"Vehicle {added.BusNumber} added successfully";
                    }
                    else
                    {
                        var updated = await _busService.UpdateBusAsync(vehicle);
                        if (!updated)
                        {
                            Logger.Warning(
                                "UpdateBusAsync reported no changes for BusId={BusId}",
                                vehicle.BusId);
                        }

                        var index = Vehicles.ToList().FindIndex(v => v.BusId == vehicle.BusId);
                        if (index >= 0)
                        {
                            Vehicles[index] = vehicle;
                        }

                        Logger.Information(
                            "Updated vehicle BusId={BusId} BusNumber={BusNumber}",
                            vehicle.BusId,
                            vehicle.BusNumber);
                        StatusMessage = $"Vehicle {vehicle.BusNumber} updated successfully";
                    }

                    ApplyFilters();
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "Error saving vehicle BusId={BusId} BusNumber={BusNumber}",
                        vehicle.BusId,
                        vehicle.BusNumber);
                    StatusMessage = $"Error saving vehicle: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        /// <summary>
        /// Update selected vehicle
        /// </summary>
        private async Task UpdateVehicleAsync()
        {
            await SaveVehicleAsync(); // Delegate to save method
        }

        /// <summary>
        /// Delete selected vehicle
        /// </summary>
        private async Task DeleteVehicleAsync()
        {
            if (SelectedVehicle is null)
            {
                return;
            }

            var vehicle = SelectedVehicle;
            var busId = vehicle.BusId;
            var busNumber = vehicle.BusNumber;

            using (LogContext.PushProperty("Operation", "DeleteVehicle"))
            using (LogContext.PushProperty("BusId", busId))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = $"Deleting vehicle {busNumber}...";
                    Logger.Information(
                        "Deleting vehicle BusId={BusId} BusNumber={BusNumber}",
                        busId,
                        busNumber);

                    if (busId <= 0)
                    {
                        Vehicles.Remove(vehicle);
                        SelectedVehicle = null;
                        ApplyFilters();
                        Logger.Information("Removed unsaved vehicle draft from list");
                        StatusMessage = "Draft vehicle removed";
                        return;
                    }

                    var deleted = await _busService.DeleteBusAsync(busId);
                    if (!deleted)
                    {
                        Logger.Warning("DeleteBusAsync returned false for BusId={BusId}", busId);
                        StatusMessage = "Vehicle could not be deleted";
                        return;
                    }

                    Vehicles.Remove(vehicle);
                    SelectedVehicle = null;
                    ApplyFilters();

                    Logger.Information("Successfully deleted vehicle BusId={BusId}", busId);
                    StatusMessage = "Vehicle deleted successfully";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error deleting vehicle BusId={BusId}", busId);
                    StatusMessage = $"Error deleting vehicle: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        /// <summary>
        /// Cancel edit operation
        /// </summary>
        private void CancelEdit()
        {
            if (SelectedVehicle?.BusId == 0)
            {
                Logger.Information("Cancelled unsaved vehicle draft");
                SelectedVehicle = null;
            }

            StatusMessage = "Edit cancelled";
        }

        /// <summary>
        /// Search vehicles by text
        /// </summary>
        private async Task SearchVehiclesAsync()
        {
            ApplyFilters(); // Just apply filters, no need for separate search
        }

        /// <summary>
        /// Refresh the vehicle list
        /// </summary>
        private async Task RefreshAsync()
        {
            SearchText = string.Empty;
            SelectedStatusFilter = "All Status";
            await LoadVehiclesAsync();
        }

        /// <summary>
        /// Check if a vehicle can be edited
        /// </summary>
        private bool CanEditVehicle()
        {
            return SelectedVehicle != null && !IsBusy;
        }

        /// <summary>
        /// Check if a vehicle can be updated
        /// </summary>
        private bool CanUpdateVehicle()
        {
            return SelectedVehicle != null &&
                SelectedVehicle.BusId > 0 &&
                      !IsBusy;
        }

        /// <summary>
        /// Check if a vehicle can be saved
        /// </summary>
        private bool CanSaveVehicle()
        {
            return SelectedVehicle != null &&
                   !string.IsNullOrWhiteSpace(SelectedVehicle.BusNumber) &&
                   SelectedVehicle.SeatingCapacity > 0 &&
                   SelectedVehicle.Year > 0 &&
                   !IsBusy;
        }

        /// <summary>
        /// Check if a vehicle can be deleted
        /// </summary>
        private bool CanDeleteVehicle()
        {
            return SelectedVehicle != null &&
                SelectedVehicle.BusId > 0 &&
                      !IsBusy;
        }

        /// <summary>
        /// Property change notification for selected vehicle
        /// </summary>
        partial void OnSelectedVehicleChanged(BusBuddy.Core.Models.Bus? value)
        {
            // Unsubscribe from previous selection changes
            if (_lastSelectedVehicle is not null)
            {
                _lastSelectedVehicle.PropertyChanged -= SelectedVehicle_PropertyChanged;
            }

            // Subscribe to new selection changes
            if (value is not null)
            {
                value.PropertyChanged += SelectedVehicle_PropertyChanged;
            }

            _lastSelectedVehicle = value;

            // Refresh command states
            RefreshCommandStates();
        }

        private void SelectedVehicle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When any field of the selected vehicle changes (e.g., BusNumber),
            // update command CanExecute states so Save becomes enabled immediately.
            RefreshCommandStates();
        }

        /// <summary>
        /// Property change notification for busy state
        /// </summary>
        partial void OnIsBusyChanged(bool value)
        {
            // Refresh all command states when busy state changes
            RefreshCommandStates();
        }

        /// <summary>
        /// Get vehicle fleet summary for dashboard
        /// </summary>
        public VehicleFleetSummary GetFleetSummary()
        {
            return new VehicleFleetSummary
            {
                TotalVehicles = Vehicles.Count,
                ActiveVehicles = Vehicles.Count(v => v.Status == "Active"),
                InactiveVehicles = Vehicles.Count(v => v.Status != "Active"),
                VehiclesInService = Vehicles.Count(v => v.Status == "InService"),
                VehiclesInMaintenance = Vehicles.Count(v => v.Status == "Maintenance"),
                VehiclesOutOfService = Vehicles.Count(v => v.Status == "OutOfService"),
                AverageCapacity = Vehicles.Any() ? (int)Vehicles.Average(v => v.SeatingCapacity) : 0
            };
        }
    }

    /// <summary>
    /// Summary data for vehicle fleet dashboard
    /// </summary>
    public class VehicleFleetSummary
    {
        public int TotalVehicles { get; set; }
        public int ActiveVehicles { get; set; }
        public int InactiveVehicles { get; set; }
        public int VehiclesInService { get; set; }
        public int VehiclesInMaintenance { get; set; }
        public int VehiclesOutOfService { get; set; }
        public int AverageCapacity { get; set; }
    }
}
