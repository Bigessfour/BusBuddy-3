using System;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Route;
using Serilog;
using Syncfusion.SfSkinManager;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services;

namespace BusBuddy.WPF.Views.Route
{
    /// <summary>
    /// Interaction logic for RouteAssignmentView.xaml
    /// MVP-ready route assignment interface with drag-drop functionality
    /// Supports comprehensive route building workflow with Syncfusion integration
    /// </summary>
    public partial class RouteAssignmentView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<RouteAssignmentView>();

    public RouteAssignmentView()
        {
            Logger.Debug("RouteAssignmentView constructor starting");
            try
            {
                Logger.Debug("Initializing RouteAssignmentView XAML components");
                InitializeComponent();

                Logger.Debug("Setting DataContext to RouteAssignmentViewModel");
                RouteAssignmentViewModel viewModel;
                try
                {
                    var sp = App.ServiceProvider;
                    if (sp != null)
                    {
                        var routeService = sp.GetService<IRouteService>();
                        viewModel = new RouteAssignmentViewModel(routeService);
                    }
                    else
                    {
                        viewModel = new RouteAssignmentViewModel();
                    }
                }
                catch
                {
                    viewModel = new RouteAssignmentViewModel();
                }
                DataContext = viewModel;

                Logger.Information("RouteAssignmentView initialized successfully with MVP route building interface");
                Loaded += OnLoaded;
                Unloaded += OnUnloaded;
                Logger.Debug("RouteAssignmentView constructor completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize RouteAssignmentView - Critical MVP component failure");
                throw; // Re-throw to maintain application stability
            }
        }

        /// <summary>
        /// Overload allowing caller to provide a pre-selected route (used when invoked from RouteManagementView)
        /// </summary>
        /// <param name="preselectedRoute">Route to preselect in assignment UI</param>
        public RouteAssignmentView(BusBuddy.Core.Models.Route preselectedRoute) : this()
        {
            try
            {
                if (DataContext is RouteAssignmentViewModel vm)
                {
                    // Replace DataContext with one that has preselected route so initial load can pick it
                    var sp = App.ServiceProvider;
                    IRouteService? routeService = null;
                    try { routeService = sp?.GetService<IRouteService>(); } catch { }
                    DataContext = new RouteAssignmentViewModel(routeService, preselectedRoute);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply preselected route in RouteAssignmentView");
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                // Apply theme to containing Window for consistent styling
                try
                {
                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        // Prefer centralized Syncfusion theme application to ensure consistency for modal dialogs
                        BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(window);
                    }
                }
                catch
                {
                    // Fallback to direct theme application if utility path fails
                    try
                    {
                        var window = Window.GetWindow(this);
                        if (window != null)
                        {
                            SfSkinManager.ApplyThemeAsDefaultStyle = true;
                            using var dark = new Theme("FluentDark");
                            SfSkinManager.SetTheme(window, dark);
                        }
                    }
                    catch
                    {
                        try
                        {
                            var window = Window.GetWindow(this);
                            if (window != null)
                            {
                                using var light = new Theme("FluentLight");
                                SfSkinManager.SetTheme(window, light);
                            }
                        }
                        catch { }
                    }
                }

                Logger.Information("Loaded {ViewName} with theme resource {ResourceKey}", GetType().Name, "BusBuddy.Brush.Primary");
            }
            catch { }
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Best-effort cleanup — UserControl doesn't own the theme; avoid disposing Window
        }
    }
}
