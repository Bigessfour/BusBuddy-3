using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Input;
using Serilog;
using System.Linq;
using BusBuddy.Core.Models;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Phase 2+ Enhanced Drivers ViewModel — Advanced MVVM with commands, search, and state management
    /// Implements mentor recommendations: Commands, Loading States, Search, Selection
    /// </summary>
    public class DriversViewModel : BaseViewModel
    {
        private readonly BusBuddyDbContext _context;

        // 🔍 Phase 2+ Enhancement: Search functionality
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterDrivers();
                }
            }
        }

        // 👤 Phase 2+ Enhancement: Selected driver for details/editing
        private BusBuddy.Core.Models.Driver? _selectedDriver;
        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (SetProperty(ref _selectedDriver, value))
                {
                    Logger.Information("📌 Driver selected: {DriverName} (ID: {DriverId})",
                        value?.DriverName ?? "None", value?.DriverId ?? 0);
                }
            }
        }

        // 📊 Collections for data binding
        private ObservableCollection<BusBuddy.Core.Models.Driver> _drivers = new();
        public ObservableCollection<BusBuddy.Core.Models.Driver> Drivers
        {
            get => _drivers;
            set => SetProperty(ref _drivers, value);
        }
        public ObservableCollection<BusBuddy.Core.Models.Driver> FilteredDrivers { get; } = new();

        // 🎯 Phase 2+ Enhancement: Command Pattern Implementation
        public ICommand LoadDriversCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand EditDriverCommand { get; }
        public ICommand DeleteDriverCommand { get; }

        public DriversViewModel(BusBuddyDbContext context)
        {
            _context = context;

            // Initialize commands
            LoadDriversCommand = new RelayCommand(async () => await LoadDriversAsync(), () => !IsLoading);
            RefreshCommand = new RelayCommand(async () => await RefreshDriversAsync(), () => !IsLoading);
            ClearSearchCommand = new RelayCommand(() => ClearSearch(), () => !string.IsNullOrEmpty(SearchText));
            EditDriverCommand = new RelayCommand(() => EditDriver(), () => SelectedDriver != null && !IsLoading);
            DeleteDriverCommand = new RelayCommand(async () => await DeleteDriverAsync(), () => SelectedDriver != null && !IsLoading);

            Logger.Information("🚌 Phase 2+ DriversViewModel initialized with advanced command patterns and search");
        }

        // 📊 Phase 2+ Enhancement: Enhanced data loading with state management
        public async Task LoadDriversAsync()
        {
            await LoadDataAsync(async () =>
            {
                Logger.Information("📊 Loading drivers from Phase 1 seeded data...");
                var drivers = await _context.Drivers.ToListAsync();

                Drivers.Clear();
                foreach (var driver in drivers)
                {
                    Drivers.Add(driver);
                }

                FilterDrivers(); // Apply current search filter
                Logger.Information("✅ Loaded {DriverCount} drivers successfully", drivers.Count);
            });
        }

        // 🔄 Phase 2+ Enhancement: Refresh command implementation
        private async Task RefreshDriversAsync()
        {
            await LoadDriversAsync();
            Logger.Information("🔄 Driver data refreshed successfully");
        }

        // 🔍 Phase 2+ Enhancement: Real-time search filtering
        private void FilterDrivers()
        {
            FilteredDrivers.Clear();

            var filteredItems = string.IsNullOrEmpty(SearchText)
                ? Drivers
                : Drivers.Where(d =>
                    d.DriverName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    d.DriverEmail?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                    d.DriverPhone?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true);

            foreach (var driver in filteredItems)
            {
                FilteredDrivers.Add(driver);
            }

            Logger.Information("🔍 Search filter applied: '{SearchText}' - {FilteredCount}/{TotalCount} drivers",
                SearchText, FilteredDrivers.Count, Drivers.Count);
        }

        // 🧹 Phase 2+ Enhancement: Clear search functionality
        private void ClearSearch()
        {
            SearchText = string.Empty;
            Logger.Information("🧹 Search cleared - showing all drivers");
        }

        // ✏️ Phase 2+ Enhancement: Edit driver (placeholder for future dialog)
        private void EditDriver()
        {
            if (SelectedDriver == null)
            {
                return;
            }


            Logger.Information("✏️ Edit driver requested: {DriverName} (ID: {DriverId})",
                SelectedDriver.DriverName, SelectedDriver.DriverId);

            // TODO: Phase 3 - Implement edit dialog
            MessageBox.Show($"Edit driver: {SelectedDriver.DriverName}\n(Edit dialog coming in Phase 3)",
                "Phase 2+ Feature", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 🗑️ Phase 2+ Enhancement: Delete driver with confirmation
        private async Task DeleteDriverAsync()
        {
            if (SelectedDriver == null)
            {
                return;
            }


            var result = MessageBox.Show(
                $"Are you sure you want to delete driver '{SelectedDriver.DriverName}'?\n\nThis action cannot be undone.",
                "Confirm Delete Driver",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await LoadDataAsync(async () =>
                {
                    Logger.Information("🗑️ Deleting driver: {DriverName} (ID: {DriverId})",
                        SelectedDriver.DriverName, SelectedDriver.DriverId);

                    _context.Drivers.Remove(SelectedDriver);
                    await _context.SaveChangesAsync();

                    Drivers.Remove(SelectedDriver);
                    FilterDrivers();

                    SelectedDriver = null;
                    Logger.Information("✅ Driver deleted successfully");
                });
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                // TODO: Load drivers data
                await Task.Delay(100); // Placeholder
            }
            catch (Exception ex)
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Added overload: LoadDataAsync(Func<Task> action) for proper async command wrapping
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
    }
}
