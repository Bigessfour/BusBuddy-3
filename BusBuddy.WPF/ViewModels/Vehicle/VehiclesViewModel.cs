using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using BusBuddy.WPF.Commands;
using System.Windows.Input;

namespace BusBuddy.WPF.ViewModels.Vehicle
{
    /// <summary>
    /// Phase 1 ViewModel for Vehicle Management — Simple and functional
    /// </summary>
    public class VehiclesViewModel : BaseViewModel
    {
        #region Fields
        private ObservableCollection<BusBuddy.Core.Models.Bus> _vehicles = new();
        private BusBuddy.Core.Models.Bus? _selectedVehicle;
        #endregion

        #region Properties
        public ObservableCollection<BusBuddy.Core.Models.Bus> Vehicles
        {
            get => _vehicles;
            set => SetProperty(ref _vehicles, value);
        }

        public BusBuddy.Core.Models.Bus? SelectedVehicle
        {
            get => _selectedVehicle;
            set => SetProperty(ref _selectedVehicle, value);
        }
        #endregion

        #region Commands
        public ICommand AddVehicleCommand { get; }
        public ICommand EditVehicleCommand { get; }
        public ICommand DeleteVehicleCommand { get; }
        #endregion

        #region Constructor
        public VehiclesViewModel()
        {
            // Initialize commands - Phase 1 simple approach
            AddVehicleCommand = new RelayCommand(AddVehicle);
            EditVehicleCommand = new RelayCommand(EditVehicle, CanEditVehicle);
            DeleteVehicleCommand = new RelayCommand(DeleteVehicle, CanDeleteVehicle);

            // Load sample data
            LoadVehicleData();
        }
        #endregion

        #region Command Methods
        private void AddVehicle()
        {
            try
            {
                // Phase 1: Simple add functionality
                var newVehicle = new BusBuddy.Core.Models.Bus
                {
                    BusId = Vehicles.Count + 1,
                    BusNumber = "NEW",
                    LicenseNumber = "NEW123",
                    Make = "New",
                    Model = "Bus",
                    Year = DateTime.Now.Year,
                    SeatingCapacity = 72,
                    Status = "Active",
                    CurrentOdometer = 0
                };

                Vehicles.Add(newVehicle);
                SelectedVehicle = newVehicle;

                Logger.Information("Added new vehicle: {BusNumber}", newVehicle.BusNumber);
            }
            catch (Exception ex)
            {
                // Phase 1 error handling
                Logger.Error(ex, "Error adding vehicle");
                ShowError($"Error adding vehicle: {ex.Message}");
            }
        }

