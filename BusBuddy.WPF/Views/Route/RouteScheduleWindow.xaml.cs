using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Route;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteScheduleWindow : ChromelessWindow
{
    public RouteScheduleWindow(RouteScheduleViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }
}
