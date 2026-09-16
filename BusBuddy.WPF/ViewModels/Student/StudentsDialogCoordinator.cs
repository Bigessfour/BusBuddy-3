using System.Windows;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using StudentModel = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// What the students grid should do after a modal form closes. The coordinator never touches
/// view-model state directly; <see cref="StudentsViewModel"/> applies the outcome.
/// </summary>
public readonly record struct StudentsDialogOutcome(
    string? StatusMessage = null,
    bool ReloadStudents = false,
    bool ReloadReferenceData = false,
    bool SchoolCatalogChanged = false,
    bool PickupStopCatalogChanged = false,
    int? SavedCatalogId = null)
{
    /// <summary>Dialog was cancelled or skipped — leave the grid untouched.</summary>
    public static StudentsDialogOutcome None => default;
}

/// <summary>Opens the modal clerk forms reachable from the students grid.</summary>
public sealed class StudentsDialogCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentsDialogCoordinator>();

    /// <summary>Opens a blank student form.</summary>
    public StudentsDialogOutcome AddStudent()
    {
        try
        {
            Logger.Information("Add student command executed");

            var studentForm = new BusBuddy.WPF.Views.Student.StudentForm();
            DialogOwner.Assign(studentForm);

            return studentForm.ShowDialog() == true
                ? new StudentsDialogOutcome("Student added successfully", ReloadStudents: true)
                : StudentsDialogOutcome.None;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing add student command");
            return new StudentsDialogOutcome($"Error adding student: {ex.Message}");
        }
    }

    /// <summary>Opens the school destination form so the clerk can add an assignable school.</summary>
    public StudentsDialogOutcome AddSchool()
    {
        try
        {
            var dest = App.ServiceProvider?.GetService<IDestinationService>();
            if (dest is null)
            {
                Logger.Warning("Add school skipped: IDestinationService not registered");
                return new StudentsDialogOutcome("Destination service is not available.");
            }

            var vm = new SchoolDestinationFormViewModel(dest);
            var form = new BusBuddy.WPF.Views.Student.SchoolDestinationForm(vm);
            DialogOwner.Assign(form);
            if (form.ShowDialog() != true)
            {
                return StudentsDialogOutcome.None;
            }

            var status = vm.SavedWithGps
                ? "School saved. Assign it on the student form, then Generate Routes."
                : "School saved without GPS. Generate Routes will not persist stop times until coordinates are set.";
            return new StudentsDialogOutcome(
                status,
                ReloadReferenceData: true,
                SchoolCatalogChanged: true,
                SavedCatalogId: vm.SavedDestinationId);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing add school command");
            return new StudentsDialogOutcome($"Error adding school: {ex.Message}");
        }
    }

    /// <summary>Picks a cataloged school and opens the full campus editor.</summary>
    public async Task<StudentsDialogOutcome> EditSchoolAsync()
    {
        try
        {
            var dest = App.ServiceProvider?.GetService<IDestinationService>();
            if (dest is null)
            {
                return new StudentsDialogOutcome("Destination service is not available.");
            }

            var school = await PickSchoolAsync(dest, "Select the school to edit.").ConfigureAwait(true);
            if (school is null)
            {
                return StudentsDialogOutcome.None;
            }

            using var vm = SchoolDestinationFormViewModel.ForEdit(dest, school);
            var form = new BusBuddy.WPF.Views.Student.SchoolDestinationForm(vm);
            DialogOwner.Assign(form);
            if (form.ShowDialog() != true)
            {
                return StudentsDialogOutcome.None;
            }

            var status = string.IsNullOrWhiteSpace(vm.StatusMessage)
                ? $"Saved {school.Name}"
                : vm.StatusMessage;
            return new StudentsDialogOutcome(
                status,
                ReloadReferenceData: true,
                SchoolCatalogChanged: true,
                SavedCatalogId: vm.SavedDestinationId);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing edit school command");
            return new StudentsDialogOutcome($"Error editing school: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes an unused school, or retires it when students or trips still reference it.
    /// </summary>
    public async Task<StudentsDialogOutcome> DeleteSchoolAsync()
    {
        try
        {
            var dest = App.ServiceProvider?.GetService<IDestinationService>();
            if (dest is null)
            {
                return new StudentsDialogOutcome("Destination service is not available.");
            }

            var school = await PickSchoolAsync(dest, "Select the school to delete or retire.").ConfigureAwait(true);
            if (school is null)
            {
                return StudentsDialogOutcome.None;
            }

            var confirm = MessageBox.Show(
                $"{school.Name} will be removed from the catalog if nothing uses it. " +
                "If students, transfers, activities, or trips still point here, the campus is retired instead of deleted.",
                "Delete school?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return StudentsDialogOutcome.None;
            }

            var result = await dest.DeleteSchoolAsync(school.DestinationId).ConfigureAwait(true);
            return result switch
            {
                SchoolDeleteResult.Deleted => new StudentsDialogOutcome(
                    $"{school.Name} deleted.",
                    ReloadReferenceData: true,
                    SchoolCatalogChanged: true,
                    SavedCatalogId: school.DestinationId),
                SchoolDeleteResult.Retired => new StudentsDialogOutcome(
                    $"{school.Name} is still in use, so it was retired (hidden from new assignments).",
                    ReloadReferenceData: true,
                    SchoolCatalogChanged: true,
                    SavedCatalogId: school.DestinationId),
                _ => new StudentsDialogOutcome("School was not found.")
            };
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing delete school command");
            return new StudentsDialogOutcome($"Error deleting school: {ex.Message}");
        }
    }

    private static async Task<Destination?> PickSchoolAsync(IDestinationService dest, string prompt)
    {
        var schools = await dest.GetActiveSchoolsAsync().ConfigureAwait(true);
        if (schools.Count == 0)
        {
            MessageBox.Show(
                "Add a school first.",
                "No schools",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        if (schools.Count == 1)
        {
            return schools[0];
        }

        var vm = new SchoolCatalogPickerDialogViewModel(schools, prompt);
        var picker = new BusBuddy.WPF.Views.Student.SchoolCatalogPickerDialog(vm);
        DialogOwner.Assign(picker);
        return picker.ShowDialog() == true ? vm.SelectedSchool : null;
    }

    /// <summary>Opens the catalog pickup stop form (in-town shared boarding point).</summary>
    public StudentsDialogOutcome AddPickupStop()
    {
        try
        {
            var stopService = App.ServiceProvider?.GetService<IPickupStopService>();
            if (stopService is null)
            {
                Logger.Warning("Add pickup stop skipped: IPickupStopService not registered");
                return new StudentsDialogOutcome("Pickup stop service is not available.");
            }

            var vm = new PickupStopFormViewModel(stopService);
            var form = new BusBuddy.WPF.Views.Student.PickupStopForm(vm);
            DialogOwner.Assign(form);
            if (form.ShowDialog() != true)
            {
                return StudentsDialogOutcome.None;
            }

            return new StudentsDialogOutcome(
                $"Pickup stop saved (Id={vm.SavedPickupStopId}). Assign it on the student form.",
                PickupStopCatalogChanged: true,
                SavedCatalogId: vm.SavedPickupStopId);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing add pickup stop command");
            return new StudentsDialogOutcome($"Error adding pickup stop: {ex.Message}");
        }
    }

    /// <summary>Opens the student form bound to the selected row.</summary>
    public StudentsDialogOutcome EditStudent(StudentModel? student)
    {
        try
        {
            if (student is null)
            {
                return StudentsDialogOutcome.None;
            }

            Logger.Information("Edit student command executed for student {StudentId}", student.StudentId);

            var studentForm = new BusBuddy.WPF.Views.Student.StudentForm(student);
            DialogOwner.Assign(studentForm);

            return studentForm.ShowDialog() == true
                ? new StudentsDialogOutcome("Student updated successfully", ReloadStudents: true)
                : StudentsDialogOutcome.None;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error executing edit student command");
            return new StudentsDialogOutcome($"Error editing student: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the school-to-school transfer dialog. specs/students.md treats a transfer as a second
    /// assignment on a transfer route, not a second student.
    /// </summary>
    public StudentsDialogOutcome SchoolTransfer(StudentModel? student)
    {
        try
        {
            if (student is null)
            {
                return StudentsDialogOutcome.None;
            }

            var sp = App.ServiceProvider;
            var transferService = sp?.GetService<IStudentSchoolTransferService>();
            var destinationService = sp?.GetService<IDestinationService>();
            if (transferService is null || destinationService is null)
            {
                Logger.Warning("School transfer skipped — services not registered");
                return new StudentsDialogOutcome("Transfer services unavailable");
            }

            var vm = new StudentSchoolTransferViewModel(
                student.StudentId,
                student.StudentName ?? $"Student {student.StudentId}",
                transferService,
                destinationService);
            var dialog = new BusBuddy.WPF.Views.Student.StudentSchoolTransferForm(vm);
            DialogOwner.Assign(dialog);

            return dialog.ShowDialog() == true
                ? new StudentsDialogOutcome($"School transfer saved for {student.StudentName}", ReloadStudents: true)
                : StudentsDialogOutcome.None;
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error opening school transfer");
            return new StudentsDialogOutcome($"Transfer error: {ex.Message}");
        }
    }
}
