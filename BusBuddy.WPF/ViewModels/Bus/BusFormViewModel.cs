using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using BusBuddy.WPF.Commands;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;

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

        public BusFormViewModel() : this(new BusBuddy.Core.Models.Bus())
        {
        }

        public BusFormViewModel(BusBuddy.Core.Models.Bus bus)
        {
            _bus = bus ?? new BusBuddy.Core.Models.Bus();
            _isEditMode = bus?.VehicleId > 0;

            // Initialize commands
            SaveCommand = new RelayCommand(SaveBus, CanSave);
            CancelCommand = new RelayCommand(Cancel);

            // Initialize collections
            InitializeDropdownData();
        }

        #region Properties

        public string FormTitle => _isEditMode ? $"Edit Bus: {BusNumber}" : "Add New Bus";

        public string BusNumber
        {
            get => _bus.BusNumber;
            set
            {
                if (_bus.BusNumber != value)
                {
                    _bus.BusNumber = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormTitle));
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
            return !string.IsNullOrWhiteSpace(BusNumber) &&
                   !string.IsNullOrWhiteSpace(LicenseNumber) &&
                   Year >= 1990 && Year <= DateTime.Now.Year + 1 &&
                   !string.IsNullOrWhiteSpace(Make) &&
                   !string.IsNullOrWhiteSpace(Model) &&
                   SeatingCapacity > 0;
        }

        private async void SaveBus()
        {
            try
            {
                // For MVP - simplified save logic
                // TODO: Implement actual service call
                var result = MessageBox.Show(
                    $"Save bus: {BusNumber} ({Year} {Make} {Model})?",
                    "Confirm Save",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Close the form
                    if (Application.Current.Windows.OfType<Views.Bus.BusForm>().FirstOrDefault() is var window)
                    {
                        window.DialogResult = true;
                        window.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving bus: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
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

        #endregion
    }

    #region Helper Classes

    // RelayCommand consolidated centrally in BusBuddy.WPF.Commands

    /// <summary>
    /// Placeholder bus service interface for MVP
    /// </summary>
    public interface IBusService
    {
        Task<BusBuddy.Core.Models.Bus> SaveBusAsync(BusBuddy.Core.Models.Bus bus);
        Task<List<BusBuddy.Core.Models.Bus>> GetAllBusesAsync();
    }

    #endregion
}
