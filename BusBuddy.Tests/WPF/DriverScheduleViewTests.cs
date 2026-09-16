using BusBuddy.Core.Models;
using BusBuddy.Tests.WPF;
using BusBuddy.WPF.ViewModels.Driver;
using FluentAssertions;
using NUnit.Framework;
using CoreRoute = BusBuddy.Core.Models.Route;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class DriverScheduleViewTests
{
    [Test]
    public void DriverScheduleViewXaml_WiresRefreshAndScheduler()
    {
        var xaml = XamlViewFile.Read("Views/Driver/DriverScheduleView.xaml");
        Assert.That(xaml, Does.Contain("Command=\"{Binding RefreshCommand}\""));
        Assert.That(xaml, Does.Contain("SfScheduler"));
        Assert.That(xaml, Does.Contain("ItemsSource=\"{Binding Appointments}\""));
        Assert.That(xaml, Does.Contain("AppointmentEditFlag=\"None\""));
    }

    [Test]
    public void FromPublishedStops_OrdersByStopOrderOnTheRouteDate()
    {
        var route = new CoreRoute
        {
            RouteName = "Draft-School-1",
            Date = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc)
        };
        var stops = new[]
        {
            new RouteStop
            {
                StopOrder = 2,
                StopName = "School",
                StopAddress = "Wiley School",
                ScheduledArrival = new TimeSpan(8, 0, 0),
                ScheduledDeparture = new TimeSpan(8, 5, 0)
            },
            new RouteStop
            {
                StopOrder = 1,
                StopName = "Home",
                StopAddress = "1 Main",
                ScheduledArrival = new TimeSpan(7, 20, 0),
                ScheduledDeparture = new TimeSpan(7, 22, 0)
            }
        };

        var appointments = DriverScheduleViewModel.FromPublishedStops(route, stops);

        appointments.Should().HaveCount(2);
        appointments[0].Subject.Should().Be("1. Home");
        appointments[0].Location.Should().Be("1 Main");
        appointments[0].StartTime.Should().Be(new DateTime(2026, 9, 16, 7, 20, 0, DateTimeKind.Unspecified));
        appointments[0].StartTime.Kind.Should().Be(DateTimeKind.Unspecified);
        appointments[0].EndTime.Should().Be(new DateTime(2026, 9, 16, 7, 22, 0, DateTimeKind.Unspecified));
        appointments[1].Subject.Should().Be("2. School");
        appointments[1].Notes.Should().Be("Draft-School-1");
    }
}
