using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using Serilog;
using BusBuddy.WPF.Commands;
using Serilog.Context;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.Views.Fuel
{
    public partial class FuelReconciliationDialog : Window, INotifyPropertyChanged
    {
        private static readonly ILogger Logger = Log.ForContext<FuelReconciliationDialog>();

        private readonly IFuelService _fuelService;
        private readonly IBusService _busService;

        // Date range properties
        private DateTime _startDate = DateTime.Now.AddDays(-30);
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
                _ = LoadReconciliationDataAsync();
            }
        }

        private DateTime _endDate = DateTime.Now;
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
                _ = LoadReconciliationDataAsync();
            }
        }

        // Location filtering
        public ObservableCollection<string> FuelLocations { get; } = new()
        {
            "All Locations", "Key Pumps", "School District Pump", "BP", "Shell", "Chevron", "Exxon", "Mobil", "Marathon", "Sunoco", "Other"
        };

        private string _selectedLocation = "All Locations";
        public string SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                _selectedLocation = value;
                OnPropertyChanged();
                _ = LoadReconciliationDataAsync();
            }
        }

        // Reconciliation summary data
        private double _bulkStationGallons;
        public double BulkStationGallons
        {
            get => _bulkStationGallons;
            set
            {
                _bulkStationGallons = value;
                OnPropertyChanged();
                UpdateDiscrepancy();
            }
        }

        private double _vehicleUsageGallons;
        public double VehicleUsageGallons
        {
            get => _vehicleUsageGallons;
            set
            {
                _vehicleUsageGallons = value;
                OnPropertyChanged();
                UpdateDiscrepancy();
            }
        }

        private double _discrepancyGallons;
        public double DiscrepancyGallons
        {
            get => _discrepancyGallons;
            set
            {
                _discrepancyGallons = value;
                OnPropertyChanged();
            }
        }

        private double _discrepancyPercentage;
        public double DiscrepancyPercentage
        {
            get => _discrepancyPercentage;
            set
            {
                _discrepancyPercentage = value;
                OnPropertyChanged();
                UpdateDiscrepancyColors();
            }
        }

        // Discrepancy visualization
        private Brush _discrepancyBackground = new SolidColorBrush(Colors.LightGreen);
        public Brush DiscrepancyBackground
        {
            get => _discrepancyBackground;
            set
            {
                _discrepancyBackground = value;
                OnPropertyChanged();
            }
        }

        private Brush _discrepancyBorder = new SolidColorBrush(Colors.Green);
        public Brush DiscrepancyBorder
        {
            get => _discrepancyBorder;
            set
            {
                _discrepancyBorder = value;
                OnPropertyChanged();
            }
        }

        private Brush _discrepancyForeground = new SolidColorBrush(Colors.Green);
        public Brush DiscrepancyForeground
        {
            get => _discrepancyForeground;
            set
            {
                _discrepancyForeground = value;
                OnPropertyChanged();
            }
        }

        private string _reconciliationSummary = "Loading reconciliation data...";
        public string ReconciliationSummary
        {
            get => _reconciliationSummary;
            set
            {
                _reconciliationSummary = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _lastReconciliationDate;
        public DateTime? LastReconciliationDate
        {
            get => _lastReconciliationDate;
            set
            {
                _lastReconciliationDate = value;
                OnPropertyChanged();
            }
        }

        // Observable collections for charts and grids
        public ObservableCollection<DailyReconciliationItem> DailyReconciliation { get; } = new();
        public ObservableCollection<DiscrepancyDetailItem> DiscrepancyDetails { get; } = new();

        // Commands
        public CommunityToolkit.Mvvm.Input.RelayCommand ExportCommand { get; }
        public CommunityToolkit.Mvvm.Input.RelayCommand PrintCommand { get; }

        public FuelReconciliationDialog(IFuelService fuelService, IBusService busService)
        {
            _fuelService = fuelService ?? throw new ArgumentNullException(nameof(fuelService));
            _busService = busService ?? throw new ArgumentNullException(nameof(busService));

            InitializeComponent();
            DataContext = this;

            // Initialize commands
            ExportCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExportReconciliationReport);
            PrintCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(PrintReconciliationReport);

            // Load initial data
            _ = LoadReconciliationDataAsync();
        }

        private async Task LoadReconciliationDataAsync()
        {
            try
            {
                // Clear existing data
                DailyReconciliation.Clear();
                DiscrepancyDetails.Clear();

                // Get all fuel records within the date range
                var fuelRecords = await _fuelService.GetAllFuelRecordsAsync();
                var filteredRecords = fuelRecords.Where(r =>
                    r.FuelDate >= StartDate &&
                    r.FuelDate <= EndDate &&
                    (SelectedLocation == "All Locations" || r.FuelLocation == SelectedLocation)).ToList();

                // Get buses for reference
                var buses = await _busService.GetAllBusesAsync();
                var busLookup = buses.ToDictionary(b => b.VehicleId, b => b.BusNumber);

                // Calculate totals
                double bulkTotal = 0;
                double vehicleTotal = 0;

                // Generate daily reconciliation data
                var dailyData = filteredRecords
                    .GroupBy(r => r.FuelDate.Date)
                    .Select(g => new DailyReconciliationItem
                    {
                        Date = g.Key,
                        // Simulate bulk station data - in a real app, this would come from a separate source
                        BulkStationGallons = (double)(g.Sum(r => r.Gallons ?? 0) * (decimal)(new Random(g.Key.GetHashCode()).Next(97, 104) / 100.0)),
                        VehicleUsageGallons = (double)g.Sum(r => r.Gallons ?? 0)
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                foreach (var day in dailyData)
                {
                    DailyReconciliation.Add(day);
                    bulkTotal += day.BulkStationGallons;
                    vehicleTotal += day.VehicleUsageGallons;

                    // Generate discrepancy details for days with significant differences
                    double dayDiscrepancy = day.BulkStationGallons - day.VehicleUsageGallons;
                    double dayDiscrepancyPct = day.VehicleUsageGallons > 0 ?
                        Math.Abs(dayDiscrepancy / day.VehicleUsageGallons) : 0;

                    if (dayDiscrepancyPct > 0.03) // More than 3% discrepancy
                    {
                        // Find the records for this day
                        var dayRecords = filteredRecords.Where(r => r.FuelDate.Date == day.Date).ToList();

                        foreach (var record in dayRecords)
                        {
                            string busNumber = busLookup.TryGetValue(record.VehicleFueledId, out var num) ? num : "Unknown";

                            if (dayDiscrepancy < 0) // Less fuel at bulk station than reported by vehicles
                            {
                                DiscrepancyDetails.Add(new DiscrepancyDetailItem
                                {
                                    Date = record.FuelDate,
                                    BusNumber = busNumber,
                                    Driver = "Unknown", // In a real app, we would get this from the driver database
                                    GallonsReported = (double)(record.Gallons ?? 0),
                                    OdometerReading = record.VehicleOdometerReading,
                                    DiscrepancyType = "Under-reporting",
                                    PotentialIssue = "Possible bulk station meter inaccuracy or fuel loss",
                                    RecommendedAction = "Calibrate bulk station meter and check for leaks"
                                });
                            }
                            else // More fuel at bulk station than reported by vehicles
                            {
                                DiscrepancyDetails.Add(new DiscrepancyDetailItem
                                {
                                    Date = record.FuelDate,
                                    BusNumber = busNumber,
                                    Driver = "Unknown", // In a real app, we would get this from the driver database
                                    GallonsReported = (double)(record.Gallons ?? 0),
                                    OdometerReading = record.VehicleOdometerReading,
                                    DiscrepancyType = "Over-reporting",
                                    PotentialIssue = "Possible vehicle fuel log inaccuracy or unreported fueling",
                                    RecommendedAction = "Verify driver fuel logs and check for unreported usage"
                                });
                            }
                        }
                    }
                }

                // Update summary values
                BulkStationGallons = bulkTotal;
                VehicleUsageGallons = vehicleTotal;

                // Set last reconciliation date
                LastReconciliationDate = DateTime.Now;

                // Update summary text
                if (filteredRecords.Count == 0)
                {
                    ReconciliationSummary = "No fuel data available for the selected period and location.";
                }
                else
                {
                    ReconciliationSummary = $"Analysis of {filteredRecords.Count} fuel records from {StartDate:d} to {EndDate:d}" +
                        $" shows a {Math.Abs(DiscrepancyPercentage):P2} {(DiscrepancyGallons >= 0 ? "surplus" : "deficit")} " +
                        $"between bulk station meters and vehicle usage logs. " +
                        $"{(Math.Abs(DiscrepancyPercentage) > 0.05 ? "This discrepancy requires investigation." : "This is within acceptable limits.")}";
                }

                using (LogContext.PushProperty("ViewType", "FuelReconciliationDialog"))
                using (LogContext.PushProperty("OperationType", "LoadReconciliationData"))
                {
                    Logger.Information("Loaded reconciliation data: {RecordCount} records, {DiscrepancyPercentage:P2} discrepancy",
                        filteredRecords.Count, DiscrepancyPercentage);
                }
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("ViewType", "FuelReconciliationDialog"))
                using (LogContext.PushProperty("OperationType", "LoadReconciliationData"))
                {
                    Logger.Error(ex, "Error loading reconciliation data");
                }
                MessageBox.Show($"Error loading reconciliation data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ReconciliationSummary = "Error loading reconciliation data. Please try again.";
            }
        }

        private void UpdateDiscrepancy()
        {
            DiscrepancyGallons = BulkStationGallons - VehicleUsageGallons;
            DiscrepancyPercentage = VehicleUsageGallons > 0 ?
                DiscrepancyGallons / VehicleUsageGallons : 0;
        }

        private void UpdateDiscrepancyColors()
        {
            double absPercentage = Math.Abs(DiscrepancyPercentage);

            if (absPercentage <= 0.02) // Less than 2% discrepancy - good
            {
                DiscrepancyBackground = new SolidColorBrush(Colors.LightGreen);
                DiscrepancyBorder = new SolidColorBrush(Colors.Green);
                DiscrepancyForeground = new SolidColorBrush(Colors.Green);
            }
            else if (absPercentage <= 0.05) // 2-5% discrepancy - warning
            {
                DiscrepancyBackground = new SolidColorBrush(Colors.LightYellow);
                DiscrepancyBorder = new SolidColorBrush(Colors.Orange);
                DiscrepancyForeground = new SolidColorBrush(Colors.DarkOrange);
            }
            else // More than 5% discrepancy - bad
            {
                DiscrepancyBackground = new SolidColorBrush(Colors.MistyRose);
                DiscrepancyBorder = new SolidColorBrush(Colors.Red);
                DiscrepancyForeground = new SolidColorBrush(Colors.Red);
            }
        }

        private void ExportReconciliationReport()
        {
            try
            {
                // Create a Save File Dialog
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Fuel_Reconciliation_{StartDate:yyyyMMdd}_to_{EndDate:yyyyMMdd}",
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv"
                };

                var result = dialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    // Export data to CSV
                    using var writer = new System.IO.StreamWriter(dialog.FileName);

                    // Write header and summary
                    writer.WriteLine("Fuel Reconciliation Report");
                    writer.WriteLine($"Period: {StartDate:d} to {EndDate:d}");
                    writer.WriteLine($"Location: {SelectedLocation}");
                    writer.WriteLine($"Generated: {DateTime.Now}");
                    writer.WriteLine();
                    writer.WriteLine($"Bulk Station Total: {BulkStationGallons:N2} gallons");
                    writer.WriteLine($"Vehicle Usage Total: {VehicleUsageGallons:N2} gallons");
                    writer.WriteLine($"Discrepancy: {DiscrepancyGallons:N2} gallons ({DiscrepancyPercentage:P2})");
                    writer.WriteLine();

                    // Write daily data
                    writer.WriteLine("Daily Reconciliation");
                    writer.WriteLine("Date,Bulk Station Gallons,Vehicle Usage Gallons,Discrepancy Gallons,Discrepancy %");

                    foreach (var day in DailyReconciliation)
                    {
                        double discrepancy = day.BulkStationGallons - day.VehicleUsageGallons;
                        double discrepancyPct = day.VehicleUsageGallons > 0 ?
                            discrepancy / day.VehicleUsageGallons : 0;

                        writer.WriteLine(
                            $"{day.Date:yyyy-MM-dd}," +
                            $"{day.BulkStationGallons:N2}," +
                            $"{day.VehicleUsageGallons:N2}," +
                            $"{discrepancy:N2}," +
                            $"{discrepancyPct:P2}");
                    }

                    writer.WriteLine();

                    // Write discrepancy details
                    if (DiscrepancyDetails.Count > 0)
                    {
                        writer.WriteLine("Discrepancy Details");
                        writer.WriteLine("Date,Bus Number,Driver,Gallons Reported,Odometer Reading,Discrepancy Type,Potential Issue,Recommended Action");

                        foreach (var detail in DiscrepancyDetails)
                        {
                            writer.WriteLine(
                                $"{detail.Date:yyyy-MM-dd}," +
                                $"\"{detail.BusNumber}\"," +
                                $"\"{detail.Driver}\"," +
                                $"{detail.GallonsReported:N2}," +
                                $"{detail.OdometerReading}," +
                                $"\"{detail.DiscrepancyType}\"," +
                                $"\"{detail.PotentialIssue}\"," +
                                $"\"{detail.RecommendedAction}\"");
                        }
                    }

                    MessageBox.Show($"Successfully exported reconciliation report to {dialog.FileName}",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    using (LogContext.PushProperty("ViewType", "FuelReconciliationDialog"))
                    using (LogContext.PushProperty("OperationType", "ExportReconciliationReport"))
                    {
                        Logger.Information("Exported fuel reconciliation report to CSV");
                    }
                }
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("ViewType", "FuelReconciliationDialog"))
                using (LogContext.PushProperty("OperationType", "ExportReconciliationReport"))
                {
                    Logger.Error(ex, "Error exporting reconciliation report");
                }
                MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReconciliationReport()
        {
            MessageBox.Show("Print functionality will be implemented in the next sprint.",
                "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DailyReconciliationItem
    {
        public DateTime Date { get; set; }
        public double BulkStationGallons { get; set; }
        public double VehicleUsageGallons { get; set; }
    }

    public class DiscrepancyDetailItem
    {
        public DateTime Date { get; set; }
        public string BusNumber { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public double GallonsReported { get; set; }
        public int OdometerReading { get; set; }
        public string DiscrepancyType { get; set; } = string.Empty;
        public string PotentialIssue { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }
}
