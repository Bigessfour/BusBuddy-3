using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using Serilog;
using Serilog.Context;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using CoreModels = BusBuddy.Core.Models;

namespace BusBuddy.WPF.Views.Fuel
{
    /// <summary>
    /// Fuel add/edit dialog — Syncfusion numeric fields + numpad support via <see cref="NumpadInputHelper"/>.
    /// </summary>
    public partial class FuelDialog : Window, INotifyPropertyChanged
    {
        private static readonly ILogger Logger = Log.ForContext<FuelDialog>();

        private readonly IBusService _busService;
        private CoreModels.Fuel _fuel;
        private CoreModels.Bus? _selectedBus;
        private bool _isValid;
        private double _mpg;
        private bool _isUpdatingCost;

        public ObservableCollection<CoreModels.Bus> AvailableBuses { get; } = new();
        public ObservableCollection<string> FuelLocations { get; } = new()
        {
            "Key Pumps", "School District Pump", "BP", "Shell", "Chevron", "Exxon", "Mobil", "Marathon", "Sunoco", "Other"
        };
        public ObservableCollection<string> FuelTypes { get; } = new()
        {
            "Gasoline", "Diesel", "Biodiesel", "CNG", "Propane"
        };

        public string DialogTitle { get; private set; }

        public CoreModels.Fuel Fuel
        {
            get => _fuel;
            set
            {
                _fuel = value;
                OnPropertyChanged();
                CalculateMPG();
                ValidateForm();
            }
        }

        public CoreModels.Bus? SelectedBus
        {
            get => _selectedBus;
            set
            {
                _selectedBus = value;
                if (_selectedBus != null)
                {
                    Fuel.VehicleFueledId = _selectedBus.BusId;
                }
                OnPropertyChanged();
                ValidateForm();
            }
        }

        public bool IsValid
        {
            get => _isValid;
            set
            {
                _isValid = value;
                OnPropertyChanged();
            }
        }

        public double MPG
        {
            get => _mpg;
            set
            {
                _mpg = value;
                OnPropertyChanged();
            }
        }

        public FuelDialog(CoreModels.Fuel fuel, IBusService busService)
        {
            _fuel = fuel ?? throw new ArgumentNullException(nameof(fuel));
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));

            DialogTitle = fuel.FuelId == 0 ? "Add Fuel Record" : "Edit Fuel Record";

            InitializeComponent();
            SyncfusionThemeManager.ApplyTheme(this);
            DataContext = this;

            if (fuel.FuelId == 0)
            {
                fuel.FuelDate = DateTime.Now;
                fuel.FuelType = "Diesel";
                fuel.FuelLocation = "Key Pumps";
            }

            ValidateForm();
            _ = LoadBusesAsync();
        }

        private void FuelDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ensure numpad works even if class handlers miss this dialog's DoubleTextBox/IntegerTextBox.
            NumpadInputHelper.HandlePreviewKeyDown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                SfSkinManager.Dispose(this);
                Logger.Information("SfSkinManager resources disposed for {ViewName}", GetType().Name);
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing SfSkinManager for {ViewName}: {Error}", GetType().Name, ex.Message);
            }
            base.OnClosed(e);
        }

        private async System.Threading.Tasks.Task LoadBusesAsync()
        {
            try
            {
                AvailableBuses.Clear();
                var buses = await _busService.GetAllBusesAsync();
                foreach (var bus in buses)
                {
                    AvailableBuses.Add(bus);
                }

                _selectedBus = AvailableBuses.FirstOrDefault(b => b.BusId == Fuel.VehicleFueledId);
                OnPropertyChanged(nameof(SelectedBus));
                ValidateForm();

                using (LogContext.PushProperty("ViewType", "FuelDialog"))
                using (LogContext.PushProperty("OperationType", "LoadBuses"))
                {
                    Logger.Information("Loaded {BusCount} buses for fuel dialog", AvailableBuses.Count);
                }
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("ViewType", "FuelDialog"))
                using (LogContext.PushProperty("OperationType", "LoadBuses"))
                {
                    Logger.Error(ex, "Error loading buses for fuel dialog");
                }
                UserToast.Error($"Error loading buses: {ex.Message}", "Fuel dialog");
            }
        }

        private void ValidateForm()
        {
            IsValid =
                SelectedBus != null &&
                !string.IsNullOrWhiteSpace(Fuel.FuelLocation) &&
                !string.IsNullOrWhiteSpace(Fuel.FuelType) &&
                Fuel.Gallons.HasValue &&
                Fuel.Gallons.Value > 0;
        }

        private void CalculateMPG()
        {
            if (Fuel.Gallons.HasValue && Fuel.Gallons.Value > 0)
            {
                MPG = 0;

                if (!_isUpdatingCost && Fuel.PricePerGallon.HasValue && Fuel.PricePerGallon.Value > 0)
                {
                    _isUpdatingCost = true;
                    Fuel.TotalCost = Math.Round(Fuel.Gallons.Value * Fuel.PricePerGallon.Value, 2);
                    OnPropertyChanged(nameof(Fuel));
                    _isUpdatingCost = false;
                }
            }
            else
            {
                MPG = 0;
            }
        }

        private void NumericField_ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ValidateForm();
        }

        private void Gallons_ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CalculateMPG();
            ValidateForm();
        }

        private void PricePerGallon_ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (_isUpdatingCost)
            {
                return;
            }

            _isUpdatingCost = true;
            if (Fuel.Gallons.HasValue && Fuel.Gallons.Value > 0 && Fuel.PricePerGallon.HasValue)
            {
                Fuel.TotalCost = Math.Round(Fuel.Gallons.Value * Fuel.PricePerGallon.Value, 2);
                OnPropertyChanged(nameof(Fuel));
            }
            _isUpdatingCost = false;
            ValidateForm();
        }

        private void TotalCost_ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (_isUpdatingCost)
            {
                return;
            }

            _isUpdatingCost = true;
            if (Fuel.Gallons.HasValue && Fuel.Gallons.Value > 0 && Fuel.TotalCost.HasValue)
            {
                Fuel.PricePerGallon = Math.Round(Fuel.TotalCost.Value / Fuel.Gallons.Value, 3);
                OnPropertyChanged(nameof(Fuel));
            }
            _isUpdatingCost = false;
            ValidateForm();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateForm();
            if (!IsValid)
            {
                UserToast.Warning(
                    "Cannot save yet — choose a bus, location, fuel type, and enter gallons greater than zero.",
                    "Save blocked");
                return;
            }

            UserToast.Success(
                Fuel.FuelId == 0
                    ? $"Ready to save new fuel record ({Fuel.Gallons:N3} gal)."
                    : $"Ready to save changes to fuel record #{Fuel.FuelId}.",
                "Saving fuel record");
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
