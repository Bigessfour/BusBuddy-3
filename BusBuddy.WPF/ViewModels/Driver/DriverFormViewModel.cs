using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using DriverModel = BusBuddy.Core.Models.Driver;

namespace BusBuddy.WPF.ViewModels.Driver
{
    /// <summary>
    /// ViewModel for the DriverForm - handles adding and editing drivers
    /// </summary>
    public class DriverFormViewModel : BaseViewModel
    {
        private readonly IDriverService _driverService;
        private static readonly new ILogger Logger = Log.ForContext<DriverFormViewModel>();

    private DriverModel _driver = new();
    private DriverModel? _selectedDriver;
        private string _searchText = string.Empty;
        private string _formTitle = "Add New Driver";
        private bool _isEditMode;

    // Close coordination for dialog usage — mirrors StudentForm pattern
    public event EventHandler<bool?>? RequestClose;

        public DriverFormViewModel(IDriverService driverService)
        {
            _driverService = driverService ?? throw new ArgumentNullException(nameof(driverService));
            InitializeCommands();
            _ = LoadDriversAsync();
        }

        // Properties
        public DriverModel Driver
        {
            get => _driver;
            set
            {
                if (SetProperty(ref _driver, value))
                {
                    if (SaveDriverCommand is IRelayCommand save)
                    {
                        save.NotifyCanExecuteChanged();
                    }
                    if (DeleteDriverCommand is IRelayCommand del)
                    {
                        del.NotifyCanExecuteChanged();
                    }
                    OnPropertyChanged(nameof(CanSaveDriver));
                }
            }
        }

        public DriverModel? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (SetProperty(ref _selectedDriver, value) && value is not null)
                {
                    LoadDriverForEdit(value);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

    public ObservableCollection<DriverModel> Drivers { get; } = new();

        public bool CanSaveDriver => !string.IsNullOrWhiteSpace(Driver.DriverName) &&
                                     !string.IsNullOrWhiteSpace(Driver.DriverPhone) &&
                                     !string.IsNullOrWhiteSpace(Driver.LicenseNumber) &&
                                     !string.IsNullOrWhiteSpace(Driver.LicenseClass);

        public bool CanDeleteDriver => IsEditMode && SelectedDriver is not null;

        // Commands
        public ICommand AddDriverCommand { get; private set; } = null!;
        public ICommand SaveDriverCommand { get; private set; } = null!;
        public ICommand DeleteDriverCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            AddDriverCommand = new RelayCommand(ExecuteAddDriver);
            SaveDriverCommand = new AsyncRelayCommand(ExecuteSaveDriverAsync, () => CanSaveDriver);
            DeleteDriverCommand = new AsyncRelayCommand(ExecuteDeleteDriverAsync, () => CanDeleteDriver);
            CancelCommand = new RelayCommand(ExecuteCancel);
            RefreshCommand = new AsyncRelayCommand(LoadDriversAsync);
        }

