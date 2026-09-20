using System;
using System.Windows;
using BusBuddy.WPF.Controls;
using BusBuddy.WPF.ViewModels.Route;
using BusBuddy.WPF.Utilities;
using Serilog;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteStopEditDialog : Window
{
    private static readonly ILogger Logger = Log.ForContext<RouteStopEditDialog>();

    public RouteStopEditDialog(RouteStopEditDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        // SetTheme after InitializeComponent; assign DataContext after SetTheme
        // so Fluent restyle cannot leave the window and PlacesAddressBox unbound.
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = ViewModel;
        StopAddressBox.DataContext = ViewModel;
        ViewModel.CloseRequested += OnCloseRequested;
        Logger.Information("RouteStopEditDialog opened StopName={StopName}", ViewModel.StopName);
    }

    public RouteStopEditDialogViewModel ViewModel { get; }

    public string StopName => ViewModel.StopName.Trim();

    public string StopAddress => ViewModel.StopAddress.Trim();

    public decimal? Latitude => ViewModel.Latitude;

    public decimal? Longitude => ViewModel.Longitude;

    private void StopAddressBox_AddressApplied(object sender, PlaceAddressAppliedEventArgs e)
    {
        ViewModel.ApplyAddress(e.Applied);
    }

    private void OnCloseRequested(bool accepted)
    {
        try
        {
            DialogResult = accepted;
        }
        catch (InvalidOperationException)
        {
            // Not shown as a dialog.
        }

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        try
        {
            SfSkinManager.Dispose(this);
        }
        catch
        {
            // Theme dispose is best-effort.
        }

        base.OnClosed(e);
    }
}
