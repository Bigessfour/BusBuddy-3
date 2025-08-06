using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBuddy.Core.Models.Base;

/// <summary>
/// Base entity class with audit fields and common properties
/// Supports agile schema evolution with audit tracking and soft deletes
/// Implements INotifyPropertyChanged for Syncfusion data binding
/// </summary>
public abstract class BaseEntity : INotifyPropertyChanged
{
    private DateTime _createdDate = DateTime.UtcNow;
    private DateTime? _updatedDate;
    private string? _createdBy;
    private string? _updatedBy;
    private bool _isDeleted;
    private string? _customFields;

    /// <summary>
    /// Primary key - override in derived classes if needed
    /// </summary>
    [Key]
    public virtual int Id { get; set; }

    /// <summary>
    /// Audit field: When the record was created
    /// </summary>
    [Required]
    [Display(Name = "Created Date")]
    public DateTime CreatedDate
    {
        get => _createdDate;
        set
        {
            if (_createdDate != value)
            {
                _createdDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Audit field: When the record was last updated
    /// </summary>
    [Display(Name = "Updated Date")]
    public DateTime? UpdatedDate
    {
        get => _updatedDate;
        set
        {
            if (_updatedDate != value)
            {
                _updatedDate = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Audit field: Who created the record
    /// </summary>
    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy
    {
        get => _createdBy;
        set
        {
            if (_createdBy != value)
            {
                _createdBy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Audit field: Who last updated the record
    /// </summary>
    [StringLength(100)]
    [Display(Name = "Updated By")]
    public string? UpdatedBy
    {
        get => _updatedBy;
        set
        {
            if (_updatedBy != value)
            {
                _updatedBy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Soft delete flag - records are marked as deleted instead of being physically removed
    /// </summary>
    [Display(Name = "Is Deleted")]
    public bool IsDeleted
    {
        get => _isDeleted;
        set
        {
            if (_isDeleted != value)
            {
                _isDeleted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// JSON column for storing custom/dynamic fields to support schema evolution
    /// Allows adding fields without database schema changes
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    [Display(Name = "Custom Fields")]
    public string? CustomFields
    {
        get => _customFields;
        set
        {
            if (_customFields != value)
            {
                _customFields = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Computed property: Opposite of IsDeleted for UI convenience
    /// </summary>
    [NotMapped]
    [Display(Name = "Is Active")]
    public bool IsActive
    {
        get => !IsDeleted;
        set => IsDeleted = !value;
    }

    /// <summary>
    /// Computed property: Version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    [Display(Name = "Row Version")]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Computed property: Days since creation
    /// </summary>
    [NotMapped]
    [Display(Name = "Days Old")]
    public int DaysOld => (DateTime.UtcNow - CreatedDate).Days;

    /// <summary>
    /// Computed property: Days since last update
    /// </summary>
    [NotMapped]
    [Display(Name = "Days Since Update")]
    public int? DaysSinceUpdate => UpdatedDate.HasValue ? (DateTime.UtcNow - UpdatedDate.Value).Days : null;

    /// <summary>
    /// Computed property: Display text for audit information
    /// </summary>
    [NotMapped]
    [Display(Name = "Audit Info")]
    public string AuditInfo
    {
        get
        {
            var created = $"Created {CreatedDate:MM/dd/yyyy}";
            if (!string.IsNullOrEmpty(CreatedBy))
            {
                created += $" by {CreatedBy}";
            }


            if (UpdatedDate.HasValue)
            {
                var updated = $", Updated {UpdatedDate.Value:MM/dd/yyyy}";
                if (!string.IsNullOrEmpty(UpdatedBy))
                {
                    updated += $" by {UpdatedBy}";
                }


                created += updated;
            }

            return created;
        }
    }

    /// <summary>
    /// Extension point for derived classes to add custom validation
    /// </summary>
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield break; // Default: no validation errors
    }

    /// <summary>
    /// Extension point for derived classes to perform pre-save operations
    /// </summary>
    public virtual void OnSaving()
    {
        if (UpdatedDate == null || UpdatedDate == DateTime.MinValue)
        {
            UpdatedDate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// INotifyPropertyChanged implementation for Syncfusion data binding
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
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
