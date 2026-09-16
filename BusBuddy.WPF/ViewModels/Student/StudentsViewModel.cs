using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using System.Windows.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Data;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Serilog;
using Serilog.Context;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using BusBuddy.WPF.Messages;

namespace BusBuddy.WPF.ViewModels.Student
{
    /// <summary>
    /// ViewModel for the StudentsView — owns the bindable surface of the students grid and delegates
    /// each operation to a focused coordinator in this folder.
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
        private readonly StudentsDialogCoordinator _dialogs;
        private readonly StudentsFilterCoordinator _filter;
        private readonly StudentsCsvCoordinator _csv;
        private readonly StudentsArchiveCoordinator _archive;
        private readonly StudentsMapCoordinator _map;
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
            _dialogs = new StudentsDialogCoordinator();
            _filter = new StudentsFilterCoordinator();
            _csv = new StudentsCsvCoordinator(_contextFactory);
            _archive = new StudentsArchiveCoordinator(_list);
            _map = new StudentsMapCoordinator();
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
            _dialogs = new StudentsDialogCoordinator();
            _filter = new StudentsFilterCoordinator(_studentService);
            _csv = new StudentsCsvCoordinator(_contextFactory);
            _archive = new StudentsArchiveCoordinator(_list);
            _map = new StudentsMapCoordinator();
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
            _dialogs = new StudentsDialogCoordinator();
            _filter = new StudentsFilterCoordinator();
            _csv = new StudentsCsvCoordinator(_contextFactory);
            _archive = new StudentsArchiveCoordinator(_list);
            _map = new StudentsMapCoordinator();
            Students = new ObservableCollection<Core.Models.Student>();
            StudentsView = CollectionViewSource.GetDefaultView(Students);
            StudentsView.Filter = StudentFilter;

