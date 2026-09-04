using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using System.Windows.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using BusBuddy.WPF;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Serilog;
using Serilog.Context;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.WPF.ViewModels.Map;
using CommunityToolkit.Mvvm.Messaging;
using BusBuddy.WPF.Messages;

namespace BusBuddy.WPF.ViewModels.Student
{
    /// <summary>
    /// ViewModel for the StudentsView - manages student list display and operations
    /// Basic CRUD operations
    /// </summary>
    public class StudentsViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<StudentsViewModel>();

        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly AddressService _addressService;
        private readonly IStudentService? _studentService;
        private readonly StudentsGridAddressCoordinator _gridAddress;
        private readonly StudentsReferenceDataCoordinator _referenceData;
        private readonly StudentsListCoordinator _list;
        private readonly StudentsBulkRouteCoordinator _bulkRoute;
        private Core.Models.Student? _selectedStudent;
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private string _quickSearchText = string.Empty;

        // New properties for enhanced features
        private ObservableCollection<string> _availableGrades = new();
        private ObservableCollection<Destination> _availableSchools = new();
        private ObservableCollection<string> _availableRoutes = new();
        private List<Destination> _schoolCatalog = new();
        private List<Core.Models.Route> _routeCatalog = new();
        /// <summary>
        /// Default constructor for production use
        /// </summary>
        /// <summary>
        /// Initializes a new instance of StudentsViewModel for production usage.
        /// Sets up observable collections, filtered view, commands, and kicks off async loads.
        /// </summary>
        public StudentsViewModel()
        {
            // Fallback for XAML new StudentsViewModel(); prefer DI constructor below.
            _contextFactory = new BusBuddyDbContextFactory();
            _addressService = new AddressService();
            _gridAddress = new StudentsGridAddressCoordinator(_addressService);
            _referenceData = new StudentsReferenceDataCoordinator(_contextFactory);
            _list = new StudentsListCoordinator(_contextFactory);
            _bulkRoute = new StudentsBulkRouteCoordinator(_contextFactory);
            Students = new ObservableCollection<Core.Models.Student>();
            StudentsView = CollectionViewSource.GetDefaultView(Students);
            StudentsView.Filter = StudentFilter;

            InitializeCommands();
            SubscribeToSaveNotifications();
            Logger.Information("StudentsViewModel initialized — commands created and data load started");
            _ = LoadStudentsAsync();
            _ = LoadReferenceDataAsync();
        }

        /// <summary>
        /// DI-friendly constructor — ensures we use the same DbContext factory as the rest of the app.
        /// </summary>
        public StudentsViewModel(
            IBusBuddyDbContextFactory contextFactory,
            IStudentService? studentService = null,
            AddressService? addressService = null)
        {
            _contextFactory = contextFactory;
            _studentService = studentService;
            _addressService = addressService ?? new AddressService();
            _gridAddress = new StudentsGridAddressCoordinator(_addressService, _studentService);
            _referenceData = new StudentsReferenceDataCoordinator(_contextFactory);
            _list = new StudentsListCoordinator(_contextFactory, _studentService);
            _bulkRoute = new StudentsBulkRouteCoordinator(_contextFactory, _studentService);
            Students = new ObservableCollection<Core.Models.Student>();
            StudentsView = CollectionViewSource.GetDefaultView(Students);
            StudentsView.Filter = StudentFilter;

            InitializeCommands();
            SubscribeToSaveNotifications();
            Logger.Information("StudentsViewModel (DI) initialized — commands created and data load started");
            _ = LoadStudentsAsync();
            _ = LoadReferenceDataAsync();
        }

        /// <summary>
        /// Constructor for testing (dependency injection)
        /// </summary>
        /// <summary>
        /// Testing constructor allowing dependency injection of a DbContext and AddressService.
        /// </summary>
        public StudentsViewModel(BusBuddyDbContext context, AddressService addressService)
        {
            // Wrap provided context in a simple factory that returns the same instance without disposing in tests
            _contextFactory = new TestContextFactory(context);
            _addressService = addressService;
            _gridAddress = new StudentsGridAddressCoordinator(_addressService);
            _referenceData = new StudentsReferenceDataCoordinator(_contextFactory);
            _list = new StudentsListCoordinator(_contextFactory);
            _bulkRoute = new StudentsBulkRouteCoordinator(_contextFactory);
            Students = new ObservableCollection<Core.Models.Student>();
            StudentsView = CollectionViewSource.GetDefaultView(Students);
            StudentsView.Filter = StudentFilter;

            InitializeCommands();
            Logger.Debug("StudentsViewModel (test) initialized — commands created");
        }

