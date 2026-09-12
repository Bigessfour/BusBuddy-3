using System.Windows;
using Serilog;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteStopEditDialog : Window
{
    private static readonly ILogger Logger = Log.ForContext<RouteStopEditDialog>();

    public RouteStopEditDialog(string? stopName = null, string? stopAddress = null)
    {
        InitializeComponent();
        StopNameBox.Text = stopName ?? string.Empty;
        StopAddressBox.AddressText = stopAddress ?? string.Empty;
        StopNameBox.Focus();
        Logger.Information("RouteStopEditDialog opened StopName={StopName}", StopNameBox.Text);
    }

    public string StopName => StopNameBox.Text.Trim();
    public string StopAddress => (StopAddressBox.AddressText ?? string.Empty).Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StopName))
        {
            Logger.Warning("Route stop save blocked — empty stop name");
            MessageBox.Show("Stop name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Logger.Information("Route stop saved Name={StopName} Address={Address}", StopName, StopAddress);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Logger.Information("Route stop edit cancelled");
        DialogResult = false;
        Close();
    }
}
