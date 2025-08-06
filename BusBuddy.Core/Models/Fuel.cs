using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
    [Key]
    public int FuelId { get; set; }

    [Required]
    [Display(Name = "Fuel Date")]
    public DateTime FuelDate { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Fuel Location")]
    public string FuelLocation { get; set; } = string.Empty; // Key Pumps or Gas Station

    [Required]
    [ForeignKey("Vehicle")]
    [Display(Name = "Vehicle Fueled")]
    public int VehicleFueledId { get; set; }

    [Required]
    [Display(Name = "Vehicle Odometer Reading")]
    public int VehicleOdometerReading { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Fuel Type")]
    public string FuelType { get; set; } = string.Empty; // Gasoline or Diesel

    // Additional useful properties for fleet management
    [Column(TypeName = "decimal(8,3)")]
    [Display(Name = "Gallons")]
    public decimal? Gallons { get; set; }

    [Column(TypeName = "decimal(8,3)")]
    [Display(Name = "Price per Gallon")]
    public decimal? PricePerGallon { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Total Cost")]
    public decimal? TotalCost { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Bus Vehicle { get; set; } = null!;

    // INotifyPropertyChanged implementation for Syncfusion data binding
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
