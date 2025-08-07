using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Syncfusion.Windows.Shared;
using System;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Data;

namespace BusBuddy.WPF.ViewModels;

public class RouteManagementViewModel : NotificationObject
{
    private readonly RouteService _routeService;
    private readonly IBusBuddyDbContextFactory _contextFactory;
    public ObservableCollection<RouteGridItem> Routes { get; set; } = new();

    public ICommand? GenerateScheduleCommand { get; }
    public ICommand? ViewMapCommand { get; }

    public RouteManagementViewModel(RouteService routeService, IBusBuddyDbContextFactory contextFactory)
    {
        _routeService = routeService;
        _contextFactory = contextFactory;
        GenerateScheduleCommand = new DelegateCommand(async _ => await GenerateScheduleAsync());
        ViewMapCommand = new DelegateCommand(_ => ViewMap());
        _ = LoadRoutesAsync();
    }

    private async Task LoadRoutesAsync()
    {
        using var context = _contextFactory.CreateDbContext();
        var routes = await context.Routes.ToListAsync();
        Routes.Clear();
        foreach (var route in routes)
        {
            var bus = await context.Buses.FirstOrDefaultAsync(v => v.Description == route.RouteName || v.BusNumber == route.RouteName || v.BusNumber == route.RouteName.Replace(" Route", ""));
            // TODO: RouteAssignments removed. Replace with new assignment logic if needed.
            var students = await context.Students.Where(s => s.RouteId == route.RouteId).ToListAsync();
            Routes.Add(new RouteGridItem
            {
                RouteName = route.RouteName,
                RouteDescription = route.RouteDescription,
                Path = route.Path,
                BusNumber = bus?.BusNumber ?? "",
                VINNumber = bus?.VINNumber ?? "",
                AssignedStudents = string.Join(", ", students.Select(s => s.StudentName))
            });
        }
    }

    private async Task GenerateScheduleAsync()
    {
        try
        {
            using var context = _contextFactory.CreateDbContext();
            var studentService = new StudentService(_contextFactory);
            await _routeService.GenerateWileySchedulesAsync(context, studentService);
        }
        catch (Exception)
        {
            // Handle/log error as per Error-Handling.md
        }
    }

    private void ViewMap()
    {
        // Placeholder for Google Earth integration
    }
}

public class RouteGridItem
{
    public string RouteName { get; set; } = "";
    public string RouteDescription { get; set; } = "";
    public string Path { get; set; } = "";
    public string BusNumber { get; set; } = "";
    public string VINNumber { get; set; } = "";
    public string AssignedStudents { get; set; } = "";
}
