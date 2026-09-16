using System;
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
}