        private void SubscribeToSaveNotifications()
        {
            // Refresh list and show success message immediately when a student is saved from the form
            WeakReferenceMessenger.Default.Register<StudentSavedMessage>(this, async (_, msg) =>
            {
                try
                {
                    Logger.Information("StudentSavedMessage received — refreshing list");
                    await LoadStudentsAsync();
                    StatusMessage = "Successfully Saved";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error refreshing after save");
                }
            });

            WeakReferenceMessenger.Default.Register<StudentsImportedMessage>(this, async (_, msg) =>
            {
                try
                {
                    Logger.Information("StudentsImportedMessage received — refreshing list Added={Added}", msg.Added);
                    await LoadStudentsAsync();
                    StatusMessage = msg.Added == 0
                        ? "No new students imported (file empty or names already exist)"
                        : $"Imported {msg.Added} student(s) from CSV";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error refreshing after CSV import");
                }
            });
        }

        // Minimal internal factory wrapper for tests
        private sealed class TestContextFactory : IBusBuddyDbContextFactory
        {
            private readonly BusBuddyDbContext _ctx;
            public TestContextFactory(BusBuddyDbContext ctx) => _ctx = ctx;
            public BusBuddyDbContext CreateDbContext() => _ctx;
            public BusBuddyDbContext CreateWriteDbContext() => _ctx;
        }

        #region Properties

        /// <summary>
        /// Collection of all students for display in the data grid
        /// </summary>
        public ObservableCollection<Core.Models.Student> Students { get; }

        /// <summary>
        /// View over Students that supports filtering/sorting/grouping for UI binding
        /// </summary>
        public ICollectionView StudentsView { get; }

