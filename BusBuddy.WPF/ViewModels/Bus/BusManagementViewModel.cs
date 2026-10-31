using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.ViewModels.Bus
{
    /// <summary>
    /// ViewModel for managing the bus fleet
    /// CRUD for district buses via IBusService
    /// </summary>
    public partial class BusManagementViewModel : BaseViewModel
    {
        private static new readonly ILogger Logger = Log.ForContext<BusManagementViewModel>();

        private readonly IBusService _busService;
        private BusBuddy.Core.Models.Bus? _lastSelectedBus;

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> _buses = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> _filteredBuses = new();

        [ObservableProperty]
        private BusBuddy.Core.Models.Bus? _selectedBus;

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

        // Operational status options for bus form
        public List<string> OperationalStatusOptions { get; } = new()
        {
            "Active",
            "InService",
            "Maintenance",
            "OutOfService",
            "Retired"
        };

        // Operational status options for bus form
        public List<string> FleetTypeOptions { get; } = new()
        {
            "Regular",
            "Special Needs",
            "Activity"
        };

        // Total bus count for status bar
        public int TotalBusCount => FilteredBuses?.Count ?? 0;

        public ICommand LoadBusesCommand { get; }
        public ICommand AddBusCommand { get; }
        public ICommand EditBusCommand { get; }
        public ICommand UpdateBusCommand { get; }
        public ICommand SaveBusCommand { get; }
        public ICommand DeleteBusCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SearchBusesCommand { get; }
        public ICommand RefreshCommand { get; }

        public BusManagementViewModel(IBusService busService)
        {
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));

            LoadBusesCommand = new AsyncRelayCommand(LoadBusesAsync);
            AddBusCommand = new AsyncRelayCommand(AddBusDraftAsync);
            EditBusCommand = new AsyncRelayCommand(EditBusAsync, CanEditBus);
            UpdateBusCommand = new AsyncRelayCommand(UpdateBusFromFormAsync, CanUpdateBus);
            SaveBusCommand = new AsyncRelayCommand(SaveBusAsync, CanSaveBus);
            DeleteBusCommand = new AsyncRelayCommand(DeleteBusAsync, CanDeleteBus);
            CancelEditCommand = new RelayCommand(CancelEdit);
            SearchBusesCommand = new AsyncRelayCommand(SearchBusesAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);

            // Initialize with loading
            _ = LoadBusesAsync();
        }

        /// <summary>
        /// Runs a deferred startup action after the view loads (e.g. Add Bus shortcut).
        /// </summary>
        public async Task ApplyStartupAsync(BusManagementStartup startup)
        {
            if (startup == BusManagementStartup.None)
            {
                return;
            }

            using (LogContext.PushProperty("Startup", startup.ToString()))
            {
                Logger.Information("Applying fleet startup action {Startup}", startup);
                switch (startup)
                {
                    case BusManagementStartup.AddBus:
                        await AddBusDraftAsync();
                        break;
                }
            }
        }

        private void RefreshCommandStates()
        {
            ((AsyncRelayCommand)EditBusCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)UpdateBusCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)SaveBusCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)DeleteBusCommand).NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Load all buses from the service
        /// </summary>
        private async Task LoadBusesAsync()
        {
            using (LogContext.PushProperty("Operation", "LoadBuses"))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Loading buses...";
                    Logger.Information("Loading buses from IBusService");

                    var vehicles = await _busService.GetAllBusesAsync();

                    Buses.Clear();
                    foreach (var vehicle in vehicles)
                    {
                        Buses.Add(vehicle);
                    }

                    ApplyFilters();
                    StatusMessage = $"Loaded {Buses.Count} buses";
                    Logger.Information("Loaded {BusCount} buses", Buses.Count);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading buses: {ex.Message}";
                    Logger.Error(ex, "Failed to load buses; leaving bus grid empty");
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
            if (Buses == null)
            {
                return;
            }

            var filtered = Buses.AsEnumerable();

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

            FilteredBuses.Clear();
            foreach (var vehicle in filtered)
            {
                FilteredBuses.Add(vehicle);
            }

            OnPropertyChanged(nameof(TotalBusCount));
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
        /// Start a new bus draft in the detail panel
        /// </summary>
        private async Task AddBusDraftAsync()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Adding new vehicle...";

                var newBus = new BusBuddy.Core.Models.Bus
                {
                    BusNumber = $"BUS{(Buses.Count + 1):000}",
                    Make = "",
                    Model = "",
                    LicenseNumber = "",
                    SeatingCapacity = 40,
                    Year = DateTime.Now.Year,
                    Status = "Active",
                    FleetType = "Regular"
                };

                SelectedBus = newBus;
                StatusMessage = "Ready to add new bus - fill in details and click Save";
                Logger.Information(
                    "Prepared new bus draft BusNumber={BusNumber}",
                    newBus.BusNumber);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding vehicle: {ex.Message}";
                Logger.Error(ex, "Error preparing new bus draft");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Edit selected bus
        /// </summary>
        private async Task EditBusAsync()
        {
            if (SelectedBus == null)
            {
                return;
            }

            StatusMessage = $"Editing bus {SelectedBus.BusNumber}";
        }

        /// <summary>
        /// Save bus (add or update)
        /// </summary>
        private async Task SaveBusAsync()
        {
            if (SelectedBus is null)
            {
                return;
            }

            var vehicle = SelectedBus;
            var isNew = vehicle.BusId == 0;

            using (LogContext.PushProperty("Operation", "SaveBus"))
            using (LogContext.PushProperty("BusId", vehicle.BusId))
            using (LogContext.PushProperty("BusNumber", vehicle.BusNumber))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Saving bus...";
                    Logger.Information(
                        "Saving bus BusNumber={BusNumber} IsNew={IsNew}",
                        vehicle.BusNumber,
                        isNew);

                    // Ensure latest UI edits are propagated (in case some controls haven't lost focus yet)
                    vehicle.BusNumber = vehicle.BusNumber;

                    if (string.IsNullOrWhiteSpace(vehicle.BusNumber))
                    {
                        Logger.Warning("Bus save blocked — BusNumber blank");
                        StatusMessage = "Bus Number is required";
                        return;
                    }

                    if (isNew)
                    {
                        var added = await _busService.AddBusAsync(vehicle);
                        Buses.Add(added);
                        SelectedBus = added;
                        Logger.Information(
                            "Added bus BusId={BusId} BusNumber={BusNumber}",
                            added.BusId,
                            added.BusNumber);
                        StatusMessage = $"Bus {added.BusNumber} added successfully";
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

                        var index = Buses.ToList().FindIndex(v => v.BusId == vehicle.BusId);
                        if (index >= 0)
                        {
                            Buses[index] = vehicle;
                        }

                        Logger.Information(
                            "Updated bus BusId={BusId} BusNumber={BusNumber}",
                            vehicle.BusId,
                            vehicle.BusNumber);
                        StatusMessage = $"Bus {vehicle.BusNumber} updated successfully";
                    }

                    ApplyFilters();
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "Error saving bus BusId={BusId} BusNumber={BusNumber}",
                        vehicle.BusId,
                        vehicle.BusNumber);
                    StatusMessage = $"Error saving bus: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        /// <summary>
        /// Update selected bus
        /// </summary>
        private async Task UpdateBusFromFormAsync()
        {
            await SaveBusAsync(); // Delegate to save method
        }

        /// <summary>
        /// Delete selected bus — hard-delete when Restrict FKs are clear; otherwise soft-retire.
        /// </summary>
        private async Task DeleteBusAsync()
        {
            if (SelectedBus is null)
            {
                return;
            }

            var vehicle = SelectedBus;
            var busId = vehicle.BusId;
            var busNumber = vehicle.BusNumber;

            using (LogContext.PushProperty("Operation", "DeleteBus"))
            using (LogContext.PushProperty("BusId", busId))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = $"Removing vehicle {busNumber}...";
                    Logger.Information(
                        "Deleting bus BusId={BusId} BusNumber={BusNumber}",
                        busId,
                        busNumber);

                    if (busId <= 0)
                    {
                        Buses.Remove(vehicle);
                        SelectedBus = null;
                        ApplyFilters();
                        Logger.Information("Removed unsaved bus draft from list");
                        StatusMessage = "Draft bus removed";
                        return;
                    }

                    var ok = await _busService.DeleteBusAsync(busId);
                    if (!ok)
                    {
                        Logger.Warning("DeleteBusAsync returned false for BusId={BusId}", busId);
                        StatusMessage = "Bus could not be removed";
                        UserToast.Error(
                            $"Could not remove bus {busNumber} (not found or blocked).",
                            "Bus remove failed");
                        return;
                    }

                    // Hard delete removes the row; soft-retire leaves Status=Retired when FKs remain.
                    var stillPresent = await _busService.GetBusByIdAsync(busId);
                    if (stillPresent is null)
                    {
                        Buses.Remove(vehicle);
                        SelectedBus = null;
                        ApplyFilters();
                        StatusMessage = $"Bus {busNumber} deleted";
                        UserToast.Success($"Deleted bus {busNumber}.", "Bus deleted");
                        Logger.Information("Hard-deleted bus BusId={BusId}", busId);
                        return;
                    }

                    vehicle.Status = stillPresent.Status;
                    ApplyFilters();
                    StatusMessage =
                        $"Bus {busNumber} retired — still referenced by routes or fuel/maintenance history";
                    UserToast.Warning(
                        $"Bus {busNumber} is assigned to a route or has fuel/maintenance history.\n" +
                        "It was retired (not hard-deleted). Unassign on Route Management if you need a hard delete later.",
                        "Bus retired");
                    Logger.Information("Soft-retired bus BusId={BusId} Status={Status}", busId, stillPresent.Status);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error deleting bus BusId={BusId}", busId);
                    StatusMessage = "Error removing bus — see toast for details";
                    UserToast.Error(
                        $"Could not remove bus {busNumber}.\nUse Route Management to unassign first if it is still on a route.",
                        "Bus remove failed");
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
            if (SelectedBus?.BusId == 0)
            {
                Logger.Information("Cancelled unsaved bus draft");
                SelectedBus = null;
            }

            StatusMessage = "Edit cancelled";
        }

        /// <summary>
        /// Search buses by text
        /// </summary>
        private async Task SearchBusesAsync()
        {
            ApplyFilters(); // Just apply filters, no need for separate search
        }

        /// <summary>
        /// Refresh the bus list
        /// </summary>
        private async Task RefreshAsync()
        {
            SearchText = string.Empty;
            SelectedStatusFilter = "All Status";
            await LoadBusesAsync();
        }

        /// <summary>
        /// Check if a bus can be edited
        /// </summary>
        private bool CanEditBus()
        {
            return SelectedBus != null && !IsBusy;
        }

        /// <summary>
        /// Check if a bus can be updated
        /// </summary>
        private bool CanUpdateBus()
        {
            return SelectedBus != null &&
                SelectedBus.BusId > 0 &&
                      !IsBusy;
        }

        /// <summary>
        /// Check if a bus can be saved
        /// </summary>
        private bool CanSaveBus()
        {
            return SelectedBus != null &&
                   !string.IsNullOrWhiteSpace(SelectedBus.BusNumber) &&
                   SelectedBus.SeatingCapacity > 0 &&
                   SelectedBus.Year > 0 &&
                   !IsBusy;
        }

        /// <summary>
        /// Check if a bus can be deleted
        /// </summary>
        private bool CanDeleteBus()
        {
            return SelectedBus != null &&
                SelectedBus.BusId > 0 &&
                      !IsBusy;
        }

        /// <summary>
        /// Property change notification for selected bus
        /// </summary>
        partial void OnSelectedBusChanged(BusBuddy.Core.Models.Bus? value)
        {
            // Unsubscribe from previous selection changes
            if (_lastSelectedBus is not null)
            {
                _lastSelectedBus.PropertyChanged -= SelectedBus_PropertyChanged;
            }

            // Subscribe to new selection changes
            if (value is not null)
            {
                value.PropertyChanged += SelectedBus_PropertyChanged;
            }

            _lastSelectedBus = value;

            // Refresh command states
            RefreshCommandStates();
        }

        private void SelectedBus_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When any field of the selected bus changes (e.g., BusNumber),
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
        /// Get bus fleet summary for dashboard
        /// </summary>
        public BusFleetSummary GetFleetSummary()
        {
            return new BusFleetSummary
            {
                TotalVehicles = Buses.Count,
                ActiveVehicles = Buses.Count(v => v.Status == "Active"),
                InactiveVehicles = Buses.Count(v => v.Status != "Active"),
                VehiclesInService = Buses.Count(v => v.Status == "InService"),
                VehiclesInMaintenance = Buses.Count(v => v.Status == "Maintenance"),
                VehiclesOutOfService = Buses.Count(v => v.Status == "OutOfService"),
                AverageCapacity = Buses.Any() ? (int)Buses.Average(v => v.SeatingCapacity) : 0
            };
        }
    }

    /// <summary>
    /// Summary data for bus fleet dashboard
    /// </summary>
    public class BusFleetSummary
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
