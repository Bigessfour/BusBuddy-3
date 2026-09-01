using BusBuddy.Core.Models;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[NUnit.Framework.Category("Unit")]
public class DriverNameNotificationTests
{
    [Test]
    public void FirstName_RaisesPropertyChanged()
    {
        var driver = new Driver();
        var names = new System.Collections.Generic.HashSet<string>();
        driver.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
            {
                names.Add(e.PropertyName);
            }
        };

        driver.FirstName = "Ada";

        Assert.That(names, Does.Contain(nameof(Driver.FirstName)));
        Assert.That(driver.FirstName, Is.EqualTo("Ada"));
    }

    [Test]
    public void LastName_And_LicenseFields_RaisePropertyChanged()
    {
        var driver = new Driver();
        var names = new System.Collections.Generic.HashSet<string>();
        driver.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
            {
                names.Add(e.PropertyName);
            }
        };

        driver.LastName = "Lovelace";
        driver.LicenseNumber = "D123";
        driver.LicenseClass = "B";

        Assert.That(names, Does.Contain(nameof(Driver.LastName)));
        Assert.That(names, Does.Contain(nameof(Driver.LicenseNumber)));
        Assert.That(names, Does.Contain(nameof(Driver.LicenseClass)));
    }
}
