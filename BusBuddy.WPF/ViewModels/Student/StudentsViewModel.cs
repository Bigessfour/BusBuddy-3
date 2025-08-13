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
using Serilog.Context;
using CommunityToolkit.Mvvm.Input;
using System.IO;
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
                    OnPropertyChanged(nameof(HasSelectedStudents));
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
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    // Disable actions while busy
                    _editStudentRelay?.NotifyCanExecuteChanged();
                    _deleteStudentRelay?.NotifyCanExecuteChanged();
                    _validateAddressRelay?.NotifyCanExecuteChanged();
                    _bulkAssignRouteRelay?.NotifyCanExecuteChanged();
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
    public ICommand SaveGridEditsCommand { get; private set; } = null!; // Inline save for grid edits

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
            SaveGridEditsCommand = new AsyncRelayCommand(SaveInlineGridEditsAsync);

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
                var result = ShowInHostDialog(studentForm, "Add Student");

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
                    var result = ShowInHostDialog(studentForm, "Edit Student");

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
        /// Hosts a StudentForm (UserControl) inside a transient Window for modal interaction.
        /// </summary>
        private bool? ShowInHostDialog(BusBuddy.WPF.Views.Student.StudentForm form, string title)
        {
            try
            {
                var host = new System.Windows.Window
                {
                    Title = title,
                    Content = form,
                    SizeToContent = System.Windows.SizeToContent.WidthAndHeight,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                    Owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current?.MainWindow,
                    MinWidth = 820,
                    MinHeight = 640
                };

                bool? dialogResult = null;
                form.RequestCloseByHost += (s, _) => { var f = (BusBuddy.WPF.Views.Student.StudentForm)s!; dialogResult = f.DialogResult; host.DialogResult = f.DialogResult; host.Close(); };
                var result = host.ShowDialog();
                return dialogResult ?? result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to host StudentForm in dialog");
                System.Windows.MessageBox.Show($"Failed to open form: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
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
                    sw.WriteLine("StudentId,StudentName,StudentNumber,Grade,AMRoute,PMRoute,School,Active");
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
            try
            {
                IsLoading = true; // Re‑use flag to disable edit buttons during save
                Logger.Information("Saving inline grid edits for students");
                using var context = _contextFactory.CreateWriteDbContext();
                foreach (var s in Students)
                {
                    // Normalize phone format before save (digits only -> (###) ###-#### )
                    s.HomePhone = NormalizePhone(s.HomePhone);
                    s.EmergencyPhone = NormalizePhone(s.EmergencyPhone);
                    context.Students.Update(s);
                }
                await context.SaveChangesAsync();
                StatusMessage = "Inline changes saved";
                Logger.Information("Inline grid edits saved successfully");
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

        /// <summary>
        /// Normalizes a phone string to (###) ###-#### if it has 10 digits; otherwise returns original.
        /// </summary>
        private static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return phone;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
            {
                return $"({digits.Substring(0,3)}) {digits.Substring(3,3)}-{digits.Substring(6,4)}";
            }
            return phone; // leave as-is if not 10 digits
        }

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
                try
                {
                    // Diagnostics: provider and connection string (mask password) to detect env/connection mismatches
                    var provider = context.Database.ProviderName;
                    var rawConn = context.Database.GetConnectionString();
                    string masked = rawConn ?? "(null)";
                    if (!string.IsNullOrEmpty(masked))
                    {
                        // Very basic masking of password and SAS-style secrets
                        masked = System.Text.RegularExpressions.Regex.Replace(masked, "(?i)(Password|Pwd)=([^;]+)", "$1=***");
                    }
                    Logger.Debug("EF Provider: {Provider}; Connection: {Connection}", provider, masked);

                    // Warn if unresolved placeholders exist (common cause: missing env vars for Azure)
                    if (!string.IsNullOrEmpty(rawConn) && rawConn.Contains("${"))
                    {
                        Logger.Warning("Connection string contains unresolved placeholders (e.g., ${AZURE_SQL_USER}). Check appsettings and environment variables.");
                    }
                }
                catch { /* non-fatal diagnostics */ }
                try
                {
                    var cs = context.Database.GetConnectionString();
                    Logger.Information("StudentsViewModel using connection: {ConnectionString}", cs ?? "(null)");
                }
                catch { /* ignore diagnostics failures */ }
                var students = await context.Students
                    .OrderBy(s => s.StudentName)
                    .ToListAsync();

                // Bulk replace strategy to avoid reentrancy issues (ObservableCollection mutation during CollectionChanged processing)
                // We create a new collection and swap the reference so the grid sees a single Reset.
                // Reentrancy-safe refresh: suppress view refresh while we batch update the existing collection
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
                OnPropertyChanged(nameof(TotalStudents));
                OnPropertyChanged(nameof(ActiveStudents));
                StatusMessage = $"Loaded {Students.Count} students";

                // Initialize selection to first row to enable edit-related commands by default
                if (SelectedStudent == null && Students.Count > 0)
                {
                    SelectedStudent = Students[0];
                }
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
                using (LogContext.PushProperty("Operation", "BulkAssignRoute"))
                {
                    if (AvailableRoutes.Count == 0)
                    {
                        StatusMessage = "No routes available";
                        return;
                    }

                    // Determine target route (first active preferred)
                    var targetRoute = AvailableRoutes.FirstOrDefault(r => r.IsActive) ?? AvailableRoutes[0];

                    // Gather target students:
                    // If a student is selected, treat that as a single-target bulk (user intent is explicit)
                    // Otherwise assign all currently filtered students that lack BOTH AM & PM routes.
                    var visibleStudents = StudentsView.Cast<Core.Models.Student>().ToList();
                    var candidates = SelectedStudent != null
                        ? new List<Core.Models.Student> { SelectedStudent }
                        : visibleStudents.Where(s => string.IsNullOrWhiteSpace(s.AMRoute) || string.IsNullOrWhiteSpace(s.PMRoute)).ToList();

                    if (candidates.Count == 0)
                    {
                        StatusMessage = "No eligible students (all have AM & PM routes)";
                        return;
                    }

                    // Cap very large operations for MVP safety
                    const int MaxBatch = 500;
                    if (candidates.Count > MaxBatch)
                    {
                        candidates = candidates.Take(MaxBatch).ToList();
                        Logger.Warning("Bulk assignment candidate list truncated to {MaxBatch}", MaxBatch);
                    }

                    var affected = 0;
                    var context = _contextFactory.CreateWriteDbContext();
                    try
                    {
                        var ids = candidates.Select(c => c.StudentId).ToHashSet();
                        var dbStudents = context.Students.Where(s => ids.Contains(s.StudentId)).ToList();
                        foreach (var db in dbStudents)
                        {
                            // Mirror heuristic: fill AM then PM; if both present skip unless explicit single selection (overwrite AM)
                            if (SelectedStudent != null && db.StudentId == SelectedStudent.StudentId && !string.IsNullOrWhiteSpace(db.AMRoute) && !string.IsNullOrWhiteSpace(db.PMRoute))
                            {
                                db.AMRoute = targetRoute.RouteName; // explicit overwrite
                            }
                            else if (string.IsNullOrWhiteSpace(db.AMRoute))
                            {
                                db.AMRoute = targetRoute.RouteName;
                            }
                            else if (string.IsNullOrWhiteSpace(db.PMRoute))
                            {
                                db.PMRoute = targetRoute.RouteName;
                            }
                            else
                            {
                                continue; // both set & not single explicit selection
                            }
                            affected++;
                            // Reflect in-memory model
                            var inMem = candidates.FirstOrDefault(c => c.StudentId == db.StudentId);
                            if (inMem != null)
                            {
                                inMem.AMRoute = db.AMRoute;
                                inMem.PMRoute = db.PMRoute;
                            }
                        }
                        if (affected > 0)
                        {
                            context.SaveChanges();
                            try
                            {
                                // Recompute and persist Route.StudentCount after bulk assignment (MVP requirement)
                                // Count unique students whose AMRoute or PMRoute matches the target route name
                                var routeEntity = context.Routes.FirstOrDefault(r => r.RouteId == targetRoute.RouteId);
                                if (routeEntity != null)
                                {
                                    var routeName = routeEntity.RouteName; // ensure any DB-normalized value
                                    var newCount = context.Students.Count(s => s.AMRoute == routeName || s.PMRoute == routeName);
                                    routeEntity.StudentCount = newCount;
                                    context.SaveChanges();
                                    Logger.Information("Route.StudentCount recomputed and saved — RouteId={RouteId}, RouteName={RouteName}, StudentCount={StudentCount}", routeEntity.RouteId, routeEntity.RouteName, newCount);
                                }
                                else
                                {
                                    Logger.Warning("Target route not found during StudentCount recompute — RouteId={RouteId}", targetRoute.RouteId);
                                }
                            }
                            catch (Exception exCount)
                            {
                                Logger.Error(exCount, "Failed recomputing Route.StudentCount after bulk assignment — proceeding without blocking UI");
                            }
                        }
                    }
                    finally
                    {
                        context.Dispose();
                    }

                    Logger.Information("Bulk route assignment completed: Route {RouteId}:{RouteName} applied to {Count} students (SelectedMode={SelectedMode})",
                        targetRoute.RouteId, targetRoute.RouteName, affected, SelectedStudent != null);
                    StatusMessage = affected == 0
                        ? "No students updated"
                        : $"Assigned {targetRoute.RouteName} to {affected} student(s)";
                    OnPropertyChanged(nameof(StudentsWithRoutes));
                    OnPropertyChanged(nameof(UnassignedStudents));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error executing bulk assign route command");
                StatusMessage = "Error in bulk route assignment";
            }
        }

        private bool CanExecuteBulkAssignRoute()
        {
            var can = HasSelectedStudent && !IsLoading;
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
                StatusMessage = "Opening quick actions";
                var qc = new Views.Student.QuickActionsDialog();
                var host = new System.Windows.Window
                {
                    Title = "Quick Actions",
                    Content = qc,
                    SizeToContent = System.Windows.SizeToContent.WidthAndHeight,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                    Owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current?.MainWindow
                };
                qc.RequestCloseByHost += (s, _) => host.Close();
                host.ShowDialog();
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
