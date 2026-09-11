using System;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using BusBuddy.WPF.ViewModels.Fuel;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Serilog;

namespace BusBuddy.WPF.Views.Fuel
{
#pragma warning disable CA1001 // ViewModel disposed in OnClosed; Window lifetime owns the VM
    public partial class FuelReconciliationDialog : ChromelessWindow
#pragma warning restore CA1001
    {
        private static readonly ILogger Logger = Log.ForContext<FuelReconciliationDialog>();
        private readonly FuelReconciliationViewModel _viewModel;

        public FuelReconciliationDialog(
            IFuelService fuelService,
            IBusService busService,
            IFuelLocationCatalog? locationCatalog = null,
            IUserSettingsService? settings = null)
        {
            InitializeComponent();
            SyncfusionThemeManager.ApplyTheme(this);

            _viewModel = new FuelReconciliationViewModel(fuelService, busService, locationCatalog, settings);
            _viewModel.CloseRequested += OnCloseRequested;
            DataContext = _viewModel;
        }

        private void OnCloseRequested(object? sender, EventArgs e)
        {
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException)
            {
                // Not shown as a dialog
            }

            Close();
        }

        private void FuelReconciliationDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            NumpadInputHelper.HandlePreviewKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.Dispose();
            try
            {
                SfSkinManager.Dispose(this);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "SfSkinManager dispose failed for FuelReconciliationDialog");
            }

            base.OnClosed(e);
        }
    }
}
