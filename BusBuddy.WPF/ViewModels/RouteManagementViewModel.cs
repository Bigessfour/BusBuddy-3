using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Syncfusion.Windows.Shared;
using System;
using System.IO;
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
    public ICommand? PrintRoutesCommand { get; }

    public RouteManagementViewModel(RouteService routeService, IBusBuddyDbContextFactory contextFactory)
    {
        _routeService = routeService;
        _contextFactory = contextFactory;
        GenerateScheduleCommand = new DelegateCommand(async _ => await GenerateScheduleAsync());
        ViewMapCommand = new DelegateCommand(_ => ViewMap());
        PrintRoutesCommand = new DelegateCommand(async _ => await PrintRoutesAsync());
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

    private async Task PrintRoutesAsync()
    {
        try
        {
            var fileName = $"routes_report_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

            using var writer = new StreamWriter(filePath);
            await writer.WriteLineAsync("BusBuddy Route Report");
            await writer.WriteLineAsync($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync(new string('=', 50));
            await writer.WriteLineAsync();

            if (Routes.Count == 0)
            {
                await writer.WriteLineAsync("No routes found.");
                return;
            }

            foreach (var route in Routes)
            {
                await writer.WriteLineAsync($"Route: {route.RouteName}");
                await writer.WriteLineAsync($"Description: {route.RouteDescription}");
                await writer.WriteLineAsync($"Path: {route.Path}");
                await writer.WriteLineAsync($"Bus Number: {route.BusNumber}");
                await writer.WriteLineAsync($"VIN Number: {route.VINNumber}");
                await writer.WriteLineAsync($"Assigned Students: {route.AssignedStudents}");
                await writer.WriteLineAsync(new string('-', 30));
                await writer.WriteLineAsync();
            }

            await writer.WriteLineAsync($"Total Routes: {Routes.Count}");

            // Open the file for viewing (optional)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Log error - for MVP we'll use simple error handling
            System.Windows.MessageBox.Show($"Error generating route report: {ex.Message}",
                "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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
