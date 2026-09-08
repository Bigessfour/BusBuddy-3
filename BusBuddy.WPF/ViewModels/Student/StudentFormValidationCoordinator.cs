using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Utilities;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Decides whether a student record is good enough to save, and owns the status surface every other
/// form action writes to: the global error banner, the busy flag, and the status line. Per-field
/// error state lives in <see cref="StudentFormFieldErrorTracker"/> and is re-published from here so
/// <see cref="StudentFormViewModel"/> has one thing to listen to.
/// </summary>
public sealed class StudentFormValidationCoordinator : INotifyPropertyChanged
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormValidationCoordinator>();

    private readonly Func<StudentModel> _student;
    private readonly IStudentService? _studentService;
    private readonly StudentFormAddressCoordinator _address;
    private readonly Action _notifyCanSaveChanged;
    private readonly StudentFormFieldErrorTracker _fields = new();

    private bool _hasGlobalError;
    private string _globalErrorMessage = string.Empty;
    private bool _isValidating;
    private string _validationStatus = "Ready";
    private Brush _validationStatusBrush = Brushes.Gray;
    private bool _canSave = true;

    public StudentFormValidationCoordinator(
        Func<StudentModel> student,
        IStudentService? studentService,
        StudentFormAddressCoordinator address,
        Action notifyCanSaveChanged)
    {
        _student = student;
        _studentService = studentService;
        _address = address;
        _notifyCanSaveChanged = notifyCanSaveChanged;
        _fields.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        _fields.RequestFocusField += (_, key) => RequestFocusField?.Invoke(this, key);
        Policy = StudentFormValidationPolicy.FromEnvironment();
        Policy.LogIfNotDefault(Logger);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when validation should move keyboard focus to a named form field.</summary>
    public event EventHandler<string>? RequestFocusField;

    /// <summary>Resolved once per form so a relaxed save path cannot change mid-edit.</summary>
    public StudentFormValidationPolicy Policy { get; }

    public ObservableCollection<string> ValidationErrors => _fields.ValidationErrors;

    public IReadOnlyDictionary<string, string> FieldErrors => _fields.FieldErrors;

    public string? StudentNameFieldError => _fields.StudentNameFieldError;

    public bool HasStudentNameFieldError => _fields.HasStudentNameFieldError;

    public string? GradeFieldError => _fields.GradeFieldError;

    public bool HasGradeFieldError => _fields.HasGradeFieldError;

    public bool HasValidationErrors
    {
        get => _fields.HasValidationErrors;
        set => _fields.HasValidationErrors = value;
    }

    public bool HasGlobalError { get => _hasGlobalError; set => SetProperty(ref _hasGlobalError, value); }

    public string GlobalErrorMessage { get => _globalErrorMessage; set => SetProperty(ref _globalErrorMessage, value); }

    public bool IsValidating { get => _isValidating; set => SetProperty(ref _isValidating, value); }

    public string ValidationStatus { get => _validationStatus; set => SetProperty(ref _validationStatus, value); }

    public Brush ValidationStatusBrush { get => _validationStatusBrush; set => SetProperty(ref _validationStatusBrush, value); }

    public bool CanSave { get => _canSave; set => SetProperty(ref _canSave, value); }

    /// <summary>Status line + colour, written by save, map, import, and route-suggest actions.</summary>
    public void SetStatus(string message, Brush brush)
    {
        ValidationStatus = message;
        ValidationStatusBrush = brush;
    }

    /// <summary>Set a global error message (non-blocking banner; no modal).</summary>
    public void SetGlobalError(string message)
    {
        GlobalErrorMessage = message;
        HasGlobalError = true;
        Logger.Warning("Global error set: {Message}", message);
    }

    /// <summary>Clear the global error message and field highlights.</summary>
    public void ClearGlobalError()
    {
        HasGlobalError = false;
        GlobalErrorMessage = string.Empty;
        _fields.ClearAll();
    }

    public void ClearAllFieldErrors() => _fields.ClearAll();

    /// <summary>Clear one field error when the operator edits that control.</summary>
    public void ClearFieldError(string fieldKey)
    {
        if (_fields.Clear(fieldKey) && HasGlobalError)
        {
            HasGlobalError = false;
            GlobalErrorMessage = string.Empty;
        }
    }

    /// <summary>Publish field-keyed errors, raise the banner, and focus the first offending control.</summary>
    public void ReportFieldValidation(IReadOnlyList<(string FieldKey, string Message)> errors)
    {
        _fields.Report(errors);
        if (errors.Count == 0)
        {
            return;
        }

        GlobalErrorMessage = errors[0].Message;
        HasGlobalError = true;
    }

    /// <summary>
    /// Minimal validation for Save — only ensure required fields are present.
    /// Detailed address checks are available via the Validate actions and should not block Save.
    /// </summary>
    public bool IsValidStudent()
    {
        var student = _student();
        if (string.IsNullOrWhiteSpace(student.StudentName) || string.IsNullOrWhiteSpace(student.Grade))
        {
            return false;
        }

        _address.SetMinimalFieldsPresentMessage();
        return true;
    }

    /// <summary>Build validation errors with field keys for inline highlighting and focus.</summary>
    public List<(string FieldKey, string Message)> GetValidationErrorsWithFields()
    {
        var student = _student();
        var errors = new List<(string FieldKey, string Message)>();
        if (string.IsNullOrWhiteSpace(student.StudentName))
        {
            errors.Add((StudentFormFields.StudentName, "Student name is required."));
        }

        if (string.IsNullOrWhiteSpace(student.Grade))
        {
            errors.Add((StudentFormFields.Grade, "Grade is required."));
        }

        return errors;
    }

    /// <summary>Run service-layer rules before persist so VM and DB stay aligned.</summary>
    public async Task<List<(string FieldKey, string Message)>> ValidateWithServiceAsync()
    {
        if (_studentService is null)
        {
            return [];
        }

        var messages = await _studentService.ValidateStudentAsync(_student()).ConfigureAwait(true);
        return messages.Select(StudentFormFieldErrorMap.ForServiceMessage).ToList();
    }

    /// <summary>Validate all student data including address geocoding (the Validate Data button).</summary>
    public async Task ValidateAllDataAsync()
    {
        try
        {
            Logger.Information("Starting comprehensive data validation");
            SetStatus("Validating all data...", Brushes.Orange);

            var student = _student();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(student.StudentName))
            {
                errors.Add("Student name is required");
            }

            if (string.IsNullOrWhiteSpace(student.Grade))
            {
                errors.Add("Grade is required");
            }

            // Address fields are optional for Save. Do not flag as blocking errors.
            // You can still validate the address via the dedicated Validate Address action.
            ValidationErrors.Clear();
            foreach (var err in errors)
            {
                ValidationErrors.Add("• " + err);
            }

            HasValidationErrors = ValidationErrors.Count > 0;
            if (errors.Count > 0)
            {
                SetGlobalError($"Please review: {string.Join(", ", errors)}");
            }

            if (!_address.DisableValidation && !string.IsNullOrWhiteSpace(student.HomeAddress))
            {
                await _address.ValidateAsync(student).ConfigureAwait(true);
                if (_address.ValidationFailed)
                {
                    errors.Add(_address.ValidationMessage);
                    ValidationErrors.Add("• " + _address.ValidationMessage);
                }
            }

            if (errors.Count > 0 || _address.ValidationFailed)
            {
                HasValidationErrors = true;
                SetStatus($"❌ {errors.Count} validation error(s)", Brushes.Red);
                SetCanSave(false);
                Logger.Warning("Comprehensive validation failed with {ErrorCount} error(s)", errors.Count);
                return;
            }

            SetStatus("✓ All data validated successfully", Brushes.Green);
            HasValidationErrors = false;
            ValidationErrors.Clear();
            SetCanSave(true);
            Logger.Information("Comprehensive data validation completed successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during comprehensive validation");
            SetGlobalError($"Validation failed: {ex.Message}");
            SetStatus("❌ Validation failed", Brushes.Red);
            CanSave = false;
            HasValidationErrors = true;
        }
        finally
        {
            IsValidating = false;
        }
    }

    /// <summary>Update <see cref="CanSave"/> and keep the Save command's CanExecute aligned.</summary>
    public void SetCanSave(bool canSave)
    {
        CanSave = canSave;
        _notifyCanSaveChanged();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