        /// <summary>
        /// Currently selected student in the data grid
        /// </summary>
        /// <summary>
        /// Currently selected student in the grid. Updates selection-dependent command CanExecute states.
        /// </summary>
        public Core.Models.Student? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (SetProperty(ref _selectedStudent, value))
                {
                    Logger.Debug("SelectedStudent changed to {@Student}", _selectedStudent == null ? null : new { _selectedStudent.StudentId, _selectedStudent.StudentName });
                    OnPropertyChanged(nameof(HasSelectedStudent));
                    OnPropertyChanged(nameof(HasSelectedStudents));
                    // Ensure selection-dependent commands update their CanExecute state
                    _editStudentRelay?.NotifyCanExecuteChanged();
                    _deleteStudentRelay?.NotifyCanExecuteChanged();
                    _validateAddressRelay?.NotifyCanExecuteChanged();
                    _bulkAssignRouteRelay?.NotifyCanExecuteChanged();
                    _schoolTransferRelay?.NotifyCanExecuteChanged();
                    Logger.Debug("Selection-dependent commands invalidated (CanExecute re-evaluated)");
                }
            }
        }

        /// <summary>
        /// Whether a student is currently selected
        /// </summary>
        public bool HasSelectedStudent => SelectedStudent != null;

        /// <summary>
        /// Total number of students
        /// </summary>
        public int TotalStudents => Students.Count;

        /// <summary>
        /// Number of active students
        /// </summary>
        public int ActiveStudents => Students.Count(s => s.Active);

        /// <summary>
        /// Number of students with assigned routes
        /// </summary>
        public int StudentsWithRoutes => Students.Count(s => !string.IsNullOrEmpty(s.AMRoute) || !string.IsNullOrEmpty(s.PMRoute));
        /// <summary>
        /// Number of students without assigned routes
        /// </summary>
        public int UnassignedStudents => Students.Count(s => string.IsNullOrEmpty(s.AMRoute) && string.IsNullOrEmpty(s.PMRoute));

        /// <summary>
        /// Quick search text for filtering
        /// </summary>
        /// <summary>
        /// Text used for quick filtering; updates ICollectionView filter and status text.
        /// </summary>
        public string QuickSearchText
        {
            get => _quickSearchText;
            set
            {
                if (SetProperty(ref _quickSearchText, value))
                {
                    Logger.Debug("QuickSearchText changed: {Text}", _quickSearchText);
                    ApplyQuickFilter();
                    OnPropertyChanged(nameof(FilterStatusText));
                }
            }
        }

        /// <summary>
        /// Status text showing current filter state
        /// </summary>
        public string FilterStatusText => string.IsNullOrEmpty(QuickSearchText) ? "" : $"Filtered: '{QuickSearchText}'";

        /// <summary>
        /// Available grades for dropdown selection
        /// </summary>
        public ObservableCollection<string> AvailableGrades
        {
            get => _availableGrades;
            set => SetProperty(ref _availableGrades, value);
        }

        /// <summary>
        /// Available schools for dropdown selection (destination catalog).
        /// </summary>
        public ObservableCollection<Destination> AvailableSchools
        {
            get => _availableSchools;
            set => SetProperty(ref _availableSchools, value);
        }

        /// <summary>
        /// Active route names for grid combo columns.
        /// </summary>
        public ObservableCollection<string> AvailableRoutes
        {
            get => _availableRoutes;
            set => SetProperty(ref _availableRoutes, value);
        }

        /// <summary>
        /// Whether multiple students are selected (for bulk operations)
        /// </summary>
        public bool HasSelectedStudents => SelectedStudent != null; // For now, single selection

        /// <summary>
        /// Whether data is currently being loaded
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    // Disable actions while busy
                    _editStudentRelay?.NotifyCanExecuteChanged();
                    _deleteStudentRelay?.NotifyCanExecuteChanged();
                    _validateAddressRelay?.NotifyCanExecuteChanged();
                    _bulkAssignRouteRelay?.NotifyCanExecuteChanged();
                    _schoolTransferRelay?.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Status message for user feedback
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region Commands

        public ICommand AddStudentCommand { get; private set; } = null!;
        public ICommand AddSchoolCommand { get; private set; } = null!;
        public ICommand AddPickupStopCommand { get; private set; } = null!;
        public ICommand EditStudentCommand { get; private set; } = null!;
        public ICommand DeleteStudentCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ExportCommand { get; private set; } = null!;
        public ICommand ValidateAddressCommand { get; private set; } = null!;

        // Backing fields to allow NotifyCanExecuteChanged on selection changes
        private RelayCommand? _editStudentRelay;
        private RelayCommand? _deleteStudentRelay;
        private AsyncRelayCommand? _validateAddressRelay;
        private AsyncRelayCommand? _bulkAssignRouteRelay;

        // New enhanced commands for route building
        public ICommand ImportStudentsCommand { get; private set; } = null!;
        public ICommand BulkAssignRouteCommand { get; private set; } = null!;
        public ICommand OptimizeRoutesCommand { get; private set; } = null!;
        public ICommand ViewMapCommand { get; private set; } = null!;
        public ICommand ViewOnMapCommand { get; private set; } = null!;
        public ICommand SuggestRouteCommand { get; private set; } = null!;
        public ICommand ShowSummaryCommand { get; private set; } = null!;
        public ICommand PlotStudentsCommand { get; private set; } = null!;
        public ICommand SaveGridEditsCommand { get; private set; } = null!; // Inline save for grid edits
        public ICommand SchoolTransferCommand { get; private set; } = null!;
        private RelayCommand? _schoolTransferRelay;

        #endregion

        #region Command Initialization

        /// <summary>
        /// Wire up all commands. Edit/Delete/Validate/BulkAssign use CanExecute predicated on HasSelectedStudent.
        /// </summary>
        private void InitializeCommands()
        {
            // Existing commands
            AddStudentCommand = new RelayCommand(ExecuteAddStudent);
            AddSchoolCommand = new RelayCommand(ExecuteAddSchool);
            AddPickupStopCommand = new RelayCommand(ExecuteAddPickupStop);
            _editStudentRelay = new RelayCommand(ExecuteEditStudent, CanExecuteEditStudent);
            EditStudentCommand = _editStudentRelay;
            _deleteStudentRelay = new RelayCommand(ExecuteDeleteStudent, CanExecuteDeleteStudent);
            DeleteStudentCommand = _deleteStudentRelay;
            RefreshCommand = new AsyncRelayCommand(LoadStudentsAsync);
            ExportCommand = new RelayCommand(ExecuteExport);
            _validateAddressRelay = new AsyncRelayCommand(ExecuteValidateAddressAsync, CanExecuteValidateAddress);
            ValidateAddressCommand = _validateAddressRelay;

            // New enhanced commands
            ImportStudentsCommand = new AsyncRelayCommand(ExecuteImportStudentsAsync);
            _bulkAssignRouteRelay = new AsyncRelayCommand(ExecuteBulkAssignRouteAsync, CanExecuteBulkAssignRoute);
            BulkAssignRouteCommand = _bulkAssignRouteRelay;
            OptimizeRoutesCommand = new AsyncRelayCommand(ExecuteOptimizeRoutes);
            ViewMapCommand = new RelayCommand(ExecuteViewMap);
            ViewOnMapCommand = new RelayCommand<Core.Models.Student>(ExecuteViewOnMap);
            SuggestRouteCommand = new RelayCommand<Core.Models.Student>(ExecuteSuggestRoute);
            ShowSummaryCommand = new RelayCommand(ExecuteShowSummary);
            PlotStudentsCommand = new RelayCommand(ExecutePlotStudents);
            SaveGridEditsCommand = new AsyncRelayCommand(SaveInlineGridEditsAsync);
            _schoolTransferRelay = new RelayCommand(ExecuteSchoolTransfer, () => HasSelectedStudent);
            SchoolTransferCommand = _schoolTransferRelay;

            Logger.Debug("Commands initialized: AddStudent/AddSchool/Edit/Delete/Import/BulkAssign/Optimize/ViewMap/ViewOnMap/Suggest/Validate/Refresh/Export/ShowSummary/Plot/SchoolTransfer");
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Opens the StudentForm for adding a new student and reloads the list on success.
        /// </summary>
        private void ExecuteAddStudent()
        {
            try
            {
                Logger.Information("Add student command executed");

                var studentForm = new BusBuddy.WPF.Views.Student.StudentForm();
                DialogOwner.Assign(studentForm);
                var result = studentForm.ShowDialog();

                if (result == true)
                {
                    // Refresh the student list after successful add
                    _ = LoadStudentsAsync();
                    StatusMessage = "Student added successfully";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing add student command");
                StatusMessage = $"Error adding student: {ex.Message}";
            }
        }

        private void ExecuteAddSchool()
        {
            try
            {
                var dest = App.ServiceProvider?.GetService<IDestinationService>();
                if (dest is null)
                {
                    StatusMessage = "Destination service is not available.";
                    Logger.Warning("Add school skipped: IDestinationService not registered");
                    return;
                }

                var vm = new SchoolDestinationFormViewModel(dest);
                var form = new BusBuddy.WPF.Views.Student.SchoolDestinationForm(vm);
                DialogOwner.Assign(form);
                var result = form.ShowDialog();
                if (result == true)
                {
                    _ = LoadReferenceDataAsync();
                    WeakReferenceMessenger.Default.Send(new SchoolCatalogChangedMessage(vm.SavedDestinationId));
                    StatusMessage = vm.SavedWithGps
                        ? "School saved. Assign it on the student form, then Generate Routes."
                        : "School saved without GPS. Generate Routes will not persist stop times until coordinates are set.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing add school command");
                StatusMessage = $"Error adding school: {ex.Message}";
            }
        }

        private void ExecuteAddPickupStop()
        {
            try
            {
                var stopService = App.ServiceProvider?.GetService<IPickupStopService>();
                if (stopService is null)
                {
                    StatusMessage = "Pickup stop service is not available.";
                    Logger.Warning("Add pickup stop skipped: IPickupStopService not registered");
                    return;
                }

                var vm = new PickupStopFormViewModel(stopService);
                var form = new BusBuddy.WPF.Views.Student.PickupStopForm(vm);
                DialogOwner.Assign(form);
                var result = form.ShowDialog();
                if (result == true)
                {
                    StatusMessage = $"Pickup stop saved (Id={vm.SavedPickupStopId}). Assign it on the student form.";
                    WeakReferenceMessenger.Default.Send(new PickupStopCatalogChangedMessage(vm.SavedPickupStopId));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing add pickup stop command");
                StatusMessage = $"Error adding pickup stop: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens the StudentForm for editing the currently selected student.
        /// </summary>
        private void ExecuteEditStudent()
        {
            try
            {
                if (SelectedStudent != null)
                {
                    Logger.Information("Edit student command executed for student {StudentId}", SelectedStudent.StudentId);

                    var studentForm = new BusBuddy.WPF.Views.Student.StudentForm(SelectedStudent);
                    DialogOwner.Assign(studentForm);
                    var result = studentForm.ShowDialog();

                    if (result == true)
                    {
                        // Refresh the student list after successful edit
                        _ = LoadStudentsAsync();
                        StatusMessage = "Student updated successfully";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing edit student command");
                StatusMessage = $"Error editing student: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens school-to-school transfer dialog (pickup/dropoff locations + times required).
        /// </summary>
        private void ExecuteSchoolTransfer()
        {
            try
            {
                if (SelectedStudent is null)
                {
                    return;
                }

                var sp = App.ServiceProvider;
                var transferService = sp?.GetService<IStudentSchoolTransferService>();
                var destinationService = sp?.GetService<IDestinationService>();
                if (transferService is null || destinationService is null)
                {
                    StatusMessage = "Transfer services unavailable";
                    Logger.Warning("School transfer skipped — services not registered");
                    return;
                }

                var vm = new StudentSchoolTransferViewModel(
                    SelectedStudent.StudentId,
                    SelectedStudent.StudentName ?? $"Student {SelectedStudent.StudentId}",
                    transferService,
                    destinationService);
                var dialog = new BusBuddy.WPF.Views.Student.StudentSchoolTransferForm(vm);
                DialogOwner.Assign(dialog);
                if (dialog.ShowDialog() == true)
                {
                    _ = LoadStudentsAsync();
                    StatusMessage = $"School transfer saved for {SelectedStudent.StudentName}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening school transfer");
                StatusMessage = $"Transfer error: {ex.Message}";
            }
        }

        /// <summary>
        /// Only enabled when a student is selected.
        /// </summary>
        private bool CanExecuteEditStudent()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug("CanExecuteEditStudent evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

        /// <summary>
        /// Deletes the currently selected student after confirmation (TBD).
        /// </summary>
        private async void ExecuteDeleteStudent()
        {
            try
            {
                if (SelectedStudent != null)
                {
                    var confirm = MessageBox.Show(
                        $"Delete {SelectedStudent.StudentName}?",
                        "Confirm delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    Logger.Information("Delete student command executed for student {StudentId}", SelectedStudent.StudentId);
                    await DeleteStudentAsync(SelectedStudent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing delete student command");
            }
        }

        /// <summary>
        /// Only enabled when a student is selected.
        /// </summary>
        private bool CanExecuteDeleteStudent()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug("CanExecuteDeleteStudent evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

        /// <summary>
        /// Exports the current list to CSV (TBD).
        /// </summary>
        private void ExecuteExport()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportStudents"))
                using (LogContext.PushProperty("Filtered", !string.IsNullOrWhiteSpace(QuickSearchText)))
                {
                    var exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BusBuddy", "Exports");
                    Directory.CreateDirectory(exportDir);
                    var fileName = $"students-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                    var fullPath = Path.Combine(exportDir, fileName);

                    // Export only currently visible (filtered) items
                    var rows = StudentsView.Cast<Core.Models.Student>().ToList();
                    using var sw = new StreamWriter(fullPath, false, System.Text.Encoding.UTF8);
                    sw.WriteLine("StudentId,StudentName,StudentNumber,Grade,AMRoute,PMRoute,School,DestinationId,Latitude,Longitude,Active");
                    foreach (var s in rows)
                    {
                        string Csv(string? v)
                        {
                            if (string.IsNullOrEmpty(v)) return string.Empty;
                            var escaped = v.Replace("\"", "\"\"", StringComparison.Ordinal);
                            return "\"" + escaped + "\"";
                        }
                        sw.WriteLine(string.Join(',',
                            s.StudentId,
                            Csv(s.StudentName),
                            Csv(s.StudentNumber),
                            Csv(s.Grade),
                            Csv(s.AMRoute),
                            Csv(s.PMRoute),
                            Csv(s.School),
                            s.DestinationId,
                            s.Latitude,
                            s.Longitude,
                            s.Active));
                    }
                    sw.Flush();
                    Logger.Information("Exported {Count} students to {File}", rows.Count, fullPath);
                    StatusMessage = $"Exported {rows.Count} students";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing export command");
                StatusMessage = "Error exporting students";
            }
        }



        /// <summary>
        /// Convenience redirect to ViewMap.
        /// </summary>
        private void ExecutePlotStudents() => ExecuteViewMap();

        /// <summary>
        /// Validates and geocodes the selected student's address; persists coordinates when possible.
        /// </summary>
        private async Task ExecuteValidateAddressAsync()
        {
            using (LogContext.PushProperty("Operation", "ValidateAddress"))
            using (LogContext.PushProperty("StudentId", SelectedStudent?.StudentId))
            {
            try
            {
                if (SelectedStudent?.HomeAddress == null)
                {
                    Logger.Warning("Validate address blocked — no home address on selected student");
                    StatusMessage = "No address to validate";
                    return;
                }

                IsLoading = true;
                StatusMessage = "Validating address...";
                StatusMessage = await _gridAddress.ValidateAndPersistAsync(SelectedStudent).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error validating address";
                Logger.Error(ex, "Error executing validate address command");
            }
            finally
            {
                IsLoading = false;
            }
            }
        }

        /// <summary>
        /// Plots the provided student on the map via MapViewModel.
        /// </summary>
        private async void ExecuteViewOnMap(Core.Models.Student? student)
        {
            try
            {
                if (student == null)
                {
                    return;
                }
                // Resolve services from WPF App's DI container
                var sp = App.ServiceProvider;
                if (sp == null)
                {
                    StatusMessage = "Mapping not available";
                    return;
                }

                var geocoder = sp.GetService<IGeocodingService>();
                if (geocoder == null)
                {
                    StatusMessage = "Geocoding not available";
                    return;
                }

                double? lat = null, lon = null;
                if (geocoder != null)
                {
                    var result = await geocoder.GeocodeAsync(student.HomeAddress, student.City, student.State, student.Zip);
                    if (result != null)
                    {
                        lat = result.Value.latitude;
                        lon = result.Value.longitude;
                    }
                }

                if (lat == null || lon == null)
                {
                    StatusMessage = "Could not locate address";
                    return;
                }

                MapViewLauncher.Show(Application.Current?.MainWindow as Window, vm =>
                {
                    vm.PlotStop(lat.Value, lon.Value, new[] { student.StudentName ?? "Student" }, student.StudentName);
                    vm.CenterOnMarkers();
                });

                StatusMessage = $"Plotted {student.StudentName}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error plotting student on map");
                StatusMessage = "Error plotting on map";
            }
        }

        private bool CanExecuteValidateAddress()
        {
            var can = HasSelectedStudent && !IsLoading && !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress);
            Logger.Debug("CanExecuteValidateAddress evaluated — HasSelectedStudent={Has}, HasAddress={HasAddress}, Result={Result}",
                HasSelectedStudent, !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress), can);
            return can;
        }

        #endregion

        #region Data Operations

        /// <summary>
        /// Persists any modified student entities currently tracked in the collection. This supports inline grid editing.
        /// </summary>
        private async Task SaveInlineGridEditsAsync()
        {
            using (LogContext.PushProperty("Operation", "SaveInlineGridEdits"))
            {
            try
            {
                IsLoading = true;
                var (saved, errors) = await _list
                    .SaveInlineGridEditsAsync(Students, _schoolCatalog)
                    .ConfigureAwait(true);

                StatusMessage = errors.Count > 0
                    ? $"Saved {saved} student(s); {errors.Count} failed"
                    : saved == 1 ? "Inline changes saved" : $"Saved {saved} students";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving inline grid edits");
                StatusMessage = "Error saving changes";
            }
            finally
            {
                IsLoading = false;
            }
            }
        }

        /// <summary>
        /// Load all students from the database
        /// </summary>
        /// <inheritdoc />
        public async Task LoadStudentsAsync()
        {
            using (LogContext.PushProperty("Operation", "LoadStudents"))
            {
            try
            {
                IsLoading = true;
                Logger.Information("Loading students from database");
                var students = await _list.LoadStudentsAsync().ConfigureAwait(true);

                var previousSelectionId = SelectedStudent?.StudentId;
                if (StudentsView != null)
                {
                    var view = StudentsView; // local
                    var currentFilter = view.Filter;
                    view.Filter = null; // temporarily detach filter to reduce per-item evaluations
                    try
                    {
                        // Strategy: copy into temp list then replace contents of existing ObservableCollection
                        Students.Clear();
                        for (int idx = 0; idx < students.Count; idx++)
                        {
                            Students.Add(students[idx]);
                        }
                    }
                    finally
                    {
                        view.Filter = currentFilter ?? StudentFilter;
                    }
                }
                else
                {
                    Students.Clear();
                    foreach (var s in students) Students.Add(s);
                }

                if (previousSelectionId.HasValue)
                {
                    var restored = Students.FirstOrDefault(s => s.StudentId == previousSelectionId.Value);
                    if (restored != null) SelectedStudent = restored;
                }

                Logger.Information("Loaded {StudentCount} students", Students.Count);
                StatusMessage = $"Loaded {Students.Count} students";

                // Initialize selection to first row to enable edit-related commands by default
                if (SelectedStudent == null && Students.Count > 0)
                {
                    SelectedStudent = Students[0];
                }

                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(ActiveStudents));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading students");
                StatusMessage = "Error loading students. Check connection, migrations, and logs.";
            }
            finally
            {
                IsLoading = false;
            }
            }
        }

        /// <summary>
        /// Delete a student from the database
        /// </summary>
        /// <summary>
        /// Removes the specified student from the database and updates the UI collections.
        /// </summary>
        private async Task DeleteStudentAsync(Core.Models.Student student)
        {
            using (LogContext.PushProperty("Operation", "DeleteStudent"))
            using (LogContext.PushProperty("StudentId", student.StudentId))
            {
            try
            {
                Logger.Information(
                    "Deleting student StudentId={StudentId} Name={StudentName}",
                    student.StudentId,
                    student.StudentName);

                var deleted = await _list.DeleteStudentAsync(student).ConfigureAwait(true);
                if (!deleted)
                {
                    Logger.Warning(
                        "DeleteStudentAsync returned false for StudentId={StudentId}",
                        student.StudentId);
                    MessageBox.Show("Could not delete student — no row was removed.", "Delete failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Students.Remove(student);
                SelectedStudent = null;

                Logger.Information("Successfully deleted student {StudentId}", student.StudentId);
                StatusMessage = "Student deleted";
                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(ActiveStudents));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting student StudentId={StudentId}", student.StudentId);
                MessageBox.Show($"Could not delete student: {ex.Message}", "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            }
        }

        #endregion

        #region Enhanced Command Handlers

        /// <summary>
        /// Apply quick filter to the students collection
        /// </summary>
        /// <summary>
        /// Forces the ICollectionView to refresh and apply the current filter predicate.
        /// </summary>
        private void ApplyQuickFilter()
        {
            // Refresh the ICollectionView to apply predicate
            StudentsView.Refresh();
            Logger.Information("Quick filter applied: {FilterText}", QuickSearchText);
            StatusMessage = string.IsNullOrEmpty(QuickSearchText) ? "Filter cleared" : $"Filtering by: {QuickSearchText}";
        }

        private bool StudentFilter(object obj)
        {
            if (obj is not Core.Models.Student s)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(QuickSearchText))
            {
                return true;
            }

            var q = QuickSearchText.Trim();
            // Case-insensitive contains across key fields
            return (s.StudentName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (s.StudentNumber?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (s.AMRoute?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (s.PMRoute?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (s.School?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Imports students from a student CSV via <see cref="ISeedDataService"/>.
        /// </summary>
        private async Task ExecuteImportStudentsAsync()
        {
            try
            {
                Logger.Information("Import students command executed");
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import students from CSV",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    CheckFileExists = true
                };
                if (dialog.ShowDialog() != true)
                {
                    StatusMessage = "CSV import cancelled";
                    return;
                }

                IsLoading = true;
                StatusMessage = "Importing students from CSV...";

                var seed = App.ServiceProvider?.GetService<ISeedDataService>()
                    ?? new SeedDataService(_contextFactory);
                var added = await seed.ImportStudentsFromCsvAsync(dialog.FileName);
                await LoadStudentsAsync();
                StatusMessage = added == 0
                    ? "No new students imported (file empty or names already exist)"
                    : $"Imported {added} student(s) from CSV";
                Logger.Information("CSV import finished Added={Added} Path={Path}", added, dialog.FileName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing import students command");
                StatusMessage = $"Error importing students: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Assigns routes to a selection of students via <see cref="IStudentService"/>.
        /// </summary>
        private async Task ExecuteBulkAssignRouteAsync()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "BulkAssignRoute"))
                {
                    if (_routeCatalog.Count == 0)
                    {
                        StatusMessage = "No routes available";
                        return;
                    }

                    var visibleStudents = StudentsView.Cast<Core.Models.Student>().ToList();
                    var candidates = SelectedStudent != null
                        ? new List<Core.Models.Student> { SelectedStudent }
                        : visibleStudents.Where(s => string.IsNullOrWhiteSpace(s.AMRoute) || string.IsNullOrWhiteSpace(s.PMRoute)).ToList();

                    if (candidates.Count == 0)
                    {
                        StatusMessage = "No eligible students (all have AM & PM routes)";
                        return;
                    }

                    const int MaxBatch = 500;
                    if (candidates.Count > MaxBatch)
                    {
                        candidates = candidates.Take(MaxBatch).ToList();
                        Logger.Warning("Bulk assignment candidate list truncated to {MaxBatch}", MaxBatch);
                    }

                    IsLoading = true;
                    var (affected, errors, routeName) = await _bulkRoute
                        .AssignAsync(_routeCatalog, candidates, SelectedStudent)
                        .ConfigureAwait(true);

                    StatusMessage = errors > 0
                        ? $"Assigned {routeName} to {affected} student(s); {errors} failed"
                        : affected == 0
                            ? "No students updated"
                            : $"Assigned {routeName} to {affected} student(s)";
                    OnPropertyChanged(nameof(StudentsWithRoutes));
                    OnPropertyChanged(nameof(UnassignedStudents));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing bulk assign route command");
                StatusMessage = "Error in bulk route assignment";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanExecuteBulkAssignRoute()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug("CanExecuteBulkAssignRoute evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

        /// <summary>
        /// Assigns unassigned students to active routes, then asks local Ollama (or mock AI) for commentary.
        /// </summary>
        private async Task ExecuteOptimizeRoutes()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Optimizing routes with AI...";
                Logger.Information("AI route optimization started");

                var optimizer = App.ServiceProvider?.GetService<IStudentRouteOptimizer>()
                    ?? new StudentRouteOptimizer(new RouteService(_contextFactory));
                var result = await optimizer.OptimizeUnassignedAsync();
                await LoadStudentsAsync();
                StatusMessage = result.Status;
                OnPropertyChanged(nameof(StudentsWithRoutes));
                OnPropertyChanged(nameof(UnassignedStudents));
                Logger.Information(
                    "AI route optimization completed Assigned={Assigned} Remaining={Remaining} MockAi={Mock}",
                    result.AssignedCount, result.RemainingUnassigned, result.UsedMockAi);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing route optimization");
                StatusMessage = $"Error in route optimization: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Plots all students on the map using the MapViewModel.
        /// </summary>
        private void ExecuteViewMap()
        {
            try
            {
                Logger.Information("View map command executed (bulk plot)");
                StatusMessage = "Opening district map...";

                MapViewLauncher.Show(Application.Current?.MainWindow as Window, vm =>
                {
                    if (vm.BulkPlotEligibleStudentsCommand is IAsyncRelayCommand plotCmd)
                    {
                        _ = plotCmd.ExecuteAsync(null);
                    }
                    else if (vm.BulkPlotEligibleStudentsCommand.CanExecute(null))
                    {
                        vm.BulkPlotEligibleStudentsCommand.Execute(null);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing view map command");
                StatusMessage = "Error opening map view";
            }
        }



        /// <summary>
        /// Placeholder for AI route suggestion for a single student.
        /// </summary>
        private void ExecuteSuggestRoute(Core.Models.Student? student)
        {
            try
            {
                if (student == null)
                {
                    return;
                }
                Logger.Information("AI route suggestion for student {StudentId}", student.StudentId);
                StatusMessage = $"Getting AI route suggestions for {student.StudentName}";
                _ = ExecuteOptimizeRoutes();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting route suggestions");
                StatusMessage = "Error getting route suggestions";
            }
        }

        /// <summary>
        /// Creates and displays a quick summary of student counts.
        /// </summary>
        private void ExecuteShowSummary()
        {
            try
            {
                Logger.Information("Show summary command executed");
                var summary = $"Students: {TotalStudents}, Active: {ActiveStudents}, With Routes: {StudentsWithRoutes}, Unassigned: {UnassignedStudents}";
                StatusMessage = $"Summary: {summary}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing summary");
                StatusMessage = "Error generating summary";
            }
        }

        #endregion

        #region Startup actions (MainWindow shortcuts → StudentsView)

        /// <summary>Student to select and edit after the grid loads (set before ShowDialog).</summary>
        public int? PendingEditStudentId { get; set; }

        /// <summary>Run add/edit once reference data and students are loaded.</summary>
        public async Task CompleteStartupActionAsync(StudentsViewStartup startup)
        {
            if (startup == StudentsViewStartup.None)
            {
                return;
            }

            await LoadStudentsAsync().ConfigureAwait(true);
            await LoadReferenceDataAsync().ConfigureAwait(true);

            switch (startup)
            {
                case StudentsViewStartup.AddStudent:
                    ExecuteAddStudent();
                    break;
                case StudentsViewStartup.EditStudent when PendingEditStudentId is int studentId:
                    PendingEditStudentId = null;
                    SelectedStudent = Students.FirstOrDefault(s => s.StudentId == studentId);
                    if (SelectedStudent is not null)
                    {
                        ExecuteEditStudent();
                    }
                    else
                    {
                        StatusMessage = $"Student {studentId} was not found in the list.";
                        Logger.Warning("Startup edit skipped — StudentId {StudentId} not in grid", studentId);
                    }

                    break;
            }
        }

        #endregion

        #region Data Loading Helpers

        /// <summary>
        /// Load reference data for dropdowns
        /// </summary>
        /// <summary>
        /// Loads grades, schools, and routes used by dropdowns.
        /// </summary>
        private async Task LoadReferenceDataAsync()
        {
            try
            {
                await _referenceData.LoadAsync(
                    AvailableGrades,
                    AvailableSchools,
                    AvailableRoutes,
                    _schoolCatalog,
                    _routeCatalog).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading reference data");
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // ...existing code...

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }
            field = value;
            // ...existing code...
            OnPropertyChanged(propertyName);
            Logger.Verbose("PropertyChanged: {Property}", propertyName);
            return true;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            // No-op: context is now always local and disposed via using
            Logger.Debug("StudentsViewModel disposed");
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }
        }
        // No-op: context is now always local and disposed via using
        #endregion
    }
}
