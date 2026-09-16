using System;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Maintenance;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class MaintenanceViewModelTests
{
    [Test]
    public async Task OverlappingRefresh_DoesNotOverlapGetAllMaintenanceRecordsCalls()
    {
        var inflight = 0;
        var maxInflight = 0;
        var maintenance = new Mock<IMaintenanceService>();
        maintenance.Setup(s => s.GetAllMaintenanceRecordsAsync())
            .Returns(async () =>
            {
                var now = Interlocked.Increment(ref inflight);
                maxInflight = Math.Max(maxInflight, now);
                await Task.Delay(40);
                Interlocked.Decrement(ref inflight);
                return Array.Empty<Maintenance>();
            });

        var bus = new Mock<IBusService>();
        bus.Setup(s => s.GetAllBusesAsync()).ReturnsAsync(Array.Empty<Bus>());

        var vm = new MaintenanceViewModel(maintenance.Object, bus.Object);
        _ = vm.RefreshCommand.ExecuteAsync(null);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && inflight > 0)
        {
            await Task.Delay(20);
        }

        Assert.That(maxInflight, Is.EqualTo(1));
    }
}
