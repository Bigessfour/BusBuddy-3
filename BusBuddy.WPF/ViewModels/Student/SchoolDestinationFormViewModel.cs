using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

public sealed class SchoolDestinationFormViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<SchoolDestinationFormViewModel>();
    private readonly IDestinationService _destinations;

    private string _name = string.Empty;
    private string _address = string.Empty;
    private string _city = string.Empty;
    private string _state = string.Empty;
    private string _zipCode = string.Empty;
    private string _startTimeText = "08:00";
    private string _dismissalTimeText = "15:30";
    private string _latitudeText = string.Empty;
    private string _longitudeText = string.Empty;
    private string _validationMessage = string.Empty;

    public event EventHandler<bool?>? RequestClose;

    public SchoolDestinationFormViewModel(IDestinationService destinations)
    {
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
    }

    public string Title => "Add school";

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                NotifySave();
            }
        }
    }

    public string Address
    {
        get => _address;
        set
        {
            if (SetProperty(ref _address, value))
            {
                NotifySave();
            }
        }
    }

    public string City
    {
        get => _city;
        set
        {
            if (SetProperty(ref _city, value))
            {
                NotifySave();
            }
        }
    }

    public string State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                NotifySave();
            }
        }
    }

    public string ZipCode
    {
        get => _zipCode;
        set
        {
            if (SetProperty(ref _zipCode, value))
            {
                NotifySave();
            }
        }
    }

    public string StartTimeText
    {
        get => _startTimeText;
        set
        {
            if (SetProperty(ref _startTimeText, value))
            {
                NotifySave();
            }
        }
    }

    public string DismissalTimeText
    {
        get => _dismissalTimeText;
        set
        {
            if (SetProperty(ref _dismissalTimeText, value))
            {
                NotifySave();
            }
        }
    }

    public string LatitudeText
    {
        get => _latitudeText;
        set => SetProperty(ref _latitudeText, value);
    }

    public string LongitudeText
    {
        get => _longitudeText;
        set => SetProperty(ref _longitudeText, value);
    }

    /// <summary>True after a successful save that stored school GPS (needed for Generate Routes stop times).</summary>
    public bool SavedWithGps { get; private set; }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(Address)
        && !string.IsNullOrWhiteSpace(City)
        && !string.IsNullOrWhiteSpace(State)
        && !string.IsNullOrWhiteSpace(ZipCode)
        && TimeSpan.TryParse(StartTimeText, CultureInfo.InvariantCulture, out _)
        && TimeSpan.TryParse(DismissalTimeText, CultureInfo.InvariantCulture, out _);

    private void NotifySave()
    {
        ValidationMessage = string.Empty;
        if (SaveCommand is AsyncRelayCommand asyncCmd)
        {
            asyncCmd.NotifyCanExecuteChanged();
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!TimeSpan.TryParse(StartTimeText, CultureInfo.InvariantCulture, out var start)
                || !TimeSpan.TryParse(DismissalTimeText, CultureInfo.InvariantCulture, out var dismissal))
            {
                ValidationMessage = "Use HH:mm for start and dismissal (example 08:00).";
                return;
            }

            var (lat, lon) = await ResolveSchoolGpsAsync().ConfigureAwait(true);
            var school = await _destinations.AddSchoolAsync(
                Name, Address, City, State, ZipCode, start, dismissal, lat, lon).ConfigureAwait(true);
            SavedWithGps = school.Latitude.HasValue && school.Longitude.HasValue;
            Logger.Information(
                "School cataloged DestinationId={Id} Name={Name} HasGps={HasGps}",
                school.DestinationId, school.Name, SavedWithGps);
            StatusMessage = SavedWithGps
                ? $"Saved {school.Name}"
                : $"Saved {school.Name} without GPS; Generate Routes will not persist stop times";
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Add school failed");
            ValidationMessage = ex.Message;
        }
    }

    private async Task<(decimal? Lat, decimal? Lon)> ResolveSchoolGpsAsync()
    {
        if (TryParseCoord(LatitudeText, out var typedLat) && TryParseCoord(LongitudeText, out var typedLon))
        {
            return (typedLat, typedLon);
        }

        var geocoder = App.ServiceProvider?.GetService<IGeocodingService>();
        if (geocoder is null)
        {
            Logger.Warning("IGeocodingService not registered; school will save without GPS unless lat/lng were typed");
            return (null, null);
        }

        var coords = await geocoder.GeocodeAsync(Address, City, State, ZipCode).ConfigureAwait(true);
        if (coords is { } pair)
        {
            return ((decimal)pair.latitude, (decimal)pair.longitude);
        }

        Logger.Warning("School address geocode returned no coordinates");
        return (null, null);
    }

    private static bool TryParseCoord(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