        // Command Handlers
        private void ExecuteAddDriver()
        {
            try
            {
                Logger.Information("Starting new driver entry");
                Driver = new DriverModel
                {
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow
                };
                IsEditMode = false;
                FormTitle = "Add New Driver";
                SelectedDriver = null;

                if (SaveDriverCommand is IRelayCommand save)
                {
                    save.NotifyCanExecuteChanged();
                }
                if (DeleteDriverCommand is IRelayCommand del)
                {
                    del.NotifyCanExecuteChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting new driver entry");
                ShowError($"Error preparing new driver form: {ex.Message}");
            }
        }

        private async Task ExecuteSaveDriverAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Saving driver: {DriverName}", Driver.DriverName);

                var validationErrors = await _driverService.ValidateDriverAsync(Driver);
                if (validationErrors.Count > 0)
                {
                    ShowError($"Validation failed: {string.Join(", ", validationErrors)}");
                    return;
                }

                DriverModel savedDriver;
                if (IsEditMode)
                {
                    var success = await _driverService.UpdateDriverAsync(Driver);
                    if (!success)
                    {
                        ShowError("Failed to update driver");
                        return;
                    }
                    savedDriver = Driver;
                    ShowSuccess("Driver updated successfully");
                }
                else
                {
                    savedDriver = await _driverService.AddDriverAsync(Driver);
                    ShowSuccess("Driver added successfully");
                }

                await LoadDriversAsync();
                SelectedDriver = Drivers.FirstOrDefault(d => d.DriverId == savedDriver.DriverId);
                Logger.Information("Driver saved successfully: {DriverName} (ID: {DriverId})",
                    savedDriver.DriverName, savedDriver.DriverId);

                // Signal dialog close with success
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving driver: {DriverName}", Driver.DriverName);
                ShowError($"Error saving driver: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExecuteDeleteDriverAsync()
        {
            try
            {
                if (SelectedDriver is null)
                {
                    return;
                }

                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete driver '{SelectedDriver.DriverName}'?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }

                IsLoading = true;
                Logger.Information("Deleting driver: {DriverName} (ID: {DriverId})",
                    SelectedDriver.DriverName, SelectedDriver.DriverId);

                var success = await _driverService.DeleteDriverAsync(SelectedDriver.DriverId);
                if (success)
                {
                    ShowSuccess("Driver deleted successfully");
                    await LoadDriversAsync();
                    ExecuteAddDriver();
                }
                else
                {
                    ShowError("Failed to delete driver");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting driver");
                ShowError($"Error deleting driver: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteCancel()
        {
            try
            {
                Logger.Information("Cancel requested");
                if (IsEditMode && SelectedDriver is not null)
                {
                    LoadDriverForEdit(SelectedDriver);
                }
                else
                {
                    ExecuteAddDriver();
                }

                // Signal dialog close with cancel
                RequestClose?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during cancel operation");
            }
        }

        // Helpers
        private void ShowError(string message)
        {
            StatusMessage = message;
            Logger.Warning("User error: {Message}", message);
            System.Windows.MessageBox.Show(message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        private void ShowSuccess(string message)
        {
            StatusMessage = message;
            Logger.Information("User success: {Message}", message);
        }

    private async Task LoadDriversAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading drivers from database");

                var drivers = await _driverService.GetAllDriversAsync();
                Drivers.Clear();
                foreach (var d in drivers.OrderBy(d => d.DriverName))
                {
                    Drivers.Add(d);
                }

                Logger.Information("Loaded {Count} drivers", Drivers.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading drivers");
                ShowError($"Error loading drivers: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

    private void LoadDriverForEdit(DriverModel driver)
        {
            try
            {
                Logger.Information("Loading driver for edit: {DriverName} (ID: {DriverId})",
                    driver.DriverName, driver.DriverId);

        Driver = new DriverModel
                {
                    DriverId = driver.DriverId,
                    DriverName = driver.DriverName,
                    FirstName = driver.FirstName,
                    LastName = driver.LastName,
                    DriverPhone = driver.DriverPhone,
                    DriverEmail = driver.DriverEmail,
                    LicenseNumber = driver.LicenseNumber,
                    LicenseClass = driver.LicenseClass,
                    LicenseExpiryDate = driver.LicenseExpiryDate,
                    Endorsements = driver.Endorsements,
                    Status = driver.Status,
                    TrainingComplete = driver.TrainingComplete,
                    BackgroundCheckDate = driver.BackgroundCheckDate,
                    DrugTestDate = driver.DrugTestDate,
                    Address = driver.Address,
                    City = driver.City,
                    State = driver.State,
                    Zip = driver.Zip,
                    EmergencyContactName = driver.EmergencyContactName,
                    EmergencyContactPhone = driver.EmergencyContactPhone,
                    CreatedDate = driver.CreatedDate,
                    UpdatedDate = driver.UpdatedDate
                };

                IsEditMode = true;
                FormTitle = $"Edit Driver - {driver.DriverName}";

                if (SaveDriverCommand is IRelayCommand save)
                {
                    save.NotifyCanExecuteChanged();
                }
                if (DeleteDriverCommand is IRelayCommand del)
                {
                    del.NotifyCanExecuteChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading driver for edit");
                ShowError($"Error loading driver: {ex.Message}");
            }
        }
    }
}
