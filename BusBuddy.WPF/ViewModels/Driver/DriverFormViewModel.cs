using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
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
            ["Active", "Inactive", "On Leave", "Suspended", "Terminated"];

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

        public IReadOnlyList<string> LicenseTypeOptions { get; } =
            ["CDL", "Regular", "Permit"];

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
            SaveDriverCommand = new AsyncRelayCommand(ExecuteSaveDriverAsync);
            DeleteDriverCommand = new AsyncRelayCommand(ExecuteDeleteDriverAsync, () => CanDeleteDriver);
            CancelCommand = new RelayCommand(ExecuteCancel);
            RefreshCommand = new AsyncRelayCommand(LoadDriversAsync);
        }

        // Command Handlers
        /// <summary>Reset the form for a new driver entry (call when opening Add Driver).</summary>
        public void PrepareNewDriver() => ExecuteAddDriver();

        private void ExecuteAddDriver()
        {
            try
            {
                Logger.Information("Starting new driver entry");
                Driver = new DriverModel
                {
                    Status = "Active",
                    DriversLicenceType = "CDL",
                    TrainingComplete = false,
                    CreatedDate = DateTime.UtcNow
                };
                TryUpdateDriverName();
                IsEditMode = false;
                FormTitle = "Add New Driver";
                SelectedDriver = null;
                StatusMessage = string.Empty;

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
                TryUpdateDriverName();
                EnsureLicenseType();
                NormalizeStatusForSave();

                var missing = GetMissingRequiredFields();
                if (missing.Count > 0)
                {
                    var detail = string.Join(", ", missing);
                    ShowError(
                        $"Cannot save yet — missing required fields: {detail}.",
                        "Save blocked");
                    Logger.Information("Save blocked — missing required fields: {Missing}", detail);
                    return;
                }

                IsLoading = true;
                Logger.Information(
                    "Saving driver: {DriverName} EditMode={Edit} Id={Id} LicenseType={LicenseType} Class={Class}",
                    Driver.DriverName,
                    IsEditMode,
                    Driver.DriverId,
                    Driver.DriversLicenceType,
                    Driver.LicenseClass);
                Logger.Debug(
                    "Driver pre-save snapshot -> Id={Id} Name={Name} Phone={Phone} License={Lic} Class={Class} Type={Type} Status={Status}",
                    Driver.DriverId,
                    Driver.DriverName,
                    Driver.DriverPhone,
                    Driver.LicenseNumber,
                    Driver.LicenseClass,
                    Driver.DriversLicenceType,
                    Driver.Status);

                var validationErrors = await _driverService.ValidateDriverAsync(Driver);
                if (validationErrors.Count > 0)
                {
                    ShowError(
                        $"Cannot save — validation failed:\n• {string.Join("\n• ", validationErrors)}",
                        "Validation failed");
                    return;
                }

                DriverModel savedDriver;
                if (IsEditMode)
                {
                    var updated = await _driverService.UpdateDriverAsync(Driver);
                    if (!updated.IsSuccess)
                    {
                        ShowError(updated.Error, "Save failed");
                        Logger.Debug("Update operation failed for Id={Id}: {Error}", Driver.DriverId, updated.Error);
                        return;
                    }
                    savedDriver = Driver;
                    ShowSuccess($"Saved changes for '{savedDriver.DriverName}'.", "Driver updated");
                }
                else
                {
                    TryUpdateDriverName();
                    var added = await _driverService.AddDriverAsync(Driver);
                    if (!added.IsSuccess)
                    {
                        ShowError(added.Error, "Save failed");
                        return;
                    }

                    savedDriver = added.Value;
                    Logger.Debug("Add operation returned Id={Id}", savedDriver.DriverId);
                    ShowSuccess($"Added '{savedDriver.DriverName}' to the roster.", "Driver added");
                }

                Logger.Information("Driver saved successfully: {DriverName} (ID: {DriverId})",
                    savedDriver.DriverName, savedDriver.DriverId);

                // Close before reloading selection — setting SelectedDriver re-enters LoadDriverForEdit.
                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving driver: {DriverName}", Driver.DriverName);
                ShowError($"Error saving driver: {ex.Message}", "Save failed");
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
                    $"Retire driver '{SelectedDriver.DriverName}'?\n\n" +
                    "This marks them Inactive, keeps history, and clears future route assignments.",
                    "Confirm Retire Driver",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }

                IsLoading = true;
                Logger.Information("Retiring driver: {DriverName} (ID: {DriverId})",
                    SelectedDriver.DriverName, SelectedDriver.DriverId);
                Logger.Debug("Driver retire snapshot -> Id={Id} Name={Name}", SelectedDriver.DriverId, SelectedDriver.DriverName);

                var retired = await _driverService.DeleteDriverAsync(SelectedDriver.DriverId);
                if (!retired.IsSuccess)
                {
                    ShowError(retired.Error, "Retire failed");
                    return;
                }

                ShowSuccess(
                    string.IsNullOrWhiteSpace(retired.Error)
                        ? $"Driver '{SelectedDriver.DriverName}' retired."
                        : retired.Error,
                    "Driver retired");
                await LoadDriversAsync();
                ExecuteAddDriver();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error retiring driver");
                ShowError($"Error retiring driver: {ex.Message}");
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
        private void ShowError(string message, string title = "Error")
        {
            StatusMessage = message;
            OnPropertyChanged(nameof(HasStatusMessage));
            Logger.Warning("User error: {Message}", message);
            UserToast.Error(message, title);
        }

        private void ShowSuccess(string message, string title = "Success")
        {
            StatusMessage = message;
            OnPropertyChanged(nameof(HasStatusMessage));
            Logger.Information("User success: {Message}", message);
            UserToast.Success(message, title);
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

                // Copy every persisted field — omitting DriversLicenceType made every Save fail validation.
                Driver = new DriverModel
                {
                    DriverId = driver.DriverId,
                    DriverName = driver.DriverName,
                    FirstName = driver.FirstName,
                    LastName = driver.LastName,
                    DriverPhone = driver.DriverPhone,
                    DriverEmail = driver.DriverEmail,
                    LicenseNumber = driver.LicenseNumber,
                    LicenseClass = NormalizeLicenseClass(driver.LicenseClass),
                    DriversLicenceType = string.IsNullOrWhiteSpace(driver.DriversLicenceType)
                        ? "CDL"
                        : driver.DriversLicenceType,
                    LicenseExpiryDate = driver.LicenseExpiryDate,
                    Endorsements = driver.Endorsements,
                    Status = NormalizeStatus(driver.Status),
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
                    Notes = driver.Notes,
                    CreatedDate = driver.CreatedDate,
                    UpdatedDate = driver.UpdatedDate
                };

                IsEditMode = true;
                FormTitle = $"Edit Driver - {driver.DriverName}";
                StatusMessage = string.Empty;
                TryUpdateDriverName();
                EnsureLicenseType();
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

        private void EnsureLicenseType()
        {
            if (string.IsNullOrWhiteSpace(Driver.DriversLicenceType))
            {
                Driver.DriversLicenceType = "CDL";
            }
        }

        private void NormalizeStatusForSave()
        {
            Driver.Status = NormalizeStatus(Driver.Status);
        }

        private static string NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "Active";
            }

            // Legacy UI offered "Training"; validator only accepts employment statuses.
            if (status.Equals("Training", StringComparison.OrdinalIgnoreCase))
            {
                return "Active";
            }

            return status;
        }

        /// <summary>Map short DB values (A/B/C) onto ComboBoxAdv items (Class A/B/C).</summary>
        private static string? NormalizeLicenseClass(string? licenseClass)
        {
            if (string.IsNullOrWhiteSpace(licenseClass))
            {
                return licenseClass;
            }

            var trimmed = licenseClass.Trim();
            return trimmed.ToUpperInvariant() switch
            {
                "A" => "Class A",
                "B" => "Class B",
                "C" => "Class C",
                "CLASS A" => "Class A",
                "CLASS B" => "Class B",
                "CLASS C" => "Class C",
                _ => trimmed
            };
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
        }

        private List<string> GetMissingRequiredFields()
        {
            var missing = new List<string>();
            if (!HasUsableDriverName())
            {
                missing.Add("First/Last name");
            }

            if (!HasRealInput(Driver.DriverPhone))
            {
                missing.Add("Phone");
            }

            if (!HasRealInput(Driver.LicenseNumber))
            {
                missing.Add("License number");
            }

            if (!HasRealInput(Driver.LicenseClass))
            {
                missing.Add("License class");
            }

            return missing;
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
