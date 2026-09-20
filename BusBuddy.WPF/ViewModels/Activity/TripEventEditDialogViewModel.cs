using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.ViewModels.Activity;

/// <summary>
/// Clerk editor for Core <see cref="TripEvent"/>. Does not write leftover calendars or hop-5 Schedule rows.
/// Route != Trip — never sets RouteId.
/// </summary>
public sealed class TripEventEditDialogViewModel : INotifyPropertyChanged
{
    private static readonly TripType[] AthleticTypes =
    {
        TripType.Athletic_Football,
        TripType.Athletic_Basketball,
        TripType.Athletic_Baseball,
        TripType.Athletic_Volleyball,
        TripType.Athletic_JH_Football,
        TripType.Athletic_JH_Basketball,
        TripType.Athletic_JH_Baseball,
        TripType.Athletic_JH_Volleyball,
        TripType.Athletic_Wrestling,
        TripType.Athletic_JH_Wrestling,
        TripType.Athletic_Softball
    };

    private readonly TripEvent _original;
    private readonly bool _isEditMode;
    private readonly ITripEventService? _trips;
    private readonly IRouteService? _routes;
    private readonly IDestinationService? _destinations;
    private readonly ITripReasonCatalog? _reasons;

    private DateTime _tripDate = DateTime.Today.AddDays(1);
    private string _purpose = "Sports";
    private string _sportKind = "Football";
    private string _requestingSchool = "HS";
    private string _groupOrActivity = string.Empty;
    private string _pickupPoint = string.Empty;
    private string _destinationName = string.Empty;
    private string _leaveTimeText = TimeSpanParser.Format(new TimeSpan(14, 30, 0));
    private string _returnTimeText = TimeSpanParser.Format(new TimeSpan(18, 0, 0));
    private int _plannedHeadcount = 1;
    private int? _actualHeadcount;
    private string _externalTicketNo = string.Empty;
    private string _notes = string.Empty;
    private string _pocName = string.Empty;
    private Destination? _selectedOrigin;
    private BusBuddy.Core.Models.Driver? _selectedDriver;
    private BusBuddy.Core.Models.Bus? _selectedVehicle;
    private string _validationMessage = string.Empty;
    private bool _hasValidationErrors;
    private bool _listsReady;
    private string _assignmentWarning = string.Empty;
    private int? _destinationLocationId;
    private decimal? _destinationLatitude;
    private decimal? _destinationLongitude;
    private string _destinationCity = string.Empty;
    private string _destinationState = "CO";
    private string _destinationZip = string.Empty;
    private string _destinationStreet = string.Empty;

    public TripEventEditDialogViewModel(TripEvent? tripToEdit = null)
        : this(
            tripToEdit,
            App.ServiceProvider?.GetService<ITripEventService>(),
            App.ServiceProvider?.GetService<IRouteService>(),
            App.ServiceProvider?.GetService<IDestinationService>(),
            App.ServiceProvider?.GetService<ITripReasonCatalog>())
    {
    }

    public TripEventEditDialogViewModel(
        TripEvent? tripToEdit,
        ITripEventService? trips,
        IRouteService? routes,
        IDestinationService? destinations)
        : this(tripToEdit, trips, routes, destinations, null)
    {
    }

