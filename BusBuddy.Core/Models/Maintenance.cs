using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a maintenance record for bus fleet management
/// Based on Maintenance Table from BusBuddy Tables schema
/// Matches requirements: Date, Vehicle, Odometer Reading, Maintenance Completed, Vendor, Repair Cost
/// Enhanced for Syncfusion data binding with INotifyPropertyChanged support
/// </summary>
[Table("Maintenance")]
public class Maintenance : INotifyPropertyChanged
{
    [Key]
    public int MaintenanceId { get; set; }

    [Required]
    [Display(Name = "Date")]
    public DateTime Date { get; set; }

    [Required]
    [ForeignKey("Vehicle")]
    [Display(Name = "Vehicle")]
    public int VehicleId { get; set; }

    [Required]
    [Display(Name = "Odometer Reading")]
    public int OdometerReading { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Maintenance Completed")]
    public string MaintenanceCompleted { get; set; } = string.Empty; // Tires, Windshield, Alignment, Mechanical, Car Wash, Cleaning, Accessory Install

    [Required]
    [StringLength(100)]
    [Display(Name = "Vendor")]
    public string Vendor { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Repair Cost")]
    public decimal RepairCost { get; set; }

    // Additional properties for enhanced functionality
    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(100)]
    [Display(Name = "Performed By")]
    public string? PerformedBy { get; set; }

    [Display(Name = "Next Service Due")]
    public DateTime? NextServiceDue { get; set; }

    [Display(Name = "Next Service Odometer")]
    public int? NextServiceOdometer { get; set; }

    [StringLength(20)]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Completed"; // Scheduled, In Progress, Completed

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Bus Vehicle { get; set; } = null!;

    // Extended properties for maximum flexibility
    [StringLength(100)]
    [Display(Name = "Work Order Number")]
    public string? WorkOrderNumber { get; set; }

    [StringLength(20)]
    [Display(Name = "Priority")]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Emergency

    [Display(Name = "Warranty")]
    public bool Warranty { get; set; } = false;

    [Display(Name = "Warranty Expiry")]
    public DateTime? WarrantyExpiry { get; set; }

    [StringLength(1000)]
    [Display(Name = "Parts Used")]
    public string? PartsUsed { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    [Display(Name = "Labor Hours")]
    public decimal? LaborHours { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Labor Cost")]
    public decimal? LaborCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Parts Cost")]
    public decimal? PartsCost { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    [Display(Name = "Updated By")]
    public string? UpdatedBy { get; set; }

    // Computed properties
    [NotMapped]
    [Display(Name = "Total Cost")]
    public decimal TotalCost => RepairCost + (LaborCost ?? 0) + (PartsCost ?? 0);

    [NotMapped]
    [Display(Name = "Is Recent")]
    public bool IsRecent => Date > DateTime.Now.AddDays(-30);

    [NotMapped]
    [Display(Name = "Cost Category")]
    public string CostCategory
    {
        get => TotalCost switch
        {
            < 100 => "Low",
            < 500 => "Medium",
            < 2000 => "High",
            _ => "Major"
        };
    }

    // INotifyPropertyChanged implementation for Syncfusion data binding
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
