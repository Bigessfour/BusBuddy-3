using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            // Apply Syncfusion theme — FluentDark default, FluentLight fallback
            try
            {
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                using var dark = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, dark);
            }
            catch
            {
                try { using var light = new Theme("FluentLight"); SfSkinManager.SetTheme(this, light); } catch { }
            }

            ViewModel = new ActivityScheduleEditDialogViewModel(activityToEdit);
            DataContext = ViewModel;

            // Configure dialog properties
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
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
        private string _subject = string.Empty;
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

        public ActivityScheduleEditDialogViewModel(ActivitySchedule? activityToEdit = null)
        {
            _isEditMode = activityToEdit != null;
            _originalActivity = activityToEdit ?? new ActivitySchedule();

            InitializeCollections();

            if (_isEditMode && activityToEdit != null)
            {
                LoadActivityData(activityToEdit);
            }

            LoadAvailableData();
        }

        #region Properties

        public string DialogTitle => _isEditMode ? "Edit Activity Schedule" : "Add New Activity Schedule";
        public string SaveButtonText => _isEditMode ? "Update Activity" : "Create Activity";

        public string Subject
        {
            get => _subject;
            set
            {
                _subject = value;
                OnPropertyChanged();
                ValidateSubject();
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
                OnPropertyChanged();
                ValidateTime();
            }
        }

        public TimeSpan ScheduledEventTime
        {
            get => _scheduledEventTime;
            set
            {
                _scheduledEventTime = value;
                OnPropertyChanged();
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
            }
        }

        public string ScheduledDestination
        {
            get => _scheduledDestination;
            set
            {
                _scheduledDestination = value;
                OnPropertyChanged();
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
            Subject = activity.Subject ?? string.Empty;
            ScheduledDate = activity.ScheduledDate;
            ScheduledLeaveTime = activity.ScheduledLeaveTime;
            ScheduledEventTime = activity.ScheduledEventTime;
            TripType = activity.TripType ?? "Field Trip";
            ScheduledDestination = activity.ScheduledDestination ?? string.Empty;
            ScheduledRiders = activity.ScheduledRiders ?? 1;
            RequestedBy = activity.RequestedBy ?? Environment.UserName;
            Notes = activity.Notes ?? string.Empty;
            Status = activity.Status ?? "Scheduled";
        }

        private void LoadAvailableData()
        {
            try
            {
                // Load sample data - in Phase 3, this would come from database
                LoadSampleDrivers();
                LoadSampleVehicles();
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error loading data: {ex.Message}";
                HasValidationErrors = true;
            }
        }

        private void LoadSampleDrivers()
        {
            AvailableDrivers.Clear();
            AvailableDrivers.Add(new BusBuddy.Core.Models.Driver { DriverId = 1, DriverName = "John Smith", Status = "Active" });
            AvailableDrivers.Add(new BusBuddy.Core.Models.Driver { DriverId = 2, DriverName = "Sarah Johnson", Status = "Active" });
            AvailableDrivers.Add(new BusBuddy.Core.Models.Driver { DriverId = 3, DriverName = "Mike Wilson", Status = "Active" });
            AvailableDrivers.Add(new BusBuddy.Core.Models.Driver { DriverId = 4, DriverName = "Lisa Brown", Status = "Active" });
            AvailableDrivers.Add(new BusBuddy.Core.Models.Driver { DriverId = 5, DriverName = "Tom Davis", Status = "Active" });
        }

        private void LoadSampleVehicles()
        {
            AvailableVehicles.Clear();
            AvailableVehicles.Add(new BusBuddy.Core.Models.Bus { BusId = 1, Make = "Blue Bird", Model = "Vision", LicenseNumber = "Bus-001", Capacity = 72 });
            AvailableVehicles.Add(new BusBuddy.Core.Models.Bus { BusId = 2, Make = "Blue Bird", Model = "Vision", LicenseNumber = "Bus-002", Capacity = 71 });
            AvailableVehicles.Add(new BusBuddy.Core.Models.Bus { BusId = 3, Make = "Thomas", Model = "C2", LicenseNumber = "Bus-003", Capacity = 77 });
            AvailableVehicles.Add(new BusBuddy.Core.Models.Bus { BusId = 4, Make = "Thomas", Model = "C2", LicenseNumber = "Bus-004", Capacity = 72 });
            AvailableVehicles.Add(new BusBuddy.Core.Models.Bus { BusId = 5, Make = "IC Bus", Model = "CE200", LicenseNumber = "Bus-005", Capacity = 78 });
        }

        public bool ValidateActivity()
        {
            var errors = new List<string>();

            // Subject validation
            if (string.IsNullOrWhiteSpace(Subject))
            {
                errors.Add("Subject is required");
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

        private void ValidateSubject()
        {
            if (string.IsNullOrWhiteSpace(Subject))
            {
                ValidationMessage = "Subject is required";
                HasValidationErrors = true;
            }
            else
            {
                ClearValidationIfOnlyThis("Subject is required");
            }
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
            activity.ScheduledDriverId = SelectedDriver?.DriverId ?? 0;
            activity.ScheduledBusId = SelectedVehicle?.Id ?? 0;
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
