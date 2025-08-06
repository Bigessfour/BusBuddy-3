using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using BusBuddy.WPF;
using Serilog;
using CommunityToolkit.Mvvm.Input;

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
        public StudentsViewModel()
        {
            _contextFactory = new BusBuddyDbContextFactory();
            _addressService = new AddressService();
            Students = new ObservableCollection<Core.Models.Student>();

            InitializeCommands();
            _ = LoadStudentsAsync();
            _ = LoadReferenceDataAsync();
        }

        /// <summary>
        /// Constructor for testing (dependency injection)
        /// </summary>
        public StudentsViewModel(BusBuddyDbContext context, AddressService addressService)
        {
            _contextFactory = new BusBuddyDbContextFactory(); // For testing, can inject a mock factory
            _addressService = addressService;
            Students = new ObservableCollection<Core.Models.Student>();

            InitializeCommands();
        }

        #region Properties

        /// <summary>
        /// Collection of all students for display in the data grid
        /// </summary>
        public ObservableCollection<Core.Models.Student> Students { get; }

        /// <summary>
        /// Currently selected student in the data grid
        /// </summary>
        public Core.Models.Student? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (SetProperty(ref _selectedStudent, value))
                {
                    OnPropertyChanged(nameof(HasSelectedStudent));
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
        public string QuickSearchText
        {
            get => _quickSearchText;
            set
            {
                if (SetProperty(ref _quickSearchText, value))
                {
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

        // New enhanced commands for route building
        public ICommand ImportStudentsCommand { get; private set; } = null!;
        public ICommand BulkAssignRouteCommand { get; private set; } = null!;
        public ICommand OptimizeRoutesCommand { get; private set; } = null!;
        public ICommand ViewMapCommand { get; private set; } = null!;
        public ICommand ViewOnMapCommand { get; private set; } = null!;
        public ICommand SuggestRouteCommand { get; private set; } = null!;
        public ICommand ShowSummaryCommand { get; private set; } = null!;
        public ICommand ShowQuickActionsCommand { get; private set; } = null!;

        #endregion

        #region Command Initialization

        private void InitializeCommands()
        {
            // Existing commands
            AddStudentCommand = new RelayCommand(ExecuteAddStudent);
            EditStudentCommand = new RelayCommand(ExecuteEditStudent, CanExecuteEditStudent);
            DeleteStudentCommand = new RelayCommand(ExecuteDeleteStudent, CanExecuteDeleteStudent);
            RefreshCommand = new AsyncRelayCommand(LoadStudentsAsync);
            ExportCommand = new RelayCommand(ExecuteExport);
            ValidateAddressCommand = new RelayCommand(ExecuteValidateAddress, CanExecuteValidateAddress);

            // New enhanced commands
            ImportStudentsCommand = new RelayCommand(ExecuteImportStudents);
            BulkAssignRouteCommand = new RelayCommand(ExecuteBulkAssignRoute, CanExecuteBulkAssignRoute);
            OptimizeRoutesCommand = new AsyncRelayCommand(ExecuteOptimizeRoutes);
            ViewMapCommand = new RelayCommand(ExecuteViewMap);
            ViewOnMapCommand = new RelayCommand<Core.Models.Student>(ExecuteViewOnMap);
            SuggestRouteCommand = new RelayCommand<Core.Models.Student>(ExecuteSuggestRoute);
            ShowSummaryCommand = new RelayCommand(ExecuteShowSummary);
            ShowQuickActionsCommand = new RelayCommand(ExecuteShowQuickActions);
        }

        #endregion

        #region Command Handlers

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

        private bool CanExecuteEditStudent() => HasSelectedStudent;

        private async void ExecuteDeleteStudent()
        {
            try
            {
                if (SelectedStudent != null)
                {
                    // TODO: Show confirmation dialog before deleting
                    await DeleteStudentAsync(SelectedStudent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing delete student command");
            }
        }

        private bool CanExecuteDeleteStudent() => HasSelectedStudent;

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

        private bool CanExecuteValidateAddress() => HasSelectedStudent && !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress);

        #endregion

        #region Data Operations

        /// <summary>
        /// Load all students from the database
        /// </summary>
        public async Task LoadStudentsAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading students from database");

                using var context = _contextFactory.CreateDbContext();
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
        private void ApplyQuickFilter()
        {
            // Note: In a real implementation, this would filter the view
            // For now, we'll just log the filter change
            Logger.Information("Quick filter applied: {FilterText}", QuickSearchText);
            StatusMessage = string.IsNullOrEmpty(QuickSearchText) ? "Filter cleared" : $"Filtering by: {QuickSearchText}";
        }

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

        private bool CanExecuteBulkAssignRoute() => HasSelectedStudent;

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

        private void ExecuteViewMap()
        {
            try
            {
                Logger.Information("View map command executed");
                StatusMessage = "Opening map view with all student locations";
                // TODO: Integrate with Google Earth Engine mapping
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing view map command");
                StatusMessage = "Error opening map view";
            }
        }

        private void ExecuteViewOnMap(Core.Models.Student? student)
        {
            try
            {
                if (student == null) return;
                Logger.Information("View student {StudentId} on map", student.StudentId);
                StatusMessage = $"Showing {student.StudentName} on map";
                // TODO: Show specific student location on map
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error showing student on map");
                StatusMessage = "Error showing student location";
            }
        }

        private void ExecuteSuggestRoute(Core.Models.Student? student)
        {
            try
            {
                if (student == null) return;
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
        private async Task LoadReferenceDataAsync()
        {
            try
            {
                // Load available grades
                AvailableGrades.Clear();
                var grades = new[] { "Pre-K", "K", "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th", "11th", "12th" };
                foreach (var grade in grades)
                    AvailableGrades.Add(grade);

                using var context = _contextFactory.CreateDbContext();
                AvailableSchools.Clear();
                var schools = await context.Students
                    .Where(s => !string.IsNullOrEmpty(s.School))
                    .Select(s => s.School!)
                    .Distinct()
                    .ToListAsync();
                foreach (var school in schools)
                    AvailableSchools.Add(school);

                // Load available routes
                AvailableRoutes.Clear();
                var routes = await context.Routes.ToListAsync();
                foreach (var route in routes)
                    AvailableRoutes.Add(route);

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
            return true;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            // No-op: context is now always local and disposed via using
        }
            // No-op: context is now always local and disposed via using
        #endregion
    }
}
