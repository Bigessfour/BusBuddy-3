namespace BusBuddy.WPF.ViewModels.Route;

public partial class RouteAssignmentViewModel
{
    public string AssignStudentToolTip =>
        CanAssignStudent
            ? "Assign the selected unassigned student to this route for the chosen time slot."
            : "Select a route, then select a student in Unassigned Students.";

    public string RemoveStudentToolTip =>
        CanRemoveStudent
            ? "Remove the year assignment for the selected student on this route (not a same-day absence)."
            : "Select a student in Assigned to Route.";

    public string NotRidingTodayToolTip =>
        CanMarkNotRidingToday
            ? "Record a same-day absence. Stops and year assignment stay unchanged; schedule sheet will show not riding."
            : "Select a student in Assigned to Route (not Unassigned).";

    public string TimeRouteToolTip
    {
        get
        {
            if (SelectedRoute is null)
            {
                return "Select a route first.";
            }

            if (!RouteStops.Any())
            {
                return "Add at least one route stop, then set Start (HH:mm).";
            }

            if (!IsStartTimeValid)
            {
                return "Enter a valid start time as HH:mm (example 07:30) in the Start box.";
            }

            return "Publish stop clocks from Start time and drive-path travel. Run Drive Path after changing stop order.";
        }
    }

    public string DrivePathToolTip
    {
        get
        {
            if (SelectedRoute is null)
            {
                return "Select a route first.";
            }

            if (IsLoading)
            {
                return "Wait for the current operation to finish.";
            }

            if (RouteStops.Count < 2)
            {
                return "Need at least two geocoded stops on this route.";
            }

            return "Compute Google Routes polyline from published stops and refresh path metrics.";
        }
    }

    public string ViewScheduleToolTip =>
        SelectedRoute is null || IsLoading
            ? "Select a route first."
            : "Open the published stop timetable (includes not-riding badges for today).";

    public string PlotRouteToolTip =>
        SelectedRoute is null
            ? "Select a route first."
            : "Plot this route on the district map.";

    public string AddStopToolTip =>
        CanAddStop
            ? "Add a stop with a validated address from Places."
            : "Select a route first.";

    public string EditStopToolTip =>
        CanEditStop
            ? "Edit the selected stop name and validated address."
            : "Select a stop in the grid (or double-click a row).";

    public string RemoveStopToolTip =>
        CanRemoveStop
            ? "Remove the selected stop from this route."
            : "Select a stop in the grid.";

    public string MoveStopUpToolTip =>
        CanMoveStopUp
            ? "Move the selected stop earlier in the run."
            : "Select a stop that is not already first.";

    public string MoveStopDownToolTip =>
        CanMoveStopDown
            ? "Move the selected stop later in the run."
            : "Select a stop that is not already last.";

    private void NotifyCommandHintProperties()
    {
        OnPropertyChanged(nameof(AssignStudentToolTip));
        OnPropertyChanged(nameof(RemoveStudentToolTip));
        OnPropertyChanged(nameof(NotRidingTodayToolTip));
        OnPropertyChanged(nameof(TimeRouteToolTip));
        OnPropertyChanged(nameof(DrivePathToolTip));
        OnPropertyChanged(nameof(ViewScheduleToolTip));
        OnPropertyChanged(nameof(PlotRouteToolTip));
        OnPropertyChanged(nameof(AddStopToolTip));
        OnPropertyChanged(nameof(EditStopToolTip));
        OnPropertyChanged(nameof(RemoveStopToolTip));
        OnPropertyChanged(nameof(MoveStopUpToolTip));
        OnPropertyChanged(nameof(MoveStopDownToolTip));
    }
}
