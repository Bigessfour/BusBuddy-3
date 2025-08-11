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
    private static readonly ILogger LogTheme = Log.ForContext<RouteAssignmentView>();

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

                LogTheme.Information("Loaded {ViewName} with theme resource {ResourceKey}", GetType().Name, "BusBuddy.Brush.Primary");
            }
            catch { }
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Best-effort cleanup — UserControl doesn't own the theme; avoid disposing Window
        }
    }
}
