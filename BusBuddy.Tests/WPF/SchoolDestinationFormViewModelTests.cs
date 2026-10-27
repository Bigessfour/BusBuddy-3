using System;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
public class SchoolDestinationFormViewModelTests
{
    [Test]
    public void FormatTimeText_UsesTwentyFourHourClock()
    {
        var value = new DateTime(2026, 9, 20, 15, 30, 0);
        Assert.That(SchoolDestinationFormViewModel.FormatTimeText(value, "08:00"), Is.EqualTo("15:30"));
        Assert.That(SchoolDestinationFormViewModel.FormatTimeText(null, "08:00"), Is.EqualTo("08:00"));
    }

    [Test]
    public void ParseTimePickerValue_ParsesStoredBellTimes()
    {
        var start = SchoolDestinationFormViewModel.ParseTimePickerValue("07:45", new TimeSpan(8, 0, 0));
        Assert.That(start, Is.Not.Null);
        Assert.That(start!.Value.TimeOfDay, Is.EqualTo(new TimeSpan(7, 45, 0)));
        Assert.That(SchoolDestinationFormViewModel.ParseTimePickerValue("  ", new TimeSpan(8, 0, 0)), Is.Null);
        Assert.That(SchoolDestinationFormViewModel.FormatTimeText(null, string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task Save_BlankBells_StoresNoTimes()
    {
        var destinations = new RecordingDestinations();
        var vm = ReadyToSave(new SchoolDestinationFormViewModel(destinations));

        await SaveAsync(vm);

        Assert.That(vm.ValidationMessage, Is.Empty);
        Assert.That(vm.SavedDestinationId, Is.EqualTo(9));
        Assert.That(destinations.AddCalls, Is.EqualTo(1));
        Assert.That(destinations.Start, Is.Null);
        Assert.That(destinations.Dismissal, Is.Null);
    }

    [Test]
    public async Task Save_EqualBells_IsBlocked()
    {
        var destinations = new RecordingDestinations();
        var vm = ReadyToSave(new SchoolDestinationFormViewModel(destinations));
        vm.StartTimeText = "14:16";
        vm.DismissalTimeText = "14:16";

        await SaveAsync(vm);

        Assert.That(vm.ValidationMessage, Does.Contain("after start"));
        Assert.That(destinations.AddCalls, Is.EqualTo(0));
        Assert.That(vm.SavedDestinationId, Is.Null);
    }

    [Test]
    public async Task Save_DismissalAfterStart_StoresBothBells()
    {
        var destinations = new RecordingDestinations();
        var vm = ReadyToSave(new SchoolDestinationFormViewModel(destinations));
        vm.StartTimeText = "07:30";
        vm.DismissalTimeText = "15:30";

        await SaveAsync(vm);

        Assert.That(vm.ValidationMessage, Is.Empty);
        Assert.That(destinations.Start, Is.EqualTo(new TimeSpan(7, 30, 0)));
        Assert.That(destinations.Dismissal, Is.EqualTo(new TimeSpan(15, 30, 0)));
    }

    [Test]
    public async Task Edit_ExistingBells_CanBeSavedAgain()
    {
        var destinations = new RecordingDestinations();
        var school = new Destination
        {
            DestinationId = 4,
            Name = "TEST_SCHOOL_EXISTING",
            Address = "500 South 5th Street",
            City = "Lamar",
            State = "CO",
            ZipCode = "81052",
            DestinationType = DestinationTypes.School,
            StartTime = new TimeSpan(8, 0, 0),
            DismissalTime = new TimeSpan(15, 30, 0),
            Latitude = 38.0844m,
            Longitude = -102.6207m,
            IsActive = true
        };
        var vm = SchoolDestinationFormViewModel.ForEdit(destinations, school);

        await SaveAsync(vm);

        Assert.That(vm.ValidationMessage, Is.Empty);
        Assert.That(destinations.UpdateCalls, Is.EqualTo(1));
        Assert.That(destinations.Start, Is.EqualTo(school.StartTime));
        Assert.That(destinations.Dismissal, Is.EqualTo(school.DismissalTime));
    }

    [Test]
    public void SchoolBells_AllowABlankPicker()
    {
        var xaml = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml");
        Assert.That(xaml, Does.Contain("AllowNull=\"True\""));
        Assert.That(xaml, Does.Contain("SetValueOnLostFocus=\"False\""));
        var code = XamlViewFile.Read("Views/Student/SchoolDestinationForm.xaml.cs");
        Assert.That(code, Does.Contain("AllowNull = true"));
        Assert.That(code, Does.Contain("SetValueOnLostFocus = false"));
    }

    private static SchoolDestinationFormViewModel ReadyToSave(SchoolDestinationFormViewModel vm)
    {
        vm.Name = "TEST_SCHOOL_BLANK_BELLS";
        vm.Address = "500 South 5th Street";
        vm.City = "Lamar";
        vm.State = "CO";
        vm.ZipCode = "81052";
        vm.ApplyMapClick(38.0844, -102.6207);
        return vm;
    }

    private static Task SaveAsync(SchoolDestinationFormViewModel vm) =>
        ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

    private sealed class RecordingDestinations : IDestinationService
    {
        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public TimeSpan? Start { get; private set; }
        public TimeSpan? Dismissal { get; private set; }

        public Task<Destination> AddSchoolAsync(
            string name,
            string address,
            string city,
            string state,
            string zipCode,
            TimeSpan? startTime,
            TimeSpan? dismissalTime,
            decimal? latitude = null,
            decimal? longitude = null,
            CancellationToken cancellationToken = default)
        {
            AddCalls++;
            Start = startTime;
            Dismissal = dismissalTime;
            return Task.FromResult(new Destination
            {
                DestinationId = 9,
                Name = name,
                StartTime = startTime,
                DismissalTime = dismissalTime,
                Latitude = latitude,
                Longitude = longitude
            });
        }

        public Task<Destination> UpdateSchoolAsync(
            int destinationId,
            string name,
            string address,
            string city,
            string state,
            string zipCode,
            TimeSpan? startTime,
            TimeSpan? dismissalTime,
            decimal? latitude = null,
            decimal? longitude = null,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            Start = startTime;
            Dismissal = dismissalTime;
            return Task.FromResult(new Destination
            {
                DestinationId = destinationId,
                Name = name,
                StartTime = startTime,
                DismissalTime = dismissalTime
            });
        }

        public Task EnsureDefaultSchoolsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Destination>> GetActiveSchoolsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Destination>> GetRosterSchoolsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Destination>> GetActiveDestinationsAsync(
            string? destinationType = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Destination?> GetByIdAsync(int destinationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> UpdateSchoolTimesAsync(
            int destinationId,
            TimeSpan? startTime,
            TimeSpan? dismissalTime,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SchoolDeleteResult> DeleteSchoolAsync(int destinationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Destination?> FindValidatedPlaceByNameAsync(
            string? name,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Destination> AddTripDestinationAsync(
            string name,
            string address,
            string city,
            string state,
            string zipCode,
            decimal latitude,
            decimal longitude,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
