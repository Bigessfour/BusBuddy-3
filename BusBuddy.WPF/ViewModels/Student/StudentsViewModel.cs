using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using System.Windows.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using BusBuddy.WPF;
using Serilog;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.GoogleEarth;
using CommunityToolkit.Mvvm.Messaging;
using BusBuddy.WPF.Messages;

namespace BusBuddy.WPF.ViewModels.Student
{
    /// <summary>
    /// ViewModel for the StudentsView - manages student list display and operations
    /// Implements MVP pattern with basic CRUD operations
    /// </summary>
    public class StudentsViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<StudentsViewModel>();

        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly AddressService _addressService;
        private Core.Models.Student? _selectedStudent;
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private string _quickSearchText = string.Empty;

        // New properties for enhanced features
        private ObservableCollection<string> _availableGrades = new();
        private ObservableCollection<string> _availableSchools = new();
        private ObservableCollection<Core.Models.Route> _availableRoutes = new();
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
        public StudentsViewModel(IBusBuddyDbContextFactory contextFactory, AddressService? addressService = null)
        {
            _contextFactory = contextFactory;
            _addressService = addressService ?? new AddressService();
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
                    // Ensure selection-dependent commands update their CanExecute state
                    _editStudentRelay?.NotifyCanExecuteChanged();
                    _deleteStudentRelay?.NotifyCanExecuteChanged();
                    _validateAddressRelay?.NotifyCanExecuteChanged();
                    _bulkAssignRouteRelay?.NotifyCanExecuteChanged();
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
        /// Available schools for dropdown selection
        /// </summary>
        public ObservableCollection<string> AvailableSchools
        {
            get => _availableSchools;
            set => SetProperty(ref _availableSchools, value);
        }

        /// <summary>
        /// Available routes for assignment
        /// </summary>
        public ObservableCollection<Core.Models.Route> AvailableRoutes
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
            set => SetProperty(ref _isLoading, value);
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
        public ICommand EditStudentCommand { get; private set; } = null!;
        public ICommand DeleteStudentCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ExportCommand { get; private set; } = null!;
        public ICommand ValidateAddressCommand { get; private set; } = null!;

    // Backing fields to allow NotifyCanExecuteChanged on selection changes
    private RelayCommand? _editStudentRelay;
    private RelayCommand? _deleteStudentRelay;
    private RelayCommand? _validateAddressRelay;
    private RelayCommand? _bulkAssignRouteRelay;

        // New enhanced commands for route building
        public ICommand ImportStudentsCommand { get; private set; } = null!;
        public ICommand BulkAssignRouteCommand { get; private set; } = null!;
        public ICommand OptimizeRoutesCommand { get; private set; } = null!;
        public ICommand ViewMapCommand { get; private set; } = null!;
        public ICommand ViewOnMapCommand { get; private set; } = null!;
        public ICommand SuggestRouteCommand { get; private set; } = null!;
        public ICommand ShowSummaryCommand { get; private set; } = null!;
        public ICommand ShowQuickActionsCommand { get; private set; } = null!;
    public ICommand PlotStudentsCommand { get; private set; } = null!;

        #endregion

        #region Command Initialization

    /// <summary>
    /// Wire up all commands. Edit/Delete/Validate/BulkAssign use CanExecute predicated on HasSelectedStudent.
    /// </summary>
    private void InitializeCommands()
        {
            // Existing commands
            AddStudentCommand = new RelayCommand(ExecuteAddStudent);
            _editStudentRelay = new RelayCommand(ExecuteEditStudent, CanExecuteEditStudent);
            EditStudentCommand = _editStudentRelay;
            _deleteStudentRelay = new RelayCommand(ExecuteDeleteStudent, CanExecuteDeleteStudent);
            DeleteStudentCommand = _deleteStudentRelay;
            RefreshCommand = new AsyncRelayCommand(LoadStudentsAsync);
            ExportCommand = new RelayCommand(ExecuteExport);
            _validateAddressRelay = new RelayCommand(ExecuteValidateAddress, CanExecuteValidateAddress);
            ValidateAddressCommand = _validateAddressRelay;

            // New enhanced commands
            ImportStudentsCommand = new RelayCommand(ExecuteImportStudents);
            _bulkAssignRouteRelay = new RelayCommand(ExecuteBulkAssignRoute, CanExecuteBulkAssignRoute);
            BulkAssignRouteCommand = _bulkAssignRouteRelay;
            OptimizeRoutesCommand = new AsyncRelayCommand(ExecuteOptimizeRoutes);
            ViewMapCommand = new RelayCommand(ExecuteViewMap);
            ViewOnMapCommand = new RelayCommand<Core.Models.Student>(ExecuteViewOnMap);
            SuggestRouteCommand = new RelayCommand<Core.Models.Student>(ExecuteSuggestRoute);
            ShowSummaryCommand = new RelayCommand(ExecuteShowSummary);
            ShowQuickActionsCommand = new RelayCommand(ExecuteShowQuickActions);
            PlotStudentsCommand = new RelayCommand(ExecutePlotStudents);

            Logger.Debug("Commands initialized: Add/Edit/Delete/Import/BulkAssign/Optimize/ViewMap/ViewOnMap/Suggest/Validate/Refresh/Export/ShowSummary/ShowQuickActions/Plot");
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
    /// Only enabled when a student is selected.
    /// </summary>
    private bool CanExecuteEditStudent()
        {
            var can = HasSelectedStudent;
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
                    // TODO: Show confirmation dialog before deleting
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
            var can = HasSelectedStudent;
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
                // TODO: Implement CSV export functionality
                Logger.Information("Export command executed - {StudentCount} students", Students.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing export command");
            }
        }



    /// <summary>
    /// Convenience redirect to ViewMap.
    /// </summary>
    private void ExecutePlotStudents() => ExecuteViewMap();

    /// <summary>
    /// Validates the SelectedStudent's HomeAddress using AddressService.
    /// </summary>
    private void ExecuteValidateAddress()
        {
            try
            {
                if (SelectedStudent?.HomeAddress == null)
                {
                    StatusMessage = "No address to validate";
                    return;
                }

                var validation = _addressService.ValidateAddress(SelectedStudent.HomeAddress);
                StatusMessage = validation.IsValid
                    ? "Address format is valid"
                    : $"Address validation failed: {validation.Error}";

                Logger.Information("Address validation performed for student {StudentId}: {IsValid}",
                    SelectedStudent.StudentId, validation.IsValid);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error validating address";
                Logger.Error(ex, "Error executing validate address command");
            }
        }
    /// <summary>
    /// Plots the provided student on the map via GoogleEarthViewModel.
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
                var mapVm = sp.GetService<GoogleEarthViewModel>();
                if (mapVm == null)
                {
                    StatusMessage = "Map view unavailable";
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

                mapVm.MapMarkers.Add(new GoogleEarthViewModel.MapMarker
                {
                    Label = student.StudentName,
                    Latitude = lat.Value,
                    Longitude = lon.Value
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
            var can = HasSelectedStudent && !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress);
            Logger.Debug("CanExecuteValidateAddress evaluated — HasSelectedStudent={Has}, HasAddress={HasAddress}, Result={Result}",
                HasSelectedStudent, !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress), can);
            return can;
        }

        #endregion

        #region Data Operations

        /// <summary>
        /// Load all students from the database
        /// </summary>
    /// <inheritdoc />
    public async Task LoadStudentsAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading students from database");

                var context = _contextFactory.CreateDbContext();
                var students = await context.Students
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();

                Students.Clear();
                foreach (var student in students)
                {
                    Students.Add(student);
                }

                Logger.Information("Loaded {StudentCount} students", Students.Count);
                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(ActiveStudents));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading students");
                // TODO: Show error message to user
            }
            finally
            {
                IsLoading = false;
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
            try
            {
                Logger.Information("Deleting student {StudentId} - {StudentName}", student.StudentId, student.StudentName);

                using var context = _contextFactory.CreateDbContext();
                context.Students.Remove(student);
                await context.SaveChangesAsync();

                Students.Remove(student);
                SelectedStudent = null;

                Logger.Information("Successfully deleted student {StudentId}", student.StudentId);
                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(ActiveStudents));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting student {StudentId}", student.StudentId);
                // TODO: Show error message to user
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
    /// Starts the CSV/Excel import workflow (placeholder).
    /// </summary>
    private void ExecuteImportStudents()
        {
            try
            {
                Logger.Information("Import students command executed");
                StatusMessage = "Import students feature coming soon - CSV/Excel support";
                // TODO: Implement CSV/Excel import functionality
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing import students command");
                StatusMessage = "Error importing students";
            }
        }

    /// <summary>
    /// Assigns routes to a selection of students (placeholder).
    /// </summary>
    private void ExecuteBulkAssignRoute()
        {
            try
            {
                Logger.Information("Bulk assign route command executed");
                StatusMessage = "Bulk route assignment feature coming soon";
                // TODO: Implement bulk route assignment
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing bulk assign route command");
                StatusMessage = "Error in bulk route assignment";
            }
        }

        private bool CanExecuteBulkAssignRoute()
        {
            var can = HasSelectedStudent;
            Logger.Debug("CanExecuteBulkAssignRoute evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

    /// <summary>
    /// Simulates AI-based route optimization (placeholder).
    /// </summary>
    private async Task ExecuteOptimizeRoutes()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Optimizing routes with AI...";
                Logger.Information("AI route optimization started");

                // Simulate AI processing
                await Task.Delay(2000);

                StatusMessage = "AI route optimization completed";
                Logger.Information("AI route optimization completed");
                // TODO: Integrate with xAI Grok for actual optimization
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing route optimization");
                StatusMessage = "Error in route optimization";
            }
            finally
            {
                IsLoading = false;
            }
        }

    /// <summary>
    /// Plots all students on the map using the GoogleEarthViewModel.
    /// </summary>
    private void ExecuteViewMap()
        {
            try
            {
                Logger.Information("View map command executed (bulk plot)");
                StatusMessage = "Plotting students on map...";

                var sp = App.ServiceProvider;
                if (sp == null)
                {
                    StatusMessage = "Mapping not available";
                    return;
                }

                var geocoder = sp.GetService<IGeocodingService>();
                var mapVm = sp.GetService<GoogleEarthViewModel>();
                if (mapVm == null)
                {
                    StatusMessage = "Map view unavailable";
                    return;
                }

                // Clear existing student markers; keep any seed markers (e.g., school) by filtering on label
                for (int i = mapVm.MapMarkers.Count - 1; i >= 0; i--)
                {
                    var m = mapVm.MapMarkers[i];
                    if (!string.Equals(m.Label, "Wiley School RE-13JT", StringComparison.OrdinalIgnoreCase))
                    {
                        mapVm.MapMarkers.RemoveAt(i);
                    }
                }

                // Fire-and-forget each geocode to keep UI responsive
                _ = Task.Run(async () =>
                {
                    foreach (var s in Students.ToList())
                    {
                        if (string.IsNullOrWhiteSpace(s.HomeAddress))
                        {
                            continue;
                        }
                        try
                        {
                            double? lat = null, lon = null;
                            if (geocoder != null)
                            {
                                var r = await geocoder.GeocodeAsync(s.HomeAddress, s.City, s.State, s.Zip);
                                if (r != null)
                                {
                                    lat = r.Value.latitude;
                                    lon = r.Value.longitude;
                                }
                            }
                            if (lat == null || lon == null)
                            {
                                continue;
                            }

                            // Marshal to UI thread to update collection
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                mapVm.MapMarkers.Add(new GoogleEarthViewModel.MapMarker
                                {
                                    Label = s.StudentName,
                                    Latitude = lat.Value,
                                    Longitude = lon.Value
                                });
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Failed to geocode or plot student {Student}", s.StudentName);
                        }
                    }

                    StatusMessage = "Student plotting complete";
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
                // TODO: Integrate xAI Grok for route suggestions
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

    /// <summary>
    /// Displays quick action menu (placeholder).
    /// </summary>
    private void ExecuteShowQuickActions()
        {
            try
            {
                Logger.Information("Show quick actions command executed");
                StatusMessage = "Quick actions menu coming soon";
                // TODO: Show context menu with quick actions
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing quick actions");
                StatusMessage = "Error showing quick actions";
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
                // Load available grades
                AvailableGrades.Clear();
                var grades = new[] { "Pre-K", "K", "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th", "11th", "12th" };
                foreach (var grade in grades)
                {
                    AvailableGrades.Add(grade);
                }

                using var context = _contextFactory.CreateDbContext();
                AvailableSchools.Clear();
                var schools = await context.Students
                    .Where(s => !string.IsNullOrEmpty(s.School))
                    .Select(s => s.School!)
                    .Distinct()
                    .ToListAsync();
                foreach (var school in schools)
                {
                    AvailableSchools.Add(school);
                }

                // Load available routes
                AvailableRoutes.Clear();
                var routes = await context.Routes.ToListAsync();
                foreach (var route in routes)
                {
                    AvailableRoutes.Add(route);
                }

                Logger.Information("Reference data loaded: {GradeCount} grades, {SchoolCount} schools, {RouteCount} routes",
                    AvailableGrades.Count, AvailableSchools.Count, AvailableRoutes.Count);
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
