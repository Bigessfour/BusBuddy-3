using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BusBuddy.Core.Services;

namespace BusBuddy.WPF.Controls
{
    /// <summary>
    /// Control for validating addresses and suggesting bus stops
    /// </summary>
    public partial class AddressValidationControl : UserControl, INotifyPropertyChanged
    {
        private readonly IAddressValidationService? _addressValidationService;
        private string _street = string.Empty;
        private string _city = string.Empty;
        private string _state = string.Empty;
        private string _zip = string.Empty;
        private string _normalizedAddress = string.Empty;
        private bool _isValidated;
        private bool _isValidating;
        private ObservableCollection<string> _nearbyBusStops = new ObservableCollection<string>();
        private string? _selectedBusStop;
        private string _validationMessage = string.Empty;
        private bool _showValidationMessage;
        private double _distanceToBusStop = -1;

        // These public properties match the dependency properties
        public string Street
        {
            get => _street;
            set
            {
                if (_street != value)
                {
                    _street = value;
                    OnPropertyChanged();
                    ResetValidation();
                }
            }
        }

        public string City
        {
            get => _city;
            set
            {
                if (_city != value)
                {
                    _city = value;
                    OnPropertyChanged();
                    ResetValidation();
                }
            }
        }

        public string State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                    ResetValidation();
                }
            }
        }

        public string Zip
        {
            get => _zip;
            set
            {
                if (_zip != value)
                {
                    _zip = value;
                    OnPropertyChanged();
                    ResetValidation();
                }
            }
        }

        public string NormalizedAddress
        {
            get => _normalizedAddress;
            private set
            {
                if (_normalizedAddress != value)
                {
                    _normalizedAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValidated
        {
            get => _isValidated;
            private set
            {
                if (_isValidated != value)
                {
                    _isValidated = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValidating
        {
            get => _isValidating;
            private set
            {
                if (_isValidating != value)
                {
                    _isValidating = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> NearbyBusStops
        {
            get => _nearbyBusStops;
            private set
            {
                _nearbyBusStops = value;
                OnPropertyChanged();
            }
        }

        public string? SelectedBusStop
        {
            get => _selectedBusStop;
            set
            {
                if (_selectedBusStop != value)
                {
                    _selectedBusStop = value;
                    OnPropertyChanged();
                    UpdateDistanceToBusStop();
                }
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged();
                    ShowValidationMessage = !string.IsNullOrEmpty(value);
                }
            }
        }

        public bool ShowValidationMessage
        {
            get => _showValidationMessage;
            private set
            {
                if (_showValidationMessage != value)
                {
                    _showValidationMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public double DistanceToBusStop
        {
            get => _distanceToBusStop;
            private set
            {
                if (_distanceToBusStop != value)
                {
                    _distanceToBusStop = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasNearbyBusStops => NearbyBusStops.Count > 0;

        // Dependency Properties

        public static readonly DependencyProperty StreetProperty =
            DependencyProperty.Register(
                "StreetDep",
                typeof(string),
                typeof(AddressValidationControl),
                new PropertyMetadata(string.Empty, OnStreetChanged));

        public static readonly DependencyProperty CityProperty =
            DependencyProperty.Register(
                "CityDep",
                typeof(string),
                typeof(AddressValidationControl),
                new PropertyMetadata(string.Empty, OnCityChanged));

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(
                "StateDep",
                typeof(string),
                typeof(AddressValidationControl),
                new PropertyMetadata(string.Empty, OnStateChanged));

        public static readonly DependencyProperty ZipProperty =
            DependencyProperty.Register(
                "ZipDep",
                typeof(string),
                typeof(AddressValidationControl),
                new PropertyMetadata(string.Empty, OnZipChanged));

        public static readonly DependencyProperty SelectedBusStopProperty =
            DependencyProperty.Register(
                "SelectedBusStopDep",
                typeof(string),
                typeof(AddressValidationControl),
                new PropertyMetadata(null, OnSelectedBusStopChanged));

        // XAML properties that link to the dependency properties
        public string StreetDep
        {
            get => (string)GetValue(StreetProperty);
            set => SetValue(StreetProperty, value);
        }

        public string CityDep
        {
            get => (string)GetValue(CityProperty);
            set => SetValue(CityProperty, value);
        }

        public string StateDep
        {
            get => (string)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public string ZipDep
        {
            get => (string)GetValue(ZipProperty);
            set => SetValue(ZipProperty, value);
        }

        public string? SelectedBusStopDep
        {
            get => (string?)GetValue(SelectedBusStopProperty);
            set => SetValue(SelectedBusStopProperty, value);
        }

        public AddressValidationControl()
        {
            InitializeComponent();
        }

        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            await ValidateAddressAsync();
        }

        public AddressValidationControl(IAddressValidationService addressValidationService) : this()
        {
            _addressValidationService = addressValidationService;
        }

        private static void OnStreetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressValidationControl control)
            {
                control.Street = (string)e.NewValue;
            }
        }

        private static void OnCityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressValidationControl control)
            {
                control.City = (string)e.NewValue;
            }
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressValidationControl control)
            {
                control.State = (string)e.NewValue;
            }
        }

        private static void OnZipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressValidationControl control)
            {
                control.Zip = (string)e.NewValue;
            }
        }

        private static void OnSelectedBusStopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AddressValidationControl control)
            {
                control.SelectedBusStop = (string?)e.NewValue;

                // Also update the dependency property
                if (control.SelectedBusStop != control.SelectedBusStopDep)
                {
                    control.SelectedBusStopDep = control.SelectedBusStop;
                }
            }
        }

        public async Task ValidateAddressAsync()
        {
            if (_addressValidationService == null || IsValidating)
            {
                return;
            }

            IsValidating = true;
            ValidationMessage = "Validating address...";
            ShowValidationMessage = true;
            IsValidated = false;
            NearbyBusStops.Clear();
            DistanceToBusStop = -1;

            try
            {
                // First validate the address
                var validationResult = await _addressValidationService.ValidateAddressAsync(
                    Street, City, State, Zip);

                if (!validationResult.IsValid)
                {
                    ValidationMessage = "The address appears to be invalid. Please check and correct it.";
                    IsValidated = false;
                    return;
                }

                // Store the normalized address
                NormalizedAddress = validationResult.NormalizedAddress ?? string.Empty;

                // Now get nearby bus stops
                var busStops = await _addressValidationService.FindNearbyBusStopsAsync(
                    Street, City, State, Zip);

                // Update the UI
                NearbyBusStops.Clear();
                foreach (var stop in busStops)
                {
                    NearbyBusStops.Add(stop);
                }

                if (NearbyBusStops.Count > 0)
                {
                    SelectedBusStop = NearbyBusStops.First();
                    ValidationMessage = $"Address validated. Found {NearbyBusStops.Count} nearby bus stops.";
                }
                else
                {
                    ValidationMessage = "Address validated, but no nearby bus stops were found.";
                }

                IsValidated = true;
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error validating address: {ex.Message}";
                IsValidated = false;
            }
            finally
            {
                IsValidating = false;
            }
        }

        private void ResetValidation()
        {
            IsValidated = false;
            NormalizedAddress = string.Empty;
            NearbyBusStops.Clear();
            ValidationMessage = string.Empty;
            ShowValidationMessage = false;
            DistanceToBusStop = -1;
        }

        private async void UpdateDistanceToBusStop()
        {
            if (_addressValidationService == null || string.IsNullOrEmpty(SelectedBusStop) || !IsValidated)
            {
                DistanceToBusStop = -1;
                return;
            }

            try
            {
                DistanceToBusStop = await _addressValidationService.GetDistanceToBusStopAsync(
                    NormalizedAddress, SelectedBusStop);
            }
            catch
            {
                DistanceToBusStop = -1;
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Special case for HasNearbyBusStops
            if (propertyName == nameof(NearbyBusStops))
            {
                OnPropertyChanged(nameof(HasNearbyBusStops));
            }
        }

        #endregion
    }
}
