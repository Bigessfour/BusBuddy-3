using BusBuddy.Core.Mapping;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Activity
{
    /// <summary>
    /// Enhanced Activity Schedule Edit Dialog for Phase 3
    /// Provides comprehensive activity editing with validation and real-time updates
    /// </summary>
    public partial class ActivityScheduleEditDialog : Window
    {
        public ActivityScheduleEditDialogViewModel ViewModel { get; private set; }

        public ActivityScheduleEditDialog(ActivitySchedule? activityToEdit = null)
        {
            InitializeComponent();

            BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);

            ViewModel = new ActivityScheduleEditDialogViewModel(activityToEdit);
            DataContext = ViewModel;
            Loaded += OnLoaded;

            // Configure dialog properties
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await ViewModel.LoadAvailableDataAsync();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try { SfSkinManager.Dispose(this); } catch { }
            base.OnClosed(e);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ValidateActivity())
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// ViewModel for Activity Schedule Edit Dialog - Phase 3 Enhanced
    /// Features: Comprehensive validation, real-time updates, dropdown management
    /// </summary>
    public class ActivityScheduleEditDialogViewModel : INotifyPropertyChanged
    {
        private readonly ActivitySchedule _originalActivity;
        private readonly bool _isEditMode;

        // Activity properties
        private DateTime _scheduledDate = DateTime.Today.AddDays(1);
        private TimeSpan _scheduledLeaveTime = new(8, 0, 0);
        private TimeSpan _scheduledEventTime = new(9, 0, 0);
        private string _tripType = "Field Trip";
        private string _scheduledDestination = string.Empty;
        private int _scheduledRiders = 1;
        private string _requestedBy = Environment.UserName;
        private string _notes = string.Empty;
        private string _status = "Scheduled";

        // Dropdown selections
        private BusBuddy.Core.Models.Driver? _selectedDriver;
        private BusBuddy.Core.Models.Bus? _selectedVehicle;

        // Collections for dropdowns
        public ObservableCollection<BusBuddy.Core.Models.Driver> AvailableDrivers { get; } = new();
        public ObservableCollection<BusBuddy.Core.Models.Bus> AvailableVehicles { get; } = new();
        public ObservableCollection<string> TripTypes { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();

        // Validation
        private string _validationMessage = string.Empty;
        private bool _hasValidationErrors;
        private bool _listsReady;
        private string _leaveTimeText = TimeSpanParser.Format(new TimeSpan(8, 0, 0));
        private string _eventTimeText = TimeSpanParser.Format(new TimeSpan(9, 0, 0));

        public ActivityScheduleEditDialogViewModel(ActivitySchedule? activityToEdit = null)
        {
            _isEditMode = activityToEdit != null;
            _originalActivity = activityToEdit ?? new ActivitySchedule();

            InitializeCollections();

            if (_isEditMode && activityToEdit != null)
            {
                LoadActivityData(activityToEdit);
            }
        }

        #region Properties

        public string DialogTitle => _isEditMode ? "Edit Activity Schedule" : "Add New Activity Schedule";
        public string SaveButtonText => _isEditMode ? "Update Activity" : "Create Activity";

        public string Subject => string.IsNullOrWhiteSpace(ScheduledDestination)
            ? TripType
            : $"{TripType} - {ScheduledDestination}";

        public bool ListsReady
        {
            get => _listsReady;
            private set
            {
                _listsReady = value;
                OnPropertyChanged();
            }
        }

        public string LeaveTimeText
        {
            get => _leaveTimeText;
            set
            {
                _leaveTimeText = value ?? string.Empty;
                OnPropertyChanged();
                if (TimeSpanParser.TryParse(_leaveTimeText, out var parsed))
                {
                    _scheduledLeaveTime = parsed;
                    OnPropertyChanged(nameof(ScheduledLeaveTime));
                    ValidateTime();
                }
                else
                {
                    ValidationMessage = "Leave time must be hh:mm";
                    HasValidationErrors = true;
                }
            }
        }

        public string EventTimeText
        {
            get => _eventTimeText;
            set
            {
                _eventTimeText = value ?? string.Empty;
                OnPropertyChanged();
                if (TimeSpanParser.TryParse(_eventTimeText, out var parsed))
                {
                    _scheduledEventTime = parsed;
                    OnPropertyChanged(nameof(ScheduledEventTime));
                    ValidateTime();
                }
                else
                {
                    ValidationMessage = "Event time must be hh:mm";
                    HasValidationErrors = true;
                }
            }
        }

        public DateTime ScheduledDate
        {
            get => _scheduledDate;
            set
            {
                _scheduledDate = value;
                OnPropertyChanged();
                ValidateDate();
            }
        }

        public TimeSpan ScheduledLeaveTime
        {
            get => _scheduledLeaveTime;
            set
            {
                _scheduledLeaveTime = value;
                _leaveTimeText = TimeSpanParser.Format(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(LeaveTimeText));
                ValidateTime();
            }
        }

        public TimeSpan ScheduledEventTime
        {
            get => _scheduledEventTime;
            set
            {
                _scheduledEventTime = value;
                _eventTimeText = TimeSpanParser.Format(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(EventTimeText));
                ValidateTime();
            }
        }

        public string TripType
        {
            get => _tripType;
            set
            {
                _tripType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subject));
            }
        }

        public string ScheduledDestination
        {
            get => _scheduledDestination;
            set
            {
                _scheduledDestination = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subject));
                ValidateDestination();
            }
        }

        public int ScheduledRiders
        {
            get => _scheduledRiders;
            set
            {
                _scheduledRiders = value;
                OnPropertyChanged();
                ValidateRiders();
            }
        }

        public string RequestedBy
        {
            get => _requestedBy;
            set
            {
                _requestedBy = value;
                OnPropertyChanged();
            }
        }

        public string Notes
        {
            get => _notes;
            set
            {
                _notes = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                _selectedDriver = value;
                OnPropertyChanged();
            }
        }

        public BusBuddy.Core.Models.Bus? SelectedVehicle
        {
            get => _selectedVehicle;
            set
            {
                _selectedVehicle = value;
                OnPropertyChanged();
                ValidateVehicleCapacity();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                _validationMessage = value;
                OnPropertyChanged();
            }
        }

        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            set
            {
                _hasValidationErrors = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Methods

        private void InitializeCollections()
        {
            // Trip Types
            TripTypes.Clear();
            TripTypes.Add("Field Trip");
            TripTypes.Add("Sports Event");
            TripTypes.Add("Academic Competition");
            TripTypes.Add("Special Event");
            TripTypes.Add("Regular Route");
            TripTypes.Add("Emergency Transport");
            TripTypes.Add("Maintenance");

            // Status Options
            StatusOptions.Clear();
            StatusOptions.Add("Scheduled");
            StatusOptions.Add("Confirmed");
            StatusOptions.Add("In Progress");
            StatusOptions.Add("Completed");
            StatusOptions.Add("Cancelled");
        }

        private void LoadActivityData(ActivitySchedule activity)
        {
            ScheduledDate = activity.ScheduledDate;
            ScheduledLeaveTime = activity.ScheduledLeaveTime;
            ScheduledEventTime = activity.ScheduledEventTime;
            TripType = activity.TripType ?? "Field Trip";
            ScheduledDestination = activity.ScheduledDestination ?? string.Empty;
            ScheduledRiders = activity.ScheduledRiders ?? 1;
            RequestedBy = activity.RequestedBy ?? Environment.UserName;
            Notes = activity.Notes ?? string.Empty;
            Status = activity.Status ?? "Scheduled";
            SelectedDriver = AvailableDrivers.FirstOrDefault(d => d.DriverId == activity.ScheduledDriverId);
            SelectedVehicle = AvailableVehicles.FirstOrDefault(v => v.BusId == activity.ScheduledVehicleId);
        }

        public async Task LoadAvailableDataAsync()
        {
            try
            {
                var sp = App.ServiceProvider;
                if (sp is null)
                {
                    ListsReady = true;
                    return;
                }

                using var scope = sp.CreateScope();
                var driverService = scope.ServiceProvider.GetService<IDriverService>();
                var busService = scope.ServiceProvider.GetService<IBusService>();
                var drivers = driverService is null
                    ? Enumerable.Empty<BusBuddy.Core.Models.Driver>()
                    : await driverService.GetAllDriversAsync().ConfigureAwait(true);
                var buses = busService is null
                    ? Enumerable.Empty<BusBuddy.Core.Models.Bus>()
                    : await busService.GetAllBusesAsync().ConfigureAwait(true);

                AvailableDrivers.Clear();
                foreach (var driver in drivers)
                {
                    AvailableDrivers.Add(driver);
                }

                AvailableVehicles.Clear();
                foreach (var bus in buses)
                {
                    AvailableVehicles.Add(bus);
                }

                if (_isEditMode)
                {
                    SelectedDriver = AvailableDrivers.FirstOrDefault(d => d.DriverId == _originalActivity.ScheduledDriverId);
                    SelectedVehicle = AvailableVehicles.FirstOrDefault(v => v.BusId == _originalActivity.ScheduledVehicleId);
                }

                ListsReady = true;
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error loading data: {ex.Message}";
                HasValidationErrors = true;
            }
        }

        public bool ValidateActivity()
        {
            var errors = new List<string>();

            if (!ListsReady)
            {
                errors.Add("Still loading drivers and vehicles");
            }

            if (!TimeSpanParser.TryParse(_leaveTimeText, out var leave))
            {
                errors.Add("Leave time must be hh:mm");
            }
            else
            {
                _scheduledLeaveTime = leave;
            }

            if (!TimeSpanParser.TryParse(_eventTimeText, out var eventTime))
            {
                errors.Add("Event time must be hh:mm");
            }
            else
            {
                _scheduledEventTime = eventTime;
            }

            // Date validation

            if (ScheduledDate < DateTime.Today)
            {
                errors.Add("Scheduled date cannot be in the past");
            }

            // Time validation

            if (ScheduledEventTime <= ScheduledLeaveTime)
            {
                errors.Add("Event time must be after leave time");
            }

            // Destination validation

            if (string.IsNullOrWhiteSpace(ScheduledDestination))
            {
                errors.Add("Destination is required");
            }

            // Riders validation

            if (ScheduledRiders < 1)
            {
                errors.Add("Number of riders must be at least 1");
            }

            // Vehicle capacity validation

            if (SelectedDriver is null)
            {
                errors.Add("Driver is required");
            }

            if (SelectedVehicle is null)
            {
                errors.Add("Vehicle is required");
            }

            if (SelectedVehicle != null && ScheduledRiders > SelectedVehicle.Capacity)
            {
                errors.Add($"Number of riders ({ScheduledRiders}) exceeds vehicle capacity ({SelectedVehicle.Capacity})");
            }


            if (errors.Count > 0)
            {
                ValidationMessage = string.Join("\n", errors);
                HasValidationErrors = true;
                return false;
            }

            ValidationMessage = string.Empty;
            HasValidationErrors = false;
            return true;
        }

        private void ValidateDate()
        {
            if (ScheduledDate < DateTime.Today)
            {
                ValidationMessage = "Scheduled date cannot be in the past";
                HasValidationErrors = true;
            }
            else
            {
                ClearValidationIfOnlyThis("Scheduled date cannot be in the past");
            }
        }

        private void ValidateTime()
        {
            if (ScheduledEventTime <= ScheduledLeaveTime)
            {
                ValidationMessage = "Event time must be after leave time";
                HasValidationErrors = true;
            }
            else
            {
                ClearValidationIfOnlyThis("Event time must be after leave time");
            }
        }

        private void ValidateDestination()
        {
            if (string.IsNullOrWhiteSpace(ScheduledDestination))
            {
                ValidationMessage = "Destination is required";
                HasValidationErrors = true;
            }
            else
            {
                ClearValidationIfOnlyThis("Destination is required");
            }
        }

        private void ValidateRiders()
        {
            if (ScheduledRiders < 1)
            {
                ValidationMessage = "Number of riders must be at least 1";
                HasValidationErrors = true;
            }
            else
            {
                ClearValidationIfOnlyThis("Number of riders must be at least 1");
                ValidateVehicleCapacity();
            }
        }

        private void ValidateVehicleCapacity()
        {
            if (SelectedVehicle != null && ScheduledRiders > SelectedVehicle.Capacity)
            {
                ValidationMessage = $"Number of riders ({ScheduledRiders}) exceeds vehicle capacity ({SelectedVehicle.Capacity})";
                HasValidationErrors = true;
            }
            else
            {
                ClearValidationIfOnlyThis($"Number of riders ({ScheduledRiders}) exceeds vehicle capacity");
            }
        }

        private void ClearValidationIfOnlyThis(string message)
        {
            if (ValidationMessage == message)
            {
                ValidationMessage = string.Empty;
                HasValidationErrors = false;
            }
        }

        public ActivitySchedule GetActivitySchedule()
        {
            var activity = _isEditMode ? _originalActivity : new ActivitySchedule();

            // Removed assignment to read-only property 'Subject' — it is computed from TripType and ScheduledDestination
            activity.ScheduledDate = ScheduledDate;
            activity.ScheduledLeaveTime = ScheduledLeaveTime;
            activity.ScheduledEventTime = ScheduledEventTime;
            activity.TripType = TripType;
            activity.ScheduledDestination = ScheduledDestination;
            activity.ScheduledRiders = ScheduledRiders;
            activity.RequestedBy = RequestedBy;
            activity.Notes = Notes;
            activity.Status = Status;
            activity.ScheduledDriverId = SelectedDriver?.DriverId
                ?? (_isEditMode ? _originalActivity.ScheduledDriverId : 0);
            activity.ScheduledBusId = SelectedVehicle?.BusId
                ?? (_isEditMode ? _originalActivity.ScheduledVehicleId : 0);
            activity.UpdatedDate = DateTime.Now;
            activity.UpdatedBy = Environment.UserName;

            if (!_isEditMode)
            {
                activity.CreatedDate = DateTime.Now;
                activity.CreatedBy = Environment.UserName;
            }

            return activity;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
