using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.WPF.ViewModels.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;

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
        using var vm = new StudentsViewModel(context);

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
        using var vm = new StudentsViewModel(context);

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
        using var vm = new StudentsViewModel(context);

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

    [Test]
    public void SelectedStudent_LogsIdOnly()
    {
        const string nameToken = "TEST_STUDENT_NAME_TOKEN";
        var sink = new CollectingSink();
        var testLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var previous = Log.Logger;
        Log.Logger = testLogger;
        try
        {
            var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
                .UseInMemoryDatabase($"select_log_{Guid.NewGuid():N}")
                .Options;
            using var context = new BusBuddyDbContext(options);
            using var vm = new StudentsViewModel(context);
            vm.SelectedStudent = new Student
            {
                StudentId = 41,
                StudentName = nameToken,
                Active = true,
            };
        }
        finally
        {
            Log.Logger = previous;
            (testLogger as IDisposable)?.Dispose();
        }

        var selection = sink.Events
            .Select(e => e.RenderMessage())
            .Where(message => message.Contains("SelectedStudent changed", StringComparison.Ordinal))
            .ToList();
        selection.Should().ContainSingle();
        selection[0].Should().Contain("StudentId=41");
        selection[0].Should().NotContain(nameToken);
    }

    [Test]
    public async Task LoadStudents_KeepsEditArchiveDeleteEnabledWhileTheSelectedRowReloads()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"reload_selection_{Guid.NewGuid():N}")
            .Options;
        await using var context = new BusBuddyDbContext(options);
        var row = new Student
        {
            StudentName = "TEST_STUDENT_01",
            StudentNumber = "TEST-0041",
            Grade = "3",
            HomeAddress = "100 Test St",
            City = "Testville",
            State = "CO",
            Zip = "81052",
            SchoolYear = "2026-2027",
            Active = true,
        };
        context.Students.Add(row);
        await context.SaveChangesAsync();

        using var vm = new StudentsViewModel(context);
        vm.SelectedStudent = new Student
        {
            StudentId = row.StudentId,
            StudentName = "TEST_STUDENT_01",
            Active = true,
        };

        var sawDisabled = false;
        void Watch(object? sender, EventArgs args)
        {
            if (!vm.EditStudentCommand.CanExecute(null)
                || !vm.ArchiveStudentCommand.CanExecute(null)
                || !vm.DeleteStudentCommand.CanExecute(null))
            {
                sawDisabled = true;
            }
        }

        vm.EditStudentCommand.CanExecuteChanged += Watch;
        vm.ArchiveStudentCommand.CanExecuteChanged += Watch;
        vm.DeleteStudentCommand.CanExecuteChanged += Watch;
        vm.Students.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                vm.SelectedStudent = null;
            }
        };

        vm.IsLoading = true;
        vm.EditStudentCommand.CanExecute(null).Should().BeTrue("a selected row stays editable while the list loads");
        vm.ArchiveStudentCommand.CanExecute(null).Should().BeTrue();
        vm.DeleteStudentCommand.CanExecute(null).Should().BeTrue();

        await vm.LoadStudentsAsync();

        sawDisabled.Should().BeFalse();
        vm.SelectedStudent.Should().NotBeNull();
        vm.SelectedStudent!.StudentId.Should().Be(row.StudentId);
        vm.EditStudentCommand.CanExecute(null).Should().BeTrue();
        vm.ArchiveStudentCommand.CanExecute(null).Should().BeTrue();
        vm.DeleteStudentCommand.CanExecute(null).Should().BeTrue();
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
