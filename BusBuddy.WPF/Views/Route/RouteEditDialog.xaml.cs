using System.Windows;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Route;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteEditDialog : UserControl
{
    public RouteEditDialog()
    {
        InitializeComponent();
        if (DataContext is null)
        {
            DataContext = new RouteEditDialogViewModel();
        }

        if (DataContext is RouteEditDialogViewModel vm)
        {
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, bool? result)
    {
        var window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        window.DialogResult = result;
        window.Close();
    }
}
