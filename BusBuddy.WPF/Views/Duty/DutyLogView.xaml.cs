using System;
using System.Windows.Controls;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Duty;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Views.Duty;

public partial class DutyLogView : UserControl
{
    private static readonly ILogger Logger = Log.ForContext<DutyLogView>();

    public DutyLogView()
    {
        InitializeComponent();

        try
        {
            if (App.ServiceProvider is null)
            {
                Logger.Warning("DutyLogView opened before ServiceProvider was ready");
                return;
            }

            var duty = App.ServiceProvider.GetRequiredService<IDutyEventService>();
            var buses = App.ServiceProvider.GetRequiredService<IBusService>();
            var drivers = App.ServiceProvider.GetRequiredService<IDriverService>();
            DataContext = new DutyLogViewModel(duty, buses, drivers);
            Logger.Information("DutyLogView DataContext set");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize DutyLogViewModel");
        }
    }
}
