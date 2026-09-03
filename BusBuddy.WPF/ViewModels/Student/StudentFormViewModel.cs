using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Map;
using BusBuddy.WPF.Views.Map;
using System.Text.RegularExpressions;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.RouteDetermination;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using BusBuddy.Core.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF;
using BusBuddy.WPF.Commands;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using CommunityToolkit.Mvvm.Messaging;
using BusBuddy.WPF.Messages;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.ViewModels.Student
{
    /// <summary>
    /// ViewModel for the StudentForm - handles adding and editing students
    /// Includes address validation and route assignment functionality
    /// </summary>
    public class StudentFormViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<StudentFormViewModel>();

        private readonly BusBuddyDbContext _context;
        private readonly IStudentService? _studentService; // Prefer service for persistence
        private readonly StudentFormAddressCoordinator _address;
        private readonly StudentFormCatalogCoordinator _catalog;
        private Core.Models.Student _student;
        private string _formTitle = "Add New Student";
        private bool _isEditMode;

        // Event to request the form to close
        public event EventHandler<bool?>? RequestClose;

        /// <summary>Raised when validation should move keyboard focus to a named form field.</summary>
        public event EventHandler<string>? RequestFocusField;

        public StudentFormViewModel() : this(null, new Core.Models.Student(), enableValidation: true, null, null)
        {
        }

        public StudentFormViewModel(Core.Models.Student? student, bool enableValidation = true)
            : this(null, student, enableValidation, null, null)
        {
        }

        public StudentFormViewModel(IStudentService studentService)
            : this(studentService, null, enableValidation: true, null, null)
        {
        }

        public StudentFormViewModel(
            IStudentService studentService,
            Core.Models.Student? student,
            bool enableValidation = false)
            : this(studentService, student, enableValidation, null, null)
        {
        }

        public StudentFormViewModel(
            IStudentService studentService,
            bool enableValidation,
            IMapsGeoService? mapsGeoService,
            IPlacesAutocompleteService? placesAutocomplete = null)
            : this(studentService, null, enableValidation, mapsGeoService, placesAutocomplete)
        {
        }

        public StudentFormViewModel(
            IStudentService? studentService,
            Core.Models.Student? student,
            bool enableValidation,
            IMapsGeoService? mapsGeoService,
            IPlacesAutocompleteService? placesAutocomplete)
        {
            _studentService = studentService;
            var mapsGeo = mapsGeoService ?? App.ServiceProvider?.GetService<IMapsGeoService>();
            var places = placesAutocomplete ?? App.ServiceProvider?.GetService<IPlacesAutocompleteService>();
            _address = new StudentFormAddressCoordinator(mapsGeo, places);
            WireAddressCoordinator();

            _context = TryCreateDbContextViaDi() ?? new BusBuddyDbContext();
            _address.DisableValidation = !enableValidation;

            _student = student ?? new Core.Models.Student
            {
                Active = true,
                EnrollmentDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
                CreatedDate = DateTime.UtcNow,
                School = string.Empty,
                State = "CO"
            };

            _isEditMode = student != null && student.StudentId > 0;
            _formTitle = _isEditMode ? "Edit Student" : "Add New Student · UX v3 2026-09-02";

            AvailableRoutes = new ObservableCollection<string>();
            AvailableBusStops = new ObservableCollection<string>();
            AvailablePickupStops = new ObservableCollection<PickupStop>();
            AvailableSchools = new ObservableCollection<Destination>();

            _catalog = new StudentFormCatalogCoordinator(
                _context,
                _student,
                _routeCatalog,
                AvailableRoutes,
                AvailablePickupStops,
                AvailableSchools,
                stop => SelectedPickupStop = stop,
                school => SelectedSchoolDestination = school);

            try { _student.PropertyChanged += OnStudentPropertyChanged; } catch { }
            InitializeCommands();
            RegisterCatalogRefreshHandlers();
            _ = _catalog.LoadAllAsync();
        }

        private void WireAddressCoordinator()
        {
            _address.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(StudentFormAddressCoordinator.ValidationMessage):
                        OnPropertyChanged(nameof(AddressValidationMessage));
                        break;
                    case nameof(StudentFormAddressCoordinator.ValidationColor):
                        OnPropertyChanged(nameof(AddressValidationColor));
                        break;
                    case nameof(StudentFormAddressCoordinator.IsPopupOpen):
                        OnPropertyChanged(nameof(IsAddressSuggestionPopupOpen));
                        break;
                    case nameof(StudentFormAddressCoordinator.IsAutocompleteEnabled):
                        OnPropertyChanged(nameof(IsAddressAutocompleteEnabled));
                        break;
                    case nameof(StudentFormAddressCoordinator.DisableValidation):
                        OnPropertyChanged(nameof(DisableAddressValidation));
                        break;
                }
            };
            _address.CoordinatesUpdated += (_, _) => _ = SuggestNearestPickupStopAsync();
        }

        private void RegisterCatalogRefreshHandlers()
        {
            WeakReferenceMessenger.Default.Register<PickupStopCatalogChangedMessage>(
                this,
                async (_, _) => await _catalog.LoadPickupStopsAsync().ConfigureAwait(true));
            WeakReferenceMessenger.Default.Register<SchoolCatalogChangedMessage>(
                this,
                async (_, _) => await _catalog.LoadSchoolsAsync().ConfigureAwait(true));
        }

        #region Properties

        /// <summary>
        /// Student being edited or added
        /// </summary>
        public Core.Models.Student Student
        {
            get => _student;
            set
            {
                if (SetProperty(ref _student, value))
                {
                    try
                    {
                        // Rewire property changed subscription to update CanExecute
                        if (_student != null)
                        {
                            _student.PropertyChanged -= OnStudentPropertyChanged;
                            _student.PropertyChanged += OnStudentPropertyChanged;
                        }
                    }
                    catch { /* best-effort wiring */ }
                    // Immediately re-evaluate save capability
                    _saveRelay?.NotifyCanExecuteChanged();
                    CanSave = CanSaveStudent();
                }
            }
        }

        /// <summary>
        /// Form title (Add New Student or Edit Student)
        /// </summary>
        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        /// <summary>
        /// Address validation message to display to user
        /// </summary>
        public string AddressValidationMessage => _address.ValidationMessage;

        public Brush AddressValidationColor => _address.ValidationColor;

        /// <summary>
        /// Available route names for assignment
        /// </summary>
        public ObservableCollection<string> AvailableRoutes { get; }

        /// <summary>Grade pick list for ComboBoxAdv ItemsSource (replaces XAML PriorityBinding fallback).</summary>
        public IReadOnlyList<string> AvailableGrades { get; } = StudentGradeCatalog.All;

        /// <summary>US state abbreviations for ComboBoxAdv ItemsSource (replaces XAML PriorityBinding fallback).</summary>
        public IReadOnlyList<string> AvailableStates { get; } =
        [
            "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
            "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
            "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
            "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
            "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
            "DC"
        ];

        /// <summary>Available bus stop names for assignment</summary>
        public ObservableCollection<string> AvailableBusStops { get; }

        private readonly List<(string Name, bool IsSpecialNeeds)> _routeCatalog = new();

        /// <summary>District pickup stop catalog (shared corners / blocks).</summary>
        public ObservableCollection<PickupStop> AvailablePickupStops { get; }

        private PickupStop? _selectedPickupStop;
        private string _pickupStopHint = "Assign a catalog stop near the home address, or use home as stop (rural).";

        public PickupStop? SelectedPickupStop
        {
            get => _selectedPickupStop;
            set
            {
                if (SetProperty(ref _selectedPickupStop, value))
                {
                    Student.PickupStopId = value?.PickupStopId;
                    if (value is not null)
                    {
                        Student.BusStop = value.Name;
                    }

                    OnPropertyChanged(nameof(UsesHomeAsPickupStop));
                    PickupStopHint = value is null
                        ? "No catalog stop selected — home address will be used when generating routes."
                        : $"Boarding at {value.Name}.";
                }
            }
        }

        public bool UsesHomeAsPickupStop => !Student.PickupStopId.HasValue;

        public string PickupStopHint
        {
            get => _pickupStopHint;
            private set => SetProperty(ref _pickupStopHint, value);
        }

        /// <summary>Active school Destinations for intake assignment (home-to-school routing).</summary>
        public ObservableCollection<Destination> AvailableSchools { get; }

        private Destination? _selectedSchoolDestination;
        private string _schoolStartTimeText = string.Empty;
        private string _schoolDismissalTimeText = string.Empty;

        /// <summary>Selected campus; syncs Student.School and Student.DestinationId.</summary>
        public Destination? SelectedSchoolDestination
        {
            get => _selectedSchoolDestination;
            set
            {
                if (!SetProperty(ref _selectedSchoolDestination, value))
                {
                    return;
                }

                if (value is null)
                {
                    SchoolStartTimeText = string.Empty;
                    SchoolDismissalTimeText = string.Empty;
                    Student.School = null;
                    Student.DestinationId = null;
                    return;
                }

                Student.School = value.Name;
                Student.DestinationId = value.DestinationId;
                SchoolStartTimeText = value.StartTime?.ToString(@"hh\:mm") ?? string.Empty;
                SchoolDismissalTimeText = value.DismissalTime?.ToString(@"hh\:mm") ?? string.Empty;
            }
        }

        public string SchoolStartTimeText
        {
            get => _schoolStartTimeText;
            set => SetProperty(ref _schoolStartTimeText, value);
        }

        public string SchoolDismissalTimeText
        {
            get => _schoolDismissalTimeText;
            set => SetProperty(ref _schoolDismissalTimeText, value);
        }

        /// <summary>
        /// Whether form is in edit mode (vs add mode)
        /// </summary>
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        // Enhanced Properties for AI and Validation
        private bool _hasGlobalError;
        private string _globalErrorMessage = string.Empty;
        private bool _isValidating;
        private string _validationStatus = "Ready";
        private Brush _validationStatusBrush = Brushes.Gray;
        private ObservableCollection<string> _filteredBusStops = new();
        private bool _canSave = true;
        private readonly ObservableCollection<string> _validationErrors = new();
        private bool _hasValidationErrors;
        private readonly Dictionary<string, string> _fieldErrors = new(StringComparer.Ordinal);

        /// <summary>Per-field validation messages keyed by <see cref="StudentFormFields"/>.</summary>
        public IReadOnlyDictionary<string, string> FieldErrors => _fieldErrors;

        private string? _studentNameFieldError;
        private string? _gradeFieldError;

        public string? StudentNameFieldError
        {
            get => _studentNameFieldError;
            private set => SetProperty(ref _studentNameFieldError, value);
        }

        public bool HasStudentNameFieldError => !string.IsNullOrWhiteSpace(StudentNameFieldError);

        public string? GradeFieldError
        {
            get => _gradeFieldError;
            private set => SetProperty(ref _gradeFieldError, value);
        }

        public bool HasGradeFieldError => !string.IsNullOrWhiteSpace(GradeFieldError);

        /// <summary>
        /// Whether there's a global error to display
        /// </summary>
        public bool HasGlobalError
        {
            get => _hasGlobalError;
            set => SetProperty(ref _hasGlobalError, value);
        }

        /// <summary>
        /// Global error message for system-wide issues
        /// </summary>
        public string GlobalErrorMessage
        {
            get => _globalErrorMessage;
            set => SetProperty(ref _globalErrorMessage, value);
        }

        /// <summary>
        /// Whether validation is currently running
        /// </summary>
        public bool IsValidating
        {
            get => _isValidating;
            set => SetProperty(ref _isValidating, value);
        }

        /// <summary>
        /// Current validation status message
        /// </summary>
        public string ValidationStatus
        {
            get => _validationStatus;
            set => SetProperty(ref _validationStatus, value);
        }

        /// <summary>
        /// Color brush for validation status
        /// </summary>
        public Brush ValidationStatusBrush
        {
            get => _validationStatusBrush;
            set => SetProperty(ref _validationStatusBrush, value);
        }

        /// <summary>
        /// Filtered bus stops based on selected routes
        /// </summary>
        public ObservableCollection<string> FilteredBusStops
        {
            get => _filteredBusStops;
            set => SetProperty(ref _filteredBusStops, value);
        }

        /// <summary>
        /// Whether the save button should be enabled
        /// </summary>
        public bool CanSave
        {
            get => _canSave;
            set => SetProperty(ref _canSave, value);
        }

        /// <summary>
        /// Detailed list of validation errors to show the user what to fix
        /// </summary>
        public ObservableCollection<string> ValidationErrors => _validationErrors;

        /// <summary>
        /// True when there are validation errors to display in the UI
        /// </summary>
        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            set => SetProperty(ref _hasValidationErrors, value);
        }

        /// <summary>
        /// When true, skips address validation steps
        /// </summary>
        public bool DisableAddressValidation
        {
            get => _address.DisableValidation;
            set => _address.DisableValidation = value;
        }

        public ObservableCollection<PlaceAutocompleteSuggestion> AddressSuggestions => _address.Suggestions;

        public bool IsAddressAutocompleteEnabled => _address.IsAutocompleteEnabled;

        public bool IsAddressSuggestionPopupOpen => _address.IsPopupOpen;

        #endregion

        #region Commands

        public ICommand ValidateAddressCommand { get; private set; } = null!;
        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand SaveSchoolTimesCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;

        // AI and Enhancement Commands
        public ICommand SuggestRoutesCommand { get; private set; } = null!;
        public ICommand ViewOnMapCommand { get; private set; } = null!;
        public ICommand ImportCsvCommand { get; private set; } = null!;
        public ICommand ValidateDataCommand { get; private set; } = null!;
        public ICommand ClearGlobalErrorCommand { get; private set; } = null!;
        public ICommand SuggestNearestPickupStopCommand { get; private set; } = null!;
        public ICommand UseHomeAsPickupStopCommand { get; private set; } = null!;

        #endregion

        private static BusBuddyDbContext? TryCreateDbContextViaDi()
        {
            try
            {
                // Use the app’s DI container so we get the configured connection (BusBuddyDB)
                var sp = App.ServiceProvider;
                if (sp is null) return null;
                var factory = sp.GetService(typeof(IBusBuddyDbContextFactory)) as IBusBuddyDbContextFactory;
                return factory?.CreateDbContext();
            }
            catch
            {
                return null; // Fallback to parameterless DbContext when DI not available
            }
        }

        #region Command Initialization

        private CommunityToolkit.Mvvm.Input.AsyncRelayCommand? _saveRelay;

        private void InitializeCommands()
        {
            ValidateAddressCommand = new AsyncRelayCommand(() => _address.ValidateAsync(Student));
            // Make Save always executable; we gate inside SaveStudentAsync with validation.
            _saveRelay = new AsyncRelayCommand(SaveStudentAsync);
            SaveCommand = _saveRelay;
            SaveSchoolTimesCommand = new AsyncRelayCommand(SaveSchoolTimesAsync);
            CancelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteCancel);

            // AI and Enhancement Commands
            SuggestRoutesCommand = new AsyncRelayCommand(SuggestRoutesAsync);
            ViewOnMapCommand = new AsyncRelayCommand(ViewOnMapAsync);
            ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync);
            ValidateDataCommand = new AsyncRelayCommand(ValidateAllDataAsync);
            ClearGlobalErrorCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearGlobalError);
            SuggestNearestPickupStopCommand = new AsyncRelayCommand(SuggestNearestPickupStopAsync);
            UseHomeAsPickupStopCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(UseHomeAsPickupStop);
        }

        #endregion

        #region Command Handlers

        public Task RefreshAddressSuggestionsAsync(string? input) =>
            _address.RefreshSuggestionsAsync(input);

        public Task ApplyAddressSuggestionAsync(PlaceAutocompleteSuggestion? suggestion) =>
            _address.ApplySuggestionAsync(Student, suggestion);

        /// <summary>
        /// Save the student to the database
        /// </summary>
        private async Task SaveSchoolTimesAsync()
        {
            if (SelectedSchoolDestination is null)
            {
                ValidationStatus = "Select a school first";
                return;
            }

            TimeSpan? start = null;
            TimeSpan? dismissal = null;
            if (!string.IsNullOrWhiteSpace(SchoolStartTimeText))
            {
                if (!TimeSpan.TryParse(SchoolStartTimeText.Trim(), out var startParsed))
                {
                    ValidationStatus = "Start time must be HH:mm";
                    return;
                }

                start = startParsed;
            }

            if (!string.IsNullOrWhiteSpace(SchoolDismissalTimeText))
            {
                if (!TimeSpan.TryParse(SchoolDismissalTimeText.Trim(), out var dismissParsed))
                {
                    ValidationStatus = "Dismissal time must be HH:mm";
                    return;
                }

                dismissal = dismissParsed;
            }

            var destService = App.ServiceProvider?.GetService<IDestinationService>();
            if (destService is null)
            {
                ValidationStatus = "Destination service unavailable";
                return;
            }

            var ok = await destService.UpdateSchoolTimesAsync(
                SelectedSchoolDestination.DestinationId, start, dismissal).ConfigureAwait(true);
            if (!ok)
            {
                ValidationStatus = "Failed to save school times";
                return;
            }

            SelectedSchoolDestination.StartTime = start;
            SelectedSchoolDestination.DismissalTime = dismissal;
            ValidationStatus = "School times saved";

            var planner = App.ServiceProvider?.GetService<IRouteDeterminationService>();
            if (planner is not null && start.HasValue)
            {
                var regen = await planner
                    .RegenerateSchedulesForSchoolAsync(SelectedSchoolDestination.DestinationId)
                    .ConfigureAwait(true);
                ValidationStatus = regen.Success
                    ? $"School times saved; regenerated schedules on {regen.RoutesUpdated} route(s)"
                    : $"School times saved; schedule regen: {regen.Error}";
            }
        }

        private async Task SaveStudentAsync()
        {
            using (Serilog.Context.LogContext.PushProperty("Operation", "SaveStudent"))
            using (Serilog.Context.LogContext.PushProperty("StudentId", Student.StudentId))
            using (Serilog.Context.LogContext.PushProperty("EditMode", IsEditMode))
            {
            try
            {
                Logger.Information(
                    "Saving student Name={StudentName} Grade={Grade} DestinationId={DestinationId} School={School}",
                    Student.StudentName,
                    Student.Grade,
                    Student.DestinationId,
                    Student.School);
                ClearAllFieldErrors();

                // Hard guard: prevent saving with blank name (even if validation bypass flag is set)
                if (string.IsNullOrWhiteSpace(Student.StudentName))
                {
                    Logger.Warning("Blocked save — StudentName blank");
                    ReportFieldValidation([(StudentFormFields.StudentName, "Student name is required.")]);
                    return;
                }

                // Optional flag to bypass validation and allow saving immediately
                // Enable by setting environment variable: BUSBUDDY_SKIP_STUDENT_VALIDATION=1
                static bool ShouldSkipValidation()
                    => string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_SKIP_STUDENT_VALIDATION"), "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_SKIP_STUDENT_VALIDATION"), "true", StringComparison.OrdinalIgnoreCase);

                // Validate required fields
                if (!ShouldSkipValidation() && !IsValidStudent())
                {
                    var errors = GetValidationErrorsWithFields();
                    Logger.Information("Validation failed: {Errors}", errors.Select(e => e.Message));
                    ReportFieldValidation(errors);
                    return;
                }

                if (ShouldSkipValidation())
                {
                    _validationErrors.Clear();
                    HasValidationErrors = false;
                    Logger.Warning("Bypassing student validation due to BUSBUDDY_SKIP_STUDENT_VALIDATION flag");
                }
                else
                {
                    if (_studentService is not null)
                    {
                        var serviceErrors = await ValidateWithServiceAsync().ConfigureAwait(true);
                        if (serviceErrors.Count > 0)
                        {
                            Logger.Information("Service validation failed: {Errors}", serviceErrors.Select(e => e.Message));
                            ReportFieldValidation(serviceErrors);
                            return;
                        }
                    }

                    if (!DisableAddressValidation && !string.IsNullOrWhiteSpace(Student.HomeAddress))
                    {
                        await _address.ValidateAsync(Student).ConfigureAwait(true);
                        if (_address.ValidationFailed)
                        {
                            ValidationStatus = "Address could not be validated — you can still save.";
                            ValidationStatusBrush = Brushes.Orange;
                            Logger.Warning("Address validation failed before save; continuing (non-blocking)");
                        }
                    }
                }

                // Normalize loose inputs (format but don't block)
                Student.HomePhone = NormalizePhone(Student.HomePhone);
                Student.CellPhone = NormalizePhone(Student.CellPhone);
                Student.EmergencyPhone = NormalizePhone(Student.EmergencyPhone);
                Student.Zip = NormalizeZip(Student.Zip);
                StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(Student);

                if (!await DatabaseUserMessage.CanConnectAsync(_context).ConfigureAwait(true))
                {
                    SetGlobalError(DatabaseUserMessage.UnavailableForOperation("save the student"));
                    return;
                }

                // Set audit fields (UTC for Postgres timestamptz)
                if (IsEditMode)
                {
                    Student.UpdatedDate = DateTime.UtcNow;
                    Student.UpdatedBy = Environment.UserName;
                }
                else
                {
                    Student.CreatedDate = DateTime.UtcNow;
                    Student.CreatedBy = Environment.UserName;
                }

                StudentSchoolLinker.SyncDestinationFromSchoolName(Student, AvailableSchools.ToList());
                StudentRecordNormalizer.NormalizeForPersistence(Student);

                if (!string.IsNullOrWhiteSpace(Student.HomeAddress)
                    && (!Student.Latitude.HasValue || !Student.Longitude.HasValue))
                {
                    var geocoded = await _address.TryGeocodeAsync(Student).ConfigureAwait(true);
                    if (geocoded)
                    {
                        Logger.Information("Geocoded student address before save");
                    }
                }

                var studentService = _studentService ?? App.ServiceProvider?.GetService<IStudentService>();

                // Prefer StudentService when available (normal flow). If skipping validation,
                // avoid service-level validation and use direct EF save instead.
                if (studentService != null && !ShouldSkipValidation())
                {
                    if (IsEditMode)
                    {
                        var updated = await studentService.UpdateStudentAsync(Student);
                        if (!updated)
                        {
                            throw new InvalidOperationException("Update operation reported no changes.");
                        }
                    }
                    else
                    {
                        Student = await studentService.AddStudentAsync(Student);
                    }
                }
                else
                {
                    if (studentService is null)
                    {
                        Logger.Warning("IStudentService unavailable — saving student via direct EF (no service validation/geocode)");
                    }

                    // Fallback direct EF save if service not available
                    // or when skipping validation
                    if (IsEditMode)
                    {
                        _context.Students.Update(Student);
                    }
                    else
                    {
                        _context.Students.Add(Student);
                    }
                    await _context.SaveChangesAsync();
                }

                var persistencePath = studentService != null && !ShouldSkipValidation() ? "IStudentService" : "DirectEf";
                Logger.Information(
                    "Successfully saved student StudentId={StudentId} Name={StudentName} DestinationId={DestinationId} Latitude={Latitude} Longitude={Longitude} Persistence={PersistencePath}",
                    Student.StudentId,
                    Student.StudentName,
                    Student.DestinationId,
                    Student.Latitude,
                    Student.Longitude,
                    persistencePath);

                // Broadcast that a student has been saved so list views can refresh immediately
                try { WeakReferenceMessenger.Default.Send(new StudentSavedMessage(Student)); } catch { }

                // Close the form with success result
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving student StudentId={StudentId} Name={StudentName}", Student.StudentId, Student.StudentName);
                var message = DatabaseUserMessage.ForOperation(ex, "save the student");
                if (message.Contains("grade", StringComparison.OrdinalIgnoreCase))
                {
                    ReportFieldValidation([(StudentFormFields.Grade, message)]);
                }
                else if (message.Contains("DateTime", StringComparison.OrdinalIgnoreCase)
                         || message.Contains("timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    ReportFieldValidation([(StudentFormFields.DateOfBirth,
                        "A date field could not be saved. Clear Date of Birth or pick the date again, then retry.")]);
                }
                else if (message.Contains("route", StringComparison.OrdinalIgnoreCase))
                {
                    ReportFieldValidation([(StudentFormFields.AMRoute, message)]);
                }
                else if (message.Contains("ZIP", StringComparison.OrdinalIgnoreCase)
                         || message.Contains("zip", StringComparison.OrdinalIgnoreCase))
                {
                    ReportFieldValidation([(StudentFormFields.Zip, message)]);
                }
                else
                {
                    SetGlobalError(message);
                }
            }
            }
        }

        private static string? NormalizePhone(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
            {
                return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
            }
            return input; // leave as-is if not 10 digits
        }

        private static string? NormalizeZip(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length >= 5) return digits.Substring(0, 5);
            return digits;
        }

        private bool CanSaveStudent()
        {
            // Allow Save with minimal required fields only (name + grade).
            // Address fields are optional for Save to unblock CRUD flows.
            return !string.IsNullOrWhiteSpace(Student?.StudentName)
                && !string.IsNullOrWhiteSpace(Student?.Grade);
        }

        private void OnStudentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Re-evaluate save when key fields change; keep UI IsEnabled and command CanExecute aligned
            if (e.PropertyName == nameof(Core.Models.Student.StudentName) ||
                e.PropertyName == nameof(Core.Models.Student.Grade) ||
                e.PropertyName == nameof(Core.Models.Student.HomeAddress) ||
                e.PropertyName == nameof(Core.Models.Student.City) ||
                e.PropertyName == nameof(Core.Models.Student.State) ||
                e.PropertyName == nameof(Core.Models.Student.Zip) ||
                e.PropertyName == nameof(Core.Models.Student.RequiresSpecialNeedsBus))
            {
                if (e.PropertyName == nameof(Core.Models.Student.RequiresSpecialNeedsBus))
                {
                    _catalog.RefreshAvailableRoutes();
                }

                if (e.PropertyName == nameof(Core.Models.Student.HomeAddress))
                {
                    _ = _address.RefreshSuggestionsAsync(Student.HomeAddress);
                }

                _saveRelay?.NotifyCanExecuteChanged();
                CanSave = CanSaveStudent();
            }
        }

        private void ExecuteCancel()
        {
            try
            {
                Logger.Information("Cancel command executed");
                // Close the form with cancel result
                RequestClose?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing cancel command");
            }
        }

        /// <summary>
        /// Use xAI Grok to suggest optimal routes based on student address
        /// </summary>
        private async Task SuggestRoutesAsync()
        {
            try
            {
                Logger.Information("Starting AI route suggestion for student at {Address}", Student.HomeAddress);

                if (string.IsNullOrWhiteSpace(Student.HomeAddress))
                {
                    SetGlobalError("Please enter a home address before requesting route suggestions.");
                    return;
                }

                IsValidating = true;
                ValidationStatus = "Analyzing address with AI...";
                ValidationStatusBrush = Brushes.Orange;

                var suggestedRoutes = await GetAISuggestedRoutes(Student.HomeAddress, Student.City, Student.State);

                if (suggestedRoutes.Any())
                {
                    // Update suggested routes in UI
                    Student.AMRoute = suggestedRoutes.First();
                    if (suggestedRoutes.Count > 1)
                        Student.PMRoute = suggestedRoutes.Skip(1).First();

                    ValidationStatus = $"✓ AI suggested {suggestedRoutes.Count} optimal routes";
                    ValidationStatusBrush = Brushes.Green;

                    // Update filtered bus stops
                    await UpdateFilteredBusStops();
                }
                else
                {
                    ValidationStatus = "⚠️ No optimal routes found for this location";
                    ValidationStatusBrush = Brushes.Orange;
                }

                Logger.Information("AI route suggestion completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during AI route suggestion");
                SetGlobalError($"AI route suggestion failed: {ex.Message}");
                ValidationStatus = "❌ AI suggestion failed";
                ValidationStatusBrush = Brushes.Red;
            }
            finally
            {
                IsValidating = false;
            }
        }

        /// <summary>
        /// Open map view for student location (real coordinates only — never hash scatter).
        /// </summary>
        private async Task ViewOnMapAsync()
        {
            try
            {
                Logger.Information("Opening map view for student location");

                if (string.IsNullOrWhiteSpace(Student.HomeAddress))
                {
                    SetGlobalError("Please enter a home address before viewing on map.");
                    return;
                }

                IsValidating = true;
                ValidationStatus = "Loading map preview...";
                ValidationStatusBrush = Brushes.Blue;

                var sp = App.ServiceProvider;
                var mapsGeo = sp?.GetService<IMapsGeoService>();
                var mapVm = sp?.GetService<MapViewModel>();

                (double latitude, double longitude)? coords = null;
                if (Student.Latitude.HasValue && Student.Longitude.HasValue)
                {
                    coords = ((double)Student.Latitude.Value, (double)Student.Longitude.Value);
                }
                else if (mapsGeo is not null)
                {
                    coords = await mapsGeo.GeocodeAsync(Student.HomeAddress, Student.City, Student.State, Student.Zip);
                    if (coords.HasValue)
                    {
                        Student.Latitude = (decimal)coords.Value.latitude;
                        Student.Longitude = (decimal)coords.Value.longitude;
                    }
                }

                if (coords.HasValue && mapVm is not null)
                {
                    mapVm.PlotStop(coords.Value.latitude, coords.Value.longitude, new[] { Student.StudentName }, Student.StudentName);
                }

                new Window
                {
                    Title = "Student location",
                    Content = new MapView(),
                    Width = 1100,
                    Height = 750,
                    Owner = Application.Current?.MainWindow
                }.Show();

                if (coords.HasValue)
                {
                    ValidationStatus = "✓ Location plotted on map";
                    ValidationStatusBrush = Brushes.Green;
                }
                else if (mapsGeo is null || !mapsGeo.IsConfigured)
                {
                    ValidationStatus = "Mapping is not configured (missing GOOGLE_MAPS_API_KEY).";
                    ValidationStatusBrush = Brushes.Orange;
                }
                else
                {
                    ValidationStatus = "Address could not be geocoded — map opened without a pin.";
                    ValidationStatusBrush = Brushes.Orange;
                }

                Logger.Information(
                    "Map view opened for address: {Address}, {City}, {State} {Zip}",
                    Student.HomeAddress, Student.City, Student.State, Student.Zip);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening map view");
                SetGlobalError($"Map view failed: {ex.Message}");
                ValidationStatus = "❌ Map failed to load";
                ValidationStatusBrush = Brushes.Red;
            }
            finally
            {
                IsValidating = false;
            }
        }

        /// <summary>
        /// Import student data from a student CSV via <see cref="ISeedDataService"/>.
        /// </summary>
        private async Task ImportCsvAsync()
        {
            try
            {
                Logger.Information("Starting CSV import process");

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import students from CSV",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    CheckFileExists = true
                };
                if (dialog.ShowDialog() != true)
                {
                    ValidationStatus = "CSV import cancelled";
                    ValidationStatusBrush = Brushes.Gray;
                    return;
                }

                IsValidating = true;
                ValidationStatus = "Importing CSV data...";
                ValidationStatusBrush = Brushes.Blue;

                var factory = App.ServiceProvider?.GetService<IBusBuddyDbContextFactory>();
                var seed = App.ServiceProvider?.GetService<ISeedDataService>()
                    ?? (factory != null ? new SeedDataService(factory) : null);
                if (seed == null)
                {
                    ValidationStatus = "❌ Import unavailable (no seed service)";
                    ValidationStatusBrush = Brushes.Red;
                    return;
                }

                var added = await seed.ImportStudentsFromCsvAsync(dialog.FileName);
                ValidationStatus = added == 0
                    ? "No new students imported"
                    : $"✓ Imported {added} student(s)";
                ValidationStatusBrush = added == 0 ? Brushes.Gray : Brushes.Green;
                try { WeakReferenceMessenger.Default.Send(new StudentsImportedMessage(added)); } catch { }
                Logger.Information("CSV import completed Added={Added}", added);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during CSV import");
                SetGlobalError($"CSV import failed: {ex.Message}");
                ValidationStatus = "❌ Import failed";
                ValidationStatusBrush = Brushes.Red;
            }
            finally
            {
                IsValidating = false;
            }
        }

        /// <summary>
        /// Validate all student data including address geocoding
        /// </summary>
        private async Task ValidateAllDataAsync()
        {
            try
            {
                Logger.Information("Starting comprehensive data validation");

                // Avoid artificial delay in tests
                ValidationStatus = "Validating all data...";
                ValidationStatusBrush = Brushes.Orange;

                // Validate required fields
                var validationErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(Student.StudentName))
                    validationErrors.Add("Student name is required");

                if (string.IsNullOrWhiteSpace(Student.Grade))
                    validationErrors.Add("Grade is required");

                // Address fields are optional for Save. Do not flag as blocking errors.
                // You can still validate address via the dedicated Validate Address action.

                // Populate error list for UI
                _validationErrors.Clear();
                foreach (var err in validationErrors)
                {
                    _validationErrors.Add("• " + err);
                }
                HasValidationErrors = _validationErrors.Count > 0;

                if (validationErrors.Count > 0)
                {
                    SetGlobalError($"Please review: {string.Join(", ", validationErrors)}");
                }

                // Perform address validation unless disabled
                if (!DisableAddressValidation && !string.IsNullOrWhiteSpace(Student.HomeAddress))
                {
                    await _address.ValidateAsync(Student).ConfigureAwait(true);
                    if (_address.ValidationFailed)
                    {
                        validationErrors.Add(AddressValidationMessage);
                        _validationErrors.Add("• " + AddressValidationMessage);
                    }
                }
                else if (DisableAddressValidation)
                {
                    _address.DisableValidation = true;
                }

                if (validationErrors.Count > 0 || _address.ValidationFailed)
                {
                    HasValidationErrors = true;
                    ValidationStatus = $"❌ {validationErrors.Count} validation error(s)";
                    ValidationStatusBrush = Brushes.Red;
                    CanSave = false;
                    _saveRelay?.NotifyCanExecuteChanged();
                    Logger.Warning("Comprehensive validation failed: {Errors}", validationErrors);
                    return;
                }

                ValidationStatus = "✓ All data validated successfully";
                ValidationStatusBrush = Brushes.Green;
                CanSave = true;
                HasValidationErrors = false;
                _validationErrors.Clear();
                _saveRelay?.NotifyCanExecuteChanged();

                Logger.Information("Comprehensive data validation completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during comprehensive validation");
                SetGlobalError($"Validation failed: {ex.Message}");
                ValidationStatus = "❌ Validation failed";
                ValidationStatusBrush = Brushes.Red;
                CanSave = false;
                HasValidationErrors = true;
            }
            finally
            {
                IsValidating = false;
            }
        }

        /// <summary>
        /// Clear the global error message and field highlights.
        /// </summary>
        private void ClearGlobalError()
        {
            HasGlobalError = false;
            GlobalErrorMessage = string.Empty;
            ClearAllFieldErrors();
        }

        /// <summary>
        /// Set a global error message (non-blocking banner; no modal).
        /// </summary>
        private void SetGlobalError(string message)
        {
            GlobalErrorMessage = message;
            HasGlobalError = true;
            Logger.Warning("Global error set: {Message}", message);
        }

        /// <summary>Clear one field error when the operator edits that control.</summary>
        public void ClearFieldError(string fieldKey)
        {
            if (string.IsNullOrWhiteSpace(fieldKey) || !_fieldErrors.Remove(fieldKey))
            {
                return;
            }

            switch (fieldKey)
            {
                case StudentFormFields.StudentName:
                    StudentNameFieldError = null;
                    break;
                case StudentFormFields.Grade:
                    GradeFieldError = null;
                    break;
            }

            OnPropertyChanged(nameof(FieldErrors));
            OnPropertyChanged(nameof(HasStudentNameFieldError));
            OnPropertyChanged(nameof(HasGradeFieldError));
            if (_fieldErrors.Count > 0)
            {
                return;
            }

            _validationErrors.Clear();
            HasValidationErrors = false;
            if (HasGlobalError)
            {
                HasGlobalError = false;
                GlobalErrorMessage = string.Empty;
            }
        }

        private void ClearAllFieldErrors()
        {
            if (_fieldErrors.Count == 0 && StudentNameFieldError is null && GradeFieldError is null)
            {
                return;
            }

            _fieldErrors.Clear();
            StudentNameFieldError = null;
            GradeFieldError = null;
            OnPropertyChanged(nameof(FieldErrors));
            OnPropertyChanged(nameof(HasStudentNameFieldError));
            OnPropertyChanged(nameof(HasGradeFieldError));
            _validationErrors.Clear();
            HasValidationErrors = false;
        }

        private void ReportFieldValidation(IReadOnlyList<(string FieldKey, string Message)> errors)
        {
            _fieldErrors.Clear();
            _validationErrors.Clear();
            StudentNameFieldError = null;
            GradeFieldError = null;

            foreach (var (fieldKey, message) in errors)
            {
                _fieldErrors[fieldKey] = message;
                _validationErrors.Add("• " + message);
                switch (fieldKey)
                {
                    case StudentFormFields.StudentName:
                        StudentNameFieldError = message;
                        break;
                    case StudentFormFields.Grade:
                        GradeFieldError = message;
                        break;
                }
            }

            HasValidationErrors = errors.Count > 0;
            OnPropertyChanged(nameof(FieldErrors));
            OnPropertyChanged(nameof(HasStudentNameFieldError));
            OnPropertyChanged(nameof(HasGradeFieldError));

            if (errors.Count == 0)
            {
                return;
            }

            GlobalErrorMessage = errors[0].Message;
            HasGlobalError = true;
            RequestFocusField?.Invoke(this, errors[0].FieldKey);
        }

        /// <summary>
        /// Get AI-suggested routes based on address
        /// </summary>
        private async Task<List<string>> GetAISuggestedRoutes(string? address, string? city, string? state)
        {
            var routes = new List<string>();
            var sp = App.ServiceProvider;
            if (sp is not null)
            {
                using var scope = sp.CreateScope();
                var routeService = scope.ServiceProvider.GetService<IRouteService>();
                if (routeService is not null)
                {
                    var result = await routeService.GetAllActiveRoutesAsync();
                    if (result.IsSuccess && result.Value is not null)
                    {
                        var citySafe = city ?? string.Empty;
                        routes.AddRange(result.Value
                            .Select(r => r.RouteName)
                            .Where(n => !string.IsNullOrWhiteSpace(n) &&
                                        (string.IsNullOrWhiteSpace(citySafe) ||
                                         n!.Contains(citySafe, StringComparison.OrdinalIgnoreCase)))
                            .Take(2)!);
                        if (routes.Count == 0)
                        {
                            routes.AddRange(result.Value.Select(r => r.RouteName).Where(n => !string.IsNullOrWhiteSpace(n)).Take(2)!);
                        }
                    }
                }
            }

            return routes;
        }

        /// <summary>
        /// Update filtered bus stops based on selected routes
        /// </summary>
        private async Task UpdateFilteredBusStops()
        {
            await Task.CompletedTask; // No artificial delay

            FilteredBusStops.Clear();

            // Add bus stops based on selected routes
            if (!string.IsNullOrEmpty(Student.AMRoute))
            {
                FilteredBusStops.Add($"{Student.AMRoute} - Stop A");
                FilteredBusStops.Add($"{Student.AMRoute} - Stop B");
            }

            if (!string.IsNullOrEmpty(Student.PMRoute) && Student.PMRoute != Student.AMRoute)
            {
                FilteredBusStops.Add($"{Student.PMRoute} - Stop A");
                FilteredBusStops.Add($"{Student.PMRoute} - Stop B");
            }
        }

        #endregion

        private async Task SuggestNearestPickupStopAsync()
        {
            if (Student.Latitude is not decimal lat || Student.Longitude is not decimal lon)
            {
                PickupStopHint = "Validate the home address first to suggest a nearby catalog stop.";
                return;
            }

            var stopService = App.ServiceProvider?.GetService<IPickupStopService>();
            if (stopService is null)
            {
                PickupStopHint = "Pickup stop service is not available.";
                return;
            }

            var settings = App.ServiceProvider?
                .GetService<Microsoft.Extensions.Options.IOptions<BusBuddy.Core.Configuration.RoutingDistrictSettings>>()?
                .Value;
            var maxMeters = settings?.StopSuggestMaxMeters ?? 400;

            var nearest = await stopService.FindNearestAsync(
                (double)lat, (double)lon, maxMeters).ConfigureAwait(true);
            if (nearest is null)
            {
                PickupStopHint = $"No catalog stop within {maxMeters:F0} m — use home as stop or add a pickup stop.";
                return;
            }

            SelectedPickupStop = nearest;
            PickupStopHint = $"Suggested {nearest.Name} (within {maxMeters:F0} m of home).";
        }

        private void UseHomeAsPickupStop()
        {
            SelectedPickupStop = null;
            Student.PickupStopId = null;
            Student.BusStop = "Home address";
            PickupStopHint = "Using home address as pickup stop (rural / driveway).";
            OnPropertyChanged(nameof(UsesHomeAsPickupStop));
        }

        #region Validation Helpers

        /// <summary>
        /// Minimal validation for Save — only ensure required fields are present.
        /// Detailed address checks are available via the Validate actions and should not block Save.
        /// </summary>
        private bool IsValidStudent()
        {
            try
            {
                Logger.Debug("Validating: Name={Name}, Grade={Grade}, Address={Address}, City={City}, State={State}, Zip={Zip}",
                    Student?.StudentName, Student?.Grade, Student?.HomeAddress, Student?.City, Student?.State, Student?.Zip);
            }
            catch { /* logging best-effort */ }
            // Only enforce Name and Grade for Save; address fields are optional.
            if (string.IsNullOrWhiteSpace(Student.StudentName)) return false;
            if (string.IsNullOrWhiteSpace(Student.Grade)) return false;

            _address.SetMinimalFieldsPresentMessage();
            return true;
        }

        /// <summary>
        /// Build validation errors with field keys for inline highlighting and focus.
        /// </summary>
        private List<(string FieldKey, string Message)> GetValidationErrorsWithFields()
        {
            var errors = new List<(string FieldKey, string Message)>();
            if (string.IsNullOrWhiteSpace(Student.StudentName))
            {
                errors.Add((StudentFormFields.StudentName, "Student name is required."));
            }

            if (string.IsNullOrWhiteSpace(Student.Grade))
            {
                errors.Add((StudentFormFields.Grade, "Grade is required."));
            }

            return errors;
        }

        /// <summary>Run service-layer rules before persist so VM and DB stay aligned.</summary>
        private async Task<List<(string FieldKey, string Message)>> ValidateWithServiceAsync()
        {
            if (_studentService is null)
            {
                return [];
            }

            var messages = await _studentService.ValidateStudentAsync(Student).ConfigureAwait(true);
            return messages.Select(MapServiceErrorToField).ToList();
        }

        private static (string FieldKey, string Message) MapServiceErrorToField(string message)
        {
            if (message.Contains("AM Route", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.AMRoute, message);
            }

            if (message.Contains("PM Route", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.PMRoute, message);
            }

            if (message.Contains("ZIP", StringComparison.OrdinalIgnoreCase)
                || message.Contains("zip code", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.Zip, message);
            }

            if (message.Contains("home phone", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.HomePhone, message);
            }

            if (message.Contains("emergency phone", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.EmergencyPhone, message);
            }

            if (message.Contains("grade", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.Grade, message);
            }

            if (message.Contains("name", StringComparison.OrdinalIgnoreCase))
            {
                return (StudentFormFields.StudentName, message);
            }

            return (StudentFormFields.HomeAddress, message);
        }

        /// <summary>
        /// Build a list of validation errors for diagnostics when Save fails.
        /// </summary>
        private List<string> GetValidationErrors()
        {
            return GetValidationErrorsWithFields().Select(e => e.Message).ToList();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try { if (_student != null) _student.PropertyChanged -= OnStudentPropertyChanged; } catch { }
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }
            _address.Dispose();
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
