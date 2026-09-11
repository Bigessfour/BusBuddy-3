using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a fuel record for bus fleet management
/// Based on Fuel Table from BusBuddy Tables schema
/// Matches requirements: Fuel Date, Fuel Location, Vehicle Fueled, Vehicle Odometer Reading, Fuel Type
/// Enhanced for Syncfusion data binding with INotifyPropertyChanged support
/// </summary>
[Table("Fuel")]
public class Fuel : INotifyPropertyChanged
{
    private DateTime _fuelDate;
    private string _fuelLocation = string.Empty;
    private int _vehicleFueledId;
    private int _vehicleOdometerReading;
    private string _fuelType = string.Empty;
    private decimal? _gallons;
    private decimal? _pricePerGallon;
    private decimal? _totalCost;
    private string? _notes;

    [Key]
    public int FuelId { get; set; }

    [Required]
    [Display(Name = "Fuel Date")]
    public DateTime FuelDate
    {
        get => _fuelDate;
        set
        {
            // DateTime.Equals ignores Kind — always apply when Kind changes (UTC midnight persist).
            if (_fuelDate.Ticks == value.Ticks && _fuelDate.Kind == value.Kind)
            {
                return;
            }

            _fuelDate = value;
            OnPropertyChanged();
        }
    }

    [Required]
    [StringLength(FuelConstraints.MaxLocationLength)]
    [Display(Name = "Fuel Location")]
    public string FuelLocation
    {
        get => _fuelLocation;
        set => SetField(ref _fuelLocation, value ?? string.Empty);
    }

    [Required]
    [ForeignKey("Vehicle")]
    [Display(Name = "Vehicle Fueled")]
    public int VehicleFueledId
    {
        get => _vehicleFueledId;
        set => SetField(ref _vehicleFueledId, value);
    }

    [Required]
    [Display(Name = "Vehicle Odometer Reading")]
    public int VehicleOdometerReading
    {
        get => _vehicleOdometerReading;
        set => SetField(ref _vehicleOdometerReading, value);
    }

    [Required]
    [StringLength(FuelConstraints.MaxFuelTypeLength)]
    [Display(Name = "Fuel Type")]
    public string FuelType
    {
        get => _fuelType;
        set => SetField(ref _fuelType, value ?? string.Empty);
    }

    [Column(TypeName = "decimal(8,3)")]
    [Display(Name = "Gallons")]
    public decimal? Gallons
    {
        get => _gallons;
        set => SetField(ref _gallons, value);
    }

    [Column(TypeName = "decimal(8,3)")]
    [Display(Name = "Price per Gallon")]
    public decimal? PricePerGallon
    {
        get => _pricePerGallon;
        set => SetField(ref _pricePerGallon, value);
    }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Total Cost")]
    public decimal? TotalCost
    {
        get => _totalCost;
        set => SetField(ref _totalCost, value);
    }

    [StringLength(FuelConstraints.MaxNotesLength)]
    [Display(Name = "Notes")]
    public string? Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    // Navigation properties
    public virtual Bus Vehicle { get; set; } = null!;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
