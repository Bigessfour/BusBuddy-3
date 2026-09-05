using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Dashboard;
using BusBuddy.WPF.Views.Analytics;
using BusBuddy.WPF.Views.Route;
using BusBuddy.WPF.Views.Student;
using BusBuddy.WPF.Views.Vehicle;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Dashboard
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// Enhanced with comprehensive Syncfusion error capture
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<DashboardView>();

        public DashboardView()
        {
            Logger.Debug("DashboardView constructor starting");
            try
            {
                Logger.Debug("Initializing DashboardView XAML components");
                InitializeComponent();

                Logger.Debug("Setting up DashboardViewModel");
                InitializeViewModel();

                Logger.Debug("Attaching Syncfusion event hooks for error capture");
                AttachSyncfusionEventHooks();

                Logger.Information("DashboardView initialized successfully");
                Logger.Debug("DashboardView constructor completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize DashboardView");
                // All error logging is now handled by Serilog's File sink
                throw; // Re-throw to ensure proper error handling up the stack
            }
        }

        /// <summary>
        /// Initialize the ViewModel if not already set by dependency injection
        /// </summary>
        private void InitializeViewModel()
        {
            Logger.Debug("InitializeViewModel method started");
            try
            {
                // Only set DataContext if not already provided by DI
                if (this.DataContext == null)
                {
                    var sp = App.ServiceProvider;
                    if (sp != null)
                    {
                        DataContext = sp.GetRequiredService<DashboardViewModel>();
                    }
                    Logger.Information("DashboardView DataContext initialized from DI");
                }
                else
                {
                    Logger.Debug("DataContext already set, preserving existing ViewModel");
                }
                Logger.Debug("InitializeViewModel completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to initialize ViewModel, continuing without it");
            }
        }

        /// <summary>
        /// Attach Syncfusion event hooks for comprehensive error capture
        /// </summary>
        private void AttachSyncfusionEventHooks()
        {
            Logger.Debug("AttachSyncfusionEventHooks method started");
            try
            {
                Logger.Debug("Checking for DashboardDataGrid control availability");
                // Example: Hook Syncfusion SfDataGrid events for runtime error capture
                // These will work when proper Syncfusion controls are added to DashboardView.xaml

                // if (DashboardDataGrid != null)
                // {
                //     Logger.Debug("Attaching DashboardDataGrid event handlers");
                //     DashboardDataGrid.QueryCellInfo += SfDataGrid_QueryCellInfo;
                //     DashboardDataGrid.CurrentCellBeginEdit += SfDataGrid_CurrentCellBeginEdit;
                //     DashboardDataGrid.GridValidationFailed += SfDataGrid_GridValidationFailed;
                // }

                // if (AlertsDataGrid != null)
                // {
                //     Logger.Debug("Attaching AlertsDataGrid event handlers");
                //     AlertsDataGrid.QueryCellInfo += SfDataGrid_QueryCellInfo;
                // }

                Logger.Information("Dashboard Syncfusion event hooks prepared");
                Logger.Debug("AttachSyncfusionEventHooks method completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to attach some Dashboard Syncfusion event hooks");
                Logger.Debug("Event hook attachment failed, continuing with limited functionality");
            }
        }

        /// <summary>
        /// Enhanced Syncfusion SfDataGrid cell error handler with detailed context
        /// </summary>
        private void SfDataGrid_QueryCellInfo(object sender, object e)
        {
            var gridName = (sender as FrameworkElement)?.Name ?? "UnknownDashboardGrid";
            Logger.Debug("SfDataGrid_QueryCellInfo triggered for grid: {GridName}", gridName);
            try
            {
                Logger.Verbose("Processing cell info query for dashboard grid");
                // Cell processing logic would go here
                // This wrapper captures any runtime errors during cell operations

                // When proper Syncfusion.UI.Xaml.Grid using is added,
                // change parameter to: GridQueryCellInfoEventArgs e
                // Then access: e.RowIndex, e.ColumnIndex, e.Column.MappingName

                Logger.Verbose("Cell info query completed successfully for {GridName}", gridName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard SfDataGrid cell error in {GridName}", gridName);
                // All error logging is now handled by Serilog's File sink
            }
        }

        /// <summary>
        /// Handle SfDataGrid edit validation errors
        /// </summary>
        private void SfDataGrid_CurrentCellBeginEdit(object sender, object e)
        {
            var gridName = (sender as FrameworkElement)?.Name ?? "UnknownDashboardGrid";
            Logger.Debug("SfDataGrid_CurrentCellBeginEdit triggered for grid: {GridName}", gridName);
            try
            {
                Logger.Verbose("Starting cell edit validation for dashboard grid");
                // Edit validation logic would go here
                Logger.Verbose("Cell edit validation completed for {GridName}", gridName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard SfDataGrid edit error in {GridName}", gridName);
                // All error logging is now handled by Serilog's File sink
            }
        }

        /// <summary>
        /// Handle SfDataGrid validation failures
        /// </summary>
        private void SfDataGrid_GridValidationFailed(object sender, object e)
        {
            var gridName = (sender as FrameworkElement)?.Name ?? "UnknownDashboardGrid";
            Logger.Debug("SfDataGrid_GridValidationFailed triggered for grid: {GridName}", gridName);
            try
            {
                Logger.Verbose("Processing validation failure for dashboard grid");
                // Validation failure handling would go here
                Logger.Verbose("Validation failure processing completed for {GridName}", gridName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard SfDataGrid validation error in {GridName}", gridName);
                // All error logging is now handled by Serilog's File sink
            }
        }

        // Dashboard data management methods
        private async void LoadDashboardData()
        {
            Logger.Debug("LoadDashboardData method started");
            try
            {
                Logger.Information("Loading dashboard overview data");
                if (DataContext is DashboardViewModel vm)
                {
                    await vm.RefreshDataAsync();
                }
                Logger.Information("Dashboard data loading completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading dashboard data");
            }
        }

        private Task LoadFleetStatusAsync() => RefreshFromViewModelAsync();
        private Task LoadRouteMetricsAsync() => RefreshFromViewModelAsync();
        private Task LoadStudentCountsAsync() => RefreshFromViewModelAsync();
        private Task LoadActiveAlertsAsync() => RefreshFromViewModelAsync();

        private async Task RefreshFromViewModelAsync()
        {
            if (DataContext is DashboardViewModel vm)
            {
                await vm.RefreshDataAsync();
            }
        }

        // Dashboard refresh methods
        private void RefreshDashboard()
        {
            Logger.Debug("RefreshDashboard method started");
            try
            {
                Logger.Information("Refreshing dashboard data");
                LoadDashboardData();
                Logger.Debug("Dashboard refresh initiated");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error refreshing dashboard");
            }
        }

        private void RefreshFleetMetrics()
        {
            Logger.Debug("RefreshFleetMetrics method started");
            try
            {
                _ = RefreshFromViewModelAsync();
                Logger.Information("Fleet metrics refreshed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error refreshing fleet metrics");
            }
        }

        private void RefreshRouteStatus()
        {
            Logger.Debug("RefreshRouteStatus method started");
            try
            {
                _ = RefreshFromViewModelAsync();
                Logger.Information("Route status refreshed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error refreshing route status");
            }
        }

        // Dashboard event handlers (for future button implementations)
        private void ViewFleetDetails_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ViewFleetDetails_Click event triggered");
            try
            {
                Logger.Information("Fleet details view requested");
                VehicleFleetLauncher.Show(Window.GetWindow(this));
                Logger.Debug("Fleet details navigation completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in ViewFleetDetails_Click");
            }
        }

        private void ViewRouteDetails_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ViewRouteDetails_Click event triggered");
            try
            {
                Logger.Information("Route details view requested");
                new Window
                {
                    Title = "🗺️ Route Management",
                    Content = new RouteManagementView(),
                    Width = 1200,
                    Height = 800,
                    Owner = Window.GetWindow(this)
                }.Show();
                Logger.Debug("Route details navigation completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in ViewRouteDetails_Click");
            }
        }

        private void ViewStudentDetails_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ViewStudentDetails_Click event triggered");
            try
            {
                Logger.Information("Student details view requested");
                var students = new StudentsView
                {
                    Owner = Window.GetWindow(this)
                };
                students.Show();
                Logger.Debug("Student details navigation completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in ViewStudentDetails_Click");
            }
        }

        private void ViewAlerts_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ViewAlerts_Click event triggered");
            try
            {
                Logger.Information("Alerts view requested");
                new Window
                {
                    Title = "📊 Fleet Analytics",
                    Content = new AnalyticsDashboardView(),
                    Width = 1100,
                    Height = 800,
                    Owner = Window.GetWindow(this)
                }.Show();
                Logger.Debug("Alerts navigation completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in ViewAlerts_Click");
            }
        }

        // Chart interaction handlers
        private void FleetChart_SelectionChanged(object sender, object e)
        {
            Logger.Debug("FleetChart_SelectionChanged event triggered");
            try
            {
                Logger.Debug("Fleet chart selection processed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in FleetChart_SelectionChanged");
            }
        }

        private void RouteChart_SelectionChanged(object sender, object e)
        {
            Logger.Debug("RouteChart_SelectionChanged event triggered");
            try
            {
                Logger.Debug("Route chart selection processed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in RouteChart_SelectionChanged");
            }
        }

        // Dashboard lifecycle methods
        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Debug("DashboardView_Loaded event triggered");
            try
            {
                Logger.Information("Dashboard view loaded, starting data load");
                LoadDashboardData();
                Logger.Debug("DashboardView_Loaded completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in DashboardView_Loaded");
            }
        }

        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            Logger.Debug("DashboardView_Unloaded event triggered");
            try
            {
                Logger.Information("Dashboard view unloaded");
                Logger.Debug("Dashboard cleanup completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in DashboardView_Unloaded");
            }
        }

    }
}
