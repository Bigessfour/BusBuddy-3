using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Represents a single quick action item
    /// </summary>
    public class QuickActionItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public ICommand Command { get; set; }

        public QuickActionItem(string title, string description, ICommand command, string iconPath = "")
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            IconPath = iconPath ?? string.Empty;
        }
    }

    /// <summary>
    /// ViewModel for managing quick actions in the tool panels
    /// </summary>
    public class QuickActionsViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private ObservableCollection<QuickActionItem> _quickActions = new();
        private readonly Action<string>? _navigationAction;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<QuickActionItem> QuickActions
        {
            get => _quickActions;
            set
            {
                if (_quickActions != value)
                {
                    _quickActions = value;
                    OnPropertyChanged();
                }
            }
        }

        // Primary Quick Action Commands
        public ICommand QuickAddStudentCommand { get; set; } = null!;
        public ICommand QuickAddBusCommand { get; set; } = null!;
        public ICommand QuickScheduleTripCommand { get; set; } = null!;
        public ICommand QuickMaintenanceCommand { get; set; } = null!;
        public ICommand QuickFuelEntryCommand { get; set; } = null!;
        public ICommand QuickReportCommand { get; set; } = null!;
        public ICommand QuickExportCommand { get; set; } = null!;

        public QuickActionsViewModel(Action<string>? navigationAction = null)
        {
            _navigationAction = navigationAction;
            InitializeCommands();
            InitializeQuickActions();
        }

        private void InitializeCommands()
        {
            QuickAddStudentCommand = new RelayCommand(() => ExecuteQuickAddStudent(), () => CanExecuteQuickAddStudent());
            QuickAddBusCommand = new RelayCommand(() => ExecuteQuickAddBus(), () => CanExecuteQuickAddBus());
            QuickScheduleTripCommand = new RelayCommand(() => ExecuteQuickScheduleTrip(), () => CanExecuteQuickScheduleTrip());
            QuickMaintenanceCommand = new RelayCommand(() => ExecuteQuickMaintenance(), () => CanExecuteQuickMaintenance());
            QuickFuelEntryCommand = new RelayCommand(() => ExecuteQuickFuelEntry(), () => CanExecuteQuickFuelEntry());
            QuickReportCommand = new RelayCommand(() => ExecuteQuickReport(), () => CanExecuteQuickReport());
            QuickExportCommand = new RelayCommand(() => ExecuteQuickExport(), () => CanExecuteQuickExport());
        }

        private void InitializeQuickActions()
        {
            QuickActions = new ObservableCollection<QuickActionItem>
            {
                new QuickActionItem("➕ Add New Student", "Add a new student to the system", QuickAddStudentCommand, "/Assets/Icons/student_add_16.png"),
                new QuickActionItem("🚌 Add New Bus", "Register a new bus in the fleet", QuickAddBusCommand, "/Assets/Icons/bus_add_16.png"),
                new QuickActionItem("🗓️ Schedule Trip", "Schedule a new activity trip", QuickScheduleTripCommand, "/Assets/Icons/calendar_add_16.png"),
                new QuickActionItem("🔧 Log Maintenance", "Record maintenance activity", QuickMaintenanceCommand, "/Assets/Icons/maintenance_16.png"),
                new QuickActionItem("⛽ Record Fuel", "Log fuel consumption", QuickFuelEntryCommand, "/Assets/Icons/fuel_16.png"),
                new QuickActionItem("📊 Generate Report", "Create system reports", QuickReportCommand, "/Assets/Icons/report_16.png"),
                new QuickActionItem("📤 Export Data", "Export data to external formats", QuickExportCommand, "/Assets/Icons/export_16.png")
            };
        }

        #region Command Implementations

        private void ExecuteQuickAddStudent()
        {
            _navigationAction?.Invoke("Students");
        }

        private bool CanExecuteQuickAddStudent()
        {
            return IsEnabled;
        }

        private void ExecuteQuickAddBus()
        {
            _navigationAction?.Invoke("Buses");
        }

        private bool CanExecuteQuickAddBus()
        {
            return IsEnabled;
        }

        private void ExecuteQuickScheduleTrip()
        {
            _navigationAction?.Invoke("Schedule");
        }

        private bool CanExecuteQuickScheduleTrip()
        {
            return IsEnabled;
        }

        private void ExecuteQuickMaintenance()
        {
            _navigationAction?.Invoke("Maintenance");
        }

        private bool CanExecuteQuickMaintenance()
        {
            return IsEnabled;
        }

        private void ExecuteQuickFuelEntry()
        {
            _navigationAction?.Invoke("Fuel");
        }

        private bool CanExecuteQuickFuelEntry()
        {
            return IsEnabled;
        }

        private void ExecuteQuickReport()
        {
            _navigationAction?.Invoke("Activity");
        }

        private bool CanExecuteQuickReport()
        {
            return IsEnabled;
        }

        private void ExecuteQuickExport()
        {
            // TODO: Implement data export functionality
            // This could be a separate dialog or navigate to a reporting section
            _navigationAction?.Invoke("Activity");
        }

        private bool CanExecuteQuickExport()
        {
            return IsEnabled;
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
