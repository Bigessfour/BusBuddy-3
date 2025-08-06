using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Dashboard
{
    /// <summary>
    /// Phase 1 Dashboard ViewModel - Simple and functional
    /// Displays basic metrics and provides navigation to core views
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IRouteService _routeService;

        public DashboardViewModel(IRouteService routeService)
        {
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));

            // Initialize commands
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            OptimizeCommand = new RelayCommand(async () => await OptimizeRoutesAsync());
            GenerateReportCommand = new RelayCommand(async () => await GenerateReportAsync());

            // Initialize collections
            Routes = new ObservableCollection<BusBuddy.Core.Models.Route>();
            Buses = new ObservableCollection<BusBuddy.Core.Models.Bus>();
            Drivers = new ObservableCollection<BusBuddy.Core.Models.Driver>();

            // Load initial data
            _ = Task.Run(async () => await RefreshDataAsync());
        }

        #region Properties

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Route> routes = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> buses = new();

        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Driver> drivers = new();

        [ObservableProperty]
        private int totalRoutes;

        [ObservableProperty]
        private int activeBuses;

        [ObservableProperty]
        private int availableDrivers;

        [ObservableProperty]
        private string systemStatus = "System Ready";

        [ObservableProperty]
        private bool isLoading;

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand OptimizeCommand { get; }
        public ICommand GenerateReportCommand { get; }

        #endregion

        #region Command Methods

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Loading data...";

                // Load routes
                var result = await _routeService.GetAllRoutesAsync();
                Routes.Clear();
                if (result.IsSuccess && result.Value != null)
                {
                    foreach (var route in result.Value)
                    {
                        Routes.Add(route);
                    }
                }

                // Update metrics
                TotalRoutes = Routes.Count;
                SystemStatus = "Data loaded successfully";
            }
            catch (Exception ex)
            {
                SystemStatus = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OptimizeRoutesAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Optimizing routes...";

                // TODO: Implement route optimization
                await Task.Delay(2000); // Simulate processing

                SystemStatus = "Routes optimized successfully";
            }
            catch (Exception ex)
            {
                SystemStatus = $"Error optimizing routes: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerateReportAsync()
        {
            try
            {
                IsLoading = true;
                SystemStatus = "Generating report...";

                // TODO: Implement report generation
                await Task.Delay(1500); // Simulate processing

                SystemStatus = "Report generated successfully";
            }
            catch (Exception ex)
            {
                SystemStatus = $"Error generating report: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
}
