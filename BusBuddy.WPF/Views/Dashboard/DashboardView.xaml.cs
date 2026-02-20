using System;
using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Dashboard;
using Serilog;

namespace BusBuddy.WPF.Views.Dashboard
{
    /// <summary>
    /// Modern interaction logic for DashboardView.xaml
    /// Simplified with proper MVVM patterns and dependency injection
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<DashboardView>();

        public DashboardView()
        {
            Logger.Debug("DashboardView constructor starting");
            try
            {
                InitializeComponent();
                Logger.Information("DashboardView initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize DashboardView");
                throw;
            }
        }

        /// <summary>
        /// Handle view loaded event
        /// </summary>
        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Debug("DashboardView loaded");
            // Data loading is handled by the ViewModel
        }

        /// <summary>
        /// Handle view unloaded event
        /// </summary>
        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            Logger.Debug("DashboardView unloaded");
            // Cleanup is handled by the ViewModel
        }
    }
}
