using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.WPF.Commands;

namespace BusBuddy.WPF.ViewModels.Route
{
    public class RouteEditDialogViewModel : INotifyPropertyChanged
    {
        private string _startLocation = string.Empty;
        private string _endLocation = string.Empty;
        private string _stops = string.Empty;
        private string _validationMessage = string.Empty;

        public event EventHandler<bool?>? RequestClose;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string StartLocation
        {
            get => _startLocation;
            set
            {
                if (_startLocation != value)
                {
                    _startLocation = value;
                    OnPropertyChanged();
                    ClearValidation();
                }
            }
        }

        public string EndLocation
        {
            get => _endLocation;
            set
            {
                if (_endLocation != value)
                {
                    _endLocation = value;
                    OnPropertyChanged();
                    ClearValidation();
                }
            }
        }

        /// <summary>Comma-separated stop labels for quick edit.</summary>
        public string Stops
        {
            get => _stops;
            set
            {
                if (_stops != value)
                {
                    _stops = value;
                    OnPropertyChanged();
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
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public RouteEditDialogViewModel()
        {
            SaveCommand = new RelayCommand(ExecuteSave);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        }

        private void ExecuteSave()
        {
            if (string.IsNullOrWhiteSpace(StartLocation))
            {
                ValidationMessage = "Start location is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(EndLocation))
            {
                ValidationMessage = "End location is required.";
                return;
            }

            ValidationMessage = string.Empty;
            RequestClose?.Invoke(this, true);
        }

        private void ClearValidation()
        {
            if (!string.IsNullOrEmpty(ValidationMessage))
            {
                ValidationMessage = string.Empty;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
