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
public class FuelDialogViewModelTests
{
    [Test]
    public async Task DialogOpen_LoadsBusesThenLocations_NotOverlapping()
    {
        var busesInflight = 0;
        var locationsInflight = 0;
        var overlapped = 0;

        var bus = new Mock<IBusService>();
        bus.Setup(s => s.GetAllBusesAsync())
            .Returns(async () =>
            {
                Interlocked.Increment(ref busesInflight);
                if (Volatile.Read(ref locationsInflight) > 0)
                {
                    Interlocked.Exchange(ref overlapped, 1);
                }

                await Task.Delay(40);
                Interlocked.Decrement(ref busesInflight);
                return Array.Empty<Bus>();
            });

        var catalog = new Mock<IFuelLocationCatalog>();
        catalog.Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref locationsInflight);
                if (Volatile.Read(ref busesInflight) > 0)
                {
                    Interlocked.Exchange(ref overlapped, 1);
                }

                await Task.Delay(40);
                Interlocked.Decrement(ref locationsInflight);
                return Array.Empty<string>();
            });

        var fuel = new Fuel { FuelDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc) };
        var vm = new FuelDialogViewModel(fuel, bus.Object, catalog.Object);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && (busesInflight > 0 || locationsInflight > 0))
        {
            await Task.Delay(20);
        }

        Assert.That(overlapped, Is.EqualTo(0));
        bus.Verify(s => s.GetAllBusesAsync(), Times.Once);
        catalog.Verify(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
