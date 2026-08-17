using System.Windows;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteStopEditDialog : Window
{
    public RouteStopEditDialog(string? stopName = null, string? stopAddress = null)
    {
        InitializeComponent();
        StopNameBox.Text = stopName ?? string.Empty;
        StopAddressBox.Text = stopAddress ?? string.Empty;
        StopNameBox.Focus();
    }

    public string StopName => StopNameBox.Text.Trim();
    public string StopAddress => StopAddressBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StopName))
        {
            MessageBox.Show("Stop name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
