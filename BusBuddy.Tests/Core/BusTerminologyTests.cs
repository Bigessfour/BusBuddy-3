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
        var repo = CoreSourceFile.Read("Data/Interfaces/IBusRepository.cs");
        Assert.That(repo, Does.Contain("GetActiveBusesAsync"));
        Assert.That(repo, Does.Not.Contain("GetActiveVehiclesAsync"));
        Assert.That(repo, Does.Not.Contain("IVehicleRepository"));
        var di = CoreSourceFile.Read("Extensions/ServiceCollectionExtensions.cs");
        Assert.That(di, Does.Not.Contain("IVehicleRepository"));
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
