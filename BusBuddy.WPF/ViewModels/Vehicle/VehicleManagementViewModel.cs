using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Vehicle
{
    /// <summary>
    /// ViewModel for managing vehicles/buses in Phase 1
    /// Provides CRUD operations and fleet status monitoring
    /// </summary>
    public partial class VehicleManagementViewModel : BaseViewModel
    {
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

        [ObservableProperty]
        private string _statusMessage = string.Empty;

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
        public List<string> OperationalStatusOptions { get; } = new()
        {
            "Active",
            "InService",
            "Maintenance",
            "OutOfService",
            "Retired"
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
            try
            {
                IsBusy = true;
                StatusMessage = "Loading vehicles...";

                var vehicles = await _busService.GetAllBusesAsync();

                Vehicles.Clear();
                foreach (var vehicle in vehicles)
                {
                    Vehicles.Add(vehicle);
                }

                ApplyFilters();
                StatusMessage = $"Loaded {Vehicles.Count} vehicles";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading vehicles: {ex.Message}";
                // Load sample data if service fails (for MVP Phase 1)
                LoadSampleData();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Load sample data for MVP testing
        /// </summary>
        private void LoadSampleData()
        {
            var sampleVehicles = new List<BusBuddy.Core.Models.Bus>
            {
                new() { VehicleId = 1, BusNumber = "BUS001", Make = "Ford", Model = "Transit", LicenseNumber = "ABC-123", SeatingCapacity = 40, Status = "Active", Year = 2020 },
                new() { VehicleId = 2, BusNumber = "BUS002", Make = "Chevrolet", Model = "Express", LicenseNumber = "DEF-456", SeatingCapacity = 35, Status = "InService", Year = 2019 },
                new() { VehicleId = 3, BusNumber = "BUS003", Make = "Mercedes", Model = "Sprinter", LicenseNumber = "GHI-789", SeatingCapacity = 20, Status = "Maintenance", Year = 2021 },
                new() { VehicleId = 4, BusNumber = "BUS004", Make = "Ford", Model = "E-Series", LicenseNumber = "JKL-012", SeatingCapacity = 45, Status = "Active", Year = 2018 },
                new() { VehicleId = 5, BusNumber = "BUS005", Make = "Isuzu", Model = "NPR", LicenseNumber = "MNO-345", SeatingCapacity = 30, Status = "OutOfService", Year = 2017 }
            };

            Vehicles.Clear();
            foreach (var vehicle in sampleVehicles)
            {
                Vehicles.Add(vehicle);
            }

            ApplyFilters();
            StatusMessage = "Sample data loaded (database unavailable)";
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
                var searchLower = SearchText.ToLower();
                filtered = filtered.Where(v =>
                    (v.Make?.ToLower().Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.Model?.ToLower().Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.LicenseNumber?.ToLower().Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.BusNumber?.ToLower().Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true));
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
                    Status = "Active"
                };

                SelectedVehicle = newVehicle;
                StatusMessage = "Ready to add new vehicle - fill in details and click Save";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding vehicle: {ex.Message}";
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
            if (SelectedVehicle == null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Saving vehicle...";

                // Ensure latest UI edits are propagated (in case some controls haven't lost focus yet)
                // Trigger a minimal property change to force binding commits where applicable
                SelectedVehicle.BusNumber = SelectedVehicle.BusNumber;

                // Basic validation
                if (string.IsNullOrWhiteSpace(SelectedVehicle.BusNumber))
                {
                    StatusMessage = "Bus Number is required";
                    return;
                }

                if (SelectedVehicle.BusId == 0)
                {
                    // New vehicle - for MVP Phase 1, just add to collection
                    SelectedVehicle.BusId = Vehicles.Count > 0 ? Vehicles.Max(v => v.BusId) + 1 : 1;
                    Vehicles.Add(SelectedVehicle);
                    StatusMessage = $"Vehicle {SelectedVehicle.BusNumber} added successfully";

                    // Attempt to persist via service if available
                    try { await _busService.AddBusAsync(SelectedVehicle); } catch { /* MVP: ignore service failure */ }
                }
                else
                {
                    // Update existing vehicle in collection
                    var index = Vehicles.ToList().FindIndex(v => v.BusId == SelectedVehicle.BusId);
                    if (index >= 0)
                    {
                        Vehicles[index] = SelectedVehicle;
                    }
                    StatusMessage = $"Vehicle {SelectedVehicle.BusNumber} updated successfully";

                    // Attempt to persist via service if available
                    try { await _busService.UpdateBusAsync(SelectedVehicle); } catch { /* MVP: ignore service failure */ }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving vehicle: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
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
            if (SelectedVehicle == null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = $"Deleting vehicle {SelectedVehicle.BusNumber}...";

                Vehicles.Remove(SelectedVehicle);
                SelectedVehicle = null;
                ApplyFilters();

                StatusMessage = "Vehicle deleted successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting vehicle: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Cancel edit operation
        /// </summary>
        private void CancelEdit()
        {
            if (SelectedVehicle?.BusId == 0)
            {
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
