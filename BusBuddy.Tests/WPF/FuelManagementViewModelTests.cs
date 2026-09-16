using System;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Fuel;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class FuelManagementViewModelTests
{
    [Test]
    public async Task OverlappingReloads_DoNotOverlapGetAllFuelRecordsCalls()
    {
        var inflight = 0;
        var maxInflight = 0;
        var fuel = new Mock<IFuelService>();
        fuel.Setup(s => s.GetAllFuelRecordsAsync())
            .Returns(async () =>
            {
                var now = Interlocked.Increment(ref inflight);
                maxInflight = Math.Max(maxInflight, now);
                await Task.Delay(40);
                Interlocked.Decrement(ref inflight);
                return Array.Empty<Fuel>();
            });

        var vm = new FuelManagementViewModel(fuel.Object, new Mock<IBusService>().Object);
        _ = vm.ReloadFuelRecordsAsync();

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && inflight > 0)
        {
            await Task.Delay(20);
        }

        Assert.That(maxInflight, Is.EqualTo(1));
        Assert.That(vm.IsLoading, Is.False);
    }
}
