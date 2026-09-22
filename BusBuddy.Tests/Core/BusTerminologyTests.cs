using BusBuddy.Tests.WPF;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class BusTerminologyTests
{
    [Test]
    public void PersistLayer_IsBusNotVehicle()
    {
        Assert.That(CoreSourceFile.Exists("Models/Bus.cs"), Is.True);
        Assert.That(CoreSourceFile.Exists("Models/Vehicle.cs"), Is.False);
        Assert.That(CoreSourceFile.Exists("Data/Repositories/VehicleRepository.cs"), Is.False);
        Assert.That(CoreSourceFile.Exists("Data/Repositories/BusRepository.cs"), Is.False);
        Assert.That(CoreSourceFile.Exists("Data/UnitOfWork/UnitOfWork.cs"), Is.False);
        Assert.That(CoreSourceFile.Exists("Data/Interfaces/IBusRepository.cs"), Is.False);
        var bus = CoreSourceFile.Read("Services/BusService.cs");
        Assert.That(bus, Does.Not.Contain("IVehicleRepository"));
        Assert.That(bus, Does.Not.Contain("GetActiveVehiclesAsync"));
        var di = CoreSourceFile.Read("Extensions/ServiceCollectionExtensions.cs");
        Assert.That(di, Does.Not.Contain("IVehicleRepository"));
        Assert.That(di, Does.Not.Contain("IUnitOfWork"));
        Assert.That(di, Does.Contain("AddScoped<IFamilyService>"));
        Assert.That(di, Does.Contain("AddScoped<IGuardianService>"));
        Assert.That(CoreSourceFile.Exists("Models/BaseEntity.cs"), Is.False);
        Assert.That(CoreSourceFile.Exists("Models/Base/BaseEntity.cs"), Is.False);
    }

    [Test]
    public void BusViewModel_LivesInViewModelsBus()
    {
        Assert.That(XamlViewFile.Exists("ViewModels/Bus/BusViewModel.cs"), Is.True);
        Assert.That(XamlViewFile.Exists("Models/BusViewModel.cs"), Is.False);
        var map = XamlViewFile.Read("Mapping/MappingProfile.cs");
        Assert.That(map, Does.Contain("BusBuddy.WPF.ViewModels.Bus.BusViewModel"));
    }
}
