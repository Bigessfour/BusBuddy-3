using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Windows.Shared;

namespace BusBuddy.WPF.ViewModels;

public class StudentManagementViewModel : NotificationObject
{
    private readonly StudentService _studentService;
    private readonly IBusBuddyDbContextFactory _contextFactory;

    public ObservableCollection<BusBuddy.Core.Models.Student> Students { get; set; } = new();
    public ObservableCollection<string> AvailableRoutes { get; set; } = new() { "Truck Plaza Route", "Big Bend Route", "East Route" };
    public string SelectedRoute { get; set; } = "";
    public string RuralStopInput { get; set; } = "";
    public BusBuddy.Core.Models.Student? SelectedStudent { get; set; }
    public ICommand SaveAssignmentCommand { get; }

    public StudentManagementViewModel(StudentService studentService, IBusBuddyDbContextFactory contextFactory)
    {
        _studentService = studentService;
        _contextFactory = contextFactory;
        SaveAssignmentCommand = new RelayCommand(async () => await SaveAssignmentAsync());
        _ = LoadStudentsAsync();
    }

    private async Task LoadStudentsAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var students = await context.Students.ToListAsync();
        Students.Clear();
        foreach (var student in students)
            Students.Add(student);
    }

    private async Task SaveAssignmentAsync()
    {
        if (SelectedStudent is null || string.IsNullOrEmpty(SelectedRoute)) return;
        using var context = _contextFactory.CreateWriteDbContext();
        var route = await context.Routes.FirstOrDefaultAsync(r => r.RouteName == SelectedRoute);
        if (route == null) return;
        SelectedStudent.BusStop = RuralStopInput;
        // TODO: Fix BusService instantiation for DI
        // var assignments = await _studentService.AssignStudentsToRoutesAsync(context, new[] { SelectedStudent }, new[] { route }, new BusService());
        context.Students.Update(SelectedStudent);
        await context.SaveChangesAsync();
        // TODO: Implement dashboard refresh using a documented navigation pattern
    }
}
