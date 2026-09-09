using System;
using System.Windows.Controls;
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
    }
}
