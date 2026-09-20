using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using BusBuddy.WPF.ViewModels.Student;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
public class PickupStopFormViewModelTests
{
    [Test]
    public async Task Save_HintsNearbyHomePickupsWithoutAssigningThem()
    {
        var stops = new Mock<IPickupStopService>();
        stops
            .Setup(s => s.AddStopAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PickupStop
            {
                PickupStopId = 11,
                Name = "Oak & 4th",
                Latitude = 38.0872m,
                Longitude = -102.6208m,
            });

        var students = new Mock<IStudentService>();
        students
            .Setup(s => s.GetNearbyHomePickupStudentsAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Student { StudentId = 1, StudentName = "TEST_NEAR_A" },
                new Student { StudentId = 2, StudentName = "TEST_NEAR_B" },
            });

        var vm = new PickupStopFormViewModel(stops.Object, students.Object)
        {
            Name = "Oak & 4th"
        };
        vm.ApplyMapClick(38.0872, -102.6208);

        if (vm.SaveCommand is IAsyncRelayCommand save)
        {
            await save.ExecuteAsync(null);
        }
        else
        {
            Assert.Fail("SaveCommand should be IAsyncRelayCommand");
        }

        vm.SavedPickupStopId.Should().Be(11);
        vm.NearbyHomePickupHint.Should().Contain("2 home pickup");
        vm.NearbyHomePickupHint.Should().Contain("Assign those students");
        students.Verify(
            s => s.GetNearbyHomePickupStudentsAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