            InitializeCommands();
            Logger.Debug("StudentsViewModel (test) initialized — commands created");
        }

        private void SubscribeToSaveNotifications()
        {
            // Refresh list and show success message immediately when a student is saved from the form
            WeakReferenceMessenger.Default.Register<StudentsViewModel, StudentSavedMessage>(this, async (r, _) =>
            {
                try
                {
                    Logger.Information("StudentSavedMessage received — refreshing list");
                    await r.LoadStudentsAsync();
                    r.StatusMessage = "Successfully Saved";
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error refreshing after save");
                }
            });

            WeakReferenceMessenger.Default.Register<StudentsViewModel, StudentsImportedMessage>(this, async (r, msg) =>
            {
                try
                {
                    Logger.Information("StudentsImportedMessage received — refreshing list Added={Added}", msg.Added);
                    await r.LoadStudentsAsync();
                    r.StatusMessage = msg.Added == 0
                        ? "No new students imported (file empty or names already exist)"
                        : $"Imported {msg.Added} student(s) from CSV";
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error refreshing after CSV import");
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
                    OnPropertyChanged(nameof(ArchiveStudentButtonLabel));
                    // Ensure selection-dependent commands update their CanExecute state
                    NotifySelectionDependentCommands();
                    Logger.Debug("Selection-dependent commands invalidated (CanExecute re-evaluated)");
                }
            }
        }

        /// <summary>
        /// Whether a student is currently selected
        /// </summary>
        public bool HasSelectedStudent => SelectedStudent != null;

        /// <summary>
        /// Toolbar label for archive/restore when the child may return. Delete is a separate command.
        /// </summary>
        public string ArchiveStudentButtonLabel =>
            SelectedStudent is { Active: false } ? "Restore Student" : "Archive Student";

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
        public int StudentsWithRoutes => Students.Count(StudentRouteAssignment.IsAssignedAny);

        /// <summary>
        /// Number of students without assigned routes
        /// </summary>
        public int UnassignedStudents => Students.Count(s => !StudentRouteAssignment.IsAssignedAny(s));

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
        /// Whether the grid shows active students, archived students, or both.
        /// Archive keeps the row when the child may return; delete is a separate logged action.
        /// </summary>
        public BusBuddy.WPF.Models.FilterStatus ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (SetProperty(ref _activeFilter, value))
                {
                    Logger.Debug("ActiveFilter changed: {Filter}", _activeFilter);
                    ApplyQuickFilter();
                    OnPropertyChanged(nameof(FilterStatusText));
                }
            }
        }

        private BusBuddy.WPF.Models.FilterStatus _activeFilter = BusBuddy.WPF.Models.FilterStatus.Active;

        /// <summary>Choices offered by the active-status filter control.</summary>
        public IReadOnlyList<BusBuddy.WPF.Models.FilterStatus> AvailableActiveFilters { get; } =
        [
            BusBuddy.WPF.Models.FilterStatus.Active,
            BusBuddy.WPF.Models.FilterStatus.Inactive,
            BusBuddy.WPF.Models.FilterStatus.All,
        ];

        /// <summary>
        /// Status text showing current filter state
        /// </summary>
        public string FilterStatusText => StudentsFilterCoordinator.BuildStatusText(QuickSearchText, ActiveFilter);

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
                    NotifySelectionDependentCommands();
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
        public ICommand EditSchoolCommand { get; private set; } = null!;
        public ICommand DeleteSchoolCommand { get; private set; } = null!;
        public ICommand AddPickupStopCommand { get; private set; } = null!;
        public ICommand EditStudentCommand { get; private set; } = null!;
        public ICommand ArchiveStudentCommand { get; private set; } = null!;
        public ICommand DeleteStudentCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ExportCommand { get; private set; } = null!;
        public ICommand ValidateAddressCommand { get; private set; } = null!;

        /// <summary>
        /// Loads only students whose intake is incomplete. specs/students.md: "Unvalidated addresses
        /// show as incomplete."
        /// </summary>
        public ICommand ShowIncompleteRecordsCommand { get; private set; } = null!;

        // Backing fields to allow NotifyCanExecuteChanged on selection changes
        private RelayCommand? _editStudentRelay;
        private AsyncRelayCommand? _archiveStudentRelay;
        private AsyncRelayCommand? _deleteStudentRelay;
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
        public ICommand SaveGridEditsCommand { get; private set; } = null!; // Inline save for grid edits
        public ICommand SchoolTransferCommand { get; private set; } = null!;
        private RelayCommand? _schoolTransferRelay;

        #endregion

        #region Command Initialization

        /// <summary>
        /// Wire up all commands. Edit/Archive/Validate/BulkAssign use CanExecute predicated on HasSelectedStudent.
        /// </summary>
        private void InitializeCommands()
        {
            // Existing commands
            AddStudentCommand = new RelayCommand(ExecuteAddStudent);
            AddSchoolCommand = new RelayCommand(ExecuteAddSchool);
            EditSchoolCommand = new AsyncRelayCommand(ExecuteEditSchoolAsync);
            DeleteSchoolCommand = new AsyncRelayCommand(ExecuteDeleteSchoolAsync);
            AddPickupStopCommand = new RelayCommand(ExecuteAddPickupStop);
            _editStudentRelay = new RelayCommand(ExecuteEditStudent, CanExecuteEditStudent);
            EditStudentCommand = _editStudentRelay;
            _archiveStudentRelay = new AsyncRelayCommand(ExecuteArchiveStudentAsync, CanExecuteArchiveStudent);
            ArchiveStudentCommand = _archiveStudentRelay;
            _deleteStudentRelay = new AsyncRelayCommand(ExecuteDeleteStudentAsync, CanExecuteDeleteStudent);
            DeleteStudentCommand = _deleteStudentRelay;
            RefreshCommand = new AsyncRelayCommand(LoadStudentsAsync);
            ExportCommand = new RelayCommand(ExecuteExport);
            _validateAddressRelay = new AsyncRelayCommand(ExecuteValidateAddressAsync, CanExecuteValidateAddress);
            ValidateAddressCommand = _validateAddressRelay;
            ShowIncompleteRecordsCommand = new AsyncRelayCommand(ExecuteShowIncompleteRecordsAsync);

            // New enhanced commands
            ImportStudentsCommand = new AsyncRelayCommand(ExecuteImportStudentsAsync);
            _bulkAssignRouteRelay = new AsyncRelayCommand(ExecuteBulkAssignRouteAsync, CanExecuteBulkAssignRoute);
            BulkAssignRouteCommand = _bulkAssignRouteRelay;
            OptimizeRoutesCommand = new AsyncRelayCommand(ExecuteOptimizeRoutes);
            ViewMapCommand = new RelayCommand(ExecuteViewMap);
            ViewOnMapCommand = new AsyncRelayCommand<Core.Models.Student>(ExecuteViewOnMapAsync);
            SuggestRouteCommand = new RelayCommand<Core.Models.Student>(ExecuteSuggestRoute);
            ShowSummaryCommand = new RelayCommand(ExecuteShowSummary);
            SaveGridEditsCommand = new AsyncRelayCommand(SaveInlineGridEditsAsync);
            _schoolTransferRelay = new RelayCommand(ExecuteSchoolTransfer, () => HasSelectedStudent);
            SchoolTransferCommand = _schoolTransferRelay;

            Logger.Debug("Commands initialized: AddStudent/AddSchool/EditSchool/DeleteSchool/Edit/Archive/Delete/Import/BulkAssign/Optimize/ViewMap/ViewOnMap/Suggest/Validate/Refresh/Export/ShowSummary/SchoolTransfer");
        }

        private void NotifySelectionDependentCommands()
        {
            _editStudentRelay?.NotifyCanExecuteChanged();
            _archiveStudentRelay?.NotifyCanExecuteChanged();
            _deleteStudentRelay?.NotifyCanExecuteChanged();
            _validateAddressRelay?.NotifyCanExecuteChanged();
            _bulkAssignRouteRelay?.NotifyCanExecuteChanged();
            _schoolTransferRelay?.NotifyCanExecuteChanged();
        }

        #endregion

        #region Command Handlers — modal forms (StudentsDialogCoordinator)

        private void ExecuteAddStudent() => ApplyDialogOutcome(_dialogs.AddStudent());

        private void ExecuteAddSchool() => ApplyDialogOutcome(_dialogs.AddSchool());

        private async Task ExecuteEditSchoolAsync() =>
            ApplyDialogOutcome(await _dialogs.EditSchoolAsync().ConfigureAwait(true));

        private async Task ExecuteDeleteSchoolAsync() =>
            ApplyDialogOutcome(await _dialogs.DeleteSchoolAsync().ConfigureAwait(true));

        private void ExecuteAddPickupStop() => ApplyDialogOutcome(_dialogs.AddPickupStop());

        private void ExecuteEditStudent() => ApplyDialogOutcome(_dialogs.EditStudent(SelectedStudent));

        private void ExecuteSchoolTransfer() => ApplyDialogOutcome(_dialogs.SchoolTransfer(SelectedStudent));

        /// <summary>Applies the grid-side effects a closed modal form asked for.</summary>
        private void ApplyDialogOutcome(in StudentsDialogOutcome outcome)
        {
            if (outcome.ReloadStudents)
            {
                _ = LoadStudentsAsync();
            }

            if (outcome.ReloadReferenceData)
            {
                _ = LoadReferenceDataAsync();
            }

            if (outcome.SchoolCatalogChanged)
            {
                WeakReferenceMessenger.Default.Send(new SchoolCatalogChangedMessage(outcome.SavedCatalogId));
            }

            if (outcome.PickupStopCatalogChanged)
            {
                WeakReferenceMessenger.Default.Send(new PickupStopCatalogChangedMessage(outcome.SavedCatalogId));
            }

            if (outcome.StatusMessage is not null)
            {
                StatusMessage = outcome.StatusMessage;
            }
        }

        #endregion

        #region Command Handlers — roster operations

        /// <summary>
        /// Archives or restores the selected student. specs/students.md: Archive when the child may
        /// return. <see cref="ExecuteDeleteStudentAsync"/> removes a row after a logged reason.
        /// </summary>
        private async Task ExecuteArchiveStudentAsync()
        {
            var student = SelectedStudent;
            if (student is null)
            {
                return;
            }

            var archiving = student.Active;
            if (!StudentsArchiveCoordinator.Confirm(student, archiving))
            {
                return;
            }

            Logger.Information(
                "Archive student command executed for student {StudentId} Archive={Archive}",
                student.StudentId,
                archiving);

            if (!await _archive.ApplyAsync(student, archiving).ConfigureAwait(true))
            {
                return;
            }

            StudentsView?.Refresh();
            StatusMessage = archiving ? "Student archived" : "Student restored";
            OnPropertyChanged(nameof(TotalStudents));
            OnPropertyChanged(nameof(ActiveStudents));
            OnPropertyChanged(nameof(ArchiveStudentButtonLabel));
            NotifySelectionDependentCommands();
        }

        /// <summary>
        /// Permanently removes the selected student after the clerk picks Mistake, Moved, or Not
        /// attending. specs/students.md: there is no default reason.
        /// </summary>
        private async Task ExecuteDeleteStudentAsync()
        {
            var student = SelectedStudent;
            if (student is null)
            {
                return;
            }

            var request = StudentsArchiveCoordinator.PromptDeletionReason(student);
            if (request is null)
            {
                return;
            }

            if (!await _archive.ApplyDeleteAsync(student, request.Value).ConfigureAwait(true))
            {
                return;
            }

            Students.Remove(student);
            SelectedStudent = null;
            StudentsView?.Refresh();
            StatusMessage = "Student deleted";
            OnPropertyChanged(nameof(TotalStudents));
            OnPropertyChanged(nameof(ActiveStudents));
        }

        /// <summary>
        /// Exports the currently visible (filtered) rows to CSV.
        /// </summary>
        private void ExecuteExport()
        {
            try
            {
                using (LogContext.PushProperty("Operation", "ExportStudents"))
                using (LogContext.PushProperty("Filtered", !string.IsNullOrWhiteSpace(QuickSearchText)))
                {
                    var rows = StudentsView.Cast<Core.Models.Student>().ToList();
                    StudentsCsvCoordinator.Export(rows);
                    StatusMessage = $"Exported {rows.Count} students";
                }
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error executing export command");
                StatusMessage = "Error exporting students";
            }
        }

        /// <summary>
        /// Imports students from a student CSV via <see cref="ISeedDataService"/>.
        /// </summary>
        private async Task ExecuteImportStudentsAsync()
        {
            try
            {
                Logger.Information("Import students command executed");
                var csvPath = StudentsCsvCoordinator.PromptForCsvPath();
                if (csvPath is null)
                {
                    StatusMessage = "CSV import cancelled";
                    return;
                }

                IsLoading = true;
                StatusMessage = "Importing students from CSV...";

                var added = await _csv.ImportAsync(csvPath);
                await LoadStudentsAsync();
                StatusMessage = added == 0
                    ? "No new students imported (file empty or names already exist)"
                    : $"Imported {added} student(s) from CSV";
            }
            catch (Exception ex)
            {
                DatabaseUserMessage.LogFailure(Logger, ex, "Error executing import students command");
                StatusMessage = $"Error importing students: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

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
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error executing validate address command");
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        /// <summary>
        /// Replaces the grid contents with the incomplete-intake roster. Refresh returns to the full roster.
        /// </summary>
        private async Task ExecuteShowIncompleteRecordsAsync()
        {
            using (LogContext.PushProperty("Operation", "ShowIncompleteRecords"))
            {
                try
                {
                    IsLoading = true;
                    var incomplete = await _filter.LoadIncompleteAsync().ConfigureAwait(true);
                    if (incomplete is null)
                    {
                        StatusMessage = "Incomplete-record view unavailable";
                        return;
                    }

                    // Archived rows are already excluded by the service; show everything it returns.
                    ActiveFilter = BusBuddy.WPF.Models.FilterStatus.All;
                    QuickSearchText = string.Empty;

                    Students.Clear();
                    foreach (var student in incomplete)
                    {
                        Students.Add(student);
                    }

                    // These rows bypass StudentsListCoordinator.LoadStudentsAsync, so give them a
                    // baseline: without one an inline save cannot tell an edited address from an
                    // untouched one, and every row would look unchanged.
                    _list.CaptureRowState(Students);

                    SelectedStudent = Students.FirstOrDefault();
                    StudentsView?.Refresh();
                    OnPropertyChanged(nameof(TotalStudents));
                    OnPropertyChanged(nameof(ActiveStudents));

                    Logger.Information("Loaded {Count} incomplete student records", incomplete.Count);
                    StatusMessage = incomplete.Count == 0
                        ? "No incomplete student records"
                        : $"Showing {incomplete.Count} incomplete record(s) — Refresh to see all";
                }
                catch (Exception ex)
                {
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error loading incomplete student records");
                    StatusMessage = "Error loading incomplete records";
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        /// <summary>
        /// Assigns routes to a selection of students via <see cref="StudentsBulkRouteCoordinator"/>.
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
                    var candidates = StudentsBulkRouteCoordinator.SelectCandidates(visibleStudents, SelectedStudent);
                    if (candidates.Count == 0)
                    {
                        StatusMessage = "No eligible students (all have AM & PM routes)";
                        return;
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error executing bulk assign route command");
                StatusMessage = "Error in bulk route assignment";
            }
            finally
            {
                IsLoading = false;
            }
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

                var result = await _bulkRoute.OptimizeUnassignedAsync();
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error executing route optimization");
                StatusMessage = $"Error in route optimization: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
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
                DatabaseUserMessage.LogFailure(Logger, ex, "Error showing summary");
                StatusMessage = "Error generating summary";
            }
        }

        private bool CanExecuteEditStudent()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug("CanExecuteEditStudent evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

        private bool CanExecuteArchiveStudent()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug("CanExecuteArchiveStudent evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

        private bool CanExecuteDeleteStudent()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug(
                "CanExecuteDeleteStudent evaluated — HasSelectedStudent={Has} Result={Can}",
                HasSelectedStudent,
                can);
            return can;
        }

        private bool CanExecuteValidateAddress()
        {
            var can = HasSelectedStudent && !IsLoading && !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress);
            Logger.Debug("CanExecuteValidateAddress evaluated — HasSelectedStudent={Has}, HasAddress={HasAddress}, Result={Result}",
                HasSelectedStudent, !string.IsNullOrWhiteSpace(SelectedStudent?.HomeAddress), can);
            return can;
        }

        private bool CanExecuteBulkAssignRoute()
        {
            var can = HasSelectedStudent && !IsLoading;
            Logger.Debug("CanExecuteBulkAssignRoute evaluated — HasSelectedStudent={Can}", can);
            return can;
        }

        #endregion

        #region Command Handlers — District Map

        private async Task ExecuteViewOnMapAsync(Core.Models.Student? student)
        {
            var status = await _map.ViewOnMapAsync(student).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(status))
            {
                StatusMessage = status;
            }
        }

        private void ExecuteViewMap()
        {
            StatusMessage = "Opening district map with student locations...";
            StatusMessage = _map.ViewMap();
        }

        private void ExecuteSuggestRoute(Core.Models.Student? student)
        {
            var status = _map.SuggestRoute(student, ExecuteOptimizeRoutes);
            if (!string.IsNullOrEmpty(status))
            {
                StatusMessage = status;
            }
        }

        #endregion

        #region Data Operations

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

        private bool StudentFilter(object obj) =>
            StudentsFilterCoordinator.Matches(obj, QuickSearchText, ActiveFilter);

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
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error saving inline grid edits");
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
                    DatabaseUserMessage.LogFailure(Logger, ex, "Error loading students");
                    StatusMessage = "Error loading students. Check connection, migrations, and logs.";
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        /// <summary>
        /// Loads grades, schools, and routes used by dropdowns.
        /// </summary>
        private Task LoadReferenceDataAsync() =>
            _referenceData.LoadSafeAsync(
                AvailableGrades,
                AvailableSchools,
                AvailableRoutes,
                _schoolCatalog,
                _routeCatalog);

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

        #endregion
    }
}
