using System.Windows.Media;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.Core.Utilities;
using BusBuddy.WPF.Messages;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Sends the clerk from the student form to the school destination form to correct the selected
/// campus's bell times. Editing a school from inside a student form is the workflow being preserved;
/// owning school fields on the student form is what is not — <see cref="SchoolDestinationFormViewModel"/>
/// is the one place a campus is edited.
/// </summary>
public sealed class StudentFormSchoolTimesLauncher
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormSchoolTimesLauncher>();

    private readonly Func<Destination?> _selectedSchool;
    private readonly StudentFormValidationCoordinator _validation;

    public StudentFormSchoolTimesLauncher(
        Func<Destination?> selectedSchool,
        StudentFormValidationCoordinator validation)
    {
        _selectedSchool = selectedSchool;
        _validation = validation;
    }

    public void OpenForSelectedSchool()
    {
        var school = _selectedSchool();
        if (school is null)
        {
            _validation.SetStatus("Select a school first", Brushes.Orange);
            return;
        }

        var destinations = App.ServiceProvider?.GetService<IDestinationService>();
        if (destinations is null)
        {
            Logger.Warning("Edit school times skipped: IDestinationService not registered");
            _validation.SetStatus("Destination service unavailable", Brushes.Orange);
            return;
        }

        try
        {
            using var vm = SchoolDestinationFormViewModel.ForSchoolTimes(destinations, school);
            var form = new BusBuddy.WPF.Views.Student.SchoolDestinationForm(vm);
            DialogOwner.Assign(form);
            if (form.ShowDialog() != true)
            {
                return;
            }

            _validation.SetStatus(
                string.IsNullOrWhiteSpace(vm.StatusMessage) ? "School times saved" : vm.StatusMessage,
                Brushes.Green);

            // Any other open student form is holding the same stale Destination instances.
            WeakReferenceMessenger.Default.Send(new SchoolCatalogChangedMessage(vm.SavedDestinationId));
            Logger.Information("School times updated DestinationId={DestinationId}", school.DestinationId);
        }
        catch (Exception ex)
        {
            DatabaseUserMessage.LogFailure(Logger, ex, "Error opening school times editor");
            _validation.SetGlobalError(DatabaseUserMessage.ForOperation(ex, "edit the school times"));
        }
    }
}
