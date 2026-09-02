using System;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Maps;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.Views.Student;

public partial class PickupStopForm : ChromelessWindow
{
    private static readonly ILogger Logger = Log.ForContext<PickupStopForm>();
    private readonly PickupStopFormViewModel _vm;

    public PickupStopForm(PickupStopFormViewModel viewModel)
    {
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        SyncfusionThemeManager.ApplyTheme(this);
        DataContext = _vm;

        StopTypeCombo.SelectedItem = _vm.SelectedStopType;
        StopLatBox.Value = _vm.LatitudeValue;
        StopLonBox.Value = _vm.LongitudeValue;

        _vm.RequestClose += (_, result) =>
        {
            try
            {
                DialogResult = result;
            }
            catch (InvalidOperationException)
            {
                // Not shown as dialog
            }

            Close();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PickupStopFormViewModel.LatitudeValue)
                or nameof(PickupStopFormViewModel.LongitudeValue))
            {
                if (Math.Abs((StopLatBox.Value ?? 0d) - _vm.LatitudeValue) > 0.0000001)
                {
                    StopLatBox.Value = _vm.LatitudeValue;
                }

                if (Math.Abs((StopLonBox.Value ?? 0d) - _vm.LongitudeValue) > 0.0000001)
                {
                    StopLonBox.Value = _vm.LongitudeValue;
                }
            }
        };

        Loaded += (_, _) => SaveStopButton.IsEnabled = true;
    }

    private async void SaveStopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PushFieldsToViewModel();
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
            Logger.Warning(ex, "Save pickup stop click failed");
            _vm.ValidationMessage = ex.Message;
        }
    }

    private void PushFieldsToViewModel()
    {
        _vm.Name = StopNameBox.Text?.Trim() ?? string.Empty;
        _vm.Address = StopAddressBox.Text?.Trim() ?? string.Empty;
        _vm.SelectedStopType = StopTypeCombo.SelectedItem as string ?? PickupStopTypes.Corner;
        _vm.Notes = StopNotesBox.Text?.Trim() ?? string.Empty;
        _vm.LatitudeValue = StopLatBox.Value ?? 0d;
        _vm.LongitudeValue = StopLonBox.Value ?? 0d;
        if (Math.Abs(_vm.LatitudeValue) > 0.0001 || Math.Abs(_vm.LongitudeValue) > 0.0001)
        {
            _vm.ApplyMapClick(_vm.LatitudeValue, _vm.LongitudeValue);
        }
    }

    private void PickupStopForm_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var isNumPadDigit = e.Key is >= Key.NumPad0 and <= Key.NumPad9;
        var isDecimal = e.Key is Key.Decimal or Key.OemPeriod;
        if (!isNumPadDigit && !isDecimal || Keyboard.FocusedElement is not SfTextBoxExt textExt)
        {
            return;
        }

        var insert = isNumPadDigit
            ? ((int)(e.Key - Key.NumPad0)).ToString()
            : ".";

        var start = textExt.SelectionStart;
        var len = textExt.SelectionLength;
        var current = textExt.Text ?? string.Empty;
        if (len > 0 && start >= 0 && start + len <= current.Length)
        {
            current = current.Remove(start, len);
        }

        if (start < 0 || start > current.Length)
        {
            start = current.Length;
        }

        textExt.Text = current.Insert(start, insert);
        textExt.SelectionStart = start + insert.Length;
        textExt.SelectionLength = 0;
        e.Handled = true;
    }

    private void StopPickMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (StopPickLayer is null)
            {
                return;
            }

            var pos = e.GetPosition(StopPickMap);
            var geo = StopPickLayer.GetLatLonFromPoint(pos);
            var lon = geo.X;
            var lat = geo.Y;
            if (lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                return;
            }

            _vm.ApplyMapClick(lat, lon);
            StopLatBox.Value = _vm.LatitudeValue;
            StopLonBox.Value = _vm.LongitudeValue;
            StopPickLayer.Center = new Point(lat, lon);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Pickup stop map pick failed");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SfSkinManager.Dispose(this);
        base.OnClosed(e);
    }
}
