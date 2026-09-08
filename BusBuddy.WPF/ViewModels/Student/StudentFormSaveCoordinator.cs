using System.Collections.ObjectModel;
using System.Windows.Media;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Runs the student form's save in order: field and service validation, address validation and
/// geocoding, normalization, then one write through <see cref="StudentPersistenceWriter"/>.
/// </summary>
public sealed class StudentFormSaveCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormSaveCoordinator>();

    private readonly Func<StudentModel> _getStudent;
    private readonly Action<StudentModel> _setStudent;
    private readonly BusBuddyDbContext _context;
    private readonly IStudentService? _studentService;
    private readonly StudentFormAddressCoordinator _address;
    private readonly StudentFormValidationCoordinator _validation;
    private readonly StudentPersistenceWriter _writer;
    private readonly Func<bool> _isEditMode;
    private readonly ObservableCollection<Destination> _availableSchools;
    private readonly Action _requestCloseWithSuccess;

    public StudentFormSaveCoordinator(
        Func<StudentModel> getStudent,
        Action<StudentModel> setStudent,
        BusBuddyDbContext context,
        IStudentService? studentService,
        StudentFormAddressCoordinator address,
        StudentFormValidationCoordinator validation,
        Func<bool> isEditMode,
        ObservableCollection<Destination> availableSchools,
        Action requestCloseWithSuccess)
    {
        _getStudent = getStudent;
        _setStudent = setStudent;
        _context = context;
        _studentService = studentService;
        _address = address;
        _validation = validation;
        _isEditMode = isEditMode;
        _availableSchools = availableSchools;
        _requestCloseWithSuccess = requestCloseWithSuccess;
        _writer = new StudentPersistenceWriter(
            context,
            studentService,
            isEditMode,
            () => validation.Policy.BypassesServiceValidation);
    }

    /// <summary>
    /// Save is never gated on this — the button stays clickable and <see cref="SaveAsync"/> reports
    /// what is missing. It only reflects whether the minimum fields are present.
    /// </summary>
    public static bool HasMinimumFields(StudentModel? student) =>
        !string.IsNullOrWhiteSpace(student?.StudentName)
        && !string.IsNullOrWhiteSpace(student?.Grade);

    public async Task SaveAsync()
    {
        var student = _getStudent();
        using (Serilog.Context.LogContext.PushProperty("Operation", "SaveStudent"))
        using (Serilog.Context.LogContext.PushProperty("StudentId", student.StudentId))
        using (Serilog.Context.LogContext.PushProperty("EditMode", _isEditMode()))
        {
            try
            {
                Logger.Information(
                    "Saving student Grade={Grade} DestinationId={DestinationId}",
                    student.Grade,
                    student.DestinationId);
                _validation.ClearAllFieldErrors();

                if (!await PassesValidationAsync(student).ConfigureAwait(true))
                {
                    return;
                }

                await NormalizeInputsAsync(student).ConfigureAwait(true);

                if (!await DatabaseUserMessage.CanConnectAsync(_context).ConfigureAwait(true))
                {
                    _validation.SetGlobalError(DatabaseUserMessage.UnavailableForOperation("save the student"));
                    return;
                }

                await PrepareForPersistenceAsync(student).ConfigureAwait(true);

                var (path, persisted) = await _writer.WriteAsync(student).ConfigureAwait(true);
                _setStudent(persisted);
                Logger.Information(
                    "Successfully saved student StudentId={StudentId} DestinationId={DestinationId} HasCoordinates={HasCoordinates} Persistence={PersistencePath}",
                    persisted.StudentId,
                    persisted.DestinationId,
                    persisted.Latitude.HasValue && persisted.Longitude.HasValue,
                    path);

                StudentPersistenceWriter.BroadcastSaved(persisted);
                _requestCloseWithSuccess();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving student StudentId={StudentId}", student.StudentId);
                var message = DatabaseUserMessage.ForOperation(ex, "save the student");
                if (StudentFormFieldErrorMap.ForPersistenceFailure(message) is { } mapped)
                {
                    _validation.ReportFieldValidation([mapped]);
                }
                else
                {
                    _validation.SetGlobalError(message);
                }
            }
        }
    }

    private async Task<bool> PassesValidationAsync(StudentModel student)
    {
        // Hard guard: prevent saving with blank name (even when the validation bypass flag is set)
        if (string.IsNullOrWhiteSpace(student.StudentName))
        {
            Logger.Warning("Blocked save — StudentName blank");
            _validation.ReportFieldValidation([(StudentFormFields.StudentName, "Student name is required.")]);
            return false;
        }

        if (_validation.Policy.SkipFieldValidation)
        {
            _validation.ValidationErrors.Clear();
            _validation.HasValidationErrors = false;
            Logger.Warning(
                "Bypassing student field validation due to {Variable}",
                StudentFormValidationPolicy.SkipFieldValidationVariable);
            return true;
        }

        if (!_validation.IsValidStudent())
        {
            var errors = _validation.GetValidationErrorsWithFields();
            Logger.Information("Validation failed on {ErrorCount} field(s)", errors.Count);
            _validation.ReportFieldValidation(errors);
            return false;
        }

        if (_studentService is null)
        {
            return true;
        }

        var serviceErrors = await _validation.ValidateWithServiceAsync().ConfigureAwait(true);
        if (serviceErrors.Count == 0)
        {
            return true;
        }

        Logger.Information("Service validation failed on {ErrorCount} field(s)", serviceErrors.Count);
        _validation.ReportFieldValidation(serviceErrors);
        return false;
    }

    private async Task NormalizeInputsAsync(StudentModel student)
    {
        // specs/students.md: "MUST validate home and stop addresses with Google Address Validation and
        // persist lat/lng from geocoding", and "Unvalidated addresses show as incomplete." So this runs
        // on add AND edit, and on the bypass path too. A failure is never fatal — the record saves and
        // reports as incomplete — but it is never silent.
        if (!string.IsNullOrWhiteSpace(student.HomeAddress))
        {
            await _address.ValidateAsync(student).ConfigureAwait(true);
        }

        // Normalize loose inputs (format but don't block)
        student.HomePhone = StudentPhoneNormalizer.Normalize(student.HomePhone);
        student.CellPhone = StudentPhoneNormalizer.Normalize(student.CellPhone);
        student.EmergencyPhone = StudentPhoneNormalizer.Normalize(student.EmergencyPhone);
        student.Zip = NormalizeZip(student.Zip);
        StudentSpecialNeedsHelper.SyncLegacySpecialNeedsText(student);
    }

    private async Task PrepareForPersistenceAsync(StudentModel student)
    {
        // Audit fields (UTC for Postgres timestamptz)
        if (_isEditMode())
        {
            student.UpdatedDate = DateTime.UtcNow;
            student.UpdatedBy = Environment.UserName;
        }
        else
        {
            student.CreatedDate = DateTime.UtcNow;
            student.CreatedBy = Environment.UserName;
        }

        StudentSchoolLinker.SyncDestinationFromSchoolName(student, _availableSchools.ToList());
        StudentRecordNormalizer.NormalizeForPersistence(student);

        // TryGeocodeForSaveAsync drops coordinates whose address text changed since they were
        // captured (a failed re-validation must not leave the old pin behind), then geocodes.
        if (!string.IsNullOrWhiteSpace(student.HomeAddress)
            && await _address.TryGeocodeForSaveAsync(student).ConfigureAwait(true))
        {
            Logger.Information("Student address geocoded / confirmed before save");
        }

        // Never carry a stale pin: coordinates only exist when validation/geocoding produced them,
        // which is what keeps an unvalidated student off the map.
        if (!student.HasValidatedHomeCoordinates)
        {
            student.Latitude = null;
            student.Longitude = null;
        }

        ReportIncompleteIntake(student);
    }

    private void ReportIncompleteIntake(StudentModel student)
    {
        if (student.HasUnvalidatedHomeAddress)
        {
            _validation.SetStatus(
                "Saved as incomplete — address not validated, so this student will not appear on the map.",
                Brushes.Orange);
            Logger.Warning(
                "Saving student with an unvalidated home address StudentId={StudentId} IntakeStatus={IntakeStatus}",
                student.StudentId,
                student.IntakeStatus);
        }
        else if (student.IsIntakeIncomplete)
        {
            _validation.SetStatus($"Saved as incomplete — {student.IntakeStatus}.", Brushes.Orange);
            Logger.Warning(
                "Saving student with incomplete intake StudentId={StudentId} IntakeStatus={IntakeStatus}",
                student.StudentId,
                student.IntakeStatus);
        }
    }

    private static string? NormalizeZip(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length >= 5 ? digits[..5] : digits;
    }
}
