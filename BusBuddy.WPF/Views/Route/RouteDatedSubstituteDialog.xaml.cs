using System.Windows;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Route;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteDatedSubstituteDialog : Window
{
    public RouteDatedSubstituteDialog(RouteDatedSubstituteViewModel viewModel)
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
        if (DataContext is RouteDatedSubstituteViewModel vm)
        {
            try
            {
                DialogResult = vm.DialogResult;
            }
            catch (InvalidOperationException)
            {
                // Not shown via ShowDialog
            }
        }

        Close();
    }
}
