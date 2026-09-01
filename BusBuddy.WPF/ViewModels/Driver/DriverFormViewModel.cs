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
            _driver.PropertyChanged += OnDriverModelPropertyChanged;
            InitializeCommands();
            _ = LoadDriversAsync();
        }

        // Properties
        public DriverModel Driver
        {
            get => _driver;
            set
            {
                var old = _driver;
                if (SetProperty(ref _driver, value))
                {
                    if (old is not null)
                    {
                        old.PropertyChanged -= OnDriverModelPropertyChanged;
                    }

                    if (_driver is not null)
                    {
                        _driver.PropertyChanged += OnDriverModelPropertyChanged;
                    }

                    // Keep composite name aligned
                    TryUpdateDriverName();
                    RefreshSaveCanExecute();
                    if (DeleteDriverCommand is IRelayCommand del)
                    {
                        del.NotifyCanExecuteChanged();
                    }

                    Logger.Debug("Driver object replaced -> Id={Id} Name={Name}", _driver?.DriverId, _driver?.DriverName);
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

        public IReadOnlyList<string> StatusOptions { get; } =
            ["Active", "Inactive", "On Leave", "Training", "Terminated"];

        public IReadOnlyList<string> DutyCategoryOptions { get; } =
            [DriverDutyCategories.Route, DriverDutyCategories.Activity];

        public IReadOnlyList<string> VehicleCategoryOptions { get; } =
        [
            DriverVehicleCategories.Route16PlusGvwrOver26001,
            DriverVehicleCategories.Route16PlusGvwrUnder26001,
            DriverVehicleCategories.RouteTypeA15OrLess,
            DriverVehicleCategories.ActivityMf16PlusGvwrOver26001,
            DriverVehicleCategories.ActivityMf16PlusGvwrUnder26001,
            DriverVehicleCategories.ActivityTypeA15OrLess,
            DriverVehicleCategories.ActivityUnder12,
            DriverVehicleCategories.ActivityMotorcoach
        ];

        public IReadOnlyList<string> MedicalFormTypeOptions { get; } =
            ["USDOT Physical", "STU-17"];

        /// <summary>Stored values must fit Drivers.LicenseClass (max 10).</summary>
        public IReadOnlyList<string> LicenseClassOptions { get; } =
            ["Class A", "Class B", "Class C", "Regular"];

        public bool CanSaveDriver =>
            HasUsableDriverName() &&
            HasRealInput(Driver.DriverPhone) &&
            HasRealInput(Driver.LicenseNumber) &&
            HasRealInput(Driver.LicenseClass);

        /// <summary>True when StatusMessage should show in the form status panel.</summary>
        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

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
                TryUpdateDriverName();
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
                Logger.Debug("Driver pre-save snapshot -> Id={Id} Name={Name} Phone={Phone} License={Lic} Class={Class}", Driver.DriverId, Driver.DriverName, Driver.DriverPhone, Driver.LicenseNumber, Driver.LicenseClass);

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
                        Logger.Debug("Update operation returned false for Id={Id}", Driver.DriverId);
                        return;
                    }
                    savedDriver = Driver;
                    ShowSuccess("Driver updated successfully");
                }
                else
                {
                    TryUpdateDriverName();
                    savedDriver = await _driverService.AddDriverAsync(Driver);
                    Logger.Debug("Add operation returned Id={Id}", savedDriver.DriverId);
                    ShowSuccess("Driver added successfully");
                }

                await LoadDriversAsync();
                SelectedDriver = Drivers.FirstOrDefault(d => d.DriverId == savedDriver.DriverId);
                Logger.Information("Driver saved successfully: {DriverName} (ID: {DriverId})",
                    savedDriver.DriverName, savedDriver.DriverId);
                Logger.Debug("Driver post-save snapshot -> Id={Id} UpdatedDate={Updated} CreatedDate={Created}", savedDriver.DriverId, savedDriver.UpdatedDate, savedDriver.CreatedDate);

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
                Logger.Debug("Driver delete snapshot -> Id={Id} Name={Name}", SelectedDriver.DriverId, SelectedDriver.DriverName);

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
            OnPropertyChanged(nameof(HasStatusMessage));
            Logger.Warning("User error: {Message}", message);
            System.Windows.MessageBox.Show(message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        private void ShowSuccess(string message)
        {
            StatusMessage = message;
            OnPropertyChanged(nameof(HasStatusMessage));
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
                    HireDate = driver.HireDate,
                    EmploymentEndDate = driver.EmploymentEndDate,
                    EmployingDistrict = driver.EmployingDistrict,
                    DutyCategory = driver.DutyCategory,
                    VehicleCategory = driver.VehicleCategory,
                    CdlRestrictions = driver.CdlRestrictions,
                    MedicalFormType = driver.MedicalFormType,
                    CreatedDate = driver.CreatedDate,
                    UpdatedDate = driver.UpdatedDate
                };

                IsEditMode = true;
                FormTitle = $"Edit Driver - {driver.DriverName}";
                TryUpdateDriverName();
                RefreshSaveCanExecute();
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

        private void OnDriverModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DriverModel.FirstName) or nameof(DriverModel.LastName))
            {
                TryUpdateDriverName();
            }

            if (e.PropertyName is nameof(DriverModel.FirstName)
                or nameof(DriverModel.LastName)
                or nameof(DriverModel.DriverName)
                or nameof(DriverModel.DriverPhone)
                or nameof(DriverModel.LicenseNumber)
                or nameof(DriverModel.LicenseClass))
            {
                RefreshSaveCanExecute();
            }
        }

        private void RefreshSaveCanExecute()
        {
            OnPropertyChanged(nameof(CanSaveDriver));
            if (SaveDriverCommand is IRelayCommand save)
            {
                save.NotifyCanExecuteChanged();
            }
        }

        private bool HasUsableDriverName()
        {
            if (HasRealInput(Driver.FirstName) || HasRealInput(Driver.LastName))
            {
                return true;
            }

            var name = Driver.DriverName?.Trim() ?? string.Empty;
            return HasRealInput(name) &&
                   !name.StartsWith("Driver-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when text has content beyond Syncfusion mask prompt characters.</summary>
        private static bool HasRealInput(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var cleaned = value
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("(", string.Empty, StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);
            return cleaned.Length > 0;
        }

        private void TryUpdateDriverName()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Driver.FirstName) || !string.IsNullOrWhiteSpace(Driver.LastName))
                {
                    var full = ($"{Driver.FirstName} {Driver.LastName}").Trim();
                    if (!string.IsNullOrWhiteSpace(full) &&
                        !string.Equals(Driver.DriverName, full, StringComparison.Ordinal))
                    {
                        Driver.DriverName = full;
                        OnPropertyChanged(nameof(Driver));
                        Logger.Debug("Computed DriverName -> {Name} (First={First} Last={Last})", full, Driver.FirstName, Driver.LastName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to auto-update DriverName from First/Last");
            }
        }
    }
}
