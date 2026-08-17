using System;
using System.Windows.Controls;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Driver;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Driver;

public partial class DriverScheduleView : UserControl
{
    private static readonly ILogger Logger = Log.ForContext<DriverScheduleView>();

    public DriverScheduleView()
    {
        InitializeComponent();

        try
        {
            var scheduleService = App.ServiceProvider.GetRequiredService<IScheduleService>();
            DataContext = new DriverScheduleViewModel(scheduleService);
            Logger.Information("DriverScheduleView DataContext set");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize DriverScheduleViewModel");
        }
    }
}
