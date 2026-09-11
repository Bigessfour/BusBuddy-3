using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Commands;
using BusBuddy.WPF.Utilities;
using Serilog;
using Serilog.Context;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Controls.Input;
using CoreModels = BusBuddy.Core.Models;

namespace BusBuddy.WPF.Views.Fuel
{
    /// <summary>
    /// Fuel add/edit dialog. Numeric amounts use <see cref="SfTextBoxExt"/> (no DoubleTextBox masks)
    /// so clerks can type freely with the numpad; values round on LostFocus / Save.
    /// </summary>
    public partial class FuelDialog : Window, INotifyPropertyChanged
    {
        private static readonly ILogger Logger = Log.ForContext<FuelDialog>();
        private static readonly CultureInfo ParseCulture = CultureInfo.CurrentCulture;

        private readonly IBusService _busService;
        private readonly IFuelService? _fuelService;
        private readonly IFuelLocationCatalog? _locationCatalog;
        private CoreModels.Fuel _fuel;
        private CoreModels.Bus? _selectedBus;
        private bool _isValid;
        private double _mpg;
        private bool _isUpdatingCost;
        private bool _totalEditedByUser;
        private int? _previousOdometer;
        private string _fuelLocationText = string.Empty;
        private string _gallonsText = string.Empty;
        private string _pricePerGallonText = string.Empty;
        private string _totalCostText = string.Empty;
        private string _odometerText = string.Empty;
        private string _locationError = string.Empty;
        private string _busError = string.Empty;
        private string _gallonsError = string.Empty;
        private string _priceError = string.Empty;
        private string _odometerError = string.Empty;

        public ObservableCollection<CoreModels.Bus> AvailableBuses { get; } = new();
        public ObservableCollection<string> FuelLocations { get; } = new();
        public ObservableCollection<string> FuelTypes { get; } = new() { "Gasoline", "Diesel" };

        public string DialogTitle { get; private set; }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public string LocationError
        {
            get => _locationError;
            private set
            {
                if (SetError(ref _locationError, value))
                {
                    OnPropertyChanged(nameof(HasLocationError));
                }
            }
        }

        public string BusError
        {
            get => _busError;
            private set
            {
                if (SetError(ref _busError, value))
                {
                    OnPropertyChanged(nameof(HasBusError));
                }
            }
        }

        public string GallonsError
        {
            get => _gallonsError;
            private set
            {
                if (SetError(ref _gallonsError, value))
                {
                    OnPropertyChanged(nameof(HasGallonsError));
                }
            }
        }

        public string PriceError
        {
            get => _priceError;
            private set
            {
                if (SetError(ref _priceError, value))
                {
                    OnPropertyChanged(nameof(HasPriceError));
                }
            }
        }

        public string OdometerError
        {
            get => _odometerError;
            private set
            {
                if (SetError(ref _odometerError, value))
                {
                    OnPropertyChanged(nameof(HasOdometerError));
                }
            }
        }

        public bool HasLocationError => !string.IsNullOrEmpty(LocationError);
        public bool HasBusError => !string.IsNullOrEmpty(BusError);
        public bool HasGallonsError => !string.IsNullOrEmpty(GallonsError);
        public bool HasPriceError => !string.IsNullOrEmpty(PriceError);
        public bool HasOdometerError => !string.IsNullOrEmpty(OdometerError);

        public string FuelLocationText
        {
            get => _fuelLocationText;
            set
            {
                var next = value?.Trim() ?? string.Empty;
                if (next.Length > FuelConstraints.MaxLocationLength)
                {
                    next = next[..FuelConstraints.MaxLocationLength];
                }

                if (string.Equals(_fuelLocationText, next, StringComparison.Ordinal))
                {
                    return;
                }

                _fuelLocationText = next;
                Fuel.FuelLocation = next;
                OnPropertyChanged();
                ValidateForm();
            }
        }

        public string LocationHint =>
            "Editable vendor list — type this year’s fuel bid. Names persist for later fuelings.";

        public string OdometerHint =>
            _previousOdometer.HasValue
                ? $"Last recorded odometer for this bus: {_previousOdometer:N0}."
                : "Enter current odometer. Trip MPG needs a prior fill-up for this bus.";

        public string MpgDisplay => MPG > 0 ? MPG.ToString("N1") : "—";

        public string MpgHint =>
            MPG > 0
                ? "Estimated from this odometer minus the previous fill-up for the selected bus."
                : "Trip MPG appears after you enter gallons and an odometer higher than the last fill-up.";

