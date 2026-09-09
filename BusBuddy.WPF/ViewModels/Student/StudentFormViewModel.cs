using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Messages;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student
{
    /// <summary>
    /// ViewModel for the StudentForm — adds and edits one student.
    /// <para>
    /// This type owns the form's bindable surface and nothing else. Address validation, catalog and
    /// pickup-stop selection, save, validation, mapping, and CSV import each live in their own
    /// coordinator in this folder; the properties below re-publish coordinator state so
    /// <c>StudentForm.xaml</c> keeps binding to a single DataContext.
    /// </para>
    /// </summary>
    public class StudentFormViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<StudentFormViewModel>();

        private readonly BusBuddyDbContext _context;
        private readonly StudentFormAddressCoordinator _address;
        private readonly StudentFormCatalogCoordinator _catalog;
        private readonly StudentFormValidationCoordinator _validation;
        private readonly StudentFormSaveCoordinator _save;
        private readonly StudentFormRouteSuggestionCoordinator _routeSuggestions;
        private readonly StudentFormMapCoordinator _map;
        private readonly StudentFormCsvImportCoordinator _csvImport;
        private readonly StudentFormSchoolTimesLauncher _schoolTimes;
        private readonly List<(string Name, bool IsSpecialNeeds)> _routeCatalog = new();

        private Core.Models.Student _student;
        private Destination? _selectedSchoolDestination;
        private string _formTitle = "Add New Student";
        private bool _isEditMode;
        private AsyncRelayCommand? _saveRelay;

        /// <summary>Event to request the form to close.</summary>
        public event EventHandler<bool?>? RequestClose;

        /// <summary>Raised when validation should move keyboard focus to a named form field.</summary>
        public event EventHandler<string>? RequestFocusField;

        public StudentFormViewModel()
            : this(null, new Core.Models.Student(), enableValidation: true, null, null) { }

        public StudentFormViewModel(Core.Models.Student? student, bool enableValidation = true)
            : this(null, student, enableValidation, null, null) { }

        public StudentFormViewModel(IStudentService studentService)
            : this(studentService, null, enableValidation: true, null, null) { }

        public StudentFormViewModel(
            IStudentService studentService,
            Core.Models.Student? student,
            bool enableValidation = true)
            : this(studentService, student, enableValidation, null, null) { }

        public StudentFormViewModel(
            IStudentService studentService,
            bool enableValidation,
            IMapsGeoService? mapsGeoService,
            IPlacesAutocompleteService? placesAutocomplete = null)
            : this(studentService, null, enableValidation, mapsGeoService, placesAutocomplete) { }

        public StudentFormViewModel(
            IStudentService? studentService,
            Core.Models.Student? student,
            bool enableValidation,
            IMapsGeoService? mapsGeoService,
            IPlacesAutocompleteService? placesAutocomplete)
        {
            var services = App.ServiceProvider;
            _address = new StudentFormAddressCoordinator(
                mapsGeoService ?? services?.GetService<IMapsGeoService>(),
                placesAutocomplete ?? services?.GetService<IPlacesAutocompleteService>(),
                studentService,
                loadedStudent: student);
            _context = StudentFormBootstrap.TryCreateDbContextViaDi() ?? new BusBuddyDbContext();
            _address.DisableValidation = !enableValidation;

            _student = student ?? StudentFormBootstrap.NewIntakeRecord();
            _isEditMode = student != null && student.StudentId > 0;
            _formTitle = _isEditMode ? "Edit Student" : "Add New Student · UX v3 2026-09-02";

            AvailableRoutes = new ObservableCollection<string>();
            AvailablePickupStops = new ObservableCollection<PickupStop>();
            AvailableSchools = new ObservableCollection<Destination>();

            _catalog = new StudentFormCatalogCoordinator(
                _context,
                _student,
                _routeCatalog,
                AvailableRoutes,
                AvailablePickupStops,
                AvailableSchools,
                school => SelectedSchoolDestination = school);
            _validation = new StudentFormValidationCoordinator(
                () => Student,
                studentService,
                _address,
                () => _saveRelay?.NotifyCanExecuteChanged());
            _save = new StudentFormSaveCoordinator(
                () => Student,
                saved => Student = saved,
                _context,
                studentService,
                _address,
                _validation,
                () => IsEditMode,
                AvailableSchools,
                () => RequestClose?.Invoke(this, true));
            _routeSuggestions = new StudentFormRouteSuggestionCoordinator(() => Student, _validation);
            _map = new StudentFormMapCoordinator(
                () => Student,
                () => SelectedPickupStop,
                AvailablePickupStops,
                _validation,
                _address.TrackPinnedAddress);
            _csvImport = new StudentFormCsvImportCoordinator(_validation);
            _schoolTimes = new StudentFormSchoolTimesLauncher(() => SelectedSchoolDestination, _validation);

            WireCoordinators();
            try { _student.PropertyChanged += OnStudentPropertyChanged; } catch { }
            InitializeCommands();
            _ = _catalog.LoadAllAsync();
        }

        #region Properties

        /// <summary>Student being edited or added.</summary>
        public Core.Models.Student Student
        {
            get => _student;
            set
            {
                if (!SetProperty(ref _student, value))
                {
                    return;
                }

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

                _validation.SetCanSave(StudentFormSaveCoordinator.HasMinimumFields(_student));
            }
        }

        /// <summary>Form title (Add New Student or Edit Student).</summary>
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }

        /// <summary>Whether form is in edit mode (vs add mode).</summary>
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        /// <summary>Selected campus; syncs Student.School and Student.DestinationId.</summary>
        public Destination? SelectedSchoolDestination
        {
            get => _selectedSchoolDestination;
            set
            {
                if (SetProperty(ref _selectedSchoolDestination, value))
                {
                    Student.School = value?.Name;
                    Student.DestinationId = value?.DestinationId;
                }
            }
        }

        /// <summary>Available route names for assignment.</summary>
        public ObservableCollection<string> AvailableRoutes { get; }

        /// <summary>District pickup stop catalog (shared corners / blocks).</summary>
        public ObservableCollection<PickupStop> AvailablePickupStops { get; }

        /// <summary>Active school Destinations for intake assignment (home-to-school routing).</summary>
        public ObservableCollection<Destination> AvailableSchools { get; }

        public IReadOnlyList<string> AvailableGrades => StudentFormPickLists.Grades;

        public IReadOnlyList<string> AvailableStates => StudentFormPickLists.States;

        // --- Address coordinator surface ---
        public string AddressValidationMessage => _address.ValidationMessage;
        public Brush AddressValidationColor => _address.ValidationColor;
        public ObservableCollection<PlaceAutocompleteSuggestion> AddressSuggestions => _address.Suggestions;
        public bool IsAddressAutocompleteEnabled => _address.IsAutocompleteEnabled;
        public bool IsAddressSuggestionPopupOpen => _address.IsPopupOpen;

        /// <summary>Suppresses interactive Places autocomplete only — never validation on save.</summary>
        public bool DisableAddressValidation
        {
            get => _address.DisableValidation;
            set => _address.DisableValidation = value;
        }

        // --- Catalog coordinator surface (PickupMode stays derived: no stop means home pickup) ---
        public PickupStop? SelectedPickupStop
        {
            get => _catalog.SelectedPickupStop;
            set => _catalog.SelectedPickupStop = value;
        }

        public bool UsesHomeAsPickupStop => _catalog.UsesHomeAsPickupStop;
        public string PickupStopHint => _catalog.PickupStopHint;

        // --- Validation coordinator surface ---
        public bool HasGlobalError { get => _validation.HasGlobalError; set => _validation.HasGlobalError = value; }
        public string GlobalErrorMessage { get => _validation.GlobalErrorMessage; set => _validation.GlobalErrorMessage = value; }
        public bool IsValidating { get => _validation.IsValidating; set => _validation.IsValidating = value; }
        public string ValidationStatus { get => _validation.ValidationStatus; set => _validation.ValidationStatus = value; }
        public Brush ValidationStatusBrush { get => _validation.ValidationStatusBrush; set => _validation.ValidationStatusBrush = value; }
        public bool HasValidationErrors { get => _validation.HasValidationErrors; set => _validation.HasValidationErrors = value; }

        /// <summary>Whether the save button should be enabled.</summary>
        public bool CanSave { get => _validation.CanSave; set => _validation.CanSave = value; }

        /// <summary>Detailed list of validation errors to show the user what to fix.</summary>
        public ObservableCollection<string> ValidationErrors => _validation.ValidationErrors;

        /// <summary>Per-field validation messages keyed by <see cref="StudentFormFields"/>.</summary>
        public IReadOnlyDictionary<string, string> FieldErrors => _validation.FieldErrors;

        public string? StudentNameFieldError => _validation.StudentNameFieldError;
        public bool HasStudentNameFieldError => _validation.HasStudentNameFieldError;
        public string? GradeFieldError => _validation.GradeFieldError;
        public bool HasGradeFieldError => _validation.HasGradeFieldError;

        #endregion

        #region Commands

        public ICommand ValidateAddressCommand { get; private set; } = null!;
        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand EditSchoolTimesCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;
        public ICommand SuggestRoutesCommand { get; private set; } = null!;
        public ICommand ViewOnMapCommand { get; private set; } = null!;
        public ICommand ImportCsvCommand { get; private set; } = null!;
        public ICommand ValidateDataCommand { get; private set; } = null!;
        public ICommand ClearGlobalErrorCommand { get; private set; } = null!;
        public ICommand SuggestNearestPickupStopCommand { get; private set; } = null!;
        public ICommand UseHomeAsPickupStopCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            ValidateAddressCommand = new AsyncRelayCommand(() => _address.ValidateAsync(Student));
            // Save stays always-executable; SaveAsync reports what is missing rather than going dead.
            _saveRelay = new AsyncRelayCommand(_save.SaveAsync);
            SaveCommand = _saveRelay;
            EditSchoolTimesCommand = new RelayCommand(_schoolTimes.OpenForSelectedSchool);
            CancelCommand = new RelayCommand(ExecuteCancel);
            SuggestRoutesCommand = new AsyncRelayCommand(_routeSuggestions.SuggestRoutesAsync);
            ViewOnMapCommand = new AsyncRelayCommand(_map.ViewOnMapAsync);
            ImportCsvCommand = new AsyncRelayCommand(_csvImport.ImportCsvAsync);
            ValidateDataCommand = new AsyncRelayCommand(_validation.ValidateAllDataAsync);
            ClearGlobalErrorCommand = new RelayCommand(_validation.ClearGlobalError);
            SuggestNearestPickupStopCommand = new AsyncRelayCommand(_catalog.SuggestNearestPickupStopAsync);
            UseHomeAsPickupStopCommand = new RelayCommand(_catalog.UseHomeAsPickupStop);
        }

        #endregion

        #region Code-behind entry points

        public Task ApplyAddressSuggestionAsync(PlaceAutocompleteSuggestion? suggestion) =>
            _address.ApplySuggestionAsync(Student, suggestion);

        /// <summary>Clear one field error when the operator edits that control.</summary>
        public void ClearFieldError(string fieldKey) => _validation.ClearFieldError(fieldKey);

        #endregion

        /// <summary>Re-publish coordinator state under the property names StudentForm.xaml binds.</summary>
        private void WireCoordinators()
        {
            _address.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName switch
            {
                nameof(StudentFormAddressCoordinator.ValidationMessage) => nameof(AddressValidationMessage),
                nameof(StudentFormAddressCoordinator.ValidationColor) => nameof(AddressValidationColor),
                nameof(StudentFormAddressCoordinator.IsPopupOpen) => nameof(IsAddressSuggestionPopupOpen),
                nameof(StudentFormAddressCoordinator.IsAutocompleteEnabled) => nameof(IsAddressAutocompleteEnabled),
                nameof(StudentFormAddressCoordinator.DisableValidation) => nameof(DisableAddressValidation),
                _ => null,
            });
            _address.CoordinatesUpdated += (_, _) => _ = _catalog.SuggestNearestPickupStopAsync();

            // Catalog and validation property names already match the bound names on this view model.
            _catalog.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
            _validation.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
            _validation.RequestFocusField += (_, fieldKey) => RequestFocusField?.Invoke(this, fieldKey);

            WeakReferenceMessenger.Default.Register<StudentFormViewModel, PickupStopCatalogChangedMessage>(
                this,
                async (r, _) => await r._catalog.LoadPickupStopsAsync().ConfigureAwait(true));
            WeakReferenceMessenger.Default.Register<StudentFormViewModel, SchoolCatalogChangedMessage>(
                this,
                async (r, _) => await r._catalog.LoadSchoolsAsync().ConfigureAwait(true));
        }

        private void OnStudentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Re-evaluate save when key fields change; keep UI IsEnabled and command CanExecute aligned
            if (e.PropertyName is not (nameof(Core.Models.Student.StudentName)
                or nameof(Core.Models.Student.Grade)
                or nameof(Core.Models.Student.HomeAddress)
                or nameof(Core.Models.Student.City)
                or nameof(Core.Models.Student.State)
                or nameof(Core.Models.Student.Zip)
                or nameof(Core.Models.Student.RequiresSpecialNeedsBus)))
            {
                return;
            }

            if (e.PropertyName == nameof(Core.Models.Student.RequiresSpecialNeedsBus))
            {
                _catalog.RefreshAvailableRoutes();
            }

            if (e.PropertyName == nameof(Core.Models.Student.HomeAddress))
            {
                _ = _address.RefreshSuggestionsAsync(Student.HomeAddress);
            }

            _validation.SetCanSave(StudentFormSaveCoordinator.HasMinimumFields(Student));
        }

        private void ExecuteCancel()
        {
            try
            {
                Logger.Information("Cancel command executed");
                RequestClose?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing cancel command");
            }
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName is null)
            {
                return;
            }

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

        public void Dispose()
        {
            try { if (_student != null) _student.PropertyChanged -= OnStudentPropertyChanged; } catch { }
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }
            _address.Dispose();
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
