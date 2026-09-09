using System;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Analytics;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Analytics
{
    public partial class AnalyticsDashboardView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<AnalyticsDashboardView>();

        public AnalyticsDashboardView()
        {
            InitializeComponent();
            try
            {
                if (DataContext == null && App.ServiceProvider != null)
                {
                    DataContext = App.ServiceProvider.GetRequiredService<AnalyticsDashboardViewModel>();
                    Logger.Information("AnalyticsDashboardView DataContext set");
                }
                else if (DataContext == null)
                {
                    Logger.Warning("AnalyticsDashboardView opened with null DataContext and no ServiceProvider");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AnalyticsDashboardView failed to resolve AnalyticsDashboardViewModel");
            }
        }
    }
}