        /// <summary>Free-typed gallons (no input mask). Parsed into Fuel.Gallons.</summary>
        public string GallonsText
        {
            get => _gallonsText;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_gallonsText, next, StringComparison.Ordinal))
                {
                    return;
                }

                _gallonsText = next;
                OnPropertyChanged();
                ApplyParsedGallons(commitFormatting: false);
            }
        }

        public string PricePerGallonText
        {
            get => _pricePerGallonText;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_pricePerGallonText, next, StringComparison.Ordinal))
                {
                    return;
                }

                _pricePerGallonText = next;
                OnPropertyChanged();
                ApplyParsedPrice(commitFormatting: false);
            }
        }

        public string TotalCostText
        {
            get => _totalCostText;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_totalCostText, next, StringComparison.Ordinal))
                {
                    return;
                }

                _totalCostText = next;
                OnPropertyChanged();
                if (_isUpdatingCost)
                {
                    return;
                }

                _totalEditedByUser = true;
                ApplyParsedTotal(commitFormatting: false);
            }
        }

        public string OdometerText
        {
            get => _odometerText;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_odometerText, next, StringComparison.Ordinal))
                {
                    return;
                }

                _odometerText = next;
                OnPropertyChanged();
                ApplyParsedOdometer(commitFormatting: false);
            }
        }

        public CoreModels.Fuel Fuel
        {
            get => _fuel;
            set
            {
                _fuel = value;
                _fuelLocationText = value.FuelLocation?.Trim() ?? string.Empty;
                SyncTextsFromFuel();
                OnPropertyChanged();
                OnPropertyChanged(nameof(FuelLocationText));
                RecalculateTripMpg();
                ValidateForm();
            }
        }

        public CoreModels.Bus? SelectedBus
        {
            get => _selectedBus;
            set
            {
                if (ReferenceEquals(_selectedBus, value))
                {
                    return;
                }

                _selectedBus = value;
                if (_selectedBus != null)
                {
                    Fuel.VehicleFueledId = _selectedBus.BusId;
                }

                OnPropertyChanged();
                ValidateForm();
                _ = LoadPreviousOdometerAsync();
            }
        }

        public bool IsValid
        {
            get => _isValid;
            set
            {
                if (_isValid == value)
                {
                    return;
                }

                _isValid = value;
                OnPropertyChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public double MPG
        {
            get => _mpg;
            set
            {
                if (Math.Abs(_mpg - value) < 0.0001)
                {
                    return;
                }

                _mpg = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MpgDisplay));
                OnPropertyChanged(nameof(MpgHint));
            }
        }

        public bool RememberedNewLocation { get; private set; }

        public FuelDialog(
            CoreModels.Fuel fuel,
            IBusService busService,
            IFuelLocationCatalog? locationCatalog = null,
            IFuelService? fuelService = null)
        {
            _fuel = fuel ?? throw new ArgumentNullException(nameof(fuel));
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));
            _locationCatalog = locationCatalog;
            _fuelService = fuelService;
            _fuelLocationText = fuel.FuelLocation?.Trim() ?? string.Empty;

            DialogTitle = fuel.FuelId == 0 ? "Add Fuel Record" : "Edit Fuel Record";

            SaveCommand = new RelayCommand(_ => Save(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());

            InitializeComponent();
            SyncfusionThemeManager.ApplyTheme(this);
            DataContext = this;

            if (fuel.FuelId == 0)
            {
                fuel.FuelDate = DateTime.Now;
                if (string.IsNullOrWhiteSpace(fuel.FuelType))
                {
                    fuel.FuelType = "Diesel";
                }

                // Blank amount fields for new records — do not prefill 0 (forces clerk to clear mask).
                fuel.Gallons = null;
                fuel.PricePerGallon = null;
                fuel.TotalCost = null;
            }

            SyncTextsFromFuel();

            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;

            ValidateForm();
            _ = LoadBusesAsync();
            _ = LoadFuelLocationsAsync();
        }

        private void SyncTextsFromFuel()
        {
            _gallonsText = FormatOptionalDecimal(Fuel.Gallons, "0.###");
            _pricePerGallonText = FormatOptionalDecimal(Fuel.PricePerGallon, "0.###");
            _totalCostText = FormatOptionalDecimal(Fuel.TotalCost, "0.##");
            _odometerText = Fuel.VehicleOdometerReading > 0
                ? Fuel.VehicleOdometerReading.ToString(ParseCulture)
                : string.Empty;
            OnPropertyChanged(nameof(GallonsText));
            OnPropertyChanged(nameof(PricePerGallonText));
            OnPropertyChanged(nameof(TotalCostText));
            OnPropertyChanged(nameof(OdometerText));
        }

        private static string FormatOptionalDecimal(decimal? value, string format) =>
            value.HasValue && value.Value != 0
                ? value.Value.ToString(format, ParseCulture)
                : value.HasValue
                    ? value.Value.ToString(ParseCulture)
                    : string.Empty;

        private void FuelDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // SfTextBoxExt uses a normal TextBox — leave numpad to WPF; helper only patches numeric editors elsewhere.
            NumpadInputHelper.HandlePreviewKeyDown(e);
        }

        private void NumericField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyParsedGallons(commitFormatting: true);
            ApplyParsedPrice(commitFormatting: true);
            ApplyParsedTotal(commitFormatting: true);
            ApplyParsedOdometer(commitFormatting: true);
            if (!_totalEditedByUser)
            {
                SyncTotalFromUnitPrice(forceTextRefresh: true);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                SfSkinManager.Dispose(this);
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing SfSkinManager for {ViewName}: {Error}", GetType().Name, ex.Message);
            }

            base.OnClosed(e);
        }

        private async System.Threading.Tasks.Task LoadFuelLocationsAsync()
        {
            try
            {
                FuelLocations.Clear();
                if (_locationCatalog != null)
                {
                    foreach (var location in await _locationCatalog.GetLocationsAsync())
                    {
                        FuelLocations.Add(location);
                    }
                }
                else
                {
                    foreach (var seed in FuelLocationCatalog.SeedDefaults)
                    {
                        FuelLocations.Add(seed);
                    }
                }

                if (!string.IsNullOrWhiteSpace(FuelLocationText) &&
                    !FuelLocations.Any(l => string.Equals(l, FuelLocationText, StringComparison.OrdinalIgnoreCase)))
                {
                    FuelLocations.Add(FuelLocationText);
                }

                if (string.IsNullOrWhiteSpace(FuelLocationText) && FuelLocations.Count > 0)
                {
                    FuelLocationText = FuelLocations[0];
                }

                ValidateForm();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading fuel locations for dialog");
                if (FuelLocations.Count == 0)
                {
                    foreach (var seed in FuelLocationCatalog.SeedDefaults)
                    {
                        FuelLocations.Add(seed);
                    }
                }

                UserToast.Warning("Could not load saved fuel locations — using defaults.", "Fuel locations");
            }
        }

        private async System.Threading.Tasks.Task LoadBusesAsync()
        {
            try
            {
                AvailableBuses.Clear();
                foreach (var bus in await _busService.GetAllBusesAsync())
                {
                    AvailableBuses.Add(bus);
                }

                _selectedBus = AvailableBuses.FirstOrDefault(b => b.BusId == Fuel.VehicleFueledId);
                OnPropertyChanged(nameof(SelectedBus));
                ValidateForm();
                await LoadPreviousOdometerAsync();
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("ViewType", "FuelDialog"))
                {
                    Logger.Error(ex, "Error loading buses for fuel dialog");
                }

                UserToast.Error($"Error loading buses: {ex.Message}", "Fuel dialog");
            }
        }

        private async System.Threading.Tasks.Task LoadPreviousOdometerAsync()
        {
            _previousOdometer = null;
            OnPropertyChanged(nameof(OdometerHint));

            if (_fuelService == null || SelectedBus == null)
            {
                RecalculateTripMpg();
                return;
            }

            try
            {
                var history = await _fuelService.GetFuelRecordsByVehicleAsync(SelectedBus.BusId);
                var prior = history
                    .Where(f => f.FuelId != Fuel.FuelId)
                    .OrderByDescending(f => f.FuelDate)
                    .ThenByDescending(f => f.FuelId)
                    .FirstOrDefault();

                _previousOdometer = prior?.VehicleOdometerReading;
                OnPropertyChanged(nameof(OdometerHint));
                RecalculateTripMpg();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Could not load prior odometer for bus {BusId}", SelectedBus.BusId);
                RecalculateTripMpg();
            }
        }

        private void ApplyParsedGallons(bool commitFormatting)
        {
            if (IsIntermediateNumber(_gallonsText))
            {
                ValidateForm();
                return;
            }

            if (TryParseDecimal(_gallonsText, out var gallons))
            {
                Fuel.Gallons = Math.Round(gallons, 3);
                if (commitFormatting)
                {
                    _gallonsText = Fuel.Gallons.Value.ToString("0.###", ParseCulture);
                    OnPropertyChanged(nameof(GallonsText));
                }

                if (!_totalEditedByUser)
                {
                    SyncTotalFromUnitPrice(forceTextRefresh: commitFormatting);
                }

                RecalculateTripMpg();
            }
            else if (string.IsNullOrWhiteSpace(_gallonsText))
            {
                Fuel.Gallons = null;
            }

            ValidateForm();
        }

        private void ApplyParsedPrice(bool commitFormatting)
        {
            if (IsIntermediateNumber(_pricePerGallonText))
            {
                ValidateForm();
                return;
            }

            if (TryParseDecimal(_pricePerGallonText, out var price))
            {
                Fuel.PricePerGallon = Math.Round(price, 3);
                if (commitFormatting)
                {
                    _pricePerGallonText = Fuel.PricePerGallon.Value.ToString("0.###", ParseCulture);
                    OnPropertyChanged(nameof(PricePerGallonText));
                }

                if (!_totalEditedByUser)
                {
                    SyncTotalFromUnitPrice(forceTextRefresh: commitFormatting);
                }
            }
            else if (string.IsNullOrWhiteSpace(_pricePerGallonText))
            {
                Fuel.PricePerGallon = null;
            }

            ValidateForm();
        }

        private void ApplyParsedTotal(bool commitFormatting)
        {
            if (_isUpdatingCost || IsIntermediateNumber(_totalCostText))
            {
                ValidateForm();
                return;
            }

            if (TryParseDecimal(_totalCostText, out var total))
            {
                Fuel.TotalCost = Math.Round(total, 2);
                if (commitFormatting)
                {
                    _totalCostText = Fuel.TotalCost.Value.ToString("0.##", ParseCulture);
                    OnPropertyChanged(nameof(TotalCostText));
                }

                if (Fuel.Gallons.HasValue && Fuel.Gallons.Value > 0)
                {
                    Fuel.PricePerGallon = Math.Round(Fuel.TotalCost.Value / Fuel.Gallons.Value, 3);
                    _pricePerGallonText = Fuel.PricePerGallon.Value.ToString("0.###", ParseCulture);
                    OnPropertyChanged(nameof(PricePerGallonText));
                }
            }
            else if (string.IsNullOrWhiteSpace(_totalCostText))
            {
                Fuel.TotalCost = null;
            }

            ValidateForm();
        }

        private void ApplyParsedOdometer(bool commitFormatting)
        {
            if (IsIntermediateNumber(_odometerText))
            {
                return;
            }

            if (int.TryParse(_odometerText, NumberStyles.Integer, ParseCulture, out var odo) && odo >= 0)
            {
                Fuel.VehicleOdometerReading = odo;
                if (commitFormatting)
                {
                    _odometerText = odo.ToString(ParseCulture);
                    OnPropertyChanged(nameof(OdometerText));
                }

                RecalculateTripMpg();
            }
            else if (string.IsNullOrWhiteSpace(_odometerText))
            {
                Fuel.VehicleOdometerReading = 0;
                RecalculateTripMpg();
            }
        }

        private void SyncTotalFromUnitPrice(bool forceTextRefresh)
        {
            if (_isUpdatingCost || _totalEditedByUser)
            {
                return;
            }

            if (!Fuel.Gallons.HasValue || Fuel.Gallons.Value <= 0 ||
                !Fuel.PricePerGallon.HasValue || Fuel.PricePerGallon.Value <= 0)
            {
                return;
            }

            var total = Math.Round(Fuel.Gallons.Value * Fuel.PricePerGallon.Value, 2);
            _isUpdatingCost = true;
            try
            {
                Fuel.TotalCost = total;
                _totalCostText = total.ToString("0.##", ParseCulture);
                OnPropertyChanged(nameof(TotalCostText));
                if (forceTextRefresh && TotalCostBox != null &&
                    !string.Equals(TotalCostBox.Text, _totalCostText, StringComparison.Ordinal))
                {
                    TotalCostBox.Text = _totalCostText;
                }
            }
            finally
            {
                _isUpdatingCost = false;
            }
        }

        private void ValidateForm()
        {
            LocationError = string.IsNullOrWhiteSpace(FuelLocationText)
                ? "Location is required."
                : FuelLocationText.Length > FuelConstraints.MaxLocationLength
                    ? $"Max {FuelConstraints.MaxLocationLength} characters."
                    : string.Empty;

            BusError = SelectedBus == null ? "Select a bus." : string.Empty;

            if (string.IsNullOrWhiteSpace(_gallonsText))
            {
                GallonsError = "Gallons are required.";
            }
            else if (!IsIntermediateNumber(_gallonsText) && !TryParseDecimal(_gallonsText, out _))
            {
                GallonsError = "Enter a valid number (e.g. 102 or 102.5).";
            }
            else if (Fuel.Gallons.HasValue && Fuel.Gallons.Value <= 0)
            {
                GallonsError = "Gallons must be greater than zero.";
            }
            else
            {
                GallonsError = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_pricePerGallonText)
                && !IsIntermediateNumber(_pricePerGallonText)
                && !TryParseDecimal(_pricePerGallonText, out _))
            {
                PriceError = "Enter a valid price (e.g. 6.99).";
            }
            else if (Fuel.PricePerGallon.HasValue && Fuel.PricePerGallon.Value < 0)
            {
                PriceError = "Price cannot be negative.";
            }
            else
            {
                PriceError = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_odometerText)
                && !IsIntermediateNumber(_odometerText)
                && !int.TryParse(_odometerText, NumberStyles.Integer, ParseCulture, out _))
            {
                OdometerError = "Enter a whole number for odometer.";
            }
            else
            {
                OdometerError = string.Empty;
            }

            IsValid =
                SelectedBus != null &&
                !string.IsNullOrWhiteSpace(FuelLocationText) &&
                !string.IsNullOrWhiteSpace(Fuel.FuelType) &&
                Fuel.Gallons.HasValue &&
                Fuel.Gallons.Value > 0 &&
                string.IsNullOrEmpty(GallonsError) &&
                string.IsNullOrEmpty(PriceError) &&
                string.IsNullOrEmpty(OdometerError);
        }

        private bool SetError(ref string field, string value, [CallerMemberName] string? propertyName = null)
        {
            value ??= string.Empty;
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void RecalculateTripMpg()
        {
            if (_previousOdometer.HasValue &&
                Fuel.Gallons.HasValue &&
                Fuel.Gallons.Value > 0 &&
                Fuel.VehicleOdometerReading > _previousOdometer.Value)
            {
                var miles = Fuel.VehicleOdometerReading - _previousOdometer.Value;
                var mpg = miles / (double)Fuel.Gallons.Value;
                MPG = mpg is > 0 and <= FuelTrendAggregator.MaxPlausibleBusMpg
                    ? Math.Round(mpg, 1)
                    : 0;
            }
            else
            {
                MPG = 0;
            }
        }

        /// <summary>Allow "6." / "" while typing — do not wipe or reformat mid-keystroke.</summary>
        private static bool IsIntermediateNumber(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var t = text.Trim();
            return t is "." or "-"
                   || t.EndsWith(".", StringComparison.Ordinal)
                   || t.EndsWith(ParseCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
        }

        private static bool TryParseDecimal(string? text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return decimal.TryParse(text, NumberStyles.Number, ParseCulture, out value)
                   || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private void Save()
        {
            var typed = FuelLocationCombo?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(typed))
            {
                FuelLocationText = typed;
            }

            ApplyParsedGallons(commitFormatting: true);
            ApplyParsedPrice(commitFormatting: true);
            ApplyParsedTotal(commitFormatting: true);
            ApplyParsedOdometer(commitFormatting: true);
            if (!_totalEditedByUser)
            {
                SyncTotalFromUnitPrice(forceTextRefresh: true);
            }

            Fuel.FuelLocation = FuelLocationText;
            ValidateForm();
            if (!IsValid)
            {
                UserToast.Warning(
                    "Cannot save yet — fix the highlighted fields (bus, location, type, gallons).",
                    "Save blocked");
                return;
            }

            try
            {
                FuelRecordValidator.ValidateForPersist(Fuel);
            }
            catch (ArgumentException ex)
            {
                UserToast.Warning(ex.Message, "Save blocked");
                return;
            }

            RememberedNewLocation = !FuelLocations.Any(l =>
                string.Equals(l, Fuel.FuelLocation, StringComparison.OrdinalIgnoreCase));

            if (RememberedNewLocation)
            {
                FuelLocations.Add(Fuel.FuelLocation);
            }

            DialogResult = true;
            Close();
        }

        private void Cancel()
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
