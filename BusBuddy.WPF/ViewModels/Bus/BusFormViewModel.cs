using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.WPF.Commands;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces; // Use core IBusService
using Serilog;

namespace BusBuddy.WPF.ViewModels.Bus
{
    /// <summary>
    /// ViewModel for Bus entry form
    /// MVP-ready implementation with validation and CRUD operations
    /// </summary>
    public class BusFormViewModel : INotifyPropertyChanged
    {
        private BusBuddy.Core.Models.Bus _bus;
        private bool _isEditMode;
        private readonly IBusService? _busService; // optional during MVP if DI not configured
        private static readonly ILogger Logger = Log.ForContext<BusFormViewModel>();

        public event EventHandler<bool?>? RequestClose; // mimic DriverForm pattern

        public BusFormViewModel() : this(null, new BusBuddy.Core.Models.Bus()) {}

        public BusFormViewModel(IBusService? busService) : this(busService, new BusBuddy.Core.Models.Bus()) {}

        public BusFormViewModel(IBusService? busService, BusBuddy.Core.Models.Bus bus)
        {
            _busService = busService;
            _bus = bus ?? new BusBuddy.Core.Models.Bus();
            _isEditMode = bus?.BusId > 0;

            SaveCommand = new RelayCommand(async () => await SaveBusAsync(), CanSave);
            CancelCommand = new RelayCommand(Cancel);

            InitializeDropdownData();
        }

        #region Properties

    public string Title => _isEditMode ? $"Edit Bus: {BusNumber}" : "Add New Bus"; // exposed as Title for binding

        public string BusNumber
        {
            get => _bus.BusNumber;
            set
            {
                if (_bus.BusNumber != value)
                {
                    _bus.BusNumber = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Title));
                    Logger.Debug("BusNumber changed -> {BusNumber}", _bus.BusNumber);
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string LicenseNumber
        {
            get => _bus.LicenseNumber;
            set
            {
                if (_bus.LicenseNumber != value)
                {
                    _bus.LicenseNumber = value ?? string.Empty;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                    Logger.Debug("LicenseNumber changed -> {License}", _bus.LicenseNumber);
                }
            }
        }

        public string VinNumber
        {
            get => _bus.VINNumber;
            set
            {
                if (_bus.VINNumber != value)
                {
                    _bus.VINNumber = value ?? string.Empty;
                    OnPropertyChanged();
                    Logger.Debug("VIN changed -> {VIN}", _bus.VINNumber);
                }
            }
        }

        public int Year
        {
            get => _bus.Year;
            set
            {
                if (_bus.Year != value)
                {
                    _bus.Year = value;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                    Logger.Debug("Year changed -> {Year}", _bus.Year);
                }
            }
        }

        public string Make
        {
            get => _bus.Make;
            set
            {
                if (_bus.Make != value)
                {
                    _bus.Make = value ?? string.Empty;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                    Logger.Debug("Make changed -> {Make}", _bus.Make);
                }
            }
        }

        public string Model
        {
            get => _bus.Model;
            set
            {
                if (_bus.Model != value)
                {
                    _bus.Model = value ?? string.Empty;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                    Logger.Debug("Model changed -> {Model}", _bus.Model);
                }
            }
        }

        public int SeatingCapacity
        {
            get => _bus.SeatingCapacity;
            set
            {
                if (_bus.SeatingCapacity != value)
                {
                    _bus.SeatingCapacity = value;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                    Logger.Debug("SeatingCapacity changed -> {Cap}", _bus.SeatingCapacity);
                }
            }
        }

        public string Status
        {
            get => _bus.Status;
            set
            {
                if (_bus.Status != value)
                {
                    _bus.Status = value ?? "Active";
                    OnPropertyChanged();
                }
            }
        }

        public DateTime? DateLastInspection
        {
            get => _bus.DateLastInspection;
            set
            {
                if (_bus.DateLastInspection != value)
                {
                    _bus.DateLastInspection = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? CurrentOdometer
        {
            get => _bus.CurrentOdometer;
            set
            {
                if (_bus.CurrentOdometer != value)
                {
                    _bus.CurrentOdometer = value;
                    OnPropertyChanged();
                }
            }
        }

        // Dropdown collections
        public ObservableCollection<string> AvailableMakes { get; private set; } = new();
        public ObservableCollection<string> AvailableStatuses { get; private set; } = new();

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanSave()
        {
            var can = !string.IsNullOrWhiteSpace(BusNumber) &&
                      !string.IsNullOrWhiteSpace(LicenseNumber) &&
                      Year >= 1990 && Year <= DateTime.Now.Year + 1 &&
                      !string.IsNullOrWhiteSpace(Make) &&
                      !string.IsNullOrWhiteSpace(Model) &&
                      SeatingCapacity > 0;
            return can;
        }

        private async Task SaveBusAsync()
        {
            try
            {
                Logger.Information("Attempting to save bus {BusNumber} (EditMode={EditMode})", BusNumber, _isEditMode);
                var confirm = MessageBox.Show(
                    $"Save bus: {BusNumber} ({Year} {Make} {Model})?",
                    "Confirm Save",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                {
                    Logger.Information("Save cancelled by user for bus {BusNumber}", BusNumber);
                    return;
                }

                if (_busService != null)
                {
                    if (_isEditMode)
                    {
                        var updated = await _busService.UpdateBusAsync(_bus);
                        Logger.Information("Bus update result for {BusNumber}: {Result}", BusNumber, updated);
                    }
                    else
                    {
                        var added = await _busService.AddBusAsync(_bus);
                        _bus.BusId = added.BusId;
                        _isEditMode = true;
                        OnPropertyChanged(nameof(Title));
                        Logger.Information("Bus added with ID {BusId}", added.BusId);
                    }
                }
                else
                {
                    Logger.Warning("_busService not available — skipping persistence (MVP fallback)");
                }

                RequestClose?.Invoke(this, true);
                if (Application.Current.Windows.OfType<Views.Bus.BusForm>().FirstOrDefault() is var window)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving bus {BusNumber}", BusNumber);
                MessageBox.Show($"Error saving bus: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
            if (Application.Current.Windows.OfType<Views.Bus.BusForm>().FirstOrDefault() is var window)
            {
                window.DialogResult = false;
                window.Close();
            }
        }

        #endregion

        #region Private Methods

        private void InitializeDropdownData()
        {
            // Common bus makes
            var makes = new[]
            {
                "Blue Bird", "IC Bus", "Thomas Built Buses", "Collins Bus",
                "Starcraft Bus", "Carpenter Bus", "Glaval Bus", "Trans Tech"
            };

            foreach (var make in makes)
            {
                AvailableMakes.Add(make);
            }

            // Bus statuses
            var statuses = new[] { "Active", "Maintenance", "Out of Service", "Retired" };
            foreach (var status in statuses)
            {
                AvailableStatuses.Add(status);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    public void ForceRequery() => ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();

        #endregion
    }

    // Placeholder interface removed; core IBusService in BusBuddy.Core.Services.Interfaces is used.
}
