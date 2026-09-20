using BusBuddy.Core.Services;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class StudentPickupHintTests
{
    [Test]
    public void NearbyCatalog_AsksClerkToSelect()
    {
        var text = StudentPickupHint.NearbyCatalog("Oak & 4th", 400);
        Assert.That(text, Does.Contain("Oak & 4th"));
        Assert.That(text, Does.Contain("Select it"));
        Assert.That(text, Does.Not.Contain("Assigned"));
    }

    [Test]
    public void HomeWithCluster_DoesNotCreateAStop()
    {
        var text = StudentPickupHint.HomeWithCluster(2, 400);
        Assert.That(text, Does.Contain("using home pickup"));
        Assert.That(text, Does.Contain("2 other home pickup"));
        Assert.That(text, Does.Contain("Add a catalog stop"));
    }

    [Test]
    public void ShouldHintCatalogCluster_RequiresTwoHomes()
    {
        Assert.That(StudentPickupHint.ShouldHintCatalogCluster(0, 2), Is.False);
        Assert.That(StudentPickupHint.ShouldHintCatalogCluster(1, 2), Is.True);
        Assert.That(StudentPickupHint.ShouldHintCatalogCluster(1, 3), Is.False);
        Assert.That(StudentPickupHint.ShouldHintCatalogCluster(2, 3), Is.True);
    }

    [Test]
    public void NewCatalogNearbyHomes_AsksClerkToAssignNotAutoAttach()
    {
        Assert.That(StudentPickupHint.NewCatalogNearbyHomes(0, 400), Is.Empty);
        var text = StudentPickupHint.NewCatalogNearbyHomes(2, 400);
        Assert.That(text, Does.Contain("2 home pickup"));
        Assert.That(text, Does.Contain("Assign those students"));
        Assert.That(text, Does.Not.Contain("Assigned"));
    }

    [Test]
    public void NoPublishedCatalog_TellsClerkToAddAStop()
    {
        var text = StudentPickupHint.NoPublishedCatalog(400);
        Assert.That(text, Does.Contain("No published catalog stops"));
        Assert.That(text, Does.Contain("Add a catalog stop"));
    }
}