    public TripEventEditDialogViewModel(
        TripEvent? tripToEdit,
        ITripEventService? trips,
        IRouteService? routes,
        IDestinationService? destinations,
        ITripReasonCatalog? reasons)
    {
        _isEditMode = tripToEdit is { TripEventId: > 0 };
        _original = tripToEdit ?? new TripEvent { Type = TripType.Athletic_Football, Status = TripStatus.Draft };
        _trips = trips;
        _routes = routes;
        _destinations = destinations;
        _reasons = reasons;
        SeedFallbackLists();
        LoadTrip(_original);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => ListsReady);
        RememberPurposeCommand = new AsyncRelayCommand(RememberPurposeAsync, CanEditCatalogText);
        RemovePurposeCommand = new AsyncRelayCommand(RemovePurposeAsync, CanEditCatalogText);
        RememberSportCommand = new AsyncRelayCommand(RememberSportAsync, CanEditSportCatalog);
        RemoveSportCommand = new AsyncRelayCommand(RemoveSportAsync, CanEditSportCatalog);
    }

    public ObservableCollection<string> PurposeOptions { get; } = new();
    public ObservableCollection<string> SportKinds { get; } = new();
    public ObservableCollection<string> RequestingSchools { get; } = new();
    public ObservableCollection<Destination> Origins { get; } = new();
    public ObservableCollection<BusBuddy.Core.Models.Driver> AvailableDrivers { get; } = new();
    public ObservableCollection<BusBuddy.Core.Models.Bus> AvailableVehicles { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand RememberPurposeCommand { get; }
    public ICommand RemovePurposeCommand { get; }
    public ICommand RememberSportCommand { get; }
    public ICommand RemoveSportCommand { get; }

    public event EventHandler<bool>? CloseRequested;

    public string DialogTitle => _isEditMode ? "Edit trip" : "New trip";
    public string SaveButtonText => _isEditMode ? "Update trip" : "Save trip";

    public string Subject => string.IsNullOrWhiteSpace(DestinationName)
        ? $"{Purpose} {GroupOrActivity}".Trim()
        : $"{Purpose} — {DestinationName}";

    public bool IsSports => string.Equals(Purpose, "Sports", StringComparison.OrdinalIgnoreCase);

    public string StatusDisplay => $"Status: {_original.Status}";

    public string PathMilesDisplay => _original.PathMiles is { } miles
        ? $"Distance: {miles:0.00} mi"
        : "Distance: not calculated yet";

    public string DriverHoursDisplay
    {
        get
        {
            if (!TryGetWindow(out var start, out var end))
            {
                return "Driver hours: —";
            }

            var hours = (decimal)(end - start).TotalHours + TripEvent.PrePostTripInspectionHours;
            if (hours < TripEvent.PrePostTripInspectionHours)
            {
                hours = TripEvent.PrePostTripInspectionHours;
            }

            return $"Driver hours: {hours:0.00} h (includes {TripEvent.PrePostTripInspectionHours:0.0}h inspection)";
        }
    }

    public bool ListsReady
    {
        get => _listsReady;
        private set
        {
            _listsReady = value;
            OnPropertyChanged();
            if (SaveCommand is IRelayCommand save)
            {
                save.NotifyCanExecuteChanged();
            }
        }
    }

    public DateTime TripDate
    {
        get => _tripDate;
        set
        {
            _tripDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DriverHoursDisplay));
            _ = RefreshAssignmentWarningsAsync();
        }
    }

    public string Purpose
    {
        get => _purpose;
        set
        {
            _purpose = value ?? "Sports";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSports));
            OnPropertyChanged(nameof(Subject));
            NotifyCatalogCommands();
        }
    }

    public string SportKind
    {
        get => _sportKind;
        set
        {
            _sportKind = value ?? "Football";
            OnPropertyChanged();
            NotifyCatalogCommands();
        }
    }

    public string RequestingSchool
    {
        get => _requestingSchool;
        set
        {
            _requestingSchool = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string GroupOrActivity
    {
        get => _groupOrActivity;
        set
        {
            _groupOrActivity = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Subject));
        }
    }

    public string PickupPoint
    {
        get => _pickupPoint;
        set
        {
            _pickupPoint = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string DestinationName
    {
        get => _destinationName;
        set
        {
            var next = value ?? string.Empty;
            if (!string.Equals(_destinationName, next, StringComparison.Ordinal))
            {
                _destinationLocationId = null;
            }

            _destinationName = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Subject));
        }
    }

    public string LeaveTimeText
    {
        get => _leaveTimeText;
        set
        {
            _leaveTimeText = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DriverHoursDisplay));
            _ = RefreshAssignmentWarningsAsync();
        }
    }

    public string ReturnTimeText
    {
        get => _returnTimeText;
        set
        {
            _returnTimeText = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DriverHoursDisplay));
            _ = RefreshAssignmentWarningsAsync();
        }
    }

    public int PlannedHeadcount
    {
        get => _plannedHeadcount;
        set
        {
            _plannedHeadcount = value;
            OnPropertyChanged();
        }
    }

    public int? ActualHeadcount
    {
        get => _actualHeadcount;
        set
        {
            _actualHeadcount = value;
            OnPropertyChanged();
        }
    }

    public string ExternalTicketNo
    {
        get => _externalTicketNo;
        set
        {
            _externalTicketNo = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            _notes = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string PocName
    {
        get => _pocName;
        set
        {
            _pocName = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public Destination? SelectedOrigin
    {
        get => _selectedOrigin;
        set
        {
            _selectedOrigin = value;
            OnPropertyChanged();
            if (value is not null && string.IsNullOrWhiteSpace(PickupPoint))
            {
                PickupPoint = value.Name;
            }
        }
    }

    public BusBuddy.Core.Models.Driver? SelectedDriver
    {
        get => _selectedDriver;
        set
        {
            _selectedDriver = value;
            OnPropertyChanged();
            _ = RefreshAssignmentWarningsAsync();
        }
    }

    public BusBuddy.Core.Models.Bus? SelectedVehicle
    {
        get => _selectedVehicle;
        set
        {
            _selectedVehicle = value;
            OnPropertyChanged();
            _ = RefreshAssignmentWarningsAsync();
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            _validationMessage = value;
            OnPropertyChanged();
        }
    }

    public bool HasValidationErrors
    {
        get => _hasValidationErrors;
        set
        {
            _hasValidationErrors = value;
            OnPropertyChanged();
        }
    }

    public string AssignmentWarning
    {
        get => _assignmentWarning;
        set
        {
            _assignmentWarning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAssignmentWarning));
        }
    }

    public bool HasAssignmentWarning => !string.IsNullOrWhiteSpace(AssignmentWarning);

    public void ApplyDestinationAddress(PlaceAddressApplier.AppliedAddress applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        DestinationName = applied.FormattedAddress ?? applied.Street ?? DestinationName;
        _destinationStreet = applied.Street ?? applied.FormattedAddress ?? string.Empty;
        _destinationCity = applied.City ?? string.Empty;
        _destinationState = string.IsNullOrWhiteSpace(applied.State) ? "CO" : applied.State;
        _destinationZip = applied.Zip ?? string.Empty;
        _destinationLatitude = applied.Latitude.HasValue ? (decimal)applied.Latitude.Value : null;
        _destinationLongitude = applied.Longitude.HasValue ? (decimal)applied.Longitude.Value : null;
        _ = ResolveDestinationPlaceAsync();
    }

    public async Task LoadAvailableDataAsync()
    {
        try
        {
            await LoadCatalogAsync().ConfigureAwait(true);
            ApplyPurposeFromCatalog();
            Origins.Clear();
            if (_destinations is not null)
            {
                var schools = await _destinations.GetActiveSchoolsAsync().ConfigureAwait(true);
                foreach (var school in schools)
                {
                    Origins.Add(school);
                }

                var others = await _destinations.GetActiveDestinationsAsync().ConfigureAwait(true);
                foreach (var place in others)
                {
                    if (place.LocationType is LocationTypes.Depot or LocationTypes.School
                        && Origins.All(o => o.DestinationId != place.DestinationId))
                    {
                        Origins.Add(place);
                    }
                }
            }

            if (_original.OriginLocationId.HasValue)
            {
                SelectedOrigin = Origins.FirstOrDefault(o => o.DestinationId == _original.OriginLocationId);
            }

            await LoadAssignableAssetsAsync().ConfigureAwait(true);
            ListsReady = true;
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Could not load origins or fleet: {ex.Message}";
            HasValidationErrors = true;
            ListsReady = true;
        }
        finally
        {
            NotifyAssignableLists();
        }
    }

    public bool HasOrigins => Origins.Count > 0;

    public bool HasAssignableDrivers => AvailableDrivers.Count > 0;

    public bool HasAssignableVehicles => AvailableVehicles.Count > 0;

    private void NotifyAssignableLists()
    {
        OnPropertyChanged(nameof(HasOrigins));
        OnPropertyChanged(nameof(HasAssignableDrivers));
        OnPropertyChanged(nameof(HasAssignableVehicles));
    }

    public bool ValidateTrip()
    {
        var errors = new List<string>();
        var hasLeaveText = !string.IsNullOrWhiteSpace(_leaveTimeText);
        var hasReturnText = !string.IsNullOrWhiteSpace(_returnTimeText);
        if (hasLeaveText && !TimeSpanParser.TryParse(_leaveTimeText, out _))
        {
            errors.Add("Leave time must be hh:mm");
        }

        if (hasReturnText && !TimeSpanParser.TryParse(_returnTimeText, out _))
        {
            errors.Add("Return time must be hh:mm");
        }

        if (PlannedHeadcount < 0)
        {
            errors.Add("Estimated travelers cannot be negative");
        }

        if (ActualHeadcount is < 0)
        {
            errors.Add("Rode count cannot be negative");
        }

        if (SelectedVehicle is not null && PlannedHeadcount > SelectedVehicle.Capacity)
        {
            errors.Add($"Estimated travelers ({PlannedHeadcount}) exceed bus capacity ({SelectedVehicle.Capacity})");
        }

        if (errors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors);
            HasValidationErrors = true;
            return false;
        }

        ValidationMessage = string.Empty;
        HasValidationErrors = false;
        return true;
    }

    public TripEvent Result { get; private set; } = null!;

    public async Task<TripEvent> BuildTripAsync()
    {
        TimeSpan? leave = TimeSpanParser.TryParse(_leaveTimeText, out var parsedLeave) ? parsedLeave : null;
        TimeSpan? ret = TimeSpanParser.TryParse(_returnTimeText, out var parsedReturn) ? parsedReturn : null;
        var trip = _isEditMode ? _original : new TripEvent();
        trip.RouteId = null;
        trip.TripDate = TripDate.Date;
        trip.PickupTime = leave;
        trip.ReturnClockTime = ret;
        trip.ReturnIsNextDay = leave.HasValue && ret.HasValue && ret.Value <= leave.Value;
        trip.Type = ResolveTripType();
        trip.CustomType = trip.Type == TripType.Custom
            ? (IsSports ? SportKind.Trim() : Purpose.Trim())
            : null;
        trip.RequestingSchool = RequestingSchool;
        trip.GroupOrActivity = GroupOrActivity;
        trip.OriginLocationId = SelectedOrigin?.DestinationId;
        trip.OriginName = string.IsNullOrWhiteSpace(PickupPoint) ? SelectedOrigin?.Name : PickupPoint;
        trip.DestinationName = DestinationName;
        trip.Destination = DestinationName;
        trip.PlannedHeadcount = PlannedHeadcount;
        trip.StudentCount = PlannedHeadcount;
        trip.ActualHeadcount = ActualHeadcount;
        trip.ExternalTicketNo = string.IsNullOrWhiteSpace(ExternalTicketNo) ? trip.ExternalTicketNo : ExternalTicketNo.Trim();
        trip.TripNotes = Notes;
        trip.POCName = string.IsNullOrWhiteSpace(PocName) ? string.Empty : PocName.Trim();
        trip.DriverId = SelectedDriver?.DriverId;
        if (_original.IsMultiAsset && SelectedVehicle is null)
        {
            trip.IsMultiAsset = true;
            trip.VehicleId = null;
            trip.AssignedBusNumber = _original.AssignedBusNumber;
        }
        else
        {
            trip.IsMultiAsset = false;
            trip.VehicleId = SelectedVehicle?.BusId;
            trip.AssignedBusNumber = SelectedVehicle?.BusNumber;
        }

        if (_destinationLocationId is null
            && _destinations is not null
            && LocationCoordinate.IsValidated(_destinationLatitude, _destinationLongitude)
            && !string.IsNullOrWhiteSpace(_destinationStreet)
            && !string.IsNullOrWhiteSpace(_destinationCity)
            && !string.IsNullOrWhiteSpace(_destinationZip))
        {
            var place = await _destinations.AddTripDestinationAsync(
                DestinationName,
                _destinationStreet,
                _destinationCity,
                _destinationState,
                _destinationZip,
                _destinationLatitude!.Value,
                _destinationLongitude!.Value).ConfigureAwait(true);
            trip.DestinationLocationId = place.DestinationId;
            trip.DestinationLocation = place;
            _destinationLocationId = place.DestinationId;
        }
        else
        {
            trip.DestinationLocationId = _destinationLocationId;
            if (_destinationLocationId is null)
            {
                trip.DestinationLocation = null;
            }
        }

        Result = trip;
        return trip;
    }

    private async Task SaveAsync()
    {
        if (!ValidateTrip())
        {
            return;
        }

        await BuildTripAsync().ConfigureAwait(true);
        if (_reasons is not null)
        {
            await _reasons.RememberPurposeAsync(Purpose).ConfigureAwait(true);
            if (IsSports)
            {
                await _reasons.RememberSportAsync(SportKind).ConfigureAwait(true);
            }
        }

        CloseRequested?.Invoke(this, true);
    }

    private async Task RememberPurposeAsync()
    {
        EnsureInList(PurposeOptions, Purpose);
        if (_reasons is not null)
        {
            await _reasons.RememberPurposeAsync(Purpose).ConfigureAwait(true);
            await ReloadPurposesAsync().ConfigureAwait(true);
        }
    }

    private async Task RemovePurposeAsync()
    {
        RemoveFromList(PurposeOptions, Purpose);
        if (_reasons is not null)
        {
            await _reasons.ForgetPurposeAsync(Purpose).ConfigureAwait(true);
            await ReloadPurposesAsync().ConfigureAwait(true);
        }
    }

    private async Task RememberSportAsync()
    {
        EnsureInList(SportKinds, SportKind);
        if (_reasons is not null)
        {
            await _reasons.RememberSportAsync(SportKind).ConfigureAwait(true);
            await ReloadSportsAsync().ConfigureAwait(true);
        }
    }

    private async Task RemoveSportAsync()
    {
        RemoveFromList(SportKinds, SportKind);
        if (_reasons is not null)
        {
            await _reasons.ForgetSportAsync(SportKind).ConfigureAwait(true);
            await ReloadSportsAsync().ConfigureAwait(true);
        }
    }

    private bool CanEditCatalogText() => !string.IsNullOrWhiteSpace(Purpose);

    private bool CanEditSportCatalog() => IsSports && !string.IsNullOrWhiteSpace(SportKind);

    private void SeedFallbackLists()
    {
        ReplaceList(PurposeOptions, TripReasonCatalog.SeedPurposes);
        ReplaceList(SportKinds, TripReasonCatalog.SeedSports);
        RequestingSchools.Clear();
        RequestingSchools.Add("HS");
        RequestingSchools.Add("MS");
        RequestingSchools.Add("AV");
        RequestingSchools.Add("WA");
    }

    private async Task LoadCatalogAsync()
    {
        if (_reasons is null)
        {
            return;
        }

        await ReloadPurposesAsync().ConfigureAwait(true);
        await ReloadSportsAsync().ConfigureAwait(true);
    }

    private async Task ReloadPurposesAsync()
    {
        if (_reasons is null)
        {
            return;
        }

        var purposes = await _reasons.GetPurposesAsync().ConfigureAwait(true);
        ReplaceList(PurposeOptions, purposes);
        EnsureInList(PurposeOptions, Purpose);
    }

    private async Task ReloadSportsAsync()
    {
        if (_reasons is null)
        {
            return;
        }

        var sports = await _reasons.GetSportsAsync().ConfigureAwait(true);
        ReplaceList(SportKinds, sports);
        EnsureInList(SportKinds, SportKind);
    }

    private void LoadTrip(TripEvent trip)
    {
        if (!_isEditMode)
        {
            return;
        }

        TripDate = trip.TripDate == default ? DateTime.Today : trip.TripDate.Date;
        if (trip.IsAthleticTrip || IsKnownSport(trip.CustomType))
        {
            Purpose = "Sports";
            SportKind = trip.Type == TripType.Custom
                ? trip.CustomType ?? trip.DisplayTripType
                : trip.DisplayTripType;
        }
        else
        {
            Purpose = trip.Purpose;
            if (!string.IsNullOrWhiteSpace(trip.CustomType))
            {
                SportKind = trip.CustomType;
            }
        }

        RequestingSchool = trip.RequestingSchool ?? "HS";
        GroupOrActivity = trip.GroupOrActivity ?? string.Empty;
        PickupPoint = trip.OriginName ?? string.Empty;
        DestinationName = trip.DestinationName ?? trip.Destination ?? string.Empty;
        _destinationLocationId = trip.DestinationLocationId;
        LeaveTimeText = TimeSpanParser.Format(trip.PickupTime ?? trip.LeaveTime.TimeOfDay);
        ReturnTimeText = TimeSpanParser.Format(trip.ReturnClockTime ?? trip.ReturnTime?.TimeOfDay ?? leaveFallback(trip));
        PlannedHeadcount = trip.PlannedHeadcount ?? Math.Max(1, trip.StudentCount);
        ActualHeadcount = trip.ActualHeadcount;
        ExternalTicketNo = trip.ExternalTicketNo ?? string.Empty;
        Notes = trip.TripNotes ?? string.Empty;
        PocName = trip.POCName ?? string.Empty;
        EnsureInList(PurposeOptions, Purpose);
        EnsureInList(SportKinds, SportKind);
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(PathMilesDisplay));
        OnPropertyChanged(nameof(DriverHoursDisplay));
    }

    private static TimeSpan leaveFallback(TripEvent trip) => trip.LeaveTime.AddHours(4).TimeOfDay;

    private TripType ResolveTripType()
    {
        if (!IsSports)
        {
            return string.Equals(Purpose, "Field Trip", StringComparison.OrdinalIgnoreCase)
                ? TripType.Field
                : TripType.Custom;
        }

        foreach (var kind in AthleticTypes)
        {
            if (string.Equals(DisplaySport(kind), SportKind, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return TripType.Custom;
    }

    private void ApplyPurposeFromCatalog()
    {
        if (!_isEditMode || _original.Type != TripType.Custom)
        {
            return;
        }

        if (IsKnownSport(_original.CustomType))
        {
            Purpose = "Sports";
            SportKind = _original.CustomType!;
        }
    }

    private bool IsKnownSport(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && (TripReasonCatalog.SeedSports.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
            || SportKinds.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)));

    private static string DisplaySport(TripType type) => type switch
    {
        TripType.Athletic_Football => "Football",
        TripType.Athletic_Basketball => "Basketball",
        TripType.Athletic_Baseball => "Baseball",
        TripType.Athletic_Volleyball => "Volleyball",
        TripType.Athletic_JH_Football => "JH Football",
        TripType.Athletic_JH_Basketball => "JH Basketball",
        TripType.Athletic_JH_Baseball => "JH Baseball",
        TripType.Athletic_JH_Volleyball => "JH Volleyball",
        TripType.Athletic_Wrestling => "Wrestling",
        TripType.Athletic_JH_Wrestling => "JH Wrestling",
        TripType.Athletic_Softball => "Softball",
        _ => "Football"
    };

    private async Task LoadAssignableAssetsAsync()
    {
        AvailableDrivers.Clear();
        AvailableVehicles.Clear();
        IEnumerable<BusBuddy.Core.Models.Driver> drivers = Array.Empty<BusBuddy.Core.Models.Driver>();
        IEnumerable<BusBuddy.Core.Models.Bus> buses = Array.Empty<BusBuddy.Core.Models.Bus>();
        if (_routes is not null)
        {
            var driverResult = await _routes.GetAvailableDriversAsync().ConfigureAwait(true);
            if (driverResult.IsSuccess)
            {
                drivers = driverResult.Value;
            }

            var busResult = await _routes.GetAvailableBusesAsync().ConfigureAwait(true);
            if (busResult.IsSuccess)
            {
                buses = busResult.Value;
            }
        }

        TryGetWindow(out var start, out var end);
        foreach (var driver in drivers)
        {
            if (_original.DriverId == driver.DriverId
                || _trips is null
                || !await _trips.HasConflictsAsync(null, driver.DriverId, start, end, _original.TripEventId).ConfigureAwait(true))
            {
                AvailableDrivers.Add(driver);
            }
        }

        foreach (var bus in buses.Where(b => b.IsAvailable))
        {
            if (_original.VehicleId == bus.BusId
                || _trips is null
                || !await _trips.HasConflictsAsync(bus.BusId, null, start, end, _original.TripEventId).ConfigureAwait(true))
            {
                AvailableVehicles.Add(bus);
            }
        }

        SelectedDriver = AvailableDrivers.FirstOrDefault(d => d.DriverId == _original.DriverId);
        SelectedVehicle = AvailableVehicles.FirstOrDefault(b => b.BusId == _original.VehicleId);
        await RefreshAssignmentWarningsAsync().ConfigureAwait(true);
    }

    private async Task RefreshAssignmentWarningsAsync()
    {
        if (_trips is null || (SelectedDriver is null && SelectedVehicle is null))
        {
            AssignmentWarning = string.Empty;
            return;
        }

        if (!TryGetWindow(out var start, out var end))
        {
            return;
        }

        var warnings = await _trips.GetAssignmentWarningsAsync(
            SelectedVehicle?.BusId,
            SelectedDriver?.DriverId,
            start,
            end,
            _original.TripEventId).ConfigureAwait(true);
        AssignmentWarning = string.Join(Environment.NewLine, warnings);
    }

    private bool TryGetWindow(out DateTime start, out DateTime end)
    {
        start = TripDate.Date;
        end = start.AddHours(4);
        if (!TimeSpanParser.TryParse(_leaveTimeText, out var leave))
        {
            return false;
        }

        start = TripDate.Date + leave;
        if (TimeSpanParser.TryParse(_returnTimeText, out var ret))
        {
            end = TripDate.Date + ret;
            if (end <= start)
            {
                end = end.AddDays(1);
            }
        }
        else
        {
            end = start.AddHours(4);
        }

        return true;
    }

    private async Task ResolveDestinationPlaceAsync()
    {
        if (_destinations is null || string.IsNullOrWhiteSpace(DestinationName))
        {
            return;
        }

        var match = await _destinations.FindValidatedPlaceByNameAsync(DestinationName).ConfigureAwait(true);
        if (match is not null)
        {
            _destinationLocationId = match.DestinationId;
        }
    }

    private static void ReplaceList(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target.Add(value);
            }
        }
    }

    private static void EnsureInList(ObservableCollection<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!target.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            target.Add(value);
        }
    }

    private static void RemoveFromList(ObservableCollection<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var match = target.FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            target.Remove(match);
        }
    }

    private void NotifyCatalogCommands()
    {
        if (RememberPurposeCommand is IRelayCommand rememberPurpose)
        {
            rememberPurpose.NotifyCanExecuteChanged();
        }

        if (RemovePurposeCommand is IRelayCommand removePurpose)
        {
            removePurpose.NotifyCanExecuteChanged();
        }

        if (RememberSportCommand is IRelayCommand rememberSport)
        {
            rememberSport.NotifyCanExecuteChanged();
        }

        if (RemoveSportCommand is IRelayCommand removeSport)
        {
            removeSport.NotifyCanExecuteChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
