using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Services;
using BusBuddy.Core.Models;
using Serilog;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Phase 2 Route Management ViewModel
    /// Enhanced route planning and management functionality
    /// </summary>
    public class RouteManagementViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<RouteManagementViewModel>();
        /// <summary>
        /// Backing collection of routes displayed in the grid. Bound to <see cref="RoutesView"/> for filtering.
        /// </summary>
        public ObservableCollection<BusBuddy.Core.Models.Route> Routes { get; set; } = new();

        /// <summary>
        /// CollectionView wrapper that provides filtering and view operations for <see cref="Routes"/>.
        /// </summary>
        public ICollectionView RoutesView { get; private set; }

        // Entity Framework context for data access
        private readonly IBusBuddyDbContextFactory _contextFactory;

        private BusBuddy.Core.Models.Route? _selectedRoute;
    /// <summary>
    /// Currently selected route in the grid.
    /// </summary>
    public BusBuddy.Core.Models.Route? SelectedRoute
        {
            get => _selectedRoute;
            set
            {
                _selectedRoute = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRouteSelected));
                // Ensure command CanExecute reflects the current selection state
                // Using WPF's CommandManager to prompt a requery for CanExecute
                CommandManager.InvalidateRequerySuggested();
            }
        }

    /// <summary>
    /// Indicates whether a route is currently selected in the grid.
    /// </summary>
        public bool IsRouteSelected => SelectedRoute is not null;

        private string _quickSearchText = string.Empty;
    /// <summary>
    /// Text used to filter the routes list (case-insensitive contains on name, description, and school).
    /// </summary>
        public string QuickSearchText
        {
            get => _quickSearchText;
            set
            {
                if (_quickSearchText != value)
                {
                    _quickSearchText = value;
                    OnPropertyChanged();
                    RoutesView.Refresh();
                }
            }
        }

        private string _statusMessage = "Ready";
    /// <summary>
    /// Simple status text surfaced to the UI (e.g., load results or error messages).
    /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

    /// <summary>
    /// Total number of routes in the current <see cref="Routes"/> collection.
    /// </summary>
        public int TotalRoutes => Routes.Count;
    /// <summary>
    /// Number of active routes.
    /// </summary>
        public int ActiveRoutes => Routes.Count(r => r.IsActive);
    /// <summary>
    /// Aggregate count of assigned students across all routes (null-safe).
    /// </summary>
        public int TotalAssignedStudents => Routes.Sum(r => r.StudentCount ?? 0);

    // Commands used by RouteManagementView toolbar
    public ICommand AddRouteCommand { get; }
    public ICommand EditRouteCommand { get; }
    public ICommand DeleteRouteCommand { get; }
    public ICommand GenerateScheduleCommand { get; }
    public ICommand ViewMapCommand { get; }
    public ICommand AssignStudentsCommand { get; }
    public ICommand AssignVehicleCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand PrintScheduleCommand { get; }
    public ICommand PrintRouteMapsCommand { get; }
    public ICommand RefreshCommand { get; }

        public RouteManagementViewModel()
        {
            // Initialize EF context factory
            _contextFactory = new BusBuddyDbContextFactory();
            RoutesView = CollectionViewSource.GetDefaultView(Routes);
            RoutesView.Filter = FilterRoutes;

            // Kick off async load
            _ = LoadRoutesAsync();

            // Wire commands
            AddRouteCommand = new RelayCommand(AddRoute);
            EditRouteCommand = new RelayCommand(EditSelectedRoute, () => IsRouteSelected);
            DeleteRouteCommand = new RelayCommand(DeleteSelectedRoute, () => IsRouteSelected);
            GenerateScheduleCommand = new RelayCommand(GenerateSchedule, () => IsRouteSelected);
            ViewMapCommand = new RelayCommand(OpenMapView);
            AssignStudentsCommand = new RelayCommand(AssignStudents, () => IsRouteSelected);
            AssignVehicleCommand = new RelayCommand(AssignVehicle, () => IsRouteSelected);
            ExportCsvCommand = new RelayCommand(ExportCsv);
            ExportReportCommand = new RelayCommand(ExportReport);
            PrintScheduleCommand = new RelayCommand(PrintSchedule);
            PrintRouteMapsCommand = new RelayCommand(PrintMaps);
            RefreshCommand = new AsyncRelayCommand(LoadRoutesAsync);

            // Ensure initial command states reflect current selection
            RefreshSelectionDependentCommands();
        }

        /// <summary>
        /// Loads routes from the database into the Routes collection and refreshes the view.
        /// </summary>
        private async Task LoadRoutesAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Async ordered load; DbContext configured for NoTracking when reading (if applicable)
                var routes = await context.Routes
                    .OrderBy(r => r.RouteName)
                    .ToListAsync()
                    .ConfigureAwait(true); // resume on UI thread

                Routes.Clear();
                foreach (var r in routes)
                {
                    Routes.Add(r);
                }

                RoutesView.Refresh();
                StatusMessage = $"Loaded {Routes.Count} routes";
                OnPropertyChanged(nameof(TotalRoutes));
                OnPropertyChanged(nameof(ActiveRoutes));
                OnPropertyChanged(nameof(TotalAssignedStudents));

                // Update command states after data refresh
                RefreshSelectionDependentCommands();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load routes from database");
                StatusMessage = $"Error loading routes: {ex.Message}";
                // Keep any existing items; no sample data injection on failure
            }
        }

        /// <summary>
        /// Predicate used by RoutesView to filter the collection based on QuickSearchText.
        /// </summary>
        private bool FilterRoutes(object obj)
        {
            if (obj is not BusBuddy.Core.Models.Route r)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(QuickSearchText))
            {
                return true;
            }
            var q = QuickSearchText.Trim();
            return (r.RouteName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (r.Description?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (r.School?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void AddRoute()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var newRoute = new BusBuddy.Core.Models.Route
                {
                    RouteName = $"Route {DateTime.Now:HHmmss}",
                    School = SelectedRoute?.School ?? "Wiley School District",
                    Date = DateTime.Today,
                    IsActive = true
                };
                context.Routes.Add(newRoute);
                context.SaveChanges();

                // Reflect in UI without full reload
                Routes.Add(newRoute);
                RoutesView.Refresh();
                OnPropertyChanged(nameof(TotalRoutes));
                OnPropertyChanged(nameof(ActiveRoutes));
                StatusMessage = $"Added route '{newRoute.RouteName}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to add route");
                StatusMessage = $"Error adding route: {ex.Message}";
            }
        }

        private void CopyRoute()
        {
            if (SelectedRoute is not null)
            {
                var copiedRoute = new BusBuddy.Core.Models.Route
                {
                    RouteName = $"Copy of {SelectedRoute.RouteName}",
                    School = SelectedRoute.School
                };
                // TODO: Add to service in Phase 2
                _ = LoadRoutesAsync();
            }
        }

    /// <summary>
    /// Stubs to satisfy UI; can be implemented later
    /// </summary>
        private void EditSelectedRoute()
        {
            if (SelectedRoute is null) return;
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var route = context.Routes.FirstOrDefault(r => r.RouteId == SelectedRoute.RouteId);
                if (route is null)
                {
                    StatusMessage = "Selected route not found";
                    return;
                }
                // Minimal edit: ensure name trimmed and active by default
                route.RouteName = string.IsNullOrWhiteSpace(SelectedRoute.RouteName)
                    ? $"Route-{route.RouteId}"
                    : SelectedRoute.RouteName.Trim();
                route.Description = SelectedRoute.Description;
                route.School = SelectedRoute.School;
                route.Date = SelectedRoute.Date;
                route.IsActive = SelectedRoute.IsActive;
                context.SaveChanges();
                StatusMessage = $"Saved changes for '{route.RouteName}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save route");
                StatusMessage = $"Error saving route: {ex.Message}";
            }
        }

        private void DeleteSelectedRoute()
        {
            if (SelectedRoute is null) return;
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var route = context.Routes.FirstOrDefault(r => r.RouteId == SelectedRoute.RouteId);
                if (route is null)
                {
                    StatusMessage = "Selected route not found";
                    return;
                }
                var name = route.RouteName;
                context.Routes.Remove(route);
                context.SaveChanges();

                // Update UI collection
                Routes.Remove(SelectedRoute);
                SelectedRoute = null; // triggers CanExecute updates
                RoutesView.Refresh();
                OnPropertyChanged(nameof(TotalRoutes));
                OnPropertyChanged(nameof(ActiveRoutes));
                StatusMessage = $"Deleted route '{name}'";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete route");
                StatusMessage = $"Error deleting route: {ex.Message}";
            }
        }
    private void GenerateSchedule() { StatusMessage = "Generated schedule (stub)"; }
    private void OpenMapView() { StatusMessage = "Opening map (stub)"; }
    private void AssignStudents()
    {
        if (SelectedRoute == null)
        {
            StatusMessage = "Select a route first";
            return;
        }
        try
        {
            StatusMessage = $"Opening assignment for '{SelectedRoute.RouteName}'...";
            // Lazy load to avoid direct dependency if view not used elsewhere
            var assignmentView = new BusBuddy.WPF.Views.Route.RouteAssignmentView(SelectedRoute);
            var window = new System.Windows.Window
            {
                Title = $"Assign Students - {SelectedRoute.RouteName}",
                Content = assignmentView,
                Owner = System.Windows.Application.Current?.MainWindow,
                Width = 1200,
                Height = 800,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
            // After dialog closes refresh counts
            _ = LoadRoutesAsync();
            StatusMessage = $"Closed assignment for '{SelectedRoute.RouteName}'";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed opening student assignment view");
            StatusMessage = $"Error opening assignment: {ex.Message}";
        }
    }
    private void AssignVehicle() { StatusMessage = "Assign vehicle (stub)"; }
    private void ExportCsv() { StatusMessage = "Exported CSV (stub)"; }
    private void ExportReport() { StatusMessage = "Exported report (stub)"; }
    private void PrintSchedule() { StatusMessage = "Printed schedule (stub)"; }
    private void PrintMaps() { StatusMessage = "Printed maps (stub)"; }

        public void Dispose()
        {
            // No-op: context is now always local and disposed via using
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // If selection state changed, refresh commands depending on it
            if (propertyName == nameof(IsRouteSelected))
            {
                RefreshSelectionDependentCommands();
            }
        }

        private void RefreshSelectionDependentCommands()
        {
            // Our lightweight RelayCommand implementation wires CanExecuteChanged to CommandManager.RequerySuggested
            // so forcing a global requery is sufficient.
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
