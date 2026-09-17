using System;
using System.Collections.Generic;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

/// <summary>
/// Command CanExecute gates on the students toolbar. specs/students.md: Delete is allowed for any
/// selected student after the clerk picks a reason. Archive is still available when they may return.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("UI")]
public class StudentsViewModelCommandTests
{
    [Test]
    public void DeleteStudentCommand_CanExecute_WhenAStudentIsSelected()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"delete_can_{Guid.NewGuid():N}")
            .Options;
        using var context = new BusBuddyDbContext(options);
        using var vm = new StudentsViewModel(context, new AddressService());

        vm.DeleteStudentCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedStudent = new Student
        {
            StudentId = 1,
            StudentName = "TEST_STUDENT_01",
            Active = true,
        };
        vm.DeleteStudentCommand.CanExecute(null).Should().BeTrue(
            "active students may be deleted after the clerk picks Mistake, Moved, or Not attending");

        vm.SelectedStudent = new Student
        {
            StudentId = 2,
            StudentName = "TEST_STUDENT_02",
            Active = false,
        };
        vm.DeleteStudentCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public void StudentDeletionReasonDialog_ConfirmDisabledUntilReasonIsChosen()
    {
        var vm = new StudentDeletionReasonDialogViewModel(new Student
        {
            StudentId = 1,
            StudentName = "TEST_STUDENT_01",
            Active = true,
        });

        vm.ConfirmCommand.CanExecute(null).Should().BeFalse("there is no default deletion reason");
        vm.Warning.Should().Contain("active");

        vm.SelectedReason = StudentDeletionReasonOption.All[0];
        vm.ConfirmCommand.CanExecute(null).Should().BeTrue();

        vm.ConfirmCommand.Execute(null);
        vm.Result.Should().NotBeNull();
        vm.Result!.Value.Reason.Should().Be(StudentDeletionReason.Mistake);
    }

    [Test]
    public void SelectedStudent_ReplacesStaleDeletedStatusWithStudentName()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"select_status_{Guid.NewGuid():N}")
            .Options;
        using var context = new BusBuddyDbContext(options);
        using var vm = new StudentsViewModel(context, new AddressService());

        vm.StatusMessage = "Student deleted";
        vm.SelectedStudent = new Student
        {
            StudentId = 7,
            StudentName = "Maximiliano",
            Active = true,
        };

        vm.StatusMessage.Should().Be("Maximiliano");
    }

    [Test]
    public void ActiveFilterChange_NotifiesStudentRouteAssignmentCounts()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"route_counts_{Guid.NewGuid():N}")
            .Options;
        using var context = new BusBuddyDbContext(options);
        using var vm = new StudentsViewModel(context, new AddressService());

        vm.Students.Add(new Student
        {
            StudentId = 1,
            StudentName = "TEST_STUDENT_01",
            Active = true,
            AMRoute = "North",
        });
        vm.Students.Add(new Student
        {
            StudentId = 2,
            StudentName = "TEST_STUDENT_02",
            Active = true,
            AmRouteId = 11,
        });
        vm.Students.Add(new Student
        {
            StudentId = 3,
            StudentName = "TEST_STUDENT_03",
            Active = true,
        });

        var notified = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
            {
                notified.Add(e.PropertyName);
            }
        };

        vm.ActiveFilter = BusBuddy.WPF.Models.FilterStatus.All;

        vm.StudentsWithRoutes.Should().Be(2, "name-only AMRoute or AmRouteId both count via IsAssignedAny");
        vm.UnassignedStudents.Should().Be(1);
        (vm.StudentsWithRoutes + vm.UnassignedStudents).Should().Be(vm.Students.Count);
        notified.Should().Contain(nameof(StudentsViewModel.TotalStudents));
        notified.Should().Contain(nameof(StudentsViewModel.ActiveStudents));
        notified.Should().Contain(nameof(StudentsViewModel.StudentsWithRoutes));
        notified.Should().Contain(nameof(StudentsViewModel.UnassignedStudents));
    }
}
