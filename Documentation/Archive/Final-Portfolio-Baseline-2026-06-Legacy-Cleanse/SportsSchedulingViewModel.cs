using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.SportsScheduling
{
    /// <summary>
    /// ViewModel for sports event scheduling and management
    /// Enhanced for Phase 2 sports scheduling with safety integration
    /// Follows NHTSA safety guidelines and transportation best practices
    /// Phase 1 implementation with simplified MVVM patterns
    /// </summary>
    public class SportsSchedulingViewModel : BaseViewModel
    {
        private readonly ISportsSchedulingService _schedulingService;

        // Collections
        private ObservableCollection<SportsEvent> _sportsEvents = new();
        private ObservableCollection<BusBuddy.Core.Models.Bus> _availableBuses = new();
        private ObservableCollection<BusBuddy.Core.Models.Driver> _availableDrivers = new();

        // Selected items
        private SportsEvent? _selectedEvent;
        private BusBuddy.Core.Models.Bus? _selectedBus;
        private BusBuddy.Core.Models.Driver? _selectedDriver;

        // UI state properties
        private bool _isLoading;
        private string _statusMessage = "Ready";

        // New event form properties
        private string _newEventName = string.Empty;
        private DateTime _newEventStartTime = DateTime.Now.AddHours(1);
        private DateTime _newEventEndTime = DateTime.Now.AddHours(3);
        private string _newEventLocation = string.Empty;
        private int _newEventTeamSize = 15;
        private string _newEventSport = string.Empty;
        private bool _newEventIsHomeGame = true;

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        /// <param name="schedulingService">Sports scheduling service</param>
        public SportsSchedulingViewModel(ISportsSchedulingService schedulingService)
        {
            _schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));

            // Initialize commands
            CreateEventCommand = new RelayCommand(async () => await CreateEventAsync(), () => CanCreateEvent());
            AssignResourcesCommand = new RelayCommand(async () => await AssignResourcesAsync(), () => CanAssignResources());
            RefreshDataCommand = new RelayCommand(async () => await RefreshDataAsync());
            ClearSelectionCommand = new RelayCommand(() => ClearSelection());
            ValidateEventCommand = new RelayCommand(() => ValidateCurrentEvent());

            // Load initial data
            _ = Task.Run(RefreshDataAsync);
        }

        #region Collections Properties

        public ObservableCollection<SportsEvent> SportsEvents
        {
            get => _sportsEvents;
            set => SetProperty(ref _sportsEvents, value);
        }

        public ObservableCollection<BusBuddy.Core.Models.Bus> AvailableBuses
        {
            get => _availableBuses;
            set => SetProperty(ref _availableBuses, value);
        }

        public ObservableCollection<BusBuddy.Core.Models.Driver> AvailableDrivers
        {
            get => _availableDrivers;
            set => SetProperty(ref _availableDrivers, value);
        }

        #endregion

        #region Selection Properties

        public SportsEvent? SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (SetProperty(ref _selectedEvent, value))
                {
                    OnPropertyChanged(nameof(CanAssignResources));
                    _ = Task.Run(LoadAvailableResourcesAsync);
                }
            }
        }

        public BusBuddy.Core.Models.Bus? SelectedBus
        {
            get => _selectedBus;
            set
            {
                if (SetProperty(ref _selectedBus, value))
                {
                    // Notify UI that assignment capability may have changed
                    OnPropertyChanged(nameof(CanAssignResources));
                }
            }
        }

        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (SetProperty(ref _selectedDriver, value))
                {
                    OnPropertyChanged(nameof(CanAssignResources));
                }
            }
        }

        #endregion

        #region UI State Properties

        public new bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public new string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region New Event Form Properties

        public string NewEventName
        {
            get => _newEventName;
            set
            {
                if (SetProperty(ref _newEventName, value))
                {
                    OnPropertyChanged(nameof(CanCreateEvent));
                }
            }
        }

        public DateTime NewEventStartTime
        {
            get => _newEventStartTime;
            set
            {
                if (SetProperty(ref _newEventStartTime, value))
                {
                    // Auto-adjust end time if needed
                    if (NewEventEndTime <= value)
                    {
                        NewEventEndTime = value.AddHours(2);
                    }
                    OnPropertyChanged(nameof(CanCreateEvent));
                }
            }
        }

        public DateTime NewEventEndTime
        {
            get => _newEventEndTime;
            set
            {
                if (SetProperty(ref _newEventEndTime, value))
                {
                    OnPropertyChanged(nameof(CanCreateEvent));
                }
            }
        }

        public string NewEventLocation
        {
            get => _newEventLocation;
            set
            {
                if (SetProperty(ref _newEventLocation, value))
                {
                    OnPropertyChanged(nameof(CanCreateEvent));
                }
            }
        }

        public int NewEventTeamSize
        {
            get => _newEventTeamSize;
            set
            {
                if (SetProperty(ref _newEventTeamSize, value))
                {
                    OnPropertyChanged(nameof(CanCreateEvent));
                }
            }
        }

        public string NewEventSport
        {
            get => _newEventSport;
            set => SetProperty(ref _newEventSport, value);
        }

        public bool NewEventIsHomeGame
        {
            get => _newEventIsHomeGame;
            set => SetProperty(ref _newEventIsHomeGame, value);
        }

        #endregion

        #region Commands

        public ICommand CreateEventCommand { get; }
        public ICommand AssignResourcesCommand { get; }
        public ICommand RefreshDataCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand ValidateEventCommand { get; }

        #endregion

        #region Command Implementation

        private bool CanCreateEvent()
        {
            return !IsLoading &&
                   !string.IsNullOrWhiteSpace(NewEventName) &&
                   !string.IsNullOrWhiteSpace(NewEventLocation) &&
                   NewEventTeamSize > 0 &&
                   NewEventStartTime < NewEventEndTime &&
                   NewEventStartTime > DateTime.Now;
        }

        private async Task CreateEventAsync()
        {
            if (!CanCreateEvent())
            {
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Creating sports event...";

                var newEvent = new SportsEvent
                {
                    EventName = NewEventName,
                    StartTime = NewEventStartTime,
                    EndTime = NewEventEndTime,
                    Location = NewEventLocation,
                    TeamSize = NewEventTeamSize,
                    Sport = NewEventSport,
                    IsHomeGame = NewEventIsHomeGame,
                    Status = "Pending",
                    SafetyNotes = string.Empty // Will be auto-populated by service
                };

                var createdEvent = await _schedulingService.CreateSportsEventAsync(newEvent);

                SportsEvents.Add(createdEvent);
                SelectedEvent = createdEvent;

                // Clear form
                ClearNewEventForm();

                StatusMessage = $"Event '{createdEvent.EventName}' created successfully";
                MessageBox.Show($"Sports event '{createdEvent.EventName}' has been created successfully!",
                    "Event Created", MessageBoxButton.OK, MessageBoxImage.Information);

                Logger.Information("Created new sports event {EventName} for {Sport}",
                    createdEvent.EventName, createdEvent.Sport);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error creating event";
                var errorMessage = $"Failed to create sports event: {ex.Message}";
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error(ex, "Error creating sports event {EventName}", NewEventName);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanAssignResources()
        {
            return !IsLoading &&
                   SelectedEvent != null &&
                   SelectedBus != null &&
                   SelectedDriver != null &&
                   SelectedEvent.BusId == null &&
                   SelectedEvent.DriverId == null;
        }

        private async Task AssignResourcesAsync()
        {
            if (!CanAssignResources() || SelectedEvent == null || SelectedBus == null || SelectedDriver == null)
            {
                MessageBox.Show("Please select an unassigned event, an available bus, and an available driver.",
                    "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Assigning resources...";

                var success = await _schedulingService.AssignVehicleAndDriverAsync(
                    SelectedEvent.Id, SelectedBus.Id, SelectedDriver.DriverId);

                if (success)
                {
                    // Update the event
                    SelectedEvent.BusId = SelectedBus.Id;
                    SelectedEvent.DriverId = SelectedDriver.DriverId;
                    SelectedEvent.Vehicle = SelectedBus;
                    SelectedEvent.Driver = SelectedDriver;
                    SelectedEvent.Status = "Assigned";

                    StatusMessage = "Resources assigned successfully";
                    MessageBox.Show($"Bus '{SelectedBus.LicensePlate}' and Driver '{SelectedDriver.DriverName}' have been assigned to '{SelectedEvent.EventName}'.",
                        "Assignment Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh available resources
                    await LoadAvailableResourcesAsync();

                    Logger.Information("Assigned Vehicle {VehicleId} and Driver {DriverId} to Event {EventId}",
                        SelectedBus.Id, SelectedDriver.DriverId, SelectedEvent.Id);
                }
                else
                {
                    StatusMessage = "Assignment failed";
                    MessageBox.Show("Failed to assign resources. There may be a scheduling conflict or capacity issue.",
                        "Assignment Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error assigning resources";
                var errorMessage = $"Failed to assign resources: {ex.Message}";
                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error(ex, "Error assigning resources to event {EventId}", SelectedEvent?.Id);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading sports events...";

                var events = await _schedulingService.GetSportsEventsAsync();

                SportsEvents.Clear();
                foreach (var sportsEvent in events)
                {
                    SportsEvents.Add(sportsEvent);
                }

                StatusMessage = $"Loaded {events.Count} sports events";
                Logger.Information("Refreshed sports events data: {Count} events loaded", events.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading data";
                MessageBox.Show($"Failed to load sports events: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error(ex, "Error refreshing sports events data");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAvailableResourcesAsync()
        {
            if (SelectedEvent == null)
            {
                return;
            }

            try
            {
                var buses = await _schedulingService.GetAvailableVehiclesAsync(
                    SelectedEvent.StartTime, SelectedEvent.EndTime, SelectedEvent.TeamSize);

                var drivers = await _schedulingService.GetAvailableDriversAsync(
                    SelectedEvent.StartTime, SelectedEvent.EndTime);

                AvailableBuses.Clear();
                foreach (var bus in buses)
                {
                    AvailableBuses.Add(bus);
                }

                AvailableDrivers.Clear();
                foreach (var driver in drivers)
                {
                    AvailableDrivers.Add(driver);
                }

                Logger.Information("Loaded {BusCount} available buses and {DriverCount} available drivers for event {EventId}",
                    buses.Count, drivers.Count, SelectedEvent.Id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading available resources for event {EventId}", SelectedEvent?.Id);
            }
        }

        private void ClearSelection()
        {
            SelectedEvent = null;
            SelectedBus = null;
            SelectedDriver = null;
            AvailableBuses.Clear();
            AvailableDrivers.Clear();
            StatusMessage = "Selection cleared";
        }

        private void ValidateCurrentEvent()
        {
            if (SelectedEvent == null)
            {
                MessageBox.Show("Please select an event to validate.", "No Event Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var isValid = SelectedEvent.IsEventSafe();
            var message = isValid
                ? $"Event '{SelectedEvent.EventName}' meets all safety requirements."
                : $"Event '{SelectedEvent.EventName}' has safety issues that need to be addressed.";

            var icon = isValid ? MessageBoxImage.Information : MessageBoxImage.Warning;
            MessageBox.Show(message, "Safety Validation", MessageBoxButton.OK, icon);

            Logger.Information("Safety validation for event {EventId}: {IsValid}", SelectedEvent.Id, isValid);
        }

        private void ClearNewEventForm()
        {
            NewEventName = string.Empty;
            NewEventStartTime = DateTime.Now.AddHours(1);
            NewEventEndTime = DateTime.Now.AddHours(3);
            NewEventLocation = string.Empty;
            NewEventTeamSize = 15;
            NewEventSport = string.Empty;
            NewEventIsHomeGame = true;
        }

        #endregion
    }
}
