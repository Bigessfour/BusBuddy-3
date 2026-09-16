using System.Windows;
using BusBuddy.WPF.Controls;
using BusBuddy.WPF.ViewModels.Activity;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Activity;

/// <summary>
/// Clerk trip editor bound to Core TripEvent. Does not write ActivitySchedule.
/// </summary>
public partial class TripEventEditDialog : Window
{
    public TripEventEditDialogViewModel ViewModel { get; }

    public TripEventEditDialog(TripEventEditDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadAvailableDataAsync().ConfigureAwait(true);
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        try { SfSkinManager.Dispose(this); } catch { /* theme dispose is best-effort */ }
        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, bool accepted)
    {
        DialogResult = accepted;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Destination_Applied(object sender, PlaceAddressAppliedEventArgs e)
    {
        ViewModel.ApplyDestinationAddress(e.Applied);
    }
}
