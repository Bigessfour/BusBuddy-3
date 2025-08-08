using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Driver
{
    /// <summary>
    /// ViewModel for the DriverForm - handles adding and editing drivers
    /// Includes license validation and qualification tracking
    /// </summary>
    public class DriverFormViewModel : BaseViewModel
    {
        private readonly IDriverService _driverService;
        private static readonly new ILogger Logger = Log.ForContext<DriverFormViewModel>();

        private BusBuddy.Core.Models.Driver _driver = new();
        private BusBuddy.Core.Models.Driver? _selectedDriver;
        private string _searchText = string.Empty;
        private string _formTitle = "Add New Driver";
        private bool _isEditMode;

        public DriverFormViewModel(IDriverService driverService)
        {
            _driverService = driverService ?? throw new ArgumentNullException(nameof(driverService));

            InitializeCommands();
            _ = LoadDriversAsync();
        }

        #region Properties

        /// <summary>
        /// Current driver being edited or added
        /// </summary>
        public BusBuddy.Core.Models.Driver Driver
        {
            get => _driver;
            set
            {
                if (SetProperty(ref _driver, value))
                {
                    // Update command availability when Driver property changes
                    ((RelayCommand)SaveDriverCommand)?.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(CanSaveDriver));
                }
            }
        }

        /// <summary>
        /// Selected driver from the list
        /// </summary>
        public BusBuddy.Core.Models.Driver? SelectedDriver
        {
            get => _selectedDriver;
            set
            {
                if (SetProperty(ref _selectedDriver, value) && value != null)
                {
                    LoadDriverForEdit(value);
                }
            }
        }

        /// <summary>
        /// Search text for filtering drivers
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        /// <summary>
        /// Form title (Add New Driver or Edit Driver)
        /// </summary>
        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        /// <summary>
        /// Whether form is in edit mode
        /// </summary>
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        /// <summary>
        /// Collection of all drivers
        /// </summary>
        public ObservableCollection<BusBuddy.Core.Models.Driver> Drivers { get; } = new();

        /// <summary>
        /// Can save driver (has required fields)
        /// </summary>
        public bool CanSaveDriver => !string.IsNullOrWhiteSpace(Driver.DriverName) &&
                                   !string.IsNullOrWhiteSpace(Driver.DriverPhone) &&
                                   !string.IsNullOrWhiteSpace(Driver.LicenseNumber) &&
                                   !string.IsNullOrWhiteSpace(Driver.LicenseClass);

        /// <summary>
        /// Can delete driver (driver selected and exists)
        /// </summary>
        public bool CanDeleteDriver => IsEditMode && SelectedDriver != null;

        #endregion

        #region Commands

        public ICommand AddDriverCommand { get; private set; } = null!;
        public ICommand SaveDriverCommand { get; private set; } = null!;
        public ICommand DeleteDriverCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;

        #endregion

        #region Command Initialization

        private void InitializeCommands()
        {
            AddDriverCommand = new RelayCommand(ExecuteAddDriver);
            SaveDriverCommand = new AsyncRelayCommand(ExecuteSaveDriverAsync, () => CanSaveDriver);
            DeleteDriverCommand = new AsyncRelayCommand(ExecuteDeleteDriverAsync, () => CanDeleteDriver);
            CancelCommand = new RelayCommand(ExecuteCancel);
            RefreshCommand = new AsyncRelayCommand(LoadDriversAsync);
        }

        #endregion

        #region Command Handlers

        private void ExecuteAddDriver()
        {
            try
            {
                Logger.Information("Starting new driver entry");
                Driver = new BusBuddy.Core.Models.Driver
                {
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow
                };
                IsEditMode = false;
                FormTitle = "Add New Driver";
                SelectedDriver = null;

                // Notify that save availability may have changed
                ((RelayCommand)SaveDriverCommand).NotifyCanExecuteChanged();
                ((RelayCommand)DeleteDriverCommand).NotifyCanExecuteChanged();
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

                // Validate required fields
                var validationErrors = await _driverService.ValidateDriverAsync(Driver);
                if (validationErrors.Count > 0)
                {
                    ShowError($"Validation failed: {string.Join(", ", validationErrors)}");
                    return;
                }

                BusBuddy.Core.Models.Driver savedDriver;
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

                // Update the collection
                await LoadDriversAsync();

                // Select the saved driver
                SelectedDriver = Drivers.FirstOrDefault(d => d.DriverId == savedDriver.DriverId);

                Logger.Information("Driver saved successfully: {DriverName} (ID: {DriverId})",
                    savedDriver.DriverName, savedDriver.DriverId);
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
                if (SelectedDriver == null) return;

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
                    ExecuteAddDriver(); // Reset to add mode
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
                if (IsEditMode && SelectedDriver != null)
                {
                    LoadDriverForEdit(SelectedDriver);
                }
                else
                {
                    ExecuteAddDriver();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during cancel operation");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Show error message to user
        /// </summary>
        private void ShowError(string message)
        {
            StatusMessage = message;
            Logger.Warning("User error: {Message}", message);
            System.Windows.MessageBox.Show(message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        /// <summary>
        /// Show success message to user
        /// </summary>
        private void ShowSuccess(string message)
        {
            StatusMessage = message;
            Logger.Information("User success: {Message}", message);
        }

        /// <summary>
        /// Load all drivers from the database
        /// </summary>
        private async Task LoadDriversAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Information("Loading drivers from database");

                var drivers = await _driverService.GetAllDriversAsync();

                Drivers.Clear();
                foreach (var driver in drivers.OrderBy(d => d.DriverName))
                {
                    Drivers.Add(driver);
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

        /// <summary>
        /// Load a driver for editing
        /// </summary>
        private void LoadDriverForEdit(BusBuddy.Core.Models.Driver driver)
        {
            try
            {
                Logger.Information("Loading driver for edit: {DriverName} (ID: {DriverId})",
                    driver.DriverName, driver.DriverId);

                // Create a copy to avoid modifying the original
                Driver = new BusBuddy.Core.Models.Driver
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

                // Notify that command availability may have changed
                ((RelayCommand)SaveDriverCommand).NotifyCanExecuteChanged();
                ((RelayCommand)DeleteDriverCommand).NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading driver for edit");
                ShowError($"Error loading driver: {ex.Message}");
            }
        }

        #endregion
    }
}
