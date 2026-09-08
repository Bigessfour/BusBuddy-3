using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Which student-form fields are currently wrong, and the message for each. Separate from the rules
/// that produce those messages so a control can clear its own error as the clerk retypes without
/// re-running validation. Two fields (name and grade) also get dedicated properties because the XAML
/// highlights them inline.
/// </summary>
public sealed class StudentFormFieldErrorTracker : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> _fieldErrors = new(StringComparer.Ordinal);

    private bool _hasValidationErrors;
    private string? _studentNameFieldError;
    private string? _gradeFieldError;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when validation should move keyboard focus to a named form field.</summary>
    public event EventHandler<string>? RequestFocusField;

    /// <summary>Bulleted messages shown in the "Please fix the following" panel.</summary>
    public ObservableCollection<string> ValidationErrors { get; } = new();

    public IReadOnlyDictionary<string, string> FieldErrors => _fieldErrors;

    public bool HasValidationErrors
    {
        get => _hasValidationErrors;
        set => SetProperty(ref _hasValidationErrors, value);
    }

    public string? StudentNameFieldError
    {
        get => _studentNameFieldError;
        private set
        {
            if (SetProperty(ref _studentNameFieldError, value))
            {
                OnPropertyChanged(nameof(HasStudentNameFieldError));
            }
        }
    }

    public bool HasStudentNameFieldError => !string.IsNullOrWhiteSpace(StudentNameFieldError);

    public string? GradeFieldError
    {
        get => _gradeFieldError;
        private set
        {
            if (SetProperty(ref _gradeFieldError, value))
            {
                OnPropertyChanged(nameof(HasGradeFieldError));
            }
        }
    }

    public bool HasGradeFieldError => !string.IsNullOrWhiteSpace(GradeFieldError);

    /// <summary>Publish field-keyed errors and focus the first offending control.</summary>
    public void Report(IReadOnlyList<(string FieldKey, string Message)> errors)
    {
        ClearAll(force: true);

        foreach (var (fieldKey, message) in errors)
        {
            _fieldErrors[fieldKey] = message;
            ValidationErrors.Add("• " + message);
            ApplyKnownFieldError(fieldKey, message);
        }

        HasValidationErrors = errors.Count > 0;
        OnPropertyChanged(nameof(FieldErrors));

        if (errors.Count > 0)
        {
            RequestFocusField?.Invoke(this, errors[0].FieldKey);
        }
    }

    /// <summary>
    /// Clear one field as the operator edits that control. Returns true once the last error is gone,
    /// so the caller can also drop the global banner.
    /// </summary>
    public bool Clear(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey) || !_fieldErrors.Remove(fieldKey))
        {
            return false;
        }

        ApplyKnownFieldError(fieldKey, null);
        OnPropertyChanged(nameof(FieldErrors));
        if (_fieldErrors.Count > 0)
        {
            return false;
        }

        ValidationErrors.Clear();
        HasValidationErrors = false;
        return true;
    }

    public void ClearAll(bool force = false)
    {
        if (!force && _fieldErrors.Count == 0 && StudentNameFieldError is null && GradeFieldError is null)
        {
            return;
        }

        _fieldErrors.Clear();
        StudentNameFieldError = null;
        GradeFieldError = null;
        OnPropertyChanged(nameof(FieldErrors));
        ValidationErrors.Clear();
        HasValidationErrors = false;
    }

    private void ApplyKnownFieldError(string fieldKey, string? message)
    {
        switch (fieldKey)
        {
            case StudentFormFields.StudentName:
                StudentNameFieldError = message;
                break;
            case StudentFormFields.Grade:
                GradeFieldError = message;
                break;
        }
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