        private void EditVehicle()
        {
            try
            {
                if (SelectedVehicle is not null)
                {
                    // Phase 1: Simple edit notification
                    Logger.Information("Edit requested for: {BusNumber}", SelectedVehicle.BusNumber);
                    MessageBox.Show($"Edit functionality for {SelectedVehicle.BusNumber} - {SelectedVehicle.Make} {SelectedVehicle.Model}\n\n(Phase 2: Full edit dialog)",
                                  "Edit Vehicle", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error editing vehicle");
                ShowError($"Error editing vehicle: {ex.Message}");
            }
        }

        private void DeleteVehicle()
        {
            try
            {
                if (SelectedVehicle is not null)
                {
                    var result = MessageBox.Show($"Delete vehicle {SelectedVehicle.BusNumber}?",
                                               "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Logger.Information("Deleting vehicle: {BusNumber}", SelectedVehicle.BusNumber);
                        Vehicles.Remove(SelectedVehicle);
                        SelectedVehicle = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error deleting vehicle");
                ShowError($"Error deleting vehicle: {ex.Message}");
            }
        }

        private bool CanEditVehicle() => SelectedVehicle is not null;
        private bool CanDeleteVehicle() => SelectedVehicle is not null;
        #endregion

        #region Data Loading - Phase 1
        private void LoadVehicleData()
        {
            try
            {
                // Phase 1: Sample vehicle data for demonstration
                var sampleData = new BusBuddy.Core.Models.Bus[]
                {
                    new BusBuddy.Core.Models.Bus { BusId = 1, BusNumber = "Bus-001", LicenseNumber = "SCH-001", Make = "Blue Bird", Model = "Vision", Year = 2019, SeatingCapacity = 72, Status = "Active", CurrentOdometer = 45230 },
                    new BusBuddy.Core.Models.Bus { BusId = 2, BusNumber = "Bus-002", LicenseNumber = "SCH-002", Make = "Thomas Built", Model = "Saf-T-Liner C2", Year = 2020, SeatingCapacity = 71, Status = "Active", CurrentOdometer = 38750 },
                    new BusBuddy.Core.Models.Bus { BusId = 3, BusNumber = "Bus-003", LicenseNumber = "SCH-003", Make = "IC Bus", Model = "CE Series", Year = 2018, SeatingCapacity = 77, Status = "Maintenance", CurrentOdometer = 52100 },
                    new BusBuddy.Core.Models.Bus { BusId = 4, BusNumber = "Bus-004", LicenseNumber = "SCH-004", Make = "Blue Bird", Model = "All American", Year = 2021, SeatingCapacity = 72, Status = "Active", CurrentOdometer = 28900 },
                    new BusBuddy.Core.Models.Bus { BusId = 5, BusNumber = "Bus-005", LicenseNumber = "SCH-005", Make = "Thomas Built", Model = "Saf-T-Liner HDX", Year = 2017, SeatingCapacity = 78, Status = "Active", CurrentOdometer = 67500 },
                    new BusBuddy.Core.Models.Bus { BusId = 6, BusNumber = "Bus-006", LicenseNumber = "SCH-006", Make = "IC Bus", Model = "RE Series", Year = 2022, SeatingCapacity = 77, Status = "Active", CurrentOdometer = 15600 },
                    new BusBuddy.Core.Models.Bus { BusId = 7, BusNumber = "Bus-007", LicenseNumber = "SCH-007", Make = "Blue Bird", Model = "Vision", Year = 2019, SeatingCapacity = 72, Status = "Active", CurrentOdometer = 41200 },
                    new BusBuddy.Core.Models.Bus { BusId = 8, BusNumber = "Bus-008", LicenseNumber = "SCH-008", Make = "Thomas Built", Model = "Saf-T-Liner C2", Year = 2020, SeatingCapacity = 71, Status = "Out of Service", CurrentOdometer = 43800 },
                    new BusBuddy.Core.Models.Bus { BusId = 9, BusNumber = "Bus-009", LicenseNumber = "SCH-009", Make = "IC Bus", Model = "CE Series", Year = 2018, SeatingCapacity = 77, Status = "Active", CurrentOdometer = 49300 },
                    new BusBuddy.Core.Models.Bus { BusId = 10, BusNumber = "Bus-010", LicenseNumber = "SCH-010", Make = "Blue Bird", Model = "All American", Year = 2021, SeatingCapacity = 72, Status = "Active", CurrentOdometer = 31750 },
                    new BusBuddy.Core.Models.Bus { BusId = 11, BusNumber = "Bus-011", LicenseNumber = "SCH-011", Make = "Thomas Built", Model = "Saf-T-Liner HDX", Year = 2016, SeatingCapacity = 78, Status = "Active", CurrentOdometer = 78200 },
                    new BusBuddy.Core.Models.Bus { BusId = 12, BusNumber = "Bus-012", LicenseNumber = "SCH-012", Make = "IC Bus", Model = "RE Series", Year = 2022, SeatingCapacity = 77, Status = "Active", CurrentOdometer = 12400 },
                    new BusBuddy.Core.Models.Bus { BusId = 13, BusNumber = "Bus-013", LicenseNumber = "SCH-013", Make = "Blue Bird", Model = "Vision", Year = 2017, SeatingCapacity = 72, Status = "Maintenance", CurrentOdometer = 58900 },
                    new BusBuddy.Core.Models.Bus { BusId = 14, BusNumber = "Bus-014", LicenseNumber = "SCH-014", Make = "Thomas Built", Model = "Saf-T-Liner C2", Year = 2019, SeatingCapacity = 71, Status = "Active", CurrentOdometer = 36700 },
                    new BusBuddy.Core.Models.Bus { BusId = 15, BusNumber = "Bus-015", LicenseNumber = "SCH-015", Make = "IC Bus", Model = "CE Series", Year = 2023, SeatingCapacity = 77, Status = "Active", CurrentOdometer = 8500 }
                };

                Vehicles.Clear();
                foreach (var bus in sampleData)
                {
                    Vehicles.Add(bus);
                }

                Logger.Information("Loaded {Count} vehicles for Phase 1", Vehicles.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading vehicle data");
                ShowError($"Error loading vehicle data: {ex.Message}");
            }
        }
        #endregion

        #region Helper Methods
        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        #endregion
    }

    // RelayCommand implementation consolidated in BusBuddy.WPF.Commands.RelayCommand
}
