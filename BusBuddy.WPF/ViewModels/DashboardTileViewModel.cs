using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.Core.Data.UnitOfWork;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Panels;
using Serilog;
using Serilog.Context;
using Syncfusion.Windows.Tools.Controls;
using CommunityToolkit.Mvvm.Input;
using Commands = BusBuddy.WPF.Commands;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Represents a tile in the modernized dashboard using SfTileView
    /// </summary>
    public class DashboardTileViewModel : PanelViewModel
    {
        private string? _title;
        private object? _content;
        private string _state = "Normal";
        private int _priority;
        private bool _isRefreshing;
        private DateTime _lastUpdated = DateTime.Now;

        public string? Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public object? Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public string State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        public int Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(); }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set { _lastUpdated = value; OnPropertyChanged(); }
        }

        public string LastUpdatedFormatted => LastUpdated.ToString("HH:mm:ss");

        // Commands for tile interactions
        public ICommand? RefreshCommand { get; set; }
        public ICommand? MaximizeCommand { get; set; }
        public ICommand? MinimizeCommand { get; set; }
        public ICommand? CloseCommand { get; set; }

        public DashboardTileViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(RefreshTileAsync);
            MaximizeCommand = new Commands.RelayCommand(_ => State = "Maximized");
            MinimizeCommand = new Commands.RelayCommand(_ => State = "Minimized");
        }

        public virtual async Task RefreshTileAsync()
        {
            IsRefreshing = true;
            try
            {
                // Override in derived classes for specific refresh logic
                await Task.Delay(1000); // Simulate async operation
                LastUpdated = DateTime.Now;
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }

    /// <summary>
    /// Specific tile for fleet status display
    /// </summary>
    public class FleetStatusTileViewModel : DashboardTileViewModel
    {
        private static readonly ILogger Logger = Log.ForContext<FleetStatusTileViewModel>();
        private readonly IUnitOfWork _unitOfWork;
        private int _activeBusCount;
        private int _maintenanceBusCount;
        private int _outOfServiceCount;

        public int ActiveBusCount
        {
            get => _activeBusCount;
            set { _activeBusCount = value; OnPropertyChanged(); }
        }

        public int MaintenanceBusCount
        {
            get => _maintenanceBusCount;
            set { _maintenanceBusCount = value; OnPropertyChanged(); }
        }

        public int OutOfServiceCount
        {
            get => _outOfServiceCount;
            set { _outOfServiceCount = value; OnPropertyChanged(); }
        }

        public int TotalBusCount => ActiveBusCount + MaintenanceBusCount + OutOfServiceCount;

        public FleetStatusTileViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            Title = "Fleet Status";
            Priority = 1;
        }

        public override async Task RefreshTileAsync()
        {
            IsRefreshing = true;
            try
            {
                // Get real fleet data from repository
                var busCountByStatus = await _unitOfWork.Buses.GetBusCountByStatusAsync();

                // Map status values to our display properties
                ActiveBusCount = busCountByStatus.GetValueOrDefault("Active", 0);
                MaintenanceBusCount = busCountByStatus.GetValueOrDefault("Maintenance", 0) +
                                     busCountByStatus.GetValueOrDefault("In Maintenance", 0);
                OutOfServiceCount = busCountByStatus.GetValueOrDefault("Out of Service", 0) +
                                   busCountByStatus.GetValueOrDefault("Inactive", 0);

                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                // Log error and fallback to default values
                Logger.Error(ex, "Error refreshing fleet status in FleetStatusTileViewModel");

                // Fallback values if database is unavailable
                ActiveBusCount = 0;
                MaintenanceBusCount = 0;
                OutOfServiceCount = 0;
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }

    /// <summary>
    /// Specific tile for maintenance alerts
    /// </summary>
    public class MaintenanceAlertsTileViewModel : DashboardTileViewModel
    {
        private static readonly ILogger Logger = Log.ForContext<MaintenanceAlertsTileViewModel>();
        private readonly IUnitOfWork _unitOfWork;
        private int _criticalMaintenanceCount;
        private int _upcomingMaintenanceCount;
        private int _overdueMaintenanceCount;

        public int CriticalMaintenanceCount
        {
            get => _criticalMaintenanceCount;
            set { _criticalMaintenanceCount = value; OnPropertyChanged(); }
        }

        public int UpcomingMaintenanceCount
        {
            get => _upcomingMaintenanceCount;
            set { _upcomingMaintenanceCount = value; OnPropertyChanged(); }
        }

        public int OverdueMaintenanceCount
        {
            get => _overdueMaintenanceCount;
            set { _overdueMaintenanceCount = value; OnPropertyChanged(); }
        }

        public MaintenanceAlertsTileViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            Title = "Maintenance Alerts";
            Priority = 2;
        }

        public override async Task RefreshTileAsync()
        {
            IsRefreshing = true;
            try
            {
                // Get real maintenance data from repository
                var upcomingMaintenance = await _unitOfWork.MaintenanceRecords.GetUpcomingMaintenanceAsync(30);
                var overdueMaintenance = await _unitOfWork.MaintenanceRecords.GetOverdueMaintenanceAsync();
                var busesDueForInspection = await _unitOfWork.Buses.GetBusesWithExpiredInspectionAsync();

                // Calculate counts based on real data
                UpcomingMaintenanceCount = upcomingMaintenance.Count();
                OverdueMaintenanceCount = overdueMaintenance.Count();
                CriticalMaintenanceCount = busesDueForInspection.Count();

                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                // Log error and fallback to default values
                Logger.Error(ex, "Error refreshing maintenance alerts in MaintenanceAlertsTileViewModel");

                // Fallback values if database is unavailable
                CriticalMaintenanceCount = 0;
                UpcomingMaintenanceCount = 0;
                OverdueMaintenanceCount = 0;
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }

    /// <summary>
    /// Quick actions tile for common operations
    /// </summary>
    public class QuickActionsTileViewModel : DashboardTileViewModel
    {
        private readonly Action<string>? _navigationAction;

        public ICommand QuickAddStudentCommand { get; set; }
        public ICommand QuickAddBusCommand { get; set; }
        public ICommand QuickScheduleTripCommand { get; set; }
        public ICommand QuickMaintenanceCommand { get; set; }
        public ICommand QuickFuelEntryCommand { get; set; }
        public ICommand QuickReportCommand { get; set; }

        public QuickActionsTileViewModel(Action<string>? navigationAction = null)
        {
            _navigationAction = navigationAction;
            Title = "Quick Actions";
            Priority = 5;
            State = "Minimized";

            // Initialize commands with actual navigation
            QuickAddStudentCommand = new Commands.RelayCommand(_ => _navigationAction?.Invoke("Students"));
            QuickAddBusCommand = new Commands.RelayCommand(_ => _navigationAction?.Invoke("Buses"));
            QuickScheduleTripCommand = new Commands.RelayCommand(_ => _navigationAction?.Invoke("Schedule"));
            QuickMaintenanceCommand = new Commands.RelayCommand(_ => _navigationAction?.Invoke("Maintenance"));
            QuickFuelEntryCommand = new Commands.RelayCommand(_ => _navigationAction?.Invoke("Fuel"));
            QuickReportCommand = new Commands.RelayCommand(_ => _navigationAction?.Invoke("Activity"));
        }
    }

    /// <summary>
    /// Sports trips dashboard tile for quick overview of upcoming sports events
    /// </summary>
    public class SportsTripsOverviewTileViewModel : DashboardTileViewModel
    {
        private static readonly ILogger Logger = Log.ForContext<SportsTripsOverviewTileViewModel>();
        private readonly IScheduleService _scheduleService;

        private int _upcomingVolleyballGames;
        private int _upcomingFootballGames;
        private int _upcomingJuniorHighGames;
        private int _totalSportsTrips;
        private int _awayGames;
        private int _homeGames;
        private string _nextGameInfo = "No upcoming games";

        public int UpcomingVolleyballGames
        {
            get => _upcomingVolleyballGames;
            set { _upcomingVolleyballGames = value; OnPropertyChanged(); }
        }

        public int UpcomingFootballGames
        {
            get => _upcomingFootballGames;
            set { _upcomingFootballGames = value; OnPropertyChanged(); }
        }

        public int UpcomingJuniorHighGames
        {
            get => _upcomingJuniorHighGames;
            set { _upcomingJuniorHighGames = value; OnPropertyChanged(); }
        }

        public int TotalSportsTrips
        {
            get => _totalSportsTrips;
            set { _totalSportsTrips = value; OnPropertyChanged(); }
        }

        public int AwayGames
        {
            get => _awayGames;
            set { _awayGames = value; OnPropertyChanged(); }
        }

        public int HomeGames
        {
            get => _homeGames;
            set { _homeGames = value; OnPropertyChanged(); }
        }

        public string NextGameInfo
        {
            get => _nextGameInfo;
            set { _nextGameInfo = value; OnPropertyChanged(); }
        }

        public SportsTripsOverviewTileViewModel(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
            Title = "Sports Trips Overview";
            Priority = 3;

            Logger.Information("SportsTripsOverviewTileViewModel initialized");
        }

        public override async Task RefreshTileAsync()
        {
            using (LogContext.PushProperty("Operation", "RefreshSportsTripsOverview"))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                IsRefreshing = true;
                Logger.Information("Starting to refresh sports trips overview");

                try
                {
                    // Get all sports trips (exclude regular routes and activities)
                    var allSportsTrips = await _scheduleService.GetSchedulesByCategoryAsync("Sports");
                    var sportsTrips = allSportsTrips.Where(s => s.ScheduleDate >= DateTime.Today).ToList();

                    // Calculate statistics
                    TotalSportsTrips = sportsTrips.Count;
                    AwayGames = sportsTrips.Count(s => s.IsAwayGame);
                    HomeGames = sportsTrips.Count(s => s.IsHomeGame);

                    // Category-specific counts
                    UpcomingVolleyballGames = sportsTrips.Count(s =>
                        s.SportsCategory?.Contains("Volleyball", StringComparison.OrdinalIgnoreCase) == true);
                    UpcomingFootballGames = sportsTrips.Count(s =>
                        s.SportsCategory?.Contains("Football", StringComparison.OrdinalIgnoreCase) == true);
                    UpcomingJuniorHighGames = sportsTrips.Count(s =>
                        s.SportsCategory?.Contains("Junior High", StringComparison.OrdinalIgnoreCase) == true);

                    // Find next game
                    var nextGame = sportsTrips
                        .Where(s => s.ScheduleDate >= DateTime.Today)
                        .OrderBy(s => s.ScheduleDate)
                        .ThenBy(s => s.ScheduledTime)
                        .FirstOrDefault();

                    if (nextGame != null)
                    {
                        NextGameInfo = $"{nextGame.SportsCategory} vs {nextGame.Opponent} - {nextGame.ScheduleDate:MMM dd} at {nextGame.ScheduledTime:h\\:mm}";
                        Logger.Debug("Next game found: {NextGameInfo}", NextGameInfo);
                    }
                    else
                    {
                        NextGameInfo = "No upcoming games";
                        Logger.Debug("No upcoming games found");
                    }

                    LastUpdated = DateTime.Now;
                    stopwatch.Stop();

                    Logger.Information("Successfully refreshed sports trips overview in {ElapsedMs}ms. " +
                        "Total: {TotalSportsTrips}, Away: {AwayGames}, Home: {HomeGames}, " +
                        "Volleyball: {UpcomingVolleyballGames}, Football: {UpcomingFootballGames}, JH: {UpcomingJuniorHighGames}",
                        stopwatch.ElapsedMilliseconds, TotalSportsTrips, AwayGames, HomeGames,
                        UpcomingVolleyballGames, UpcomingFootballGames, UpcomingJuniorHighGames);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Error refreshing sports trips overview after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                    // Fallback values if service is unavailable
                    TotalSportsTrips = 0;
                    AwayGames = 0;
                    HomeGames = 0;
                    UpcomingVolleyballGames = 0;
                    UpcomingFootballGames = 0;
                    UpcomingJuniorHighGames = 0;
                    NextGameInfo = "Error loading data";
                }
                finally
                {
                    IsRefreshing = false;
                }
            }
        }
    }
}
