using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BusBuddy.WPF.Commands;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.ViewModels.BusManagement
{
    /// <summary>
    /// ViewModel for the Bus Edit Dialog
    /// MVP Implementation - Basic functionality for Monday demo
    /// </summary>
    public class BusEditDialogViewModel : INotifyPropertyChanged
    {
        private string _dialogTitle = "Edit Bus";
        private string _busNumber = string.Empty;
        private string _make = string.Empty;
        private string _model = string.Empty;
        private int _capacity;
        private string _licensePlate = string.Empty;
        private bool _isActive = true;

        public BusEditDialogViewModel()
        {
            SaveCommand = new RelayCommand(_ => ExecuteSave(), _ => CanExecuteSave());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

        #region Properties

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public string BusNumber
        {
            get => _busNumber;
            set => SetProperty(ref _busNumber, value);
        }

        public string Make
        {
            get => _make;
            set => SetProperty(ref _make, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public int Capacity
        {
            get => _capacity;
            set => SetProperty(ref _capacity, value);
        }

        public string LicensePlate
        {
            get => _licensePlate;
            set => SetProperty(ref _licensePlate, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private void ExecuteSave()
        {
            // MVP Implementation - Basic save logic
            // TODO: Implement actual save functionality in Phase 2
        }

        private bool CanExecuteSave()
        {
            return !string.IsNullOrWhiteSpace(BusNumber) &&
                   !string.IsNullOrWhiteSpace(Make) &&
                   Capacity > 0;
        }

        private void ExecuteCancel()
        {
            // MVP Implementation - Basic cancel logic
            // TODO: Implement proper dialog result handling in Phase 2
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
