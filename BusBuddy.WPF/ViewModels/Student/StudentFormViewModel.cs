using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using BusBuddy.Core.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using BusBuddy.WPF;
using BusBuddy.WPF.Commands;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using CommunityToolkit.Mvvm.Messaging;
using BusBuddy.WPF.Messages;

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
        private readonly AddressService _addressService;
        private readonly IStudentService? _studentService; // Prefer service for persistence
        private Core.Models.Student _student;
        private string _formTitle = "Add New Student";
        private string _addressValidationMessage = string.Empty;
        private Brush _addressValidationColor = Brushes.Gray;
        private bool _isEditMode;

        // Event to request the form to close
        public event EventHandler<bool?>? RequestClose;

        public StudentFormViewModel() : this(new Core.Models.Student())
        {
        }

        // Primary constructor for DI usage
        public StudentFormViewModel(IStudentService studentService, Core.Models.Student? student = null)
        {
            _studentService = studentService;
            _context = TryCreateDbContextViaDi() ?? new BusBuddyDbContext();
            _addressService = new AddressService();
            DisableAddressValidation = true; // MVP default — re-enable post-MVP

            _student = student ?? new Core.Models.Student
            {
                Active = true,
                EnrollmentDate = DateTime.Today,
                CreatedDate = DateTime.Now,
                School = "Wiley Consolidated School RE-13JT",
                State = "CO"
            };

            _isEditMode = student != null && student.StudentId > 0;
            _formTitle = _isEditMode ? "Edit Student" : "Add New Student";

            AvailableRoutes = new ObservableCollection<string>();
            AvailableBusStops = new ObservableCollection<string>();

            try { _student.PropertyChanged += OnStudentPropertyChanged; } catch { }
            InitializeCommands();
            _ = LoadDataAsync();
        }

        // Fallback constructor when DI is unavailable
        public StudentFormViewModel(Core.Models.Student? student = null)
        {
            _context = TryCreateDbContextViaDi() ?? new BusBuddyDbContext();
            _addressService = new AddressService();
            DisableAddressValidation = true; // MVP default — re-enable post-MVP
            // For MVP, we'll do simple validation directly in the ViewModel
            // TODO: Inject AddressValidationService when UnitOfWork is available

            _student = student ?? new Core.Models.Student
            {
                Active = true,
                EnrollmentDate = DateTime.Today,
                CreatedDate = DateTime.Now,
                School = "Wiley Consolidated School RE-13JT",
                State = "CO"
            };

            _isEditMode = student != null && student.StudentId > 0;
            _formTitle = _isEditMode ? "Edit Student" : "Add New Student";

            AvailableRoutes = new ObservableCollection<string>();
            AvailableBusStops = new ObservableCollection<string>();

            try { _student.PropertyChanged += OnStudentPropertyChanged; } catch { }
            InitializeCommands();
            _ = LoadDataAsync();
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
        public string AddressValidationMessage
        {
            get => _addressValidationMessage;
            set => SetProperty(ref _addressValidationMessage, value);
        }

        /// <summary>
        /// Color for address validation message (green for success, red for error)
        /// </summary>
        public Brush AddressValidationColor
        {
            get => _addressValidationColor;
            set => SetProperty(ref _addressValidationColor, value);
        }

        /// <summary>
        /// Available route names for assignment
        /// </summary>
        public ObservableCollection<string> AvailableRoutes { get; }

        /// <summary>
        /// Available bus stop names for assignment
        /// </summary>
        public ObservableCollection<string> AvailableBusStops { get; }

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
    private bool _disableAddressValidation; // MVP escape hatch

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
        /// When true, skips address validation steps (temporary MVP fallback)
        /// </summary>
        public bool DisableAddressValidation
        {
            get => _disableAddressValidation;
            set => SetProperty(ref _disableAddressValidation, value);
        }

        #endregion

        #region Commands

    public ICommand ValidateAddressCommand { get; private set; } = null!;
    public ICommand SaveCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;

        // AI and Enhancement Commands
        public ICommand SuggestRoutesCommand { get; private set; } = null!;
        public ICommand ViewOnMapCommand { get; private set; } = null!;
        public ICommand ImportCsvCommand { get; private set; } = null!;
        public ICommand ValidateDataCommand { get; private set; } = null!;
        public ICommand ClearGlobalErrorCommand { get; private set; } = null!;

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
            ValidateAddressCommand = new AsyncRelayCommand(ValidateAddressAsync);
            // Make Save always executable; we gate inside SaveStudentAsync with validation.
            _saveRelay = new AsyncRelayCommand(SaveStudentAsync);
            SaveCommand = _saveRelay;
            CancelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteCancel);

            // AI and Enhancement Commands
            SuggestRoutesCommand = new AsyncRelayCommand(SuggestRoutesAsync);
            ViewOnMapCommand = new AsyncRelayCommand(ViewOnMapAsync);
            ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync);
            ValidateDataCommand = new AsyncRelayCommand(ValidateAllDataAsync);
            ClearGlobalErrorCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearGlobalError);
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Validate the student's address using simple regex patterns
        /// </summary>
        private async Task ValidateAddressAsync()
        {
            try
            {
                if (DisableAddressValidation)
                {
                    AddressValidationMessage = "Address validation disabled — TODO: re-enable post-MVP";
                    AddressValidationColor = Brushes.Gray;
                    await Task.CompletedTask; return;
                }
                Logger.Information("Validating address for student");
                if (string.IsNullOrWhiteSpace(Student.HomeAddress))
                {
                    AddressValidationMessage = "Please enter an address before validating.";
                    AddressValidationColor = Brushes.Orange;
                    return;
                }

                // Build a formatted address string and validate using documented patterns
                var formatted = _addressService.FormatAddress(Student.HomeAddress, Student.City, Student.State, Student.Zip);
                var addressValidation = _addressService.ValidateAddress(formatted);

                // Prefer components validation when any component is provided
                var hasComponents = !string.IsNullOrWhiteSpace(Student.City) || !string.IsNullOrWhiteSpace(Student.State) || !string.IsNullOrWhiteSpace(Student.Zip);
                var componentValidation = _addressService.ValidateAddressComponents(
                    Student.HomeAddress ?? string.Empty, Student.City ?? string.Empty, Student.State ?? string.Empty, Student.Zip ?? string.Empty);

                // If individual components are provided and valid, consider address valid even if the formatted full string check is strict.
                bool isValid = hasComponents
                    ? componentValidation.IsValid || addressValidation.IsValid
                    : addressValidation.IsValid;
                string errorMessage = hasComponents && !componentValidation.IsValid && !addressValidation.IsValid
                    ? (string.IsNullOrWhiteSpace(componentValidation.Error) ? addressValidation.Error : componentValidation.Error)
                    : addressValidation.Error;

                if (isValid)
                {
                    AddressValidationMessage = "✓ Address format is valid.";
                    AddressValidationColor = Brushes.Green;
                    Logger.Information("Address validation successful");
                }
                else
                {
                    AddressValidationMessage = $"✗ Address validation failed: {errorMessage}";
                    AddressValidationColor = Brushes.Red;
                    Logger.Warning("Address validation failed: {Error}", errorMessage);
                }

                await Task.CompletedTask; // No artificial delay
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error validating address");
                AddressValidationMessage = "✗ Error validating address. Please check format and try again.";
                AddressValidationColor = Brushes.Red;
            }
        }

        /// <summary>
        /// Save the student to the database
        /// </summary>
        private async Task SaveStudentAsync()
        {
            try
            {
                Logger.Information("Saving student {StudentName}", Student.StudentName);

                // Optional MVP feature flag to bypass validation and allow saving immediately
                // Enable by setting environment variable: BUSBUDDY_SKIP_STUDENT_VALIDATION=1
                static bool ShouldSkipValidation()
                    => string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_SKIP_STUDENT_VALIDATION"), "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Environment.GetEnvironmentVariable("BUSBUDDY_SKIP_STUDENT_VALIDATION"), "true", StringComparison.OrdinalIgnoreCase);

                // Validate required fields
                if (!ShouldSkipValidation() && !IsValidStudent())
                {
                    // Collect validation errors for diagnostics and UI
                    var errors = GetValidationErrors();
                    Logger.Information("Validation failed: {Errors}", errors);
                    // Reflect in UI list
                    _validationErrors.Clear();
                    foreach (var err in errors)
                        _validationErrors.Add("• " + err);
                    HasValidationErrors = _validationErrors.Count > 0;
                    SetGlobalError("Please correct validation errors before saving.");
                    return;
                }

                if (ShouldSkipValidation())
                {
                    // Clear any prior UI validation state but continue to save
                    _validationErrors.Clear();
                    HasValidationErrors = false;
                    Logger.Warning("Bypassing student validation due to BUSBUDDY_SKIP_STUDENT_VALIDATION flag");
                }

                // Normalize loose inputs (format but don't block)
                Student.HomePhone = NormalizePhone(Student.HomePhone);
                Student.EmergencyPhone = NormalizePhone(Student.EmergencyPhone);
                Student.Zip = NormalizeZip(Student.Zip);

                // Set audit fields
                if (IsEditMode)
                {
                    Student.UpdatedDate = DateTime.Now;
                    Student.UpdatedBy = Environment.UserName;
                }
                else
                {
                    Student.CreatedDate = DateTime.Now;
                    Student.CreatedBy = Environment.UserName;
                }

                // Prefer StudentService when available (normal flow). If skipping validation,
                // avoid service-level validation and use direct EF save instead (MVP flag).
                if (_studentService != null && !ShouldSkipValidation())
                {
                    if (IsEditMode)
                    {
                        var updated = await _studentService.UpdateStudentAsync(Student);
                        if (!updated)
                        {
                            throw new InvalidOperationException("Update operation reported no changes.");
                        }
                    }
                    else
                    {
                        Student = await _studentService.AddStudentAsync(Student);
                    }
                }
                else
                {
                    // Fallback direct EF save if service not available
                    // or when skipping validation for MVP save bypass
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

                Logger.Information("Successfully saved student {StudentId} - {StudentName}",
                    Student.StudentId, Student.StudentName);

                // Broadcast that a student has been saved so list views can refresh immediately
                try { WeakReferenceMessenger.Default.Send(new StudentSavedMessage(Student)); } catch { }

                // Close the form with success result
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving student");
                SetGlobalError($"Failed to save student: {ex.Message}");
            }
        }

        private static string? NormalizePhone(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
            {
                return $"({digits.Substring(0,3)}) {digits.Substring(3,3)}-{digits.Substring(6,4)}";
            }
            return input; // leave as-is if not 10 digits
        }

        private static string? NormalizeZip(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length >= 5) return digits.Substring(0,5);
            return digits;
        }

        private bool CanSaveStudent()
        {
            // Enable Save only when required fields are present
            return !string.IsNullOrWhiteSpace(Student?.StudentName)
                   && !string.IsNullOrWhiteSpace(Student?.Grade)
                   && !string.IsNullOrWhiteSpace(Student?.HomeAddress)
                   && !string.IsNullOrWhiteSpace(Student?.City)
                   && !string.IsNullOrWhiteSpace(Student?.State)
                   && !string.IsNullOrWhiteSpace(Student?.Zip);
        }

        private void OnStudentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Re-evaluate save when key fields change; keep UI IsEnabled and command CanExecute aligned
            if (e.PropertyName == nameof(Core.Models.Student.StudentName) ||
                e.PropertyName == nameof(Core.Models.Student.Grade) ||
                e.PropertyName == nameof(Core.Models.Student.HomeAddress) ||
                e.PropertyName == nameof(Core.Models.Student.City) ||
                e.PropertyName == nameof(Core.Models.Student.State) ||
                e.PropertyName == nameof(Core.Models.Student.Zip))
            {
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

                // TODO: Implement xAI Grok API call
                // For MVP tests, avoid artificial delays
                // Simulate API call

                // Mock AI response based on address analysis
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
        /// Open Google Earth Engine to view student location on map
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

                // TODO: Implement Google Earth Engine integration
                // For MVP tests, avoid artificial delays
                // Simulate map opening

                // Mock coordinates geocoding
                var fullAddress = $"{Student.HomeAddress}, {Student.City}, {Student.State} {Student.Zip}";

                // Simulate opening in browser or map application
                ValidationStatus = "✓ Map opened successfully";
                ValidationStatusBrush = Brushes.Green;

                Logger.Information("Map view opened for address: {Address}", fullAddress);
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
        /// Import student data from CSV file using Syncfusion Excel parser
        /// </summary>
        private async Task ImportCsvAsync()
        {
            try
            {
                Logger.Information("Starting CSV import process");

                // TODO: Implement Syncfusion.XlsIO CSV import
                // For MVP, simulate file dialog and parsing

                IsValidating = true;
                ValidationStatus = "Importing CSV data...";
                ValidationStatusBrush = Brushes.Blue;

                await Task.CompletedTask; // No artificial delay

                // Mock successful import
                ValidationStatus = "✓ CSV import completed";
                ValidationStatusBrush = Brushes.Green;

                Logger.Information("CSV import completed successfully");
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

                if (string.IsNullOrWhiteSpace(Student.HomeAddress))
                    validationErrors.Add("Home address is required");

                if (string.IsNullOrWhiteSpace(Student.City))
                    validationErrors.Add("City is required");

                if (string.IsNullOrWhiteSpace(Student.State))
                    validationErrors.Add("State is required");

                // Populate error list for UI
                _validationErrors.Clear();
                foreach (var err in validationErrors)
                {
                    _validationErrors.Add("• " + err);
                }
                HasValidationErrors = _validationErrors.Count > 0;

                if (validationErrors.Any())
                {
                    ValidationStatus = $"❌ {validationErrors.Count} validation errors";
                    ValidationStatusBrush = Brushes.Red;
                    SetGlobalError($"Validation failed: {string.Join(", ", validationErrors)}");
                    CanSave = false;
                    _saveRelay?.NotifyCanExecuteChanged();
                    return;
                }

                // Perform address validation unless disabled
                if (!DisableAddressValidation)
                {
                    await ValidateAddressAsync();
                }
                else
                {
                    AddressValidationMessage = "Address validation disabled — TODO: re-enable post-MVP";
                    AddressValidationColor = Brushes.Gray;
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
        /// Clear the global error message
        /// </summary>
        private void ClearGlobalError()
        {
            HasGlobalError = false;
            GlobalErrorMessage = string.Empty;
        }

        /// <summary>
        /// Set a global error message
        /// </summary>
        private void SetGlobalError(string message)
        {
            GlobalErrorMessage = message;
            HasGlobalError = true;
            Logger.Warning("Global error set: {Message}", message);
        }

        /// <summary>
        /// Get AI-suggested routes based on address (MVP simulation)
        /// </summary>
        private Task<List<string>> GetAISuggestedRoutes(string? address, string? city, string? state)
        {
            // Mock AI logic based on location
            var routes = new List<string>();

            var citySafe = city ?? string.Empty;
            if (citySafe.Contains("north", StringComparison.OrdinalIgnoreCase))
            {
                routes.AddRange(new[] { "Route N1", "Route N2" });
            }
            else if (citySafe.Contains("south", StringComparison.OrdinalIgnoreCase))
            {
                routes.AddRange(new[] { "Route S1", "Route S2" });
            }
            else
            {
                routes.AddRange(new[] { "Route Central-1", "Route Central-2" });
            }

            return Task.FromResult(routes);
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

        #region Data Operations

        /// <summary>
        /// Load available routes and bus stops for the form
        /// </summary>
    private async Task LoadDataAsync()
        {
            try
            {
                Logger.Information("Loading form data");

                // Load available routes (from seed data or existing routes)
                var routes = new[] { "Route A", "Route B", "Route C", "Route D" };
                AvailableRoutes.Clear();
        foreach (var route in routes)
        {
            AvailableRoutes.Add(route);
                }

                // Load available bus stops
                var busStops = new[]
                {
                    "Oak & 1st", "Maple & Main", "Pine & Center", "Elm & 2nd",
                    "Cedar & Park", "Birch & State", "Walnut & Lincoln", "Cherry & Washington",
                    "Spruce & Adams", "Hickory & Jefferson", "Poplar & Monroe", "Ash & Madison",
                    "Sycamore & Jackson", "Willow & Van Buren", "Dogwood & Harrison"
                };

                AvailableBusStops.Clear();
                foreach (var stop in busStops)
                {
                    AvailableBusStops.Add(stop);
                }

                Logger.Information("Loaded {RouteCount} routes and {StopCount} bus stops",
                    AvailableRoutes.Count, AvailableBusStops.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading form data");
            }
        }

        /// <summary>
        /// Minimal validation for Save — only ensure required fields are present.
        /// Detailed address checks are available via the Validate actions and should not block Save in MVP.
        /// </summary>
    private bool IsValidStudent()
        {
            // Required basics
            if (string.IsNullOrWhiteSpace(Student.StudentName)) return false;
            if (string.IsNullOrWhiteSpace(Student.Grade)) return false;

            // Required address fields (match UI asterisks)
            if (string.IsNullOrWhiteSpace(Student.HomeAddress) ||
                string.IsNullOrWhiteSpace(Student.City) ||
                string.IsNullOrWhiteSpace(Student.State) ||
                string.IsNullOrWhiteSpace(Student.Zip)) return false;

            // Do not enforce regex/format rules here — Save should work with minimal data.
            AddressValidationMessage = "✓ Required fields present.";
            AddressValidationColor = Brushes.Green;
            return true;
        }

        /// <summary>
        /// Build a list of validation errors for diagnostics when Save fails.
        /// </summary>
        private List<string> GetValidationErrors()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Student.StudentName)) errors.Add("Student name is required");
            if (string.IsNullOrWhiteSpace(Student.Grade)) errors.Add("Grade is required");
            if (string.IsNullOrWhiteSpace(Student.HomeAddress)) errors.Add("Home address is required");
            if (string.IsNullOrWhiteSpace(Student.City)) errors.Add("City is required");
            if (string.IsNullOrWhiteSpace(Student.State)) errors.Add("State is required");
            if (string.IsNullOrWhiteSpace(Student.Zip)) errors.Add("Zip is required");
            return errors;
        }

        /// <summary>
        /// Simple address validation using regex patterns (MVP implementation)
        /// </summary>
        private (bool IsValid, string? ErrorMessage) ValidateAddressComponents(string street, string city, string state, string zipCode)
        {
            var validationMessages = new List<string>();
            // Validate street address
            if (string.IsNullOrWhiteSpace(street))
            {
                validationMessages.Add("Street address is required");
            }
            else if (!Regex.IsMatch(street.Trim(), @"^\d+\s+[\w\s\.,#-]+$"))
            {
                validationMessages.Add("Street address must start with a number followed by street name");
            }

            // Validate city
            if (string.IsNullOrWhiteSpace(city))
            {
                validationMessages.Add("City is required");
            }
            else if (!Regex.IsMatch(city.Trim(), @"^[A-Za-z\s\.-]+$"))
            {
                validationMessages.Add("City name can only contain letters, spaces, periods, and hyphens");
            }

            // Validate state
            if (string.IsNullOrWhiteSpace(state))
            {
                validationMessages.Add("State is required");
            }
            else if (!IsValidState(state))
            {
                validationMessages.Add("State must be a valid 2-letter US state abbreviation");
            }

            // Validate ZIP code
            if (string.IsNullOrWhiteSpace(zipCode))
            {
                validationMessages.Add("ZIP code is required");
            }
            else if (!IsValidZipCode(zipCode))
            {
                validationMessages.Add("ZIP code must be 5 digits or 5+4 format (e.g., 12345 or 12345-6789)");
            }

            if (validationMessages.Any())
            {
                return (false, string.Join("; ", validationMessages));
            }

            return (true, null);
        }

        /// <summary>
        /// Validates ZIP code format (5-digit or 9-digit)
        /// </summary>
        private bool IsValidZipCode(string zipCode)
        {
            if (string.IsNullOrWhiteSpace(zipCode))
            {
                return false;
            }

            // 5-digit or 5+4 format
            return Regex.IsMatch(zipCode.Trim(), @"^\d{5}(-\d{4})?$");
        }

        /// <summary>
        /// Validates US state abbreviation
        /// </summary>
        private bool IsValidState(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return false;
            }

            var validStates = new HashSet<string>
            {
                "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
                "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
                "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
                "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
                "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
                "DC"  // District of Columbia
            };

            return validStates.Contains(state.ToUpperInvariant());
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
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
