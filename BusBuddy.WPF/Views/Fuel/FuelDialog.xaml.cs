using System;
using System.Linq;
using System.Windows;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Fuel;
using Serilog;
using Syncfusion.SfSkinManager;
using CoreModels = BusBuddy.Core.Models;

namespace BusBuddy.WPF.Views.Fuel
{
    /// <summary>
    /// Fuel add/edit chrome. Logic lives in <see cref="FuelDialogViewModel"/>.
    /// </summary>
    public partial class FuelDialog : Window
    {
        private static readonly ILogger Logger = Log.ForContext<FuelDialog>();
        private readonly FuelDialogViewModel _viewModel;

        public FuelDialog(
            CoreModels.Fuel fuel,
            IBusService busService,
            IFuelLocationCatalog? locationCatalog = null,
            IFuelService? fuelService = null)
        {
            InitializeComponent();
            SyncfusionThemeManager.ApplyTheme(this);

            _viewModel = new FuelDialogViewModel(fuel, busService, locationCatalog, fuelService);
            _viewModel.CloseRequested += OnCloseRequested;
            DataContext = _viewModel;

            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        }

        /// <summary>True when Save accepted a location not already in the catalog list.</summary>
        public bool RememberedNewLocation => _viewModel.RememberedNewLocation;

        private void OnCloseRequested(object? sender, bool saved)
        {
            try
            {
                DialogResult = saved;
            }
            catch (InvalidOperationException)
            {
                // Not shown via ShowDialog
            }

            Close();
        }

        private void NumericField_LostFocus(object sender, RoutedEventArgs e) =>
            _viewModel.CommitNumericFormatting();

        private void FuelLocationCombo_LostFocus(object sender, RoutedEventArgs e) =>
            _viewModel.SyncLocationFromComboText(FuelLocationCombo.Text);

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // ComboBoxAdv Text binding may not flush until LostFocus — sync before validate/save.
            // Always invoke Save: ViewModel toasts when invalid (CanExecute alone would swallow the click).
            _viewModel.SyncLocationFromComboText(FuelLocationCombo.Text);
            _viewModel.SaveCommand.Execute(null);
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            try
            {
                SfSkinManager.Dispose(this);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "SfSkinManager dispose failed for FuelDialog");
            }

            base.OnClosed(e);
        }
    }
}
