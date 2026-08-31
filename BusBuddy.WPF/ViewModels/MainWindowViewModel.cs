using System.Collections.ObjectModel;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Serilog;

namespace BusBuddy.WPF.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private new static readonly ILogger Logger = Log.ForContext<MainWindowViewModel>();

        private readonly IStudentService? _studentService;
        private readonly IDriverService? _driverService;
        private readonly IRouteService? _routeService;
        private readonly IBusService? _busService;

        private string _title = "BusBuddy - School Transportation Management";
        private BusBuddy.Core.Models.Student? _selectedStudent;
        private BusBuddy.Core.Models.Route? _selectedRoute;
        private BusBuddy.Core.Models.Bus? _selectedBus;
        private BusBuddy.Core.Models.Driver? _selectedDriver;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ObservableCollection<BusBuddy.Core.Models.Student> Students { get; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Route> Routes { get; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Bus> Buses { get; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Driver> Drivers { get; } = new();

        public bool HasDatabaseServices => _studentService != null && _routeService != null
            && _busService != null && _driverService != null;

        public BusBuddy.Core.Models.Student? SelectedStudent
        {
            get => _selectedStudent;
            set => SetProperty(ref _selectedStudent, value);
        }

        public BusBuddy.Core.Models.Route? SelectedRoute
        {
            get => _selectedRoute;
            set => SetProperty(ref _selectedRoute, value);
        }

        public BusBuddy.Core.Models.Bus? SelectedBus
        {
            get => _selectedBus;
            set => SetProperty(ref _selectedBus, value);
        }

        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set => SetProperty(ref _selectedDriver, value);
        }

        /// <summary>
        /// Designer / emergency fallback. Grids stay empty. Never invents sample rows.
        /// </summary>
        public MainWindowViewModel()
        {
            StatusMessage = "No database services. Dock grids are empty.";
            Logger.Warning("MainWindowViewModel constructed without services; grids will stay empty");
        }

        public MainWindowViewModel(
            IStudentService studentService,
            IDriverService driverService,
            IRouteService routeService,
            IBusService busService)
        {
            _studentService = studentService;
            _driverService = driverService;
            _routeService = routeService;
            _busService = busService;
            StatusMessage = "Loading from database...";
            Logger.Information("MainWindowViewModel initialized with database services");
            _ = ReloadAllAsync();
        }

        public async Task ReloadAllAsync()
        {
            try
            {
                var studentsTask = _studentService?.GetAllStudentsAsync();
                var routesTask = _routeService?.GetAllRoutesAsync();
                var busesTask = _busService?.GetAllBusesAsync();
                var driversTask = _driverService?.GetAllDriversAsync();

                var pending = new List<Task>(4);
                if (studentsTask != null)
                {
                    pending.Add(studentsTask);
                }

                if (routesTask != null)
                {
                    pending.Add(routesTask);
                }

                if (busesTask != null)
                {
                    pending.Add(busesTask);
                }

                if (driversTask != null)
                {
                    pending.Add(driversTask);
                }

                if (pending.Count > 0)
                {
                    await Task.WhenAll(pending).ConfigureAwait(true);
                }

                if (studentsTask != null)
                {
                    Replace(Students, await studentsTask.ConfigureAwait(true));
                }

                if (routesTask != null)
                {
                    var routesResult = await routesTask.ConfigureAwait(true);
                    if (routesResult.IsSuccess && routesResult.Value != null)
                    {
                        Replace(Routes, routesResult.Value);
                    }
                    else
                    {
                        Logger.Warning("Failed to load routes: {Error}", routesResult.Error);
                    }
                }

                if (busesTask != null)
                {
                    Replace(Buses, await busesTask.ConfigureAwait(true));
                }

                if (driversTask != null)
                {
                    Replace(Drivers, await driversTask.ConfigureAwait(true));
                }

                StatusMessage = HasDatabaseServices
                    ? $"Loaded {Students.Count} students, {Routes.Count} routes, {Buses.Count} buses, {Drivers.Count} drivers"
                    : "No database services. Dock grids are empty.";
                Logger.Information(
                    "Dock grids reloaded Students={Students} Routes={Routes} Buses={Buses} Drivers={Drivers}",
                    Students.Count, Routes.Count, Buses.Count, Drivers.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load data from database; leaving dock grids empty");
                ClearCollections();
                StatusMessage = "Database unavailable. Dock grids are empty.";
            }
        }

        public Task RefreshStudentsAsync() =>
            RefreshAsync(
                _studentService?.GetAllStudentsAsync(),
                Students,
                nameof(RefreshStudentsAsync));

        public async Task RefreshRoutesAsync()
        {
            if (_routeService == null)
            {
                return;
            }

            try
            {
                var routesResult = await _routeService.GetAllRoutesAsync().ConfigureAwait(true);
                if (routesResult.IsSuccess && routesResult.Value != null)
                {
                    Replace(Routes, routesResult.Value);
                }
                else
                {
                    Logger.Warning("Failed to load routes: {Error}", routesResult.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RefreshRoutesAsync failed; retaining existing collection");
            }
        }

        public Task RefreshBusesAsync() =>
            RefreshAsync(
                _busService?.GetAllBusesAsync(),
                Buses,
                nameof(RefreshBusesAsync));

        public Task RefreshDriversAsync() =>
            RefreshAsync(
                _driverService?.GetAllDriversAsync(),
                Drivers,
                nameof(RefreshDriversAsync));

        private async Task RefreshAsync<T>(
            Task<IEnumerable<T>>? load,
            ObservableCollection<T> target,
            string name)
        {
            if (load == null)
            {
                return;
            }

            try
            {
                Replace(target, await load.ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "{Name} failed; retaining existing collection", name);
            }
        }

        private async Task RefreshAsync<T>(
            Task<List<T>>? load,
            ObservableCollection<T> target,
            string name)
        {
            if (load == null)
            {
                return;
            }

            try
            {
                Replace(target, await load.ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "{Name} failed; retaining existing collection", name);
            }
        }

        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items)
            {
                target.Add(item);
            }
        }

        private void ClearCollections()
        {
            Students.Clear();
            Routes.Clear();
            Buses.Clear();
            Drivers.Clear();
        }
    }
}
