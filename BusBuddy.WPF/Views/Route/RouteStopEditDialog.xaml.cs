using System.Windows;
using BusBuddy.WPF.Controls;
using Serilog;

namespace BusBuddy.WPF.Views.Route;

public partial class RouteStopEditDialog : Window
{
    private static readonly ILogger Logger = Log.ForContext<RouteStopEditDialog>();

    public RouteStopEditDialog(
        string? stopName = null,
        string? stopAddress = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        InitializeComponent();
        StopNameBox.Text = stopName ?? string.Empty;
        StopAddressBox.AddressText = stopAddress ?? string.Empty;
        Latitude = latitude;
        Longitude = longitude;
        UpdateCoordinateStatus();
        StopNameBox.Focus();
        Logger.Information("RouteStopEditDialog opened StopName={StopName}", StopNameBox.Text);
    }

    public string StopName => StopNameBox.Text.Trim();

    public string StopAddress => (StopAddressBox.AddressText ?? string.Empty).Trim();

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    private void StopAddressBox_AddressApplied(object sender, PlaceAddressAppliedEventArgs e)
    {
        Latitude = e.Applied.Latitude.HasValue ? (decimal)e.Applied.Latitude.Value : null;
        Longitude = e.Applied.Longitude.HasValue ? (decimal)e.Applied.Longitude.Value : null;
        UpdateCoordinateStatus();
        Logger.Information(
            "Route stop address applied HasCoordinates={HasCoordinates}",
            Latitude.HasValue && Longitude.HasValue);
    }

    private void UpdateCoordinateStatus()
    {
        CoordinateStatusText.Text = Latitude.HasValue && Longitude.HasValue
            ? $"Located at {Latitude.Value:0.#####}, {Longitude.Value:0.#####}"
            : "Pick the address from the suggestion list so the stop can be mapped.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StopName))
        {
            Logger.Warning("Route stop save blocked — empty stop name");
            MessageBox.Show("Stop name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Routes only plot validated coordinates, so a stop without them cannot be saved.
        if (!Latitude.HasValue || !Longitude.HasValue)
        {
            Logger.Warning("Route stop save blocked — no validated coordinates for {StopName}", StopName);
            MessageBox.Show(
                "Choose the address from the suggestion list so the stop gets map coordinates.\n\n"
                    + "A stop without validated coordinates cannot be added to a route.",
                "Address not validated",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Logger.Information(
            "Route stop saved Name={StopName} Address={Address} Lat={Latitude} Lon={Longitude}",
            StopName,
            StopAddress,
            Latitude,
            Longitude);
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
