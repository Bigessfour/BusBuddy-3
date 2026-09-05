using System;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Maps;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class SchoolDestinationForm : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<SchoolDestinationForm>();
    private readonly SchoolDestinationFormViewModel _vm;

    public SchoolDestinationForm(SchoolDestinationFormViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        // Seed controls from VM defaults — do not rely on Text DP bindings (they were not updating the VM).
        SchoolCityBox.Text = _vm.City;
        SchoolStateBox.Text = _vm.State;
        SchoolStartBox.Text = _vm.StartTimeText;
        SchoolDismissalBox.Text = _vm.DismissalTimeText;
        SchoolLatBox.Value = _vm.LatitudeValue;
        SchoolLonBox.Value = _vm.LongitudeValue;

        _vm.RequestClose += (_, result) =>
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
                // Not shown as dialog — still close
            }

            Close();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SchoolDestinationFormViewModel.LatitudeValue)
                or nameof(SchoolDestinationFormViewModel.LongitudeValue)
                or nameof(SchoolDestinationFormViewModel.HasMapPick))
            {
                var boxLat = SchoolLatBox.Value ?? 0d;
                var boxLon = SchoolLonBox.Value ?? 0d;
                if (Math.Abs(boxLat - _vm.LatitudeValue) > 0.0000001)
                {
                    SchoolLatBox.Value = _vm.LatitudeValue;
                }

                if (Math.Abs(boxLon - _vm.LongitudeValue) > 0.0000001)
                {
                    SchoolLonBox.Value = _vm.LongitudeValue;
                }
            }
        };

        Loaded += (_, _) => SaveSchoolButton.IsEnabled = true;
    }

    /// <summary>
    /// Copy control text into the VM, then run save. Syncfusion Text bindings were leaving the VM empty
    /// while the UI still showed typed characters.
    /// </summary>
    private async void SaveSchoolButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PushFieldsToViewModel();
            Logger.Information(
                "Save school UI flush Name='{Name}' Address='{Address}' City='{City}' State='{State}' Zip='{Zip}' Start='{Start}' Dismissal='{Dismissal}' Lat={Lat} Lon={Lon}",
                _vm.Name, _vm.Address, _vm.City, _vm.State, _vm.ZipCode, _vm.StartTimeText, _vm.DismissalTimeText,
                _vm.LatitudeValue, _vm.LongitudeValue);

            if (_vm.SaveCommand is IAsyncRelayCommand asyncCmd)
            {
                await asyncCmd.ExecuteAsync(null).ConfigureAwait(true);
            }
            else if (_vm.SaveCommand.CanExecute(null))
            {
                _vm.SaveCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Save school click failed");
            _vm.ValidationMessage = ex.Message;
        }
    }

    private void PushFieldsToViewModel()
    {
        _vm.Name = SchoolNameBox.Text?.Trim() ?? string.Empty;
        _vm.Address = SchoolAddressBox.Text?.Trim() ?? string.Empty;
        _vm.City = SchoolCityBox.Text?.Trim() ?? string.Empty;
        _vm.State = SchoolStateBox.Text?.Trim() ?? string.Empty;
        _vm.ZipCode = SchoolZipBox.Text?.Trim() ?? string.Empty;
        _vm.StartTimeText = string.IsNullOrWhiteSpace(SchoolStartBox.Text) ? "08:00" : SchoolStartBox.Text.Trim();
        _vm.DismissalTimeText = string.IsNullOrWhiteSpace(SchoolDismissalBox.Text) ? "15:30" : SchoolDismissalBox.Text.Trim();
        _vm.LatitudeValue = SchoolLatBox.Value ?? 0d;
        _vm.LongitudeValue = SchoolLonBox.Value ?? 0d;
        if (Math.Abs(_vm.LatitudeValue) > 0.0001 || Math.Abs(_vm.LongitudeValue) > 0.0001)
        {
            _vm.ApplyMapClick(_vm.LatitudeValue, _vm.LongitudeValue);
        }
    }

    private void SchoolAddressBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _ = _vm.RefreshAddressSuggestionsAsync(SchoolAddressBox.Text);
    }

    private async void SchoolAddressSuggestionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SchoolAddressSuggestionsList.SelectedItem is not PlaceAutocompleteSuggestion suggestion)
        {
            return;
        }

        try
        {
            if (await _vm.ApplyAddressSuggestionAsync(suggestion).ConfigureAwait(true))
            {
                SchoolAddressBox.Text = _vm.Address;
                SchoolCityBox.Text = _vm.City;
                SchoolStateBox.Text = _vm.State;
                SchoolZipBox.Text = _vm.ZipCode;
                SchoolLatBox.Value = _vm.LatitudeValue;
                SchoolLonBox.Value = _vm.LongitudeValue;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "School Places suggestion apply failed");
        }
        finally
        {
            SchoolAddressSuggestionsList.SelectedItem = null;
        }
    }

    private void SchoolForm_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        NumpadInputHelper.HandlePreviewKeyDown(e);
    }

    private void SchoolPickMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (SchoolPickLayer is null)
            {
                return;
            }

            var pos = e.GetPosition(SchoolPickMap);
            var geo = SchoolPickLayer.GetLatLonFromPoint(pos);
            var lon = geo.X;
            var lat = geo.Y;
            if (lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                Logger.Warning("Ignored out-of-range map click Lat={Lat} Lon={Lon}", lat, lon);
                return;
            }

            _vm.ApplyMapClick(lat, lon);
            SchoolLatBox.Value = _vm.LatitudeValue;
            SchoolLonBox.Value = _vm.LongitudeValue;
            SchoolPickLayer.Center = new Point(lat, lon);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "School map pick failed");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
