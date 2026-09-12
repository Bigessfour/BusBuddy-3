using System;
using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.ViewModels.Activity;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Activity
{
    public partial class ActivityTimelineView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<ActivityTimelineView>();

        public ActivityTimelineView()
        {
            InitializeComponent();
            try
            {
                if (DataContext == null && App.ServiceProvider != null)
                {
                    DataContext = App.ServiceProvider.GetService<ActivityTimelineViewModel>()
                        ?? App.ServiceProvider.GetRequiredService<ActivityTimelineViewModel>();
                }

                if (DataContext is ActivityTimelineViewModel)
                {
                    Logger.Information("ActivityTimelineView DataContext set");
                }
                else
                {
                    Logger.Warning("ActivityTimelineView opened without ActivityTimelineViewModel");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ActivityTimelineView failed to resolve ActivityTimelineViewModel — IActivityLogService may be unregistered");
            }
        }

        private void TripBoardButton_Click(object sender, RoutedEventArgs e)
        {
            var click = UiProofLog.Label(sender, "Trip Board");
            Logger.Information("Trip board requested from activity timeline");
            try
            {
                var owner = Window.GetWindow(this);
                var window = new Window
                {
                    Title = "Trip Board",
                    Content = new ActivityManagementView(),
                    Width = 1200,
                    Height = 800,
                    Owner = owner,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.Show();
                UiProofLog.Write(Logger, click, "ActivityManagementView", "opened");
            }
            catch (Exception ex)
            {
                UiProofLog.Failed(Logger, ex, click, "ActivityManagementView");
                MessageBox.Show($"Error opening trip board: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
