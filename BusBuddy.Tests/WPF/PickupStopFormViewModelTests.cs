using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class PickupStopFormViewModelTests
{
    private sealed class FakePickupStops : IPickupStopService
    {
        public PickupStop? LastUpdated;
        public int? RetiredId;
        public PickupStop Catalog { get; } = new()
        {
            PickupStopId = 7,
            Name = "Oak & 4th",
            Address = "NE",
            Latitude = 38.1m,
            Longitude = -102.6m,
            StopType = PickupStopTypes.Corner,
            Notes = "old",
            Active = true
        };

        public Task<IReadOnlyList<PickupStop>> GetActiveStopsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PickupStop>>(new[] { Catalog });

        public Task<PickupStop?> GetByIdAsync(int pickupStopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PickupStop?>(pickupStopId == Catalog.PickupStopId ? Catalog : null);

        public Task<PickupStop> AddStopAsync(
            string name,
            string? address,
            decimal latitude,
            decimal longitude,
            string stopType = PickupStopTypes.Corner,
            string? notes = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PickupStop
            {
                PickupStopId = 3,
                Name = name,
                Address = address,
                Latitude = latitude,
                Longitude = longitude,
                StopType = stopType,
                Notes = notes,
                Active = true
            });

        public Task<PickupStop> UpdateStopAsync(
            int pickupStopId,
            string name,
            string? address,
            decimal latitude,
            decimal longitude,
            string stopType = PickupStopTypes.Corner,
            string? notes = null,
            CancellationToken cancellationToken = default)
        {
            LastUpdated = new PickupStop
            {
                PickupStopId = pickupStopId,
                Name = name,
                Address = address,
                Latitude = latitude,
                Longitude = longitude,
                StopType = stopType,
                Notes = notes
            };
            return Task.FromResult(LastUpdated);
        }

        public Task<CatalogDeleteResult> RetireStopAsync(
            int pickupStopId,
            CancellationToken cancellationToken = default)
        {
            RetiredId = pickupStopId;
            return Task.FromResult(CatalogDeleteResult.Retired);
        }

        public Task<PickupStop?> FindNearestAsync(
            double latitude,
            double longitude,
            double maxMeters = 400,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PickupStop?>(null);
    }

    [Test]
    public async Task ForEdit_Save_CallsUpdateNotAdd()
    {
        var fake = new FakePickupStops();
        var vm = PickupStopFormViewModel.ForEdit(fake, fake.Catalog);
        Assert.That(vm.IsEditing, Is.True);
        Assert.That(vm.Title, Does.Contain("Edit"));

        vm.Name = "Oak & 5th";
        await ((IAsyncRelayCommand)vm.SaveCommand).ExecuteAsync(null);

        Assert.That(fake.LastUpdated, Is.Not.Null);
        Assert.That(fake.LastUpdated!.PickupStopId, Is.EqualTo(7));
        Assert.That(fake.LastUpdated.Name, Is.EqualTo("Oak & 5th"));
        Assert.That(vm.SavedPickupStopId, Is.EqualTo(7));
        Assert.That(fake.RetiredId, Is.Null);
    }

    [Test]
    public void AddMode_SaveLabel_IsCreateCopy()
    {
        var vm = new PickupStopFormViewModel(new FakePickupStops());
        Assert.That(vm.IsEditing, Is.False);
        Assert.That(vm.SaveButtonLabel, Does.Contain("Save pickup stop"));
    }
}
