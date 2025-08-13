using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.Services;

namespace BusBuddy.Tests.SchedulerTests
{
    [TestFixture]
    [Category("Unit")]
    [Category("Scheduler")]
    public class ScheduleDataProviderTests
    {
        [Test]
        public async Task LoadActivitiesAsync_Populates_MasterList_And_RangeQueries_Work()
        {
            // Arrange
            var start = new DateTime(2025, 8, 10);
            var end = new DateTime(2025, 8, 20);

            var activities = new[]
            {
                new Activity { ActivityId = 1, Date = new DateTime(2025,8,12), ActivityType = "Field Trip", Destination = "Museum", RequestedBy = "Teacher A", AssignedVehicleId = 1, LeaveTime = new TimeSpan(9,0,0), EventTime = new TimeSpan(11,0,0) },
                new Activity { ActivityId = 2, Date = new DateTime(2025,8,15), ActivityType = "Morning", Destination = "Town Hall", RequestedBy = "Teacher B", AssignedVehicleId = 1, LeaveTime = new TimeSpan(8,0,0), EventTime = new TimeSpan(10,0,0) }
            };

            var mockService = new Mock<IActivityService>();
            mockService.Setup(s => s.GetActivitiesByDateRangeAsync(start, end))
                       .ReturnsAsync(activities);

            var provider = new BusBuddyScheduleDataProvider(mockService.Object);

            // Act
            await provider.LoadActivitiesAsync(start, end);

            // Assert master list
            provider.MasterList.Should().HaveCount(2);
            provider.IsDirty.Should().BeFalse();

            // Range query within same day
            var dayList = provider.GetScheduleForDay(new DateTime(2025,8,12));
            dayList.Should().ContainSingle(a => a.ActivityId == 1);

            // Range query spanning
            var range = provider.GetSchedule(new DateTime(2025,8,11), new DateTime(2025,8,13));
            range.Should().ContainSingle(a => a.ActivityId == 1);
            range.Should().NotContain(a => a.ActivityId == 2);

            // New appointment template
            var draft = provider.NewScheduleAppointment();
            draft.Subject.Should().NotBeNullOrEmpty();
            draft.EndTime.Should().BeAfter(draft.StartTime);
        }

        [Test]
        public void Add_Remove_Item_Marks_Dirty()
        {
            var mockService = new Mock<IActivityService>(MockBehavior.Strict);
            var provider = new BusBuddyScheduleDataProvider(mockService.Object);
            var appt = provider.NewScheduleAppointment();

            provider.AddItem(appt);
            provider.IsDirty.Should().BeTrue();

            provider.RemoveItem(appt);
            provider.IsDirty.Should().BeTrue();
        }
    }
}
