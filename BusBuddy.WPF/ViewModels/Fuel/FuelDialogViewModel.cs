using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog.Context;
using FuelModel = BusBuddy.Core.Models.Fuel;
using BusModel = BusBuddy.Core.Models.Bus;

namespace BusBuddy.WPF.ViewModels.Fuel;

/// <summary>
/// Add/Edit fuel fill-up. Free-text amounts (no Syncfusion NumberDecimalDigits masks);
/// round/format on LostFocus / Save per Syncfusion guidance.
/// </summary>
public partial class FuelDialogViewModel : BaseViewModel
{
    private static readonly CultureInfo ParseCulture = CultureInfo.CurrentCulture;

    private readonly IBusService _busService;
    private readonly IFuelService? _fuelService;
    private readonly IFuelLocationCatalog? _locationCatalog;

    private FuelModel _fuel;
    private BusModel? _selectedBus;
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

    public ObservableCollection<BusModel> AvailableBuses { get; } = new();
    public ObservableCollection<string> FuelLocations { get; } = new();
    public ObservableCollection<string> FuelTypes { get; } = new(FuelRecordValidator.AllowedFuelTypes);

    public string DialogTitle { get; }

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public event EventHandler<bool>? CloseRequested;

    public FuelDialogViewModel(
        FuelModel fuel,
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

        SaveCommand = new RelayCommand(Save, () => IsValid);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));

        if (fuel.FuelId == 0)
        {
            fuel.FuelDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            if (string.IsNullOrWhiteSpace(fuel.FuelType))
            {
                fuel.FuelType = FuelRecordValidator.AllowedFuelTypes[1]; // Diesel default
            }

            fuel.Gallons = null;
            fuel.PricePerGallon = null;
            fuel.TotalCost = null;
        }

        SyncTextsFromFuel();
        ValidateForm();
        _ = LoadBusesAsync();
        _ = LoadFuelLocationsAsync();
    }

    public FuelModel Fuel
    {
        get => _fuel;
        private set => SetProperty(ref _fuel, value);
    }

    public bool RememberedNewLocation { get; private set; }

    public string LocationError
    {
        get => _locationError;
        private set
        {
            if (SetProperty(ref _locationError, value ?? string.Empty))
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
            if (SetProperty(ref _busError, value ?? string.Empty))
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
            if (SetProperty(ref _gallonsError, value ?? string.Empty))
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
            if (SetProperty(ref _priceError, value ?? string.Empty))
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
            if (SetProperty(ref _odometerError, value ?? string.Empty))
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

            if (!SetProperty(ref _fuelLocationText, next))
            {
                return;
            }

            Fuel.FuelLocation = next;
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

    public string GallonsText
    {
        get => _gallonsText;
        set
        {
            if (!SetProperty(ref _gallonsText, value ?? string.Empty))
            {
                return;
            }

            ApplyParsedDecimal(
                _gallonsText,
                decimalPlaces: 3,
                commitFormatting: false,
                onAssigned: v =>
                {
                    Fuel.Gallons = v;
                    if (!_totalEditedByUser)
                    {
                        SyncTotalFromUnitPrice();
                    }

                    RecalculateTripMpg();
                },
                onBlank: () => Fuel.Gallons = null,
                formatPropertyName: nameof(GallonsText),
                setFormatted: f => _gallonsText = f);
            ValidateForm();
        }
    }

    public string PricePerGallonText
    {
        get => _pricePerGallonText;
        set
        {
            if (!SetProperty(ref _pricePerGallonText, value ?? string.Empty))
            {
                return;
            }

            ApplyParsedDecimal(
                _pricePerGallonText,
                decimalPlaces: 3,
                commitFormatting: false,
                onAssigned: v =>
                {
                    Fuel.PricePerGallon = v;
                    if (!_totalEditedByUser)
                    {
                        SyncTotalFromUnitPrice();
                    }
                },
                onBlank: () => Fuel.PricePerGallon = null,
                formatPropertyName: nameof(PricePerGallonText),
                setFormatted: f => _pricePerGallonText = f);
            ValidateForm();
        }
    }

    public string TotalCostText
    {
        get => _totalCostText;
        set
        {
            if (!SetProperty(ref _totalCostText, value ?? string.Empty))
            {
                return;
            }

            if (_isUpdatingCost)
            {
                return;
            }

            _totalEditedByUser = true;
            ApplyParsedDecimal(
                _totalCostText,
                decimalPlaces: 2,
                commitFormatting: false,
                onAssigned: v =>
                {
                    Fuel.TotalCost = v;
                    if (Fuel.Gallons.HasValue && Fuel.Gallons.Value > 0 && v.HasValue)
                    {
                        Fuel.PricePerGallon = Math.Round(v.Value / Fuel.Gallons.Value, 3);
                        _pricePerGallonText = Fuel.PricePerGallon.Value.ToString("0.###", ParseCulture);
                        OnPropertyChanged(nameof(PricePerGallonText));
                    }
                },
                onBlank: () => Fuel.TotalCost = null,
                formatPropertyName: nameof(TotalCostText),
                setFormatted: f => _totalCostText = f);
            ValidateForm();
        }
    }

    public string OdometerText
    {
        get => _odometerText;
        set
        {
            if (!SetProperty(ref _odometerText, value ?? string.Empty))
            {
                return;
            }

            if (FuelNumericText.IsIntermediateNumber(_odometerText, ParseCulture))
            {
                ValidateForm();
                return;
            }

            if (int.TryParse(_odometerText, NumberStyles.Integer, ParseCulture, out var odo) && odo >= 0)
            {
                Fuel.VehicleOdometerReading = odo;
                RecalculateTripMpg();
            }
            else if (string.IsNullOrWhiteSpace(_odometerText))
            {
                Fuel.VehicleOdometerReading = 0;
                RecalculateTripMpg();
            }

            ValidateForm();
        }
    }

    public BusModel? SelectedBus
    {
        get => _selectedBus;
        set
        {
            if (!SetProperty(ref _selectedBus, value))
            {
                return;
            }

            if (_selectedBus != null)
            {
                Fuel.VehicleFueledId = _selectedBus.BusId;
            }

            ValidateForm();
            _ = LoadPreviousOdometerAsync();
        }
    }

    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (SetProperty(ref _isValid, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public double MPG
    {
        get => _mpg;
        private set
        {
            if (SetProperty(ref _mpg, value))
            {
                OnPropertyChanged(nameof(MpgDisplay));
                OnPropertyChanged(nameof(MpgHint));
            }
        }
    }

    /// <summary>Round/format all numeric fields (LostFocus / Save).</summary>
    public void CommitNumericFormatting()
    {
        CommitGallons(true);
        CommitPrice(true);
        CommitTotal(true);
        CommitOdometer(true);
        if (!_totalEditedByUser)
        {
            SyncTotalFromUnitPrice();
        }

        ValidateForm();
    }

    /// <summary>Sync location from ComboBoxAdv.Text if the binding lagged.</summary>
    public void SyncLocationFromComboText(string? comboText)
    {
        var typed = comboText?.Trim();
        if (!string.IsNullOrWhiteSpace(typed))
        {
            FuelLocationText = typed;
        }
    }

    private void CommitGallons(bool commitFormatting) =>
        ApplyParsedDecimal(
            _gallonsText,
            3,
            commitFormatting,
            onAssigned: v =>
            {
                Fuel.Gallons = v;
                if (!_totalEditedByUser)
                {
                    SyncTotalFromUnitPrice();
                }

                RecalculateTripMpg();
            },
            onBlank: () => Fuel.Gallons = null,
            formatPropertyName: nameof(GallonsText),
            setFormatted: f => _gallonsText = f,
            format: "0.###");

    private void CommitPrice(bool commitFormatting) =>
        ApplyParsedDecimal(
            _pricePerGallonText,
            3,
            commitFormatting,
            onAssigned: v =>
            {
                Fuel.PricePerGallon = v;
                if (!_totalEditedByUser)
                {
                    SyncTotalFromUnitPrice();
                }
            },
            onBlank: () => Fuel.PricePerGallon = null,
            formatPropertyName: nameof(PricePerGallonText),
            setFormatted: f => _pricePerGallonText = f,
            format: "0.###");

    private void CommitTotal(bool commitFormatting)
    {
        if (_isUpdatingCost)
        {
            return;
        }

        ApplyParsedDecimal(
            _totalCostText,
            2,
            commitFormatting,
            onAssigned: v =>
            {
                Fuel.TotalCost = v;
                if (Fuel.Gallons.HasValue && Fuel.Gallons.Value > 0 && v.HasValue)
                {
                    Fuel.PricePerGallon = Math.Round(v.Value / Fuel.Gallons.Value, 3);
                    _pricePerGallonText = Fuel.PricePerGallon.Value.ToString("0.###", ParseCulture);
                    OnPropertyChanged(nameof(PricePerGallonText));
                }
            },
            onBlank: () => Fuel.TotalCost = null,
            formatPropertyName: nameof(TotalCostText),
            setFormatted: f => _totalCostText = f,
            format: "0.##");
    }

    private void CommitOdometer(bool commitFormatting)
    {
        if (FuelNumericText.IsIntermediateNumber(_odometerText, ParseCulture))
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

    private void ApplyParsedDecimal(
        string text,
        int decimalPlaces,
        bool commitFormatting,
        Action<decimal?> onAssigned,
        Action onBlank,
        string formatPropertyName,
        Action<string> setFormatted,
        string format = "0.###")
    {
        if (FuelNumericText.IsIntermediateNumber(text, ParseCulture))
        {
            return;
        }

        if (FuelNumericText.TryParseRounded(text, decimalPlaces, allowBlankAsNull: true, out var value, ParseCulture))
        {
            if (value.HasValue)
            {
                onAssigned(value);
                if (commitFormatting)
                {
                    var formatted = value.Value.ToString(format, ParseCulture);
                    setFormatted(formatted);
                    OnPropertyChanged(formatPropertyName);
                }
            }
            else
            {
                onBlank();
            }
        }
    }

    private void SyncTextsFromFuel()
    {
        _gallonsText = FuelNumericText.FormatOptionalDecimal(Fuel.Gallons, "0.###", ParseCulture);
        _pricePerGallonText = FuelNumericText.FormatOptionalDecimal(Fuel.PricePerGallon, "0.###", ParseCulture);
        _totalCostText = FuelNumericText.FormatOptionalDecimal(Fuel.TotalCost, "0.##", ParseCulture);
        _odometerText = Fuel.VehicleOdometerReading > 0
            ? Fuel.VehicleOdometerReading.ToString(ParseCulture)
            : string.Empty;
        OnPropertyChanged(nameof(GallonsText));
        OnPropertyChanged(nameof(PricePerGallonText));
        OnPropertyChanged(nameof(TotalCostText));
        OnPropertyChanged(nameof(OdometerText));
    }

    private async Task LoadFuelLocationsAsync()
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

            if (!string.IsNullOrWhiteSpace(FuelLocationText)
                && !FuelLocations.Any(l => string.Equals(l, FuelLocationText, StringComparison.OrdinalIgnoreCase)))
            {
                FuelLocations.Insert(0, FuelLocationText);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load fuel locations for dialog — using seed defaults");
            FuelLocations.Clear();
            foreach (var seed in FuelLocationCatalog.SeedDefaults)
            {
                FuelLocations.Add(seed);
            }

            if (!string.IsNullOrWhiteSpace(FuelLocationText)
                && !FuelLocations.Any(l => string.Equals(l, FuelLocationText, StringComparison.OrdinalIgnoreCase)))
            {
                FuelLocations.Insert(0, FuelLocationText);
            }

            UserToast.Warning(
                "Could not load saved fuel vendors — showing defaults. You can still type a location.",
                "Fuel locations");
        }
    }

    private async Task LoadBusesAsync()
    {
        try
        {
            AvailableBuses.Clear();
            foreach (var bus in await _busService.GetAllBusesAsync())
            {
                AvailableBuses.Add(bus);
            }

            SelectedBus = AvailableBuses.FirstOrDefault(b => b.BusId == Fuel.VehicleFueledId)
                          ?? AvailableBuses.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading buses for fuel dialog");
            UserToast.Error($"Error loading buses: {ex.Message}", "Fuel dialog");
        }
    }

    private async Task LoadPreviousOdometerAsync()
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

    private void SyncTotalFromUnitPrice()
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
        }
        finally
        {
            _isUpdatingCost = false;
        }
    }

    private void ValidateForm()
    {
        LocationError = FuelRecordValidator.GetLocationError(FuelLocationText) ?? string.Empty;
        BusError = FuelRecordValidator.GetBusError(SelectedBus != null) ?? string.Empty;
        GallonsError = FuelRecordValidator.GetGallonsError(_gallonsText, Fuel.Gallons) ?? string.Empty;
        PriceError = FuelRecordValidator.GetPriceError(_pricePerGallonText, Fuel.PricePerGallon) ?? string.Empty;
        OdometerError = FuelRecordValidator.GetOdometerError(_odometerText) ?? string.Empty;

        IsValid =
            SelectedBus != null &&
            string.IsNullOrEmpty(LocationError) &&
            !string.IsNullOrWhiteSpace(Fuel.FuelType) &&
            Fuel.Gallons.HasValue &&
            Fuel.Gallons.Value > 0 &&
            string.IsNullOrEmpty(GallonsError) &&
            string.IsNullOrEmpty(PriceError) &&
            string.IsNullOrEmpty(OdometerError);
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

    private void Save()
    {
        CommitNumericFormatting();
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

        using (LogContext.PushProperty("ViewModelType", nameof(FuelDialogViewModel)))
        {
            Logger.Information("Fuel dialog save accepted FuelId={FuelId}", Fuel.FuelId);
        }

        CloseRequested?.Invoke(this, true);
    }
}
